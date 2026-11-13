using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace FlowClicker.UI.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isAlwaysOnTop = false;

        [ObservableProperty]
        private string _selectedTheme = "System";

        [ObservableProperty]
        private string _hotkeyStartStop = "F6";

        [ObservableProperty]
        private string _hotkeyPanic = "Shift + Escape";

        [ObservableProperty]
        private string _hotkeyPick = "F7";

        [ObservableProperty]
        private bool _isAdministrator;

        public SettingsViewModel()
        {
            CheckAdminStatus();
        }

        private void CheckAdminStatus()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            IsAdministrator = principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        partial void OnSelectedThemeChanged(string value)
        {
            if (value == "Dark")
            {
                ApplicationThemeManager.Apply(ApplicationTheme.Dark, WindowBackdropType.Mica);
            }
            else if (value == "Light")
            {
                ApplicationThemeManager.Apply(ApplicationTheme.Light, WindowBackdropType.Mica);
            }
            else
            {
                ApplicationThemeManager.ApplySystemTheme();
            }
        }

        [RelayCommand]
        public void RestartAsAdmin()
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "FlowClicker.exe",
                    UseShellExecute = true,
                    Verb = "runas"
                };

                Process.Start(processInfo);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsViewModel] User declined elevation or elevation failed: {ex.Message}");
            }
        }
    }
}
