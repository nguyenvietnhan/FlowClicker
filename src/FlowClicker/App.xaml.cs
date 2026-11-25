using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
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
        public const string WaitForInstanceArg = "--wait-for-instance";
        private const string MutexName = "Local\\FlowClicker_SingleInstance_E7B6C8A4-59D2-4E1B-94F0-A5E6B7C8D9E0";
        private static Mutex? _appMutex;
        private static bool _ownsMutex;
        public static readonly int WmActivateInstance = NativeMethods.RegisterWindowMessage("FLOWCLICKER_ACTIVATE_INSTANCE");

        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

            bool waitForPrevious = e.Args.Contains(WaitForInstanceArg, StringComparer.OrdinalIgnoreCase);
            if (!AcquireSingleInstanceMutex(waitForPrevious))
            {
                NativeMethods.PostMessage((IntPtr)NativeMethods.HWND_BROADCAST, (uint)WmActivateInstance, IntPtr.Zero, IntPtr.Zero);
                Shutdown();
                return;
            }

            // Configure Dependency Injection
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            // Launch Main Window
            var settingsVm = ServiceProvider.GetRequiredService<SettingsViewModel>();
            settingsVm.ApplyTheme();
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        private static bool AcquireSingleInstanceMutex(bool waitForPrevious)
        {
            _appMutex = new Mutex(false, MutexName, out bool createdNew);
            if (createdNew)
            {
                try
                {
                    _ownsMutex = _appMutex.WaitOne(0);
                }
                catch (AbandonedMutexException)
                {
                    _ownsMutex = true;
                }
                return true;
            }

            int timeoutMs = waitForPrevious ? 5000 : 0;
            try
            {
                _ownsMutex = _appMutex.WaitOne(timeoutMs);
            }
            catch (AbandonedMutexException)
            {
                _ownsMutex = true;
            }

            if (!_ownsMutex)
            {
                _appMutex.Dispose();
                _appMutex = null;
            }

            return _ownsMutex;
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Core Engines & Services
            services.AddSingleton<SettingsPersistenceService>();
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

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MouseAction.EmergencyRelease();
        }

        private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            MouseAction.EmergencyRelease();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            DispatcherUnhandledException -= OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException -= OnDomainUnhandledException;

            // Dispose services first so only buttons held by FlowClicker are released,
            // then flush settings and release the single-instance mutex.
            (ServiceProvider as IDisposable)?.Dispose();
            if (MouseAction.HasAnyOwnedButton())
                MouseAction.EmergencyRelease();
            if (_appMutex != null)
            {
                if (_ownsMutex)
                {
                    try { _appMutex.ReleaseMutex(); } catch { }
                    _ownsMutex = false;
                }
                _appMutex.Dispose();
                _appMutex = null;
            }
            base.OnExit(e);
        }
    }
}
