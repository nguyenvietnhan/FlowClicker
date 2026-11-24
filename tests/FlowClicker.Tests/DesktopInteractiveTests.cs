using System;
using System.Diagnostics;
using System.Threading;
using FlowClicker.Core.Engine;
using FlowClicker.Core.Native;
using FlowClicker.Core.Services.Models;
using Xunit;

namespace FlowClicker.Tests
{
    [Trait("Category", "Desktop")]
    public sealed class DesktopInteractiveTests
    {
        [Fact]
        public void MultimediaTimerResolutionScope_ElevatesSleepQuantum()
        {
            using var scope = new MultimediaTimerResolutionScope(1);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 10; i++)
                Thread.Sleep(1);
            sw.Stop();
            double avgSleepMs = (double)sw.ElapsedMilliseconds / 10;
            Assert.True(avgSleepMs < 3.0, $"Expected sleep quantum < 3.0 ms, actual {avgSleepMs:F2} ms");
        }

        [Fact]
        public void AccurateWaitEngine_HighPrecisionMaintainsSubMillisecondMeanVariance()
        {
            using var scope = new MultimediaTimerResolutionScope(1);
            using var cts = new CancellationTokenSource();
            long targetTicks = (long)(10.0 * (Stopwatch.Frequency / 1000.0));

            double totalErrorMs = 0;
            const int iterations = 20;
            for (int i = 0; i < iterations; i++)
            {
                long start = Stopwatch.GetTimestamp();
                AccurateWaitEngine.AccurateWait(start + targetTicks, cts.Token, TimerEngineMode.Precision);
                long elapsed = Stopwatch.GetTimestamp() - start;
                double elapsedMs = (double)elapsed / Stopwatch.Frequency * 1000.0;
                totalErrorMs += Math.Abs(elapsedMs - 10.0);
            }

            double avgErrorMs = totalErrorMs / iterations;
            Assert.True(avgErrorMs < 0.25, $"Mean timing variance too high: {avgErrorMs:F4} ms");
        }

        [Fact]
        public void InputInjector_ReleaseAllMouseButtonLocks_AcceptedByInteractiveDesktop()
        {
            uint released = InputInjector.ReleaseAllMouseButtonLocks();
            Assert.Equal(5u, released);
        }
    }
}
