using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using FlowClicker.Core.Engine;
using FlowClicker.Core.Services;
using FlowClicker.Core.Services.Models;
using FlowClicker.UI.Converters;
using FlowClicker.UI.ViewModels;
using Xunit;

namespace FlowClicker.Tests
{
    public sealed class ViewModelAndPersistenceTests
    {
        private sealed class FakeClickEngine : IClickEngine
        {
            public bool IsRunning { get; set; }
            public bool InputFailed { get; set; }
            public InputFailureReason FailureReason { get; set; }
            public long TotalClicks { get; set; }
            public ClickEngineConfiguration? LastConfig { get; private set; }
            public event EventHandler<bool>? StateChanged;

            public void Start(ClickEngineConfiguration config)
            {
                LastConfig = config;
                IsRunning = true;
                StateChanged?.Invoke(this, true);
            }

            public void Stop()
            {
                IsRunning = false;
                StateChanged?.Invoke(this, false);
            }
        }

        private sealed class FakeSequenceEngine : ISequenceEngine
        {
            public bool IsRunning { get; set; }
            public bool InputFailed { get; set; }
            public InputFailureReason FailureReason { get; set; }
            public int CurrentStepIndex { get; set; }
            public int CompletedCycles { get; set; }
            public event EventHandler<bool>? StateChanged;

            public void Start(IReadOnlyList<SequenceActionPoint> points, int repeatCount = 0)
            {
                IsRunning = true;
                StateChanged?.Invoke(this, true);
            }

            public void Stop()
            {
                IsRunning = false;
                StateChanged?.Invoke(this, false);
            }
        }

        [Fact]
        public void AutoClickerViewModel_SlowIntervalDoesNotGetCoercedToOneCps()
        {
            var engine = new FakeClickEngine();
            using var vm = new AutoClickerViewModel(engine);

            vm.Hours = 0;
            vm.Minutes = 0;
            vm.Seconds = 10;
            vm.Milliseconds = 0;

            Assert.Equal(10_000.0, vm.IntervalMs);
            Assert.Equal(0.1, vm.Cps);
            Assert.Equal(1.0, vm.SliderCps);

            // Simulate WPF Slider clamping Value to Minimum=1 and pushing 1.0 back
            vm.SliderCps = 1.0;
            vm.ToggleClicking();

            Assert.NotNull(engine.LastConfig);
            Assert.Equal(10_000.0, engine.LastConfig!.ResolveIntervalMs());
        }

        [Fact]
        public void AutoClickerViewModel_HighAndHourlyIntervalsSyncLosslessly()
        {
            var engine = new FakeClickEngine();
            using var vm = new AutoClickerViewModel(engine);

            vm.Cps = 2000;
            Assert.Equal(0.5, vm.IntervalMs);
            Assert.Equal(0.5, vm.Milliseconds);
            Assert.Equal(0, vm.Seconds);

            vm.Milliseconds = 0;
            vm.Hours = 23;
            Assert.Equal(23 * 3_600_000.0, vm.IntervalMs);

            vm.Hours = 25;
            Assert.Equal(ClickEngineConfiguration.MaxIntervalMs, vm.IntervalMs);
            Assert.Equal(24, vm.Hours);

            vm.Cps = 10;
            Assert.Equal(0, vm.Hours);
            Assert.Equal(100, vm.Milliseconds);

            vm.SelectedClickType = ClickType.Hold;
            Assert.False(vm.IsNotHoldMode);
        }

        [Fact]
        public void AutoClickerViewModel_TelemetryGuardsZeroElapsedTimestamp()
        {
            var engine = new FakeClickEngine();
            using var vm = new AutoClickerViewModel(engine);

            engine.Start(new ClickEngineConfiguration());
            long t0 = Stopwatch.GetTimestamp();
            vm.SampleTelemetry(0, t0);
            vm.SampleTelemetry(5, t0); // Same timestamp: elapsed == 0
            Assert.DoesNotContain("Infinity", vm.InstantaneousCpsFormatted);
            Assert.DoesNotContain("NaN", vm.InstantaneousCpsFormatted);
        }

        [Fact]
        public void MultiPointViewModel_ReorderingRaisesOrderPropertyChanged()
        {
            var seq = new FakeSequenceEngine();
            using var vm = new MultiPointViewModel(seq);

            var second = vm.Points[1];
            var changedProps = new List<string?>();
            second.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName);

            vm.SelectedPoint = second;
            vm.MoveUp();

            Assert.Equal(1, vm.Points[0].Order);
            Assert.Equal(2, vm.Points[1].Order);
            Assert.Contains(nameof(SequenceActionPoint.Order), changedProps);

            vm.SelectedPoint = vm.Points[0];
            vm.RemovePoint();
            Assert.Single(vm.Points);
            Assert.Equal(1, vm.Points[0].Order);
        }

        [Fact]
        public void MultiPointViewModel_DisplayStepIsOneBasedWhileRunning()
        {
            var seq = new FakeSequenceEngine();
            using var vm = new MultiPointViewModel(seq);

            Assert.Equal("—", vm.DisplayStep);
            seq.CurrentStepIndex = 0;
            seq.Start(vm.Points);
            Assert.Equal("1 / 2", vm.DisplayStep);
            seq.Stop();
            Assert.Equal("—", vm.DisplayStep);
        }

        [Fact]
        public void NumberValueConverter_HandlesNullAndNaNSafely()
        {
            var converter = new NumberValueConverter();
            Assert.Equal(0, converter.ConvertBack(null, typeof(int), "0", CultureInfo.InvariantCulture));
            Assert.Equal(10d, converter.ConvertBack(double.NaN, typeof(double), "10", CultureInfo.InvariantCulture));
            Assert.Equal(42L, converter.ConvertBack(42.4, typeof(long), "1", CultureInfo.InvariantCulture));
        }

        [Fact]
        public void SettingsPersistenceService_RoundTripsAndRecoversCorruptFile()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "FlowClickerTests_" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(tempDir, "settings.json");

            try
            {
                using (var svc = new SettingsPersistenceService(path))
                {
                    svc.Current.SelectedTheme = "Dark";
                    svc.Current.IsAlwaysOnTop = true;
                    svc.Current.AutoClicker.IntervalMs = 250;
                    svc.Current.MultiPoint.Points = new List<SequencePointData>
                    {
                        new() { X = 12, Y = 34, Comment = "Saved" }
                    };
                    svc.SaveNow();
                }

                using (var reloaded = new SettingsPersistenceService(path))
                {
                    Assert.Equal("Dark", reloaded.Current.SelectedTheme);
                    Assert.True(reloaded.Current.IsAlwaysOnTop);
                    Assert.Equal(250, reloaded.Current.AutoClicker.IntervalMs);
                    Assert.NotNull(reloaded.Current.MultiPoint.Points);
                    Assert.Single(reloaded.Current.MultiPoint.Points!);
                    Assert.Equal("Saved", reloaded.Current.MultiPoint.Points![0].Comment);
                }

                File.WriteAllText(path, "{ invalid json !!!");
                using (var recovered = new SettingsPersistenceService(path))
                {
                    Assert.Equal("System", recovered.Current.SelectedTheme);
                    Assert.True(File.Exists(path + ".corrupt"));
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
