using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace FlowClicker.UI.Views
{
    public partial class MultiPointView : UserControl
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
                scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;

            // The WPF-UI template can show its horizontal bar even with scrolling disabled.
            if (element is ScrollBar { Name: "PART_HorizontalScrollBar" } bar)
                bar.Visibility = Visibility.Collapsed;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
                DisableHorizontalScrolling(VisualTreeHelper.GetChild(element, i));
        }
    }
}
