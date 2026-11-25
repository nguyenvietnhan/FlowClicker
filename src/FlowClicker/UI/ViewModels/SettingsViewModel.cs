using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowClicker.Core.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace FlowClicker.UI.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly SettingsPersistenceService? _persistence;
        private Window? _watchedWindow;
        private bool _isInitializing;

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
        private string _hotkeyMultiPoint = "F8";

        [ObservableProperty]
        private bool _isAdministrator;

        [ObservableProperty]
        private bool _hasHotkeyWarning;

        [ObservableProperty]
        private string _hotkeyWarningMessage = string.Empty;

        [ObservableProperty]
        private bool _hasAdminError;

        [ObservableProperty]
        private string _adminErrorMessage = string.Empty;

        public string AppVersion =>
            $"v{typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.6"}";

        public event EventHandler? StopWorkersRequested;

        public SettingsViewModel(SettingsPersistenceService? persistence = null)
        {
            _persistence = persistence;
            CheckAdminStatus();
            LoadFromPersistence();
        }

        private void LoadFromPersistence()
        {
            if (_persistence == null) return;
            _isInitializing = true;
            try
            {
                SelectedTheme = _persistence.Current.SelectedTheme is "Dark" or "Light" or "System"
                    ? _persistence.Current.SelectedTheme
                    : "System";
                IsAlwaysOnTop = _persistence.Current.IsAlwaysOnTop;
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private void CheckAdminStatus()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            IsAdministrator = principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public void AttachWindow(Window window)
        {
            _watchedWindow = window;
            UpdateSystemThemeWatcher();
        }

        public void DetachWindow()
        {
            if (_watchedWindow != null)
            {
                SystemThemeWatcher.UnWatch(_watchedWindow);
                _watchedWindow = null;
            }
        }

        public void ApplyTheme()
        {
            if (SelectedTheme == "Dark")
            {
                if (_watchedWindow != null) SystemThemeWatcher.UnWatch(_watchedWindow);
                ApplicationThemeManager.Apply(ApplicationTheme.Dark, WindowBackdropType.Mica);
            }
            else if (SelectedTheme == "Light")
            {
                if (_watchedWindow != null) SystemThemeWatcher.UnWatch(_watchedWindow);
                ApplicationThemeManager.Apply(ApplicationTheme.Light, WindowBackdropType.Mica);
            }
            else
            {
                ApplicationThemeManager.ApplySystemTheme();
                UpdateSystemThemeWatcher();
            }
        }

        private void UpdateSystemThemeWatcher()
        {
            if (_watchedWindow == null) return;
            if (SelectedTheme == "System")
                SystemThemeWatcher.Watch(_watchedWindow, WindowBackdropType.Mica);
            else
                SystemThemeWatcher.UnWatch(_watchedWindow);
        }

        partial void OnSelectedThemeChanged(string value)
        {
            if (Application.Current != null)
                ApplyTheme();

            if (!_isInitializing && _persistence != null)
            {
                _persistence.Current.SelectedTheme = value;
                _persistence.ScheduleSave();
            }
        }

        partial void OnIsAlwaysOnTopChanged(bool value)
        {
            if (!_isInitializing && _persistence != null)
            {
                _persistence.Current.IsAlwaysOnTop = value;
                _persistence.ScheduleSave();
            }
        }

        public void ReportHotkeyRegistrationFailures(IReadOnlyList<string> failedHotkeys)
        {
            if (failedHotkeys.Count == 0)
            {
                HasHotkeyWarning = false;
                HotkeyWarningMessage = string.Empty;
                return;
            }

            HasHotkeyWarning = true;
            HotkeyWarningMessage =
                $"Could not register global shortcut(s): {string.Join(", ", failedHotkeys)}. Another application is already using them.";
        }

        [RelayCommand]
        public void RestartAsAdmin()
        {
            HasAdminError = false;
            AdminErrorMessage = string.Empty;

            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                HasAdminError = true;
                AdminErrorMessage = "Could not locate the FlowClicker executable path.";
                return;
            }

            _persistence?.SaveNow();
            StopWorkersRequested?.Invoke(this, EventArgs.Empty);

            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = App.WaitForInstanceArg,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                Process.Start(processInfo);
                Application.Current?.Shutdown();
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // User cancelled UAC elevation prompt; continue running normally.
            }
            catch (Exception ex)
            {
                HasAdminError = true;
                AdminErrorMessage = $"Could not restart as administrator: {ex.Message}";
            }
        }
    }
}
