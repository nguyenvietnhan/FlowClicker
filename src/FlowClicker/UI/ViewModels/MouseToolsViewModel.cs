using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowClicker.Core.Services;
using FlowClicker.Core.Services.Models;

namespace FlowClicker.UI.ViewModels
{
    public partial class MouseToolsViewModel : ObservableObject, IDisposable
    {
        private readonly IMouseUtilityService _mouseUtility;
        private readonly SettingsPersistenceService? _persistence;
        private bool _isInitializing;

        // Auto-Scroll properties
        [ObservableProperty]
        private bool _isAutoScrollRunning;

        [ObservableProperty]
        private int _scrollSpeed = 20;

        [ObservableProperty]
        private ScrollDirection _scrollDirection = ScrollDirection.Down;

        // Jiggler properties
        [ObservableProperty]
        private bool _isJigglerRunning;

        [ObservableProperty]
        private int _jigglerInterval = 10;

        [ObservableProperty]
        private JigglerMode _jigglerMode = JigglerMode.Lissajous;

        // Hold Lock properties
        [ObservableProperty]
        private bool _isHoldLockActive;

        [ObservableProperty]
        private ClickMouseButton _holdButton = ClickMouseButton.Left;

        [ObservableProperty]
        private bool _inputFailed;

        [ObservableProperty]
        private string _failureMessage = string.Empty;

        public MouseToolsViewModel(IMouseUtilityService mouseUtility, SettingsPersistenceService? persistence = null)
        {
            _mouseUtility = mouseUtility;
            _persistence = persistence;

            _mouseUtility.AutoScrollStateChanged += OnAutoScrollStateChanged;
            _mouseUtility.JigglerStateChanged += OnJigglerStateChanged;
            _mouseUtility.HoldLockStateChanged += OnHoldLockStateChanged;
            _mouseUtility.FailureStateChanged += OnFailureStateChanged;

            LoadFromPersistence();
        }

        private void LoadFromPersistence()
        {
            if (_persistence == null) return;
            var saved = _persistence.Current.MouseTools;
            _isInitializing = true;
            try
            {
                ScrollSpeed = Math.Clamp(saved.ScrollSpeed, 1, 100);
                ScrollDirection = saved.ScrollDirection;
                JigglerInterval = Math.Clamp(saved.JigglerInterval, 2, 120);
                JigglerMode = saved.JigglerMode;
                HoldButton = saved.HoldButton;
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private void Persist()
        {
            if (_isInitializing || _persistence == null) return;
            var s = _persistence.Current.MouseTools;
            s.ScrollSpeed = ScrollSpeed;
            s.ScrollDirection = ScrollDirection;
            s.JigglerInterval = JigglerInterval;
            s.JigglerMode = JigglerMode;
            s.HoldButton = HoldButton;
            _persistence.ScheduleSave();
        }

        partial void OnScrollSpeedChanged(int value) => Persist();
        partial void OnScrollDirectionChanged(ScrollDirection value) => Persist();
        partial void OnJigglerIntervalChanged(int value) => Persist();
        partial void OnJigglerModeChanged(JigglerMode value) => Persist();
        partial void OnHoldButtonChanged(ClickMouseButton value) => Persist();

        private void OnAutoScrollStateChanged(object? sender, bool running) => IsAutoScrollRunning = running;
        private void OnJigglerStateChanged(object? sender, bool running) => IsJigglerRunning = running;
        private void OnHoldLockStateChanged(object? sender, bool active) => IsHoldLockActive = active;

        private void OnFailureStateChanged(object? sender, EventArgs e)
        {
            InputFailed = _mouseUtility.InputFailed;
            FailureMessage = InputFailed ? InputFailureMessages.Describe(_mouseUtility.FailureReason) : string.Empty;
        }

        [RelayCommand]
        public void ToggleAutoScroll()
        {
            if (IsAutoScrollRunning)
            {
                _mouseUtility.StopAutoScroll();
            }
            else
            {
                _mouseUtility.StartAutoScroll(ScrollDirection, ScrollSpeed);
            }
        }

        [RelayCommand]
        public void ToggleJiggler()
        {
            if (IsJigglerRunning)
            {
                _mouseUtility.StopJiggler();
            }
            else
            {
                _mouseUtility.StartJiggler(JigglerInterval, JigglerMode);
            }
        }

        [RelayCommand]
        public void ToggleHoldLock()
        {
            _mouseUtility.ToggleHoldLock(HoldButton);
        }

        public void Dispose()
        {
            _mouseUtility.AutoScrollStateChanged -= OnAutoScrollStateChanged;
            _mouseUtility.JigglerStateChanged -= OnJigglerStateChanged;
            _mouseUtility.HoldLockStateChanged -= OnHoldLockStateChanged;
            _mouseUtility.FailureStateChanged -= OnFailureStateChanged;
        }
    }
}
