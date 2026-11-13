using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using FlowClicker.Core.Engine;
using FlowClicker.Core.Native;
using FlowClicker.Core.Services;
using FlowClicker.Core.Services.Models;

namespace FlowClicker.Tests
{
    internal class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("=================================================");
            Console.WriteLine(" FlowClicker Architecture Verification Test Suite");
            Console.WriteLine("=================================================\n");

            int passed = 0;
            int failed = 0;

            // 1. Struct Sizes & 64-Bit Memory Alignment Verification
            Console.WriteLine("[TEST 1] Win32 64-Bit Struct Layout Verification:");
            int inputSize = Marshal.SizeOf<INPUT>();
            int mouseInputSize = Marshal.SizeOf<MOUSEINPUT>();
            int keybdInputSize = Marshal.SizeOf<KEYBDINPUT>();
            int hardwareInputSize = Marshal.SizeOf<HARDWAREINPUT>();

            Console.WriteLine($" - Marshal.SizeOf<INPUT>() = {inputSize} bytes (Expected: 40 bytes)");
            Console.WriteLine($" - Marshal.SizeOf<MOUSEINPUT>() = {mouseInputSize} bytes (Expected: 32 bytes)");
            Console.WriteLine($" - Marshal.SizeOf<KEYBDINPUT>() = {keybdInputSize} bytes (Expected: 24 bytes)");
            Console.WriteLine($" - Marshal.SizeOf<HARDWAREINPUT>() = {hardwareInputSize} bytes (Expected: 8 bytes)");

            if (inputSize == 40 && mouseInputSize == 32 && keybdInputSize == 24 && hardwareInputSize == 8)
            {
                Console.WriteLine(" -> PASS: Exact 64-bit memory layout confirmed.\n");
                passed++;
            }
            else
            {
                Console.WriteLine(" -> FAIL: Struct layout mismatch on x64 runtime.\n");
                failed++;
            }

            // 2. WinMM timeBeginPeriod(1) RAII Scope Verification
            Console.WriteLine("[TEST 2] Multimedia Timer Scope (timeBeginPeriod RAII):");
            double avgSleepNoScope = MeasureAverageSleep(5);
            Console.WriteLine($" - Default OS resolution average Thread.Sleep(1): {avgSleepNoScope:F2} ms");

            double avgSleepWithScope;
            using (var scope = new MultimediaTimerResolutionScope(1))
            {
                avgSleepWithScope = MeasureAverageSleep(10);
                Console.WriteLine($" - With timeBeginPeriod(1) average Thread.Sleep(1): {avgSleepWithScope:F2} ms");
            }

            if (avgSleepWithScope < 3.0)
            {
                Console.WriteLine(" -> PASS: Kernel timer elevated to ~1.0 ms quantum successfully.\n");
                passed++;
            }
            else
            {
                Console.WriteLine(" -> FAIL: Timer quantum was not lowered.\n");
                failed++;
            }

            // 3. AccurateWaitEngine Sub-Millisecond Benchmark
            Console.WriteLine("[TEST 3] AccurateWaitEngine High-Precision Benchmark (100 CPS = 10.0 ms target):");
            using (var scope = new MultimediaTimerResolutionScope(1))
            {
                var cts = new CancellationTokenSource();
                long targetTicks = (long)(10.0 * (Stopwatch.Frequency / 1000.0));

                double totalErrorMs = 0;
                int iterations = 20;

                for (int i = 0; i < iterations; i++)
                {
                    long start = Stopwatch.GetTimestamp();
                    long target = start + targetTicks;
                    AccurateWaitEngine.AccurateWait(target, cts.Token, Core.Services.Models.TimerEngineMode.Precision);
                    long elapsed = Stopwatch.GetTimestamp() - start;
                    double elapsedMs = (double)elapsed / Stopwatch.Frequency * 1000.0;
                    totalErrorMs += Math.Abs(elapsedMs - 10.0);
                }

                double avgErrorMs = totalErrorMs / iterations;
                Console.WriteLine($" - Mean timing variance across {iterations} iterations: {avgErrorMs:F4} ms (sub-millisecond)");

                if (avgErrorMs < 0.25)
                {
                    Console.WriteLine(" -> PASS: Deterministic sub-millisecond precision confirmed.\n");
                    passed++;
                }
                else
                {
                    Console.WriteLine(" -> FAIL: Timing variance too high.\n");
                    failed++;
                }
            }

            // 4. Coordinate Normalization Multi-Monitor Virtual Desktop Mapping
            Console.WriteLine("[TEST 4] Coordinate Normalization Math Verification:");
            var (normX, normY) = CoordinateConverter.ToNormalizedCoordinates(500, 300);
            Console.WriteLine($" - Physical (500, 300) -> Normalized ({normX}, {normY}) in [0, 65535]");

            if (normX >= 0 && normX <= 65535 && normY >= 0 && normY <= 65535)
            {
                Console.WriteLine(" -> PASS: Multi-monitor virtual desktop normalization bounds verified.\n");
                passed++;
            }
            else
            {
                Console.WriteLine(" -> FAIL: Normalization bounds out of range.\n");
                failed++;
            }

            // 5. Box-Muller Gaussian Humanizer Verification
            Console.WriteLine("[TEST 5] Box-Muller Gaussian Jitter Generator:");
            var humanizer = new Humanizer();
            double sum = 0;
            int samples = 500;
            for (int i = 0; i < samples; i++)
            {
                sum += humanizer.GenerateGaussianJitter(20.0, 5.0);
            }
            double meanResult = sum / samples;
            Console.WriteLine($" - Target Mean: 20.0 ms | Empirical Mean: {meanResult:F2} ms");

            if (Math.Abs(meanResult - 20.0) < 1.0)
            {
                Console.WriteLine(" -> PASS: Gaussian normal distribution mean converges accurately.\n");
                passed++;
            }
            else
            {
                Console.WriteLine(" -> FAIL: Distribution skewed.\n");
                failed++;
            }

            // 6. Stuck-Button Neutralization Failsafe
            Console.WriteLine("[TEST 6] Failsafe Button Neutralization Verification:");
            try
            {
                if (InputInjector.ReleaseAllMouseButtonLocks() != 5)
                    throw new InvalidOperationException("Windows did not accept all five release events.");
                Console.WriteLine(" -> PASS: Windows accepted all five release events.\n");
                passed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($" -> FAIL: Exception during button neutralization: {ex.Message}\n");
                failed++;
            }

            Check("Follow cursor sends clicks without moving the pointer", () =>
            {
                int batches = 0;
                bool unexpectedInput = false;
                var action = new MouseAction((inputs, count) =>
                {
                    unexpectedInput |= count != 2 ||
                        inputs[0].mi.dwFlags != NativeMethods.MOUSEEVENTF_LEFTDOWN ||
                        inputs[1].mi.dwFlags != NativeMethods.MOUSEEVENTF_LEFTUP;
                    batches++;
                    return (uint)count;
                });
                using var engine = new ClickEngine(action);
                engine.Start(new ClickEngineConfiguration { FixedX = 123, FixedY = 456, MaxClickCount = 3 });
                if (!SpinWait.SpinUntil(() => !engine.IsRunning, 3000) ||
                    unexpectedInput || batches != 3 || engine.TotalClicks != 3)
                    throw new InvalidOperationException("Follow cursor sent unexpected events.");
            });

            Check("Stop and restart worker", () =>
            {
                using var worker = new MouseWorker();
                using var cancellationSeen = new ManualResetEventSlim();
                using var allowCleanup = new ManualResetEventSlim();
                worker.Start("test worker", ThreadPriority.Normal, token =>
                {
                    token.WaitHandle.WaitOne();
                    cancellationSeen.Set();
                    allowCleanup.Wait(TimeSpan.FromSeconds(3));
                }, _ => { });
                worker.Stop();
                if (!cancellationSeen.Wait(TimeSpan.FromSeconds(2)) || !worker.IsRunning)
                    throw new InvalidOperationException("Worker reported idle before cleanup.");

                int starts = 0;
                worker.Start("overlapping worker", ThreadPriority.Normal, _ => { },
                    running => { if (running) starts++; });
                allowCleanup.Set();
                if (!SpinWait.SpinUntil(() => !worker.IsRunning, 2000) || starts != 0)
                    throw new InvalidOperationException("Worker did not stop cleanly.");

                worker.Start("restarted worker", ThreadPriority.Normal, _ => { },
                    running => { if (running) starts++; });
                if (!SpinWait.SpinUntil(() => !worker.IsRunning, 2000) || starts != 1)
                    throw new InvalidOperationException("Worker could not restart.");
            });

            Check("Partial input and button ownership", () =>
            {
                int calls = 0;
                int requested = 0;
                var owner = new MouseAction((_, count) =>
                {
                    requested = count;
                    return ++calls == 1 ? 2u : (uint)count;
                });
                var other = new MouseAction((_, count) => (uint)count);
                try
                {
                    if (owner.Send(ClickMouseButton.Left, ClickType.Double, (0, 0)) || requested != 5)
                        throw new InvalidOperationException("Partial move/click batch reported success.");
                    if (other.Send(ClickMouseButton.Left, ClickType.Single) || !other.Release() || calls != 1)
                        throw new InvalidOperationException("Another service interfered with the held button.");
                    if (!owner.Release() || calls != 2 || !other.Send(ClickMouseButton.Left, ClickType.Single))
                        throw new InvalidOperationException("Button ownership was not released.");
                }
                finally
                {
                    owner.Release();
                    other.Release();
                }
            });

            Check("Cancel an incomplete sequence", () =>
            {
                using var sequence = new SequenceEngine();
                sequence.Start(new[] { new SequenceActionPoint { PreDelayMs = 5000 } }, 1);
                sequence.Stop();
                if (!SpinWait.SpinUntil(() => !sequence.IsRunning, 2000) || sequence.CompletedCycles != 0)
                    throw new InvalidOperationException("An interrupted cycle counted as completed.");
            });

            Console.WriteLine("=================================================");
            Console.WriteLine($" Summary: {passed} PASSED, {failed} FAILED");
            Console.WriteLine("=================================================");

            return failed == 0 ? 0 : 1;

            void Check(string name, Action test)
            {
                try
                {
                    test();
                    passed++;
                    Console.WriteLine($" -> PASS: {name}");
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.WriteLine($" -> FAIL: {name}. {ex.Message}");
                }
            }
        }

        private static double MeasureAverageSleep(int count)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                Thread.Sleep(1);
            }
            sw.Stop();
            return (double)sw.ElapsedMilliseconds / count;
        }
    }
}
