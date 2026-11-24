using System;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Animations;

namespace FlowClicker.UI.Views
{
    public partial class AutoClickerView : UserControl, IDisposable
    {
        public AutoClickerView()
        {
            InitializeComponent();
        }

        private void OnVariationOptionsVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is true && SystemParameters.ClientAreaAnimation)
                TransitionAnimationProvider.ApplyTransition(sender, Transition.FadeIn, 83);
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
