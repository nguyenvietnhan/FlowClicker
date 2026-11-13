using System;
using System.Diagnostics;
using System.Threading;
using FlowClicker.Core.Services.Models;

namespace FlowClicker.Core.Engine
{
    public static class AccurateWaitEngine
    {
        private static readonly double TicksPerMillisecond = Stopwatch.Frequency / 1000.0;

        /// <summary>
        /// Precision hybrid wait loop combining Thread.Sleep, SpinWait, and QPC polling.
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

                if (remainingMs > 2.0)
                {
                    // Coarse Phase: Yield CPU slice to OS scheduler (~0% CPU load)
                    Thread.Sleep(1);
                }
                else if (remainingMs > 0.05)
                {
                    // Micro Phase: Keep thread warm without full kernel context switches
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
                    // Fine-Grained Phase: Sub-50 microsecond active QPC spin
                    if (mode == TimerEngineMode.Eco)
                    {
                        Thread.SpinWait(10);
                    }
                }
            }
        }
    }
}
