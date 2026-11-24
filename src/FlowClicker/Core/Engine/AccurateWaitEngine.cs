using System;
using System.Diagnostics;
using System.Threading;
using FlowClicker.Core.Native;
using FlowClicker.Core.Services.Models;
using Microsoft.Win32.SafeHandles;

namespace FlowClicker.Core.Engine
{
    public static class AccurateWaitEngine
    {
        private static readonly double TicksPerMillisecond = Stopwatch.Frequency / 1000.0;

        [ThreadStatic]
        private static SafeWaitHandle? _highResTimer;

        [ThreadStatic]
        private static bool _highResTimerUnsupported;

        [ThreadStatic]
        private static IntPtr[]? _waitHandlesBuffer;

        /// <summary>
        /// Precision hybrid wait loop combining OS waitable timers, SpinWait, and QPC polling.
        /// Eco mode uses a cancellable OS wait for intervals long enough to sleep.
        /// </summary>
        public static void AccurateWait(long targetTimestamp, CancellationToken ct, TimerEngineMode mode = TimerEngineMode.Precision)
        {
            while (true)
            {
                if (ct.IsCancellationRequested) return;

                long current = Stopwatch.GetTimestamp();
                long remainingTicks = targetTimestamp - current;

                if (remainingTicks <= 0) break;

                double remainingMs = remainingTicks / TicksPerMillisecond;

                if (mode == TimerEngineMode.Eco && remainingMs >= 2)
                {
                    if (ct.WaitHandle.WaitOne((int)remainingMs)) return;
                    continue;
                }

                if (mode == TimerEngineMode.Precision)
                {
                    if (remainingMs > 4.5)
                    {
                        if (WaitHighResolution(remainingMs - 3.5, ct)) return;
                        continue;
                    }
                }
                else if (remainingMs > 2.0)
                {
                    Thread.Sleep(1);
                    continue;
                }

                if (remainingMs > 0.05)
                {
                    if (mode == TimerEngineMode.Precision)
                    {
                        Thread.SpinWait(20);
                    }
                    else
                    {
                        Thread.Yield();
                    }
                }
                else
                {
                    if (mode == TimerEngineMode.Eco)
                    {
                        Thread.SpinWait(10);
                    }
                }
            }
        }

        private static bool WaitHighResolution(double ms, CancellationToken ct)
        {
            if (!_highResTimerUnsupported && _highResTimer == null)
            {
                var handle = NativeMethods.CreateWaitableTimerEx(
                    IntPtr.Zero,
                    null,
                    NativeMethods.CREATE_WAITABLE_TIMER_HIGH_RESOLUTION,
                    NativeMethods.TIMER_ALL_ACCESS);

                if (handle.IsInvalid)
                {
                    handle.Dispose();
                    _highResTimerUnsupported = true;
                }
                else
                {
                    _highResTimer = handle;
                }
            }

            var timer = _highResTimer;
            if (timer == null || timer.IsInvalid)
            {
                if (ms > 15.0)
                    return ct.WaitHandle.WaitOne((int)(ms - 14.0));
                Thread.Sleep(1);
                return ct.IsCancellationRequested;
            }

            long dueTime100Ns = -(long)(Math.Max(0.1, ms) * 10_000.0);
            if (!NativeMethods.SetWaitableTimer(timer, ref dueTime100Ns, 0, IntPtr.Zero, IntPtr.Zero, false))
            {
                Thread.Sleep(1);
                return ct.IsCancellationRequested;
            }

            var cancelHandle = ct.WaitHandle.SafeWaitHandle;
            bool timerRef = false;
            bool cancelRef = false;
            try
            {
                timer.DangerousAddRef(ref timerRef);
                cancelHandle.DangerousAddRef(ref cancelRef);
                var handles = _waitHandlesBuffer ??= new IntPtr[2];
                handles[0] = timer.DangerousGetHandle();
                handles[1] = cancelHandle.DangerousGetHandle();
                uint signaled = NativeMethods.WaitForMultipleObjects(2, handles, false, NativeMethods.INFINITE);
                return signaled == 1 || ct.IsCancellationRequested;
            }
            finally
            {
                if (cancelRef) cancelHandle.DangerousRelease();
                if (timerRef) timer.DangerousRelease();
            }
        }
    }
}
