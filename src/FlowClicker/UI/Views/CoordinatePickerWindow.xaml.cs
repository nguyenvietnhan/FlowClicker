using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using FlowClicker.Core.Native;

namespace FlowClicker.UI.Views
{
    public partial class CoordinatePickerWindow : Window
    {
        private static PickerSession? _activeSession;

        private readonly PickerSession _session;
        private readonly RECT _monitorBounds;

        public static bool IsSessionActive => _activeSession != null;

        private CoordinatePickerWindow(PickerSession session, RECT monitorBounds)
        {
            _session = session;
            _monitorBounds = monitorBounds;
            InitializeComponent();
            SourceInitialized += OnSourceInitialized;
            Closed += OnWindowClosed;
        }

        public static void ShowPicker(Action<POINT> onSelected)
        {
            if (_activeSession != null)
            {
                _activeSession.Confirm();
                return;
            }

            var session = new PickerSession(onSelected);
            _activeSession = session;
            session.Start();
        }

        public static void ConfirmActivePicker() => _activeSession?.Confirm();

        public static void CancelActivePicker() => _activeSession?.Cancel();

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.SetWindowPos(
                hwnd,
                NativeMethods.HWND_TOPMOST,
                _monitorBounds.Left,
                _monitorBounds.Top,
                _monitorBounds.Width,
                _monitorBounds.Height,
                NativeMethods.SWP_SHOWWINDOW);
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            _session.UpdateCursorVisual();
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            _session.Confirm();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.F7)
            {
                e.Handled = true;
                _session.Confirm();
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                _session.Cancel();
            }
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            _session.OnOverlayClosed();
        }

        private void UpdateBadgeForCursor(int physicalX, int physicalY)
        {
            if (!_monitorBounds.Contains(physicalX, physicalY))
            {
                CoordBadge.Visibility = Visibility.Collapsed;
                return;
            }

            CoordText.Text = $"X: {physicalX}, Y: {physicalY}";
            CoordBadge.Visibility = Visibility.Visible;

            var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            double dipX = (physicalX - _monitorBounds.Left) * transform.M11;
            double dipY = (physicalY - _monitorBounds.Top) * transform.M22;

            double badgeW = CoordBadge.ActualWidth > 0 ? CoordBadge.ActualWidth : 280;
            double badgeH = CoordBadge.ActualHeight > 0 ? CoordBadge.ActualHeight : 32;
            double maxLeft = Math.Max(8, ActualWidth - badgeW - 8);
            double maxTop = Math.Max(8, ActualHeight - badgeH - 8);

            Canvas.SetLeft(CoordBadge, Math.Clamp(dipX + 16, 8, maxLeft));
            Canvas.SetTop(CoordBadge, Math.Clamp(dipY + 16, 8, maxTop));
        }

        private sealed class PickerSession
        {
            private readonly Action<POINT> _onSelected;
            private readonly List<CoordinatePickerWindow> _windows = new();
            private readonly DispatcherTimer _cursorTimer;
            private bool _finished;

            public PickerSession(Action<POINT> onSelected)
            {
                _onSelected = onSelected;
                _cursorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                _cursorTimer.Tick += (_, _) => UpdateCursorVisual();
            }

            public void Start()
            {
                var (cursorX, cursorY) = CoordinateConverter.GetCurrentCursorPosition();
                CoordinatePickerWindow? activeWindow = null;

                foreach (var bounds in CoordinateConverter.GetMonitorBounds())
                {
                    var window = new CoordinatePickerWindow(this, bounds);
                    _windows.Add(window);
                    window.Show();
                    if (bounds.Contains(cursorX, cursorY))
                        activeWindow = window;
                }

                (activeWindow ?? _windows[0]).Activate();
                UpdateCursorVisual();
                _cursorTimer.Start();
            }

            public void UpdateCursorVisual()
            {
                if (_finished) return;
                var (x, y) = CoordinateConverter.GetCurrentCursorPosition();
                foreach (var window in _windows)
                    window.UpdateBadgeForCursor(x, y);
            }

            public void Confirm()
            {
                if (_finished) return;
                var (x, y) = CoordinateConverter.GetCurrentCursorPosition();
                CloseAll();
                _onSelected(new POINT(x, y));
            }

            public void Cancel()
            {
                if (_finished) return;
                CloseAll();
            }

            public void OnOverlayClosed()
            {
                if (!_finished)
                    CloseAll();
            }

            private void CloseAll()
            {
                _finished = true;
                _cursorTimer.Stop();
                if (ReferenceEquals(_activeSession, this))
                    _activeSession = null;

                foreach (var window in _windows)
                {
                    if (window.IsVisible)
                        window.Close();
                }
            }
        }
    }
}
