using System;
using System.Diagnostics;
using System.Threading;
using FlowClicker.Core.Native;
using FlowClicker.Core.Services.Models;

namespace FlowClicker.Core.Engine
{
    public sealed class ClickEngine : IClickEngine, IDisposable
    {
        private long _totalClicks;
        private volatile bool _inputFailed;
        private int _failureReason;
        private readonly Humanizer _humanizer = new();

        private readonly MouseAction _action;
        public bool InputFailed => _inputFailed;
        public InputFailureReason FailureReason => (InputFailureReason)Volatile.Read(ref _failureReason);

        private readonly MouseWorker _worker = new();

        public bool IsRunning => _worker.IsRunning;
        public long TotalClicks => Interlocked.Read(ref _totalClicks);
        public event EventHandler<bool>? StateChanged;

        public ClickEngine() : this(new MouseAction()) { }

        internal ClickEngine(MouseAction action) => _action = action;

        public void Start(ClickEngineConfiguration config)
        {
            _worker.Start("FlowClicker clicker", ThreadPriority.Normal, token =>
            {
                Interlocked.Exchange(ref _totalClicks, 0);
                _inputFailed = false;
                Volatile.Write(ref _failureReason, (int)InputFailureReason.None);
                try
                {
                    WorkerLoop(config, token);
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

        private void WorkerLoop(ClickEngineConfiguration config, CancellationToken ct)
        {
            if (config.ClickType == ClickType.Hold)
            {
                (int X, int Y)? holdPos = config.LocationMode == ClickLocationMode.FixedCoordinates
                    ? (config.FixedX, config.FixedY)
                    : null;
                var result = _action.SendDetailed(config.Button, ClickType.Hold, holdPos, ct);
                if (result != InputSendResult.Success)
                {
                    RecordFailure(result, ct);
                    return;
                }

                Interlocked.Increment(ref _totalClicks);
                ct.WaitHandle.WaitOne();
                return;
            }

            double targetIntervalMs = config.ResolveIntervalMs();
            using var timerScope = (config.EngineMode == TimerEngineMode.Precision || targetIntervalMs <= 20.0)
                ? new MultimediaTimerResolutionScope(1)
                : (MultimediaTimerResolutionScope?)null;

            long intervalTicks = (long)(targetIntervalMs * (Stopwatch.Frequency / 1000.0));
            long nextTick = Stopwatch.GetTimestamp();

            while (!ct.IsCancellationRequested)
            {
                // Execute click action
                var result = PerformClickAction(config, ct);
                if (result != InputSendResult.Success)
                {
                    RecordFailure(result, ct);
                    return;
                }

                long total = Interlocked.Increment(ref _totalClicks);

                // Max click count limit check
                if (config.MaxClickCount > 0 && total >= config.MaxClickCount)
                {
                    break;
                }

                // Calculate next interval (with optional humanizer jitter)
                long currentInterval = intervalTicks;
                if (config.EnableHumanizer)
                {
                    double jitterMs = _humanizer.GenerateGaussianJitter(targetIntervalMs, config.HumanizerJitterMs);
                    currentInterval = (long)(jitterMs * (Stopwatch.Frequency / 1000.0));
                }

                // Monotonic running timestamp accumulation (drift-free)
                nextTick += currentInterval;
                // Skip missed slots instead of sending a burst after a stall.
                long now = Stopwatch.GetTimestamp();
                if (nextTick <= now)
                    nextTick = now + currentInterval;
                AccurateWaitEngine.AccurateWait(nextTick, ct, config.EngineMode);
            }
        }

        private InputSendResult PerformClickAction(ClickEngineConfiguration config, CancellationToken ct)
        {
            if (config.LocationMode != ClickLocationMode.FixedCoordinates)
                return _action.SendDetailed(config.Button, config.ClickType, cancellationToken: ct);

            var (dx, dy) = config.EnableHumanizer
                ? _humanizer.GenerateCoordinateJitter(config.HumanizerRadiusPx)
                : (0, 0);

            return _action.SendDetailed(config.Button, config.ClickType, (config.FixedX + dx, config.FixedY + dy), ct);
        }

        private void RecordFailure(InputSendResult result, CancellationToken ct)
        {
            if (ct.IsCancellationRequested || result == InputSendResult.Cancelled)
                return;

            _inputFailed = true;
            Volatile.Write(ref _failureReason, (int)MouseAction.ToFailureReason(result));
        }

        public void Dispose()
        {
            _worker.Dispose();
            _action.Release();
        }
    }
}
