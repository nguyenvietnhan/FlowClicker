using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using FlowClicker.Core.Engine;
using FlowClicker.Core.Native;
using FlowClicker.Core.Services.Models;

namespace FlowClicker.Core.Services
{
    public sealed class SequenceEngine : ISequenceEngine, IDisposable
    {
        private int _currentStepIndex;
        private int _completedCycles;
        private volatile bool _inputFailed;
        private int _failureReason;

        private readonly MouseAction _action;
        public bool InputFailed => _inputFailed;
        public InputFailureReason FailureReason => (InputFailureReason)Volatile.Read(ref _failureReason);

        private readonly MouseWorker _worker = new();

        public bool IsRunning => _worker.IsRunning;
        public int CurrentStepIndex => Volatile.Read(ref _currentStepIndex);
        public int CompletedCycles => Volatile.Read(ref _completedCycles);

        public event EventHandler<bool>? StateChanged;

        public SequenceEngine() : this(new MouseAction()) { }

        internal SequenceEngine(MouseAction action) => _action = action;

        public void Start(IReadOnlyList<SequenceActionPoint> points, int repeatCount = 0)
        {
            if (points.Count == 0 || IsRunning)
                return;

            var snapshot = points.Select(point => point.Clone()).ToList();
            _worker.Start("FlowClicker sequence", ThreadPriority.AboveNormal, token =>
            {
                Volatile.Write(ref _currentStepIndex, 0);
                Volatile.Write(ref _completedCycles, 0);
                _inputFailed = false;
                Volatile.Write(ref _failureReason, (int)InputFailureReason.None);
                try
                {
                    SequenceLoop(snapshot, repeatCount, token);
                }
                finally
                {
                    if (!_action.Release())
                    {
                        _inputFailed = true;
                        Volatile.Write(ref _failureReason, (int)InputFailureReason.InjectionBlocked);
                    }
                }
            }, running => StateChanged?.Invoke(this, running));
        }

        public void Stop() => _worker.Stop();

        private void SequenceLoop(List<SequenceActionPoint> points, int repeatCount, CancellationToken ct)
        {
            using var timerScope = new MultimediaTimerResolutionScope(1);
            int cycles = 0;

            while (!ct.IsCancellationRequested)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    if (ct.IsCancellationRequested) return;

                    Volatile.Write(ref _currentStepIndex, i);

                    var pt = points[i];

                    // Pre-delay
                    if (pt.PreDelayMs > 0)
                    {
                        WaitExact(pt.PreDelayMs, ct);
                        if (ct.IsCancellationRequested) return;
                    }

                    var result = _action.SendDetailed(pt.Button, pt.Type, (pt.X, pt.Y), ct);
                    if (result != InputSendResult.Success)
                    {
                        if (!ct.IsCancellationRequested && result != InputSendResult.Cancelled)
                        {
                            _inputFailed = true;
                            Volatile.Write(ref _failureReason, (int)MouseAction.ToFailureReason(result));
                        }
                        return;
                    }

                    // Post-delay
                    if (pt.PostDelayMs > 0)
                    {
                        WaitExact(pt.PostDelayMs, ct);
                        if (ct.IsCancellationRequested) return;
                    }
                }

                cycles++;
                Volatile.Write(ref _completedCycles, cycles);

                if (repeatCount > 0 && cycles >= repeatCount)
                {
                    break;
                }
            }
        }

        private static void WaitExact(int ms, CancellationToken ct)
        {
            long ticks = (long)(ms * (Stopwatch.Frequency / 1000.0));
            long target = Stopwatch.GetTimestamp() + ticks;
            AccurateWaitEngine.AccurateWait(target, ct, TimerEngineMode.Precision);
        }

        public void Dispose()
        {
            _worker.Dispose();
            _action.Release();
        }
    }
}
