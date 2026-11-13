using System;
using System.Threading;
using System.Windows;
using FlowClicker.Core.Engine;
using FlowClicker.Core.Native;
using FlowClicker.Core.Services;
using FlowClicker.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Appearance;

namespace FlowClicker
{
    public partial class App : Application
    {
        private const string MutexGuid = "Global\\FlowClicker_SingleInstance_E7B6C8A4-59D2-4E1B-94F0-A5E6B7C8D9E0";
        private static Mutex? _appMutex;
        public static readonly int WmActivateInstance = NativeMethods.RegisterWindowMessage("FLOWCLICKER_ACTIVATE_INSTANCE");

        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Single-Instance Enforcement
            _appMutex = new Mutex(true, MutexGuid, out bool isNewInstance);
            if (!isNewInstance)
            {
                // Broadcast wakeup to activate existing running window
                NativeMethods.PostMessage((IntPtr)NativeMethods.HWND_BROADCAST, (uint)WmActivateInstance, IntPtr.Zero, IntPtr.Zero);
                _appMutex.Dispose();
                _appMutex = null;
                Shutdown();
                return;
            }

            // Configure Dependency Injection
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            // Launch Main Window
            ApplicationThemeManager.ApplySystemTheme();
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Core Engines & Services
            services.AddSingleton<IClickEngine, ClickEngine>();
            services.AddSingleton<ISequenceEngine, SequenceEngine>();
            services.AddSingleton<IMouseUtilityService, MouseUtilityService>();
            services.AddSingleton<HotkeyManager>();

            // ViewModels
            services.AddSingleton<AutoClickerViewModel>();
            services.AddSingleton<MultiPointViewModel>();
            services.AddSingleton<MouseToolsViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<MainViewModel>();

            // Views & Windows
            services.AddSingleton<MainWindow>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Release mouse buttons and single-instance mutex
            (ServiceProvider as IDisposable)?.Dispose();
            _appMutex?.ReleaseMutex();
            _appMutex?.Dispose();
            base.OnExit(e);
        }
    }
}
