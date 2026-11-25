using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using FlowClicker.Core.Engine;
using FlowClicker.Core.Native;
using FlowClicker.Core.Services;
using FlowClicker.Core.Services.Models;
using Xunit;

namespace FlowClicker.Tests
{
    public sealed class CoreEngineTests
    {
        [Fact]
        public void Win32InputStructs_HaveExpected64BitLayout()
        {
            Assert.Equal(40, Marshal.SizeOf<INPUT>());
            Assert.Equal(32, Marshal.SizeOf<MOUSEINPUT>());
            Assert.Equal(24, Marshal.SizeOf<KEYBDINPUT>());
            Assert.Equal(8, Marshal.SizeOf<HARDWAREINPUT>());
        }

        [Fact]
        public void CoordinateConverter_NormalizesVirtualDesktopIncludingNegativeOffsets()
        {
            var (leftNormX, leftNormY) = CoordinateConverter.NormalizeToBounds(-1920, -1080, -1920, -1080, 3840, 2160);
            var (rightNormX, rightNormY) = CoordinateConverter.NormalizeToBounds(1919, 1079, -1920, -1080, 3840, 2160);

            Assert.InRange(leftNormX, 0, 65535);
            Assert.InRange(leftNormY, 0, 65535);
            Assert.InRange(rightNormX, 0, 65535);
            Assert.InRange(rightNormY, 0, 65535);
            Assert.Equal(0, (leftNormX * 3840) >> 16);
            Assert.Equal(0, (leftNormY * 2160) >> 16);
            Assert.Equal(3839, (rightNormX * 3840) >> 16);
            Assert.Equal(2159, (rightNormY * 2160) >> 16);
        }

        [Fact]
        public void CoordinateConverter_RoundtripsOnDual4KWidth()
        {
            const int dual4KWidth = 7680;
            for (int x = 0; x < dual4KWidth; x += 257)
            {
                var (nx, _) = CoordinateConverter.NormalizeToBounds(x, 0, 0, 0, dual4KWidth, 2160);
                Assert.Equal(x, (nx * dual4KWidth) >> 16);
            }
            var (edgeX, _) = CoordinateConverter.NormalizeToBounds(dual4KWidth - 1, 0, 0, 0, dual4KWidth, 2160);
            Assert.Equal(dual4KWidth - 1, (edgeX * dual4KWidth) >> 16);
        }

        [Fact]
        public void Humanizer_ConvergesMeanAndRespectsRadiusBounds()
        {
            var humanizer = new Humanizer();
            double sum = 0;
            const int samples = 600;
            for (int i = 0; i < samples; i++)
            {
                double val = humanizer.GenerateGaussianJitter(20.0, 5.0);
                Assert.True(val >= 0.5);
                sum += val;
            }
            Assert.InRange(sum / samples, 18.5, 21.5);

            Assert.Equal((0, 0), humanizer.GenerateCoordinateJitter(0));
            for (int i = 0; i < 100; i++)
            {
                var (dx, dy) = humanizer.GenerateCoordinateJitter(5);
                Assert.InRange(dx, -5, 5);
                Assert.InRange(dy, -5, 5);
                Assert.True((dx * dx) + (dy * dy) <= 32); // Math.Round per axis on radius 5 can reach (4, 4)
            }
        }

        [Fact]
        public void ClickEngine_FollowCursor_SendsWithoutMovingPointer()
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
            Assert.True(SpinWait.SpinUntil(() => !engine.IsRunning, 3000));
            Assert.False(unexpectedInput);
            Assert.Equal(3, batches);
            Assert.Equal(3, engine.TotalClicks);
        }

        [Fact]
        public void ClickEngine_HoldMode_SendsSingleDownAndReleasesOnStop()
        {
            var sentFlags = new List<uint>();
            var firstDownSent = new ManualResetEventSlim();
            var action = new MouseAction((inputs, count) =>
            {
                for (int i = 0; i < count; i++)
                    sentFlags.Add(inputs[i].mi.dwFlags);
                firstDownSent.Set();
                return (uint)count;
            });

            using var engine = new ClickEngine(action);
            engine.Start(new ClickEngineConfiguration
            {
                Cps = 200,
                ClickType = ClickType.Hold,
                Button = ClickMouseButton.Left,
                LocationMode = ClickLocationMode.FixedCoordinates,
                FixedX = 100,
                FixedY = 100
            });

            Assert.True(firstDownSent.Wait(2000));
            Thread.Sleep(80); // Ensure high CPS does NOT spam repeated DOWN events
            Assert.Equal(1, engine.TotalClicks);
            Assert.Equal(2, sentFlags.Count); // 1 MOVE + 1 LEFTDOWN

            engine.Stop();
            Assert.True(SpinWait.SpinUntil(() => !engine.IsRunning, 3000));
            Assert.Equal(3, sentFlags.Count);
            Assert.Equal(NativeMethods.MOUSEEVENTF_LEFTUP, sentFlags[2]);
        }

        [Fact]
        public void ClickEngine_FixedCoordinatesDoubleClick_SendsFiveInputBatch()
        {
            int lastCount = 0;
            uint[] flags = Array.Empty<uint>();
            var action = new MouseAction((inputs, count) =>
            {
                lastCount = count;
                flags = new uint[count];
                for (int i = 0; i < count; i++)
                    flags[i] = inputs[i].mi.dwFlags;
                return (uint)count;
            });

            using var engine = new ClickEngine(action);
            engine.Start(new ClickEngineConfiguration
            {
                LocationMode = ClickLocationMode.FixedCoordinates,
                FixedX = 200,
                FixedY = 300,
                ClickType = ClickType.Double,
                Button = ClickMouseButton.Right,
                MaxClickCount = 1
            });

            Assert.True(SpinWait.SpinUntil(() => !engine.IsRunning, 3000));
            Assert.Equal(5, lastCount);
            Assert.Equal(NativeMethods.MOUSEEVENTF_ABSOLUTE | NativeMethods.MOUSEEVENTF_VIRTUALDESK | NativeMethods.MOUSEEVENTF_MOVE, flags[0]);
            Assert.Equal(NativeMethods.MOUSEEVENTF_RIGHTDOWN, flags[1]);
            Assert.Equal(NativeMethods.MOUSEEVENTF_RIGHTUP, flags[2]);
            Assert.Equal(NativeMethods.MOUSEEVENTF_RIGHTDOWN, flags[3]);
            Assert.Equal(NativeMethods.MOUSEEVENTF_RIGHTUP, flags[4]);
        }

        [Fact]
        public void ClickEngineConfiguration_PreservesSlowIntervalsWithoutClampingToPointOneCps()
        {
            var cfg = new ClickEngineConfiguration { IntervalMs = 3_600_000.0, Cps = 0.0003 };
            Assert.Equal(3_600_000.0, cfg.ResolveIntervalMs());
        }

        [Fact]
        public void ClickEngine_WhenButtonHeldByAnotherOwner_SetsButtonOwnedByOtherReason()
        {
            var holder = new MouseAction((_, count) => (uint)count);
            var clickerAction = new MouseAction((_, count) => (uint)count);
            try
            {
                Assert.True(holder.Send(ClickMouseButton.Left, ClickType.Hold));
                using var engine = new ClickEngine(clickerAction);
                engine.Start(new ClickEngineConfiguration { Button = ClickMouseButton.Left, MaxClickCount = 1 });
                Assert.True(SpinWait.SpinUntil(() => !engine.IsRunning, 3000));
                Assert.True(engine.InputFailed);
                Assert.Equal(InputFailureReason.ButtonOwnedByOther, engine.FailureReason);
            }
            finally
            {
                holder.Release();
            }
        }

        [Fact]
        public void MouseWorker_StopAndRapidRestart_WorksCleanly()
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
            Assert.True(cancellationSeen.Wait(TimeSpan.FromSeconds(2)));
            Assert.True(worker.IsRunning);

            int starts = 0;
            worker.Start("overlapping worker", ThreadPriority.Normal, _ => { },
                running => { if (running) starts++; });
            allowCleanup.Set();
            Assert.True(SpinWait.SpinUntil(() => !worker.IsRunning, 2000));
            Assert.Equal(0, starts);

            worker.Start("restarted worker", ThreadPriority.Normal, _ => { },
                running => { if (running) starts++; });
            Assert.True(SpinWait.SpinUntil(() => !worker.IsRunning, 2000));
            Assert.Equal(1, starts);
        }

        [Fact]
        public void MouseAction_PartialBatchAndOwnership_PreventsInterference()
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
                Assert.False(owner.Send(ClickMouseButton.Left, ClickType.Double, (0, 0)));
                Assert.Equal(5, requested);
                Assert.False(other.Send(ClickMouseButton.Left, ClickType.Single));
                Assert.True(other.Release());
                Assert.Equal(1, calls);
                Assert.True(owner.Release());
                Assert.Equal(2, calls);
                Assert.True(other.Send(ClickMouseButton.Left, ClickType.Single));
            }
            finally
            {
                owner.Release();
                other.Release();
            }
        }

        [Fact]
        public void SequenceEngine_ExecutesStepsAndStopsAtRepeatCount()
        {
            int calls = 0;
            var action = new MouseAction((_, count) =>
            {
                calls++;
                return (uint)count;
            });

            using var sequence = new SequenceEngine(action);
            var points = new[]
            {
                new SequenceActionPoint { X = 10, Y = 10, PreDelayMs = 0, PostDelayMs = 0 },
                new SequenceActionPoint { X = 20, Y = 20, PreDelayMs = 0, PostDelayMs = 0 }
            };

            sequence.Start(points, repeatCount: 2);
            Assert.True(SpinWait.SpinUntil(() => !sequence.IsRunning, 3000));
            Assert.Equal(2, sequence.CompletedCycles);
            Assert.Equal(4, calls);
            Assert.False(sequence.InputFailed);
        }

        [Fact]
        public void SequenceEngine_CancelIncompleteCycle_DoesNotCountCompleted()
        {
            using var sequence = new SequenceEngine();
            sequence.Start(new[] { new SequenceActionPoint { PreDelayMs = 5000 } }, 1);
            sequence.Stop();
            Assert.True(SpinWait.SpinUntil(() => !sequence.IsRunning, 2000));
            Assert.Equal(0, sequence.CompletedCycles);
        }

        [Fact]
        public void MouseUtilityService_HoldLock_ReportsFailureWhenInjectionFails()
        {
            var holdAction = new MouseAction((_, _) => 0u);
            using var service = new MouseUtilityService(holdAction, _ => 1u);

            service.ToggleHoldLock(ClickMouseButton.Left);
            Assert.False(service.IsHoldLockActive);
            Assert.True(service.InputFailed);
            Assert.Equal(InputFailureReason.InjectionBlocked, service.FailureReason);
        }

        [Fact]
        public void MouseUtilityService_HoldLock_NotifiesSenderOnSuccessAndRelease()
        {
            var holdAction = new MouseAction((_, count) => (uint)count);
            using var service = new MouseUtilityService(holdAction, _ => 1u);

            object? capturedSender = null;
            bool? capturedState = null;
            service.HoldLockStateChanged += (s, active) =>
            {
                capturedSender = s;
                capturedState = active;
            };

            service.ToggleHoldLock(ClickMouseButton.Middle);
            Assert.Same(service, capturedSender);
            Assert.True(capturedState);
            Assert.True(service.IsHoldLockActive);

            service.ReleaseHoldLock();
            Assert.False(capturedState);
            Assert.False(service.IsHoldLockActive);
        }

        [Fact]
        public void MouseUtilityService_AutoScroll_ReportsFailureWhenInjectionFails()
        {
            var holdAction = new MouseAction((_, count) => (uint)count);
            using var service = new MouseUtilityService(holdAction, _ => 0u);

            service.StartAutoScroll(ScrollDirection.Down, 20);
            Assert.True(SpinWait.SpinUntil(() => !service.IsAutoScrollRunning && service.InputFailed, 2000));
            Assert.Equal(InputFailureReason.InjectionBlocked, service.FailureReason);
        }

        [Fact]
        public void MouseAction_EmergencyRelease_SkipsUnpressedButtonsEspeciallyXButtons()
        {
            var holder = new MouseAction((_, count) => (uint)count);
            var releasedFlags = new List<uint>();

            try
            {
                // Nothing held and no physical keys down -> injects 0 events (no stray XBUTTON1/XBUTTON2 UP)
                uint count = MouseAction.EmergencyRelease((inputs, n) =>
                {
                    for (int i = 0; i < n; i++) releasedFlags.Add(inputs[i].mi.dwFlags);
                    return (uint)n;
                }, _ => false);
                Assert.Equal(0u, count);
                Assert.Empty(releasedFlags);

                // Hold only Left button -> releases only Left button
                Assert.True(holder.Send(ClickMouseButton.Left, ClickType.Hold));
                Assert.True(MouseAction.HasAnyOwnedButton());

                count = MouseAction.EmergencyRelease((inputs, n) =>
                {
                    for (int i = 0; i < n; i++) releasedFlags.Add(inputs[i].mi.dwFlags);
                    return (uint)n;
                }, _ => false);

                Assert.Equal(1u, count);
                Assert.Single(releasedFlags);
                Assert.Equal(NativeMethods.MOUSEEVENTF_LEFTUP, releasedFlags[0]);
                Assert.False(MouseAction.HasAnyOwnedButton());

                // Unowned stuck Right key -> releases only Right button
                releasedFlags.Clear();
                count = MouseAction.EmergencyRelease((inputs, n) =>
                {
                    for (int i = 0; i < n; i++) releasedFlags.Add(inputs[i].mi.dwFlags);
                    return (uint)n;
                }, vk => vk == NativeMethods.VK_RBUTTON);

                Assert.Equal(1u, count);
                Assert.Single(releasedFlags);
                Assert.Equal(NativeMethods.MOUSEEVENTF_RIGHTUP, releasedFlags[0]);
            }
            finally
            {
                holder.Release();
            }
        }
    }
}
