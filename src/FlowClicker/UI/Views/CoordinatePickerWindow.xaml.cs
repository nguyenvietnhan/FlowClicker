using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FlowClicker.Core.Native;

namespace FlowClicker.UI.Views
{
    public partial class CoordinatePickerWindow : Window
    {
        public event EventHandler<POINT>? CoordinateSelected;

        public CoordinatePickerWindow()
        {
            InitializeComponent();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            var (x, y) = CoordinateConverter.GetCurrentCursorPosition();
            CoordText.Text = $"X: {x}, Y: {y}";

            Point mousePos = e.GetPosition(this);
            Canvas.SetLeft(CoordBadge, mousePos.X + 15);
            Canvas.SetTop(CoordBadge, mousePos.Y + 15);
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            CaptureAndClose();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.F7)
            {
                CaptureAndClose();
            }
            else if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        private void CaptureAndClose()
        {
            var (x, y) = CoordinateConverter.GetCurrentCursorPosition();
            CoordinateSelected?.Invoke(this, new POINT(x, y));
            Close();
        }
    }
}
