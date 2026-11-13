using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Wpf.Ui.Animations;
using FlowClicker.Core.Engine;
using FlowClicker.Core.Native;
using FlowClicker.Core.Services;
using FlowClicker.UI.ViewModels;
using FlowClicker.UI.Views;
using Wpf.Ui.Controls;

namespace FlowClicker
{
    public partial class MainWindow : FluentWindow
    {
        private readonly MainViewModel _mainViewModel;
        private readonly HotkeyManager _hotkeyManager;
        private readonly IClickEngine _clickEngine;
        private readonly ISequenceEngine _sequenceEngine;
        private readonly IMouseUtilityService _mouseUtility;

        private readonly AutoClickerView _autoClickerView;
        private readonly MultiPointView _multiPointView;
        private readonly MouseToolsView _mouseToolsView;
        private readonly SettingsView _settingsView;
        private FrameworkElement _activePage;
        private RadioButton? _selectedNavigationItem;

        public MainWindow(
            MainViewModel mainViewModel,
            HotkeyManager hotkeyManager,
            IClickEngine clickEngine,
            ISequenceEngine sequenceEngine,
            IMouseUtilityService mouseUtility)
        {
            InitializeComponent();

            _mainViewModel = mainViewModel;
            _hotkeyManager = hotkeyManager;
            _clickEngine = clickEngine;
            _sequenceEngine = sequenceEngine;
            _mouseUtility = mouseUtility;

            DataContext = _mainViewModel;

            // Instantiate Views and attach respective ViewModels
            _autoClickerView = new AutoClickerView { DataContext = _mainViewModel.AutoClicker };
            _multiPointView = new MultiPointView { DataContext = _mainViewModel.MultiPoint };
            _mouseToolsView = new MouseToolsView { DataContext = _mainViewModel.MouseTools };
            _settingsView = new SettingsView { DataContext = _mainViewModel.Settings };

            // Default initial view
            _activePage = _autoClickerView;
            PageHost.Children.Add(_activePage);
            _selectedNavigationItem = NavAutoClicker;

            Loaded += OnMainWindowLoaded;
            Closed += OnMainWindowClosed;
        }

        private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
        {
            // Initialize HotkeyManager on window handle
            _hotkeyManager.Initialize(this);

            // Register F6: Toggle AutoClicker
            _hotkeyManager.Register(NativeMethods.MOD_NONE, Key.F6, () =>
            {
                _mainViewModel.AutoClicker.ToggleClicking();
            });

            // Register Shift + Escape: Emergency Panic Neutralize
            _hotkeyManager.Register(NativeMethods.MOD_SHIFT, Key.Escape, () =>
            {
                StopWorkers();
                Core.Native.MouseAction.EmergencyRelease();
                _mouseUtility.ReleaseHoldLock();
            });

            // Register F7: Pick Coordinates
            _hotkeyManager.Register(NativeMethods.MOD_NONE, Key.F7, () =>
            {
                _mainViewModel.AutoClicker.PickCoordinates();
            });

            // Hook HwndSource for Second-Instance Activation
            var helper = new WindowInteropHelper(this);
            var source = HwndSource.FromHwnd(helper.Handle);
            source?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == App.WmActivateInstance)
            {
                // Restore window and bring to foreground
                if (WindowState == WindowState.Minimized)
                {
                    WindowState = WindowState.Normal;
                }
                NativeMethods.ShowWindowAsync(hwnd, NativeMethods.SW_RESTORE);
                NativeMethods.SetForegroundWindow(hwnd);
                Activate();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void OnNavClick(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton item || item == _selectedNavigationItem)
                return;

            var page = sender switch
            {
                _ when sender == NavMultiPoint => (FrameworkElement)_multiPointView,
                _ when sender == NavMouseTools => _mouseToolsView,
                _ when sender == NavSettings => _settingsView,
                _ => _autoClickerView
            };

            _selectedNavigationItem = item;
            SelectionScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            SelectionScale.ScaleY = 0;
            PositionSelectionIndicator();
            // Hidden pages stay measured and keep their control state between visits.
            _activePage.Visibility = Visibility.Hidden;
            if (page.Parent is null)
                PageHost.Children.Add(page);
            page.Visibility = Visibility.Visible;
            _activePage = page;
            if (SystemParameters.ClientAreaAnimation)
                TransitionAnimationProvider.ApplyTransition(page, Transition.FadeInWithSlide, 167);

            // Start after the new page has been laid out so layout time doesn't consume the animation.
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                if (_selectedNavigationItem != item)
                    return;

                SelectionScale.ScaleY = 1;
                SelectionScale.BeginAnimation(ScaleTransform.ScaleYProperty,
                    new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
                    {
                        EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                        FillBehavior = FillBehavior.Stop
                    });
            });
        }

        private void OnNavigationPaneSizeChanged(object sender, SizeChangedEventArgs e)
        {
            PositionSelectionIndicator();
        }

        private void PositionSelectionIndicator()
        {
            if (_selectedNavigationItem is not { } item)
                return;

            var center = item.TranslatePoint(new Point(0, item.ActualHeight / 2), NavigationPane);
            SelectionOffset.Y = center.Y - 8;
        }

        private void OnMainWindowClosed(object? sender, EventArgs e)
        {
            StopWorkers();
            _hotkeyManager.Dispose();
            _autoClickerView.Dispose();
        }

        private void StopWorkers()
        {
            _clickEngine.Stop();
            _sequenceEngine.Stop();
            _mouseUtility.StopAutoScroll();
            _mouseUtility.StopJiggler();
        }
    }
}
