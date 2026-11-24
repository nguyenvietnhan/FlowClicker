using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowClicker.Core.Engine;
using FlowClicker.Core.Services;
using FlowClicker.Core.Services.Models;
using FlowClicker.UI.Views;

namespace FlowClicker.UI.ViewModels
{
    public partial class AutoClickerViewModel : ObservableObject, IDisposable
    {
        private readonly IClickEngine _clickEngine;
        private readonly SettingsPersistenceService? _persistence;
        private readonly DispatcherTimer _telemetryTimer;
        private readonly Queue<(long Timestamp, long Clicks)> _rateWindow = new();
        private long _lastDisplayedClicks = -1;
        private double _intervalMs = 1000.0 / ClickEngineConfiguration.DefaultCps;
        private bool _isSyncingTiming;
        private bool _isInitializing;

        [ObservableProperty]
        private bool _isRunning;

        [ObservableProperty]
        private bool _inputFailed;

        [ObservableProperty]
        private string _failureMessage = string.Empty;

        [ObservableProperty]
        private double _cps = ClickEngineConfiguration.DefaultCps;

        [ObservableProperty]
        private int _hours = 0;

        [ObservableProperty]
        private int _minutes = 0;

        [ObservableProperty]
        private int _seconds = 0;

        [ObservableProperty]
        private double _milliseconds = 1000.0 / ClickEngineConfiguration.DefaultCps;

        [ObservableProperty]
        private ClickMouseButton _selectedButton = ClickMouseButton.Left;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotHoldMode))]
        private ClickType _selectedClickType = ClickType.Single;

        [ObservableProperty]
        private bool _isCurrentCursor = true;

        [ObservableProperty]
        private int _fixedX = 100;

        [ObservableProperty]
        private int _fixedY = 100;

        [ObservableProperty]
        private bool _enableHumanizer = false;

        [ObservableProperty]
        private double _humanizerJitterMs = 5.0;

        [ObservableProperty]
        private int _humanizerRadiusPx = 2;

        [ObservableProperty]
        private bool _isInfiniteRepeat = true;

        [ObservableProperty]
        private long _repeatTimes = 100;

        [ObservableProperty]
        private string _totalClicksFormatted = "0";

        [ObservableProperty]
        private string _instantaneousCpsFormatted = "0.0 CPS";

        [ObservableProperty]
        private TimerEngineMode _engineMode = TimerEngineMode.Eco;

        public bool IsNotHoldMode => SelectedClickType != ClickType.Hold;

        public double SliderCps
        {
            get => Math.Clamp(Cps, 1.0, 1000.0);
            set
            {
                if (_isSyncingTiming) return;
                if (Cps < 1.0 && value <= 1.0) return;
                Cps = Math.Clamp(value, 1.0, 1000.0);
            }
        }

        public double IntervalMs => _intervalMs;

        public AutoClickerViewModel(IClickEngine clickEngine, SettingsPersistenceService? persistence = null)
        {
            _clickEngine = clickEngine;
            _persistence = persistence;
            _clickEngine.StateChanged += OnEngineStateChanged;

            _telemetryTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50) // 20 FPS UI refresh rate
            };
            _telemetryTimer.Tick += OnTelemetryTick;

            LoadFromPersistence();
        }

        private void LoadFromPersistence()
        {
            if (_persistence == null) return;
            var saved = _persistence.Current.AutoClicker;
            _isInitializing = true;
            _isSyncingTiming = true;
            try
            {
                _intervalMs = Math.Clamp(saved.IntervalMs, ClickEngineConfiguration.MinIntervalMs, ClickEngineConfiguration.MaxIntervalMs);
                Cps = Math.Round(1000.0 / _intervalMs, 4);
                ApplyIntervalToComponents(_intervalMs);
                SelectedButton = saved.Button;
                SelectedClickType = saved.ClickType;
                IsCurrentCursor = saved.IsCurrentCursor;
                FixedX = saved.FixedX;
                FixedY = saved.FixedY;
                EnableHumanizer = saved.EnableHumanizer;
                HumanizerJitterMs = Math.Clamp(saved.HumanizerJitterMs, 1.0, 50.0);
                HumanizerRadiusPx = Math.Clamp(saved.HumanizerRadiusPx, 0, 15);
                IsInfiniteRepeat = saved.IsInfiniteRepeat;
                RepeatTimes = Math.Max(1, saved.RepeatTimes);
                EngineMode = saved.EngineMode;
                OnPropertyChanged(nameof(SliderCps));
            }
            finally
            {
                _isSyncingTiming = false;
                _isInitializing = false;
            }
        }

        private void Persist()
        {
            if (_isInitializing || _persistence == null) return;
            var s = _persistence.Current.AutoClicker;
            s.IntervalMs = _intervalMs;
            s.Button = SelectedButton;
            s.ClickType = SelectedClickType;
            s.IsCurrentCursor = IsCurrentCursor;
            s.FixedX = FixedX;
            s.FixedY = FixedY;
            s.EnableHumanizer = EnableHumanizer;
            s.HumanizerJitterMs = HumanizerJitterMs;
            s.HumanizerRadiusPx = HumanizerRadiusPx;
            s.IsInfiniteRepeat = IsInfiniteRepeat;
            s.RepeatTimes = RepeatTimes;
            s.EngineMode = EngineMode;
            _persistence.ScheduleSave();
        }

        private void OnEngineStateChanged(object? sender, bool running)
        {
            IsRunning = _clickEngine.IsRunning;
            InputFailed = !IsRunning && _clickEngine.InputFailed;
            FailureMessage = InputFailed ? InputFailureMessages.Describe(_clickEngine.FailureReason) : string.Empty;

            if (IsRunning)
            {
                _rateWindow.Clear();
                _rateWindow.Enqueue((Stopwatch.GetTimestamp(), 0));
                _lastDisplayedClicks = 0;
                TotalClicksFormatted = "0";
                _telemetryTimer.Start();
            }
            else
            {
                _telemetryTimer.Stop();
                TotalClicksFormatted = _clickEngine.TotalClicks.ToString("N0");
                InstantaneousCpsFormatted = "0.0 CPS";
            }
        }

        internal void SampleTelemetry(long currentClicks, long currentTimestamp)
        {
            if (currentClicks != _lastDisplayedClicks)
            {
                TotalClicksFormatted = currentClicks.ToString("N0");
                _lastDisplayedClicks = currentClicks;
            }

            _rateWindow.Enqueue((currentTimestamp, currentClicks));
            while (_rateWindow.Count > 1 &&
                   Stopwatch.GetElapsedTime(_rateWindow.Peek().Timestamp, currentTimestamp).TotalSeconds > 0.5)
            {
                _rateWindow.Dequeue();
            }

            var oldest = _rateWindow.Peek();
            double elapsed = Stopwatch.GetElapsedTime(oldest.Timestamp, currentTimestamp).TotalSeconds;
            if (elapsed <= 0)
                return;

            double rate = Math.Max(0, currentClicks - oldest.Clicks) / elapsed;
            InstantaneousCpsFormatted = $"{rate:F1} CPS";
        }

        private void OnTelemetryTick(object? sender, EventArgs e)
        {
            SampleTelemetry(_clickEngine.TotalClicks, Stopwatch.GetTimestamp());
        }

        partial void OnCpsChanged(double value)
        {
            if (_isSyncingTiming) return;

            _isSyncingTiming = true;
            try
            {
                double clampedCps = Math.Clamp(double.IsNaN(value) || value <= 0 ? 1.0 : value, 0.0001, 2000.0);
                if (Math.Abs(clampedCps - value) > 1e-9)
                    Cps = clampedCps;

                _intervalMs = Math.Clamp(1000.0 / clampedCps, ClickEngineConfiguration.MinIntervalMs, ClickEngineConfiguration.MaxIntervalMs);
                ApplyIntervalToComponents(_intervalMs);
                OnPropertyChanged(nameof(SliderCps));
            }
            finally
            {
                _isSyncingTiming = false;
            }
            Persist();
        }

        partial void OnHoursChanged(int value)
        {
            if (_isSyncingTiming) return;
            if (value < 0) Hours = 0;
            SyncFromIntervalComponents();
        }

        partial void OnMinutesChanged(int value)
        {
            if (_isSyncingTiming) return;
            if (value < 0) Minutes = 0;
            SyncFromIntervalComponents();
        }

        partial void OnSecondsChanged(int value)
        {
            if (_isSyncingTiming) return;
            if (value < 0) Seconds = 0;
            SyncFromIntervalComponents();
        }

        partial void OnMillisecondsChanged(double value)
        {
            if (_isSyncingTiming) return;
            if (double.IsNaN(value) || value < 0) Milliseconds = 0;
            SyncFromIntervalComponents();
        }

        partial void OnSelectedButtonChanged(ClickMouseButton value) => Persist();
        partial void OnSelectedClickTypeChanged(ClickType value) => Persist();
        partial void OnIsCurrentCursorChanged(bool value) => Persist();
        partial void OnFixedXChanged(int value) => Persist();
        partial void OnFixedYChanged(int value) => Persist();
        partial void OnEnableHumanizerChanged(bool value) => Persist();
        partial void OnHumanizerJitterMsChanged(double value) => Persist();
        partial void OnHumanizerRadiusPxChanged(int value) => Persist();
        partial void OnIsInfiniteRepeatChanged(bool value) => Persist();
        partial void OnRepeatTimesChanged(long value)
        {
            if (value < 1)
                RepeatTimes = 1;
            Persist();
        }
        partial void OnEngineModeChanged(TimerEngineMode value) => Persist();

        private void ApplyIntervalToComponents(double intervalMs)
        {
            long wholeMs = (long)Math.Floor(intervalMs);
            double fracMs = Math.Round(intervalMs - wholeMs, 1);
            if (fracMs >= 1.0)
            {
                wholeMs += 1;
                fracMs = 0;
            }

            TimeSpan ts = TimeSpan.FromMilliseconds(wholeMs);
            Hours = (int)ts.TotalHours;
            Minutes = ts.Minutes;
            Seconds = ts.Seconds;
            Milliseconds = Math.Round(ts.Milliseconds + fracMs, 1);
        }

        private void SyncFromIntervalComponents()
        {
            if (_isSyncingTiming) return;

            _isSyncingTiming = true;
            try
            {
                double totalMs = (Math.Max(0, Hours) * 3_600_000.0)
                    + (Math.Max(0, Minutes) * 60_000.0)
                    + (Math.Max(0, Seconds) * 1_000.0)
                    + Math.Max(0.0, Milliseconds);
                _intervalMs = Math.Clamp(totalMs, ClickEngineConfiguration.MinIntervalMs, ClickEngineConfiguration.MaxIntervalMs);
                if (totalMs > ClickEngineConfiguration.MaxIntervalMs)
                    ApplyIntervalToComponents(_intervalMs);
                Cps = Math.Round(1000.0 / _intervalMs, 4);
                OnPropertyChanged(nameof(SliderCps));
            }
            finally
            {
                _isSyncingTiming = false;
            }
            Persist();
        }

        public void UpdateCpsFromInterval() => SyncFromIntervalComponents();

        [RelayCommand]
        public void ToggleClicking()
        {
            if (_clickEngine.IsRunning)
            {
                _clickEngine.Stop();
            }
            else
            {
                InputFailed = false;
                FailureMessage = string.Empty;

                var config = new ClickEngineConfiguration
                {
                    Cps = Cps,
                    IntervalMs = _intervalMs,
                    Button = SelectedButton,
                    ClickType = SelectedClickType,
                    LocationMode = IsCurrentCursor ? ClickLocationMode.CurrentCursor : ClickLocationMode.FixedCoordinates,
                    FixedX = FixedX,
                    FixedY = FixedY,
                    EnableHumanizer = EnableHumanizer,
                    HumanizerJitterMs = HumanizerJitterMs,
                    HumanizerRadiusPx = HumanizerRadiusPx,
                    MaxClickCount = IsInfiniteRepeat ? 0 : Math.Max(1, RepeatTimes),
                    EngineMode = EngineMode
                };
                _clickEngine.Start(config);
            }
        }

        [RelayCommand]
        public void PickCoordinates()
        {
            if (_clickEngine.IsRunning)
                return;

            CoordinatePickerWindow.ShowPicker(pt =>
            {
                FixedX = pt.X;
                FixedY = pt.Y;
                IsCurrentCursor = false;
            });
        }

        public void Dispose()
        {
            _telemetryTimer.Stop();
            _clickEngine.StateChanged -= OnEngineStateChanged;
        }
    }
}
