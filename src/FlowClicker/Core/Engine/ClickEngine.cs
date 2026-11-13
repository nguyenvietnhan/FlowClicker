using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using FlowClicker.Core.Native;
using FlowClicker.Core.Services.Models;

namespace FlowClicker.Core.Engine
{
    public sealed class ClickEngine : IClickEngine, IDisposable
    {
        private long _totalClicks;
        private readonly Humanizer _humanizer = new();

        private readonly MouseAction _action;
        public bool InputFailed { get; private set; }

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
                InputFailed = false;
                try
                {
                    WorkerLoop(config, token);
                }
                finally
                {
                    if (!_action.Release())
                        InputFailed = true;
                }
            }, running => StateChanged?.Invoke(this, running));
        }

        public void Stop() => _worker.Stop();

        private void WorkerLoop(ClickEngineConfiguration config, CancellationToken ct)
        {
            using var timerScope = new MultimediaTimerResolutionScope(1);

            double targetIntervalMs = 1000.0 / Math.Clamp(config.Cps, 0.1, 2000.0);
            long intervalTicks = (long)(targetIntervalMs * (Stopwatch.Frequency / 1000.0));
            long nextTick = Stopwatch.GetTimestamp();

            while (!ct.IsCancellationRequested)
            {
                // Execute click action
                if (!PerformClickAction(config, ct))
                {
                    InputFailed = !ct.IsCancellationRequested;
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

        private bool PerformClickAction(ClickEngineConfiguration config, CancellationToken ct)
        {
            if (config.LocationMode != ClickLocationMode.FixedCoordinates)
                return _action.Send(config.Button, config.ClickType, cancellationToken: ct);

            var (dx, dy) = config.EnableHumanizer
                ? _humanizer.GenerateCoordinateJitter(config.HumanizerRadiusPx)
                : (0, 0);

            return _action.Send(config.Button, config.ClickType, (config.FixedX + dx, config.FixedY + dy), ct);
        }

        public void Dispose()
        {
            _worker.Dispose();
            _action.Release();
        }
    }
}
