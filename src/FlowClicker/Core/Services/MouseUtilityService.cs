using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using FlowClicker.Core.Engine;
using FlowClicker.Core.Native;
using FlowClicker.Core.Services.Models;

namespace FlowClicker.Core.Services
{
    public sealed class MouseUtilityService : IMouseUtilityService, IDisposable
    {
        private readonly MouseWorker _scroll = new();
        private readonly MouseWorker _jiggler = new();

        private bool _isHoldLockActive;
        private volatile bool _inputFailed;
        private int _failureReason;
        private readonly MouseAction _hold;
        private readonly Func<INPUT[], uint> _inject;

        private readonly object _syncLock = new();

        public bool IsAutoScrollRunning => _scroll.IsRunning;
        public bool IsJigglerRunning => _jiggler.IsRunning;
        public bool IsHoldLockActive => Volatile.Read(ref _isHoldLockActive);
        public bool InputFailed => _inputFailed;
        public InputFailureReason FailureReason => (InputFailureReason)Volatile.Read(ref _failureReason);

        public event EventHandler<bool>? AutoScrollStateChanged;
        public event EventHandler<bool>? JigglerStateChanged;
        public event EventHandler<bool>? HoldLockStateChanged;
        public event EventHandler? FailureStateChanged;

        public MouseUtilityService() : this(new MouseAction(), InputInjector.Inject) { }

        internal MouseUtilityService(MouseAction holdAction, Func<INPUT[], uint> inject)
        {
            _hold = holdAction;
            _inject = inject;
        }

        // Auto-Scroll Implementation
        public void StartAutoScroll(ScrollDirection direction, int speedStepsPerSecond)
        {
            ClearFailure();
            _scroll.Start("FlowClicker scroll", ThreadPriority.Normal,
                token => ScrollLoop(direction, Math.Clamp(speedStepsPerSecond, 1, 100), token),
                running => AutoScrollStateChanged?.Invoke(this, running));
        }

        public void StopAutoScroll() => _scroll.Stop();

        private void ScrollLoop(ScrollDirection direction, int stepsPerSec, CancellationToken ct)
        {
            using var timerScope = stepsPerSec >= 50 ? new MultimediaTimerResolutionScope(1) : (MultimediaTimerResolutionScope?)null;

            double intervalMs = 1000.0 / stepsPerSec;
            long intervalTicks = (long)(intervalMs * (Stopwatch.Frequency / 1000.0));
            long nextTick = Stopwatch.GetTimestamp();

            var input = new INPUT[1];
            uint flag = (direction == ScrollDirection.Left || direction == ScrollDirection.Right)
                ? NativeMethods.MOUSEEVENTF_HWHEEL
                : NativeMethods.MOUSEEVENTF_WHEEL;

            // 120 is WHEEL_DELTA in Win32
            int delta = (direction == ScrollDirection.Up || direction == ScrollDirection.Right) ? 120 : -120;

            input[0] = INPUT.CreateMouseInput(new MOUSEINPUT
            {
                dwFlags = flag,
                mouseData = (uint)delta
            });

            while (!ct.IsCancellationRequested)
            {
                if (_inject(input) != 1)
                {
                    if (!ct.IsCancellationRequested)
                        SetFailure(InputFailureReason.InjectionBlocked);
                    return;
                }

                nextTick += intervalTicks;
                long now = Stopwatch.GetTimestamp();
                if (nextTick <= now)
                    nextTick = now + intervalTicks;

                AccurateWaitEngine.AccurateWait(nextTick, ct, TimerEngineMode.Eco);
            }
        }

        // Mouse Jiggler Implementation
        public void StartJiggler(int intervalSeconds = 15, JigglerMode mode = JigglerMode.Lissajous)
        {
            ClearFailure();
            _jiggler.Start("FlowClicker jiggler", ThreadPriority.Lowest,
                token => JigglerLoop(Math.Clamp(intervalSeconds, 1, 300), mode, token),
                running => JigglerStateChanged?.Invoke(this, running));
        }

        public void StopJiggler() => _jiggler.Stop();

        private void JigglerLoop(int intervalSec, JigglerMode mode, CancellationToken ct)
        {
            var moveInput = new INPUT[1];
            var (lastX, lastY) = CoordinateConverter.GetCurrentCursorPosition();

            while (!ct.IsCancellationRequested)
            {
                // Wait for the interval period in small slices to detect user motion or cancellation
                for (int i = 0; i < intervalSec * 10; i++)
                {
                    if (ct.IsCancellationRequested) return;
                    if (ct.WaitHandle.WaitOne(100)) return;

                    var (currX, currY) = CoordinateConverter.GetCurrentCursorPosition();
                    if (Math.Abs(currX - lastX) > 2 || Math.Abs(currY - lastY) > 2)
                    {
                        lastX = currX;
                        lastY = currY;
                        i = -1; // reset wait countdown cleanly before loop increment
                    }
                }

                if (ct.IsCancellationRequested) return;

                if (mode == JigglerMode.MicroNudge)
                {
                    moveInput[0] = INPUT.CreateMouseInput(new MOUSEINPUT { dx = 1, dy = 0, dwFlags = NativeMethods.MOUSEEVENTF_MOVE });
                    if (_inject(moveInput) != 1)
                    {
                        if (!ct.IsCancellationRequested) SetFailure(InputFailureReason.InjectionBlocked);
                        return;
                    }
                    if (ct.WaitHandle.WaitOne(50))
                    {
                        moveInput[0] = INPUT.CreateMouseInput(new MOUSEINPUT { dx = -1, dy = 0, dwFlags = NativeMethods.MOUSEEVENTF_MOVE });
                        _inject(moveInput);
                        return;
                    }
                    moveInput[0] = INPUT.CreateMouseInput(new MOUSEINPUT { dx = -1, dy = 0, dwFlags = NativeMethods.MOUSEEVENTF_MOVE });
                    if (_inject(moveInput) != 1)
                    {
                        if (!ct.IsCancellationRequested) SetFailure(InputFailureReason.InjectionBlocked);
                        return;
                    }
                }
                else
                {
                    // Symmetric 16-step elliptical loop where step k+8 exactly cancels step k (zero net drift).
                    const int steps = 16;
                    for (int k = 0; k < steps; k++)
                    {
                        if (ct.IsCancellationRequested) return;
                        double angle = k * (2.0 * Math.PI / steps);
                        int dx = (int)Math.Round(Math.Cos(angle) * 3);
                        int dy = (int)Math.Round(Math.Sin(angle) * 2);
                        moveInput[0] = INPUT.CreateMouseInput(new MOUSEINPUT { dx = dx, dy = dy, dwFlags = NativeMethods.MOUSEEVENTF_MOVE });
                        if (_inject(moveInput) != 1)
                        {
                            if (!ct.IsCancellationRequested) SetFailure(InputFailureReason.InjectionBlocked);
                            return;
                        }
                        if (ct.WaitHandle.WaitOne(20)) return;
                    }
                }

                var (newX, newY) = CoordinateConverter.GetCurrentCursorPosition();
                lastX = newX;
                lastY = newY;
            }
        }

        // Mouse Hold Lock Implementation
        public void ToggleHoldLock(bool leftButton = true) =>
            ToggleHoldLock(leftButton ? ClickMouseButton.Left : ClickMouseButton.Right);

        public void ToggleHoldLock(ClickMouseButton button)
        {
            lock (_syncLock)
            {
                if (_isHoldLockActive)
                {
                    ReleaseHoldLock();
                }
                else
                {
                    ClearFailure();
                    var result = _hold.SendDetailed(button, ClickType.Hold);
                    if (result != InputSendResult.Success)
                    {
                        SetFailure(MouseAction.ToFailureReason(result));
                        return;
                    }

                    Volatile.Write(ref _isHoldLockActive, true);
                    NotifyState(HoldLockStateChanged, true);
                }
            }
        }

        public void ReleaseHoldLock()
        {
            lock (_syncLock)
            {
                if (!_isHoldLockActive) return;

                if (!_hold.Release())
                {
                    SetFailure(InputFailureReason.InjectionBlocked);
                    return;
                }

                Volatile.Write(ref _isHoldLockActive, false);
                NotifyState(HoldLockStateChanged, false);
            }
        }

        private void ClearFailure()
        {
            if (!_inputFailed && FailureReason == InputFailureReason.None)
                return;
            _inputFailed = false;
            Volatile.Write(ref _failureReason, (int)InputFailureReason.None);
            NotifyEvent(FailureStateChanged);
        }

        private void SetFailure(InputFailureReason reason)
        {
            _inputFailed = true;
            Volatile.Write(ref _failureReason, (int)reason);
            NotifyEvent(FailureStateChanged);
        }

        private void NotifyState(EventHandler<bool>? handler, bool state)
        {
            if (Application.Current?.Dispatcher is { } dispatcher)
                dispatcher.BeginInvoke(() => handler?.Invoke(this, state));
            else
                handler?.Invoke(this, state);
        }

        private void NotifyEvent(EventHandler? handler)
        {
            if (Application.Current?.Dispatcher is { } dispatcher)
                dispatcher.BeginInvoke(() => handler?.Invoke(this, EventArgs.Empty));
            else
                handler?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            _scroll.Stop();
            _jiggler.Stop();
            _scroll.Dispose();
            _jiggler.Dispose();
            ReleaseHoldLock();
        }
    }
}
