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
        private readonly MouseAction _hold = new();

        private readonly object _syncLock = new();

        public bool IsAutoScrollRunning => _scroll.IsRunning;
        public bool IsJigglerRunning => _jiggler.IsRunning;
        public bool IsHoldLockActive => Volatile.Read(ref _isHoldLockActive);

        public event EventHandler<bool>? AutoScrollStateChanged;
        public event EventHandler<bool>? JigglerStateChanged;
        public event EventHandler<bool>? HoldLockStateChanged;

        // Auto-Scroll Implementation
        public void StartAutoScroll(ScrollDirection direction, int speedLinesPerSecond)
        {
            _scroll.Start("FlowClicker scroll", ThreadPriority.Normal,
                token => ScrollLoop(direction, Math.Clamp(speedLinesPerSecond, 1, 100), token),
                running => AutoScrollStateChanged?.Invoke(this, running));
        }

        public void StopAutoScroll() => _scroll.Stop();

        private void ScrollLoop(ScrollDirection direction, int linesPerSec, CancellationToken ct)
        {
            using var timerScope = new MultimediaTimerResolutionScope(1);

            int intervalMs = Math.Max(10, 1000 / linesPerSec);
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
                InputInjector.Inject(input);

                nextTick += intervalTicks;
                AccurateWaitEngine.AccurateWait(nextTick, ct, Models.TimerEngineMode.Eco);

                long current = Stopwatch.GetTimestamp();
                if (current > nextTick + intervalTicks)
                {
                    nextTick = current + intervalTicks;
                }
            }

        }

        // Mouse Jiggler Implementation
        public void StartJiggler(int intervalSeconds = 15, JigglerMode mode = JigglerMode.Lissajous)
        {
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
                        // User is actively moving mouse; reset last position and wait
                        lastX = currX;
                        lastY = currY;
                        i = 0; // reset wait countdown
                    }
                }

                if (ct.IsCancellationRequested) return;

                if (mode == JigglerMode.MicroNudge)
                {
                    // Nudge +1 then -1
                    moveInput[0] = INPUT.CreateMouseInput(new MOUSEINPUT { dx = 1, dy = 0, dwFlags = NativeMethods.MOUSEEVENTF_MOVE });
                    InputInjector.Inject(moveInput);
                    Thread.Sleep(50);
                    moveInput[0] = INPUT.CreateMouseInput(new MOUSEINPUT { dx = -1, dy = 0, dwFlags = NativeMethods.MOUSEEVENTF_MOVE });
                    InputInjector.Inject(moveInput);
                }
                else
                {
                    // Lissajous curve motion
                    for (double t = 0; t < Math.PI * 2; t += 0.4)
                    {
                        if (ct.IsCancellationRequested) return;
                        int dx = (int)Math.Round(Math.Sin(t) * 3);
                        int dy = (int)Math.Round(Math.Sin(2 * t) * 2);
                        moveInput[0] = INPUT.CreateMouseInput(new MOUSEINPUT { dx = dx, dy = dy, dwFlags = NativeMethods.MOUSEEVENTF_MOVE });
                        InputInjector.Inject(moveInput);
                        if (ct.WaitHandle.WaitOne(20)) return;
                    }
                }

                var (newX, newY) = CoordinateConverter.GetCurrentCursorPosition();
                lastX = newX;
                lastY = newY;
            }

        }

        // Mouse Hold Lock Implementation
        public void ToggleHoldLock(bool leftButton = true)
        {
            lock (_syncLock)
            {
                if (_isHoldLockActive)
                {
                    ReleaseHoldLock();
                }
                else
                {
                    var button = leftButton ? ClickMouseButton.Left : ClickMouseButton.Right;
                    if (!_hold.Send(button, ClickType.Hold))
                        return;

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
                    return;

                Volatile.Write(ref _isHoldLockActive, false);
                NotifyState(HoldLockStateChanged, false);
            }
        }

        private static void NotifyState(EventHandler<bool>? handler, bool state)
        {
            if (Application.Current != null && Application.Current.Dispatcher != null)
            {
                Application.Current.Dispatcher.BeginInvoke(() => handler?.Invoke(null, state));
            }
            else
            {
                handler?.Invoke(null, state);
            }
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
