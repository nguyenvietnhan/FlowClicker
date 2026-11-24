using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace FlowClicker.UI.Views
{
    public partial class MultiPointView : UserControl, IDisposable
    {
        public MultiPointView()
        {
            InitializeComponent();
        }

        private void OnPointsGridLoaded(object sender, RoutedEventArgs e)
        {
            DisableHorizontalScrolling((DependencyObject)sender);
        }

        private static void DisableHorizontalScrolling(DependencyObject element)
        {
            if (element is ScrollViewer scrollViewer && scrollViewer.Name == "DG_ScrollViewer")
                scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
                DisableHorizontalScrolling(VisualTreeHelper.GetChild(element, i));
        }

        public void Dispose()
        {
            if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
