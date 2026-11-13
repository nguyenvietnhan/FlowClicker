using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowClicker.Core.Services;

namespace FlowClicker.UI.ViewModels
{
    public partial class MouseToolsViewModel : ObservableObject
    {
        private readonly IMouseUtilityService _mouseUtility;

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
        private bool _holdLockIsLeftButton = true;

        public MouseToolsViewModel(IMouseUtilityService mouseUtility)
        {
            _mouseUtility = mouseUtility;

            _mouseUtility.AutoScrollStateChanged += (s, running) => IsAutoScrollRunning = running;
            _mouseUtility.JigglerStateChanged += (s, running) => IsJigglerRunning = running;
            _mouseUtility.HoldLockStateChanged += (s, active) => IsHoldLockActive = active;
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
            _mouseUtility.ToggleHoldLock(HoldLockIsLeftButton);
        }
    }
}
