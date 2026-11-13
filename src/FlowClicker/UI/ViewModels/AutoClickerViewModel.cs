using System;
using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowClicker.Core.Engine;
using FlowClicker.Core.Services.Models;
using FlowClicker.UI.Views;

namespace FlowClicker.UI.ViewModels
{
    public partial class AutoClickerViewModel : ObservableObject, IDisposable
    {
        private readonly IClickEngine _clickEngine;
        private readonly DispatcherTimer _telemetryTimer;
        private long _lastClicks;
        private long _lastTimestamp;
        private bool _isUpdatingIntervalInternal;

        [ObservableProperty]
        private bool _isRunning;

        [ObservableProperty]
        private bool _inputFailed;

        [ObservableProperty]
        private double _cps = ClickEngineConfiguration.DefaultCps;

        [ObservableProperty]
        private int _hours = 0;

        [ObservableProperty]
        private int _minutes = 0;

        [ObservableProperty]
        private int _seconds = 0;

        [ObservableProperty]
        private int _milliseconds = (int)(1000 / ClickEngineConfiguration.DefaultCps);

        [ObservableProperty]
        private ClickMouseButton _selectedButton = ClickMouseButton.Left;

        [ObservableProperty]
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

        public AutoClickerViewModel(IClickEngine clickEngine)
        {
            _clickEngine = clickEngine;
            _clickEngine.StateChanged += OnEngineStateChanged;

            _telemetryTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50) // 20 FPS UI refresh rate
            };
            _telemetryTimer.Tick += OnTelemetryTick;
        }

        private void OnEngineStateChanged(object? sender, bool running)
        {
            IsRunning = _clickEngine.IsRunning;
            InputFailed = !IsRunning && _clickEngine.InputFailed;

            if (IsRunning)
            {
                _lastClicks = 0;
                _lastTimestamp = Stopwatch.GetTimestamp();
                _telemetryTimer.Start();
            }
            else
            {
                _telemetryTimer.Stop();
                TotalClicksFormatted = _clickEngine.TotalClicks.ToString("N0");
                InstantaneousCpsFormatted = "0.0 CPS";
            }
        }

        private void OnTelemetryTick(object? sender, EventArgs e)
        {
            long currentClicks = _clickEngine.TotalClicks;
            long currentTimestamp = Stopwatch.GetTimestamp();
            double elapsed = Stopwatch.GetElapsedTime(_lastTimestamp, currentTimestamp).TotalSeconds;
            if (currentClicks != _lastClicks)
                TotalClicksFormatted = currentClicks.ToString("N0");

            InstantaneousCpsFormatted = $"{Math.Max(0, currentClicks - _lastClicks) / elapsed:F1} CPS";
            _lastClicks = currentClicks;
            _lastTimestamp = currentTimestamp;
        }

        partial void OnCpsChanged(double value)
        {
            if (_isUpdatingIntervalInternal) return;

            _isUpdatingIntervalInternal = true;
            try
            {
                if (value <= 0) value = 1.0;
                double totalMs = 1000.0 / value;

                TimeSpan ts = TimeSpan.FromMilliseconds(totalMs);
                Hours = ts.Hours;
                Minutes = ts.Minutes;
                Seconds = ts.Seconds;
                Milliseconds = ts.Milliseconds;
            }
            finally
            {
                _isUpdatingIntervalInternal = false;
            }
        }

        public void UpdateCpsFromInterval()
        {
            if (_isUpdatingIntervalInternal) return;

            _isUpdatingIntervalInternal = true;
            try
            {
                double totalMs = (Hours * 3600000.0) + (Minutes * 60000.0) + (Seconds * 1000.0) + Milliseconds;
                if (totalMs < 0.5) totalMs = 0.5;

                Cps = Math.Round(1000.0 / totalMs, 1);
            }
            finally
            {
                _isUpdatingIntervalInternal = false;
            }
        }

        [RelayCommand]
        public void ToggleClicking()
        {
            if (_clickEngine.IsRunning)
            {
                _clickEngine.Stop();
            }
            else
            {
                var config = new ClickEngineConfiguration
                {
                    Cps = Cps,
                    Button = SelectedButton,
                    ClickType = SelectedClickType,
                    LocationMode = IsCurrentCursor ? ClickLocationMode.CurrentCursor : ClickLocationMode.FixedCoordinates,
                    FixedX = FixedX,
                    FixedY = FixedY,
                    EnableHumanizer = EnableHumanizer,
                    HumanizerJitterMs = HumanizerJitterMs,
                    HumanizerRadiusPx = HumanizerRadiusPx,
                    MaxClickCount = IsInfiniteRepeat ? 0 : RepeatTimes,
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

            var picker = new CoordinatePickerWindow();
            picker.CoordinateSelected += (s, pt) =>
            {
                FixedX = pt.X;
                FixedY = pt.Y;
                IsCurrentCursor = false;
            };
            picker.ShowDialog();
        }

        public void Dispose()
        {
            _telemetryTimer.Stop();
            _clickEngine.StateChanged -= OnEngineStateChanged;
        }
    }
}
