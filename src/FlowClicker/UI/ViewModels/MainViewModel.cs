using CommunityToolkit.Mvvm.ComponentModel;

namespace FlowClicker.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _applicationTitle = "FlowClicker - Windows 11 Fluent AutoClicker";

        public AutoClickerViewModel AutoClicker { get; }
        public MultiPointViewModel MultiPoint { get; }
        public MouseToolsViewModel MouseTools { get; }
        public SettingsViewModel Settings { get; }

        public MainViewModel(
            AutoClickerViewModel autoClicker,
            MultiPointViewModel multiPoint,
            MouseToolsViewModel mouseTools,
            SettingsViewModel settings)
        {
            AutoClicker = autoClicker;
            MultiPoint = multiPoint;
            MouseTools = mouseTools;
            Settings = settings;
        }
    }
}
