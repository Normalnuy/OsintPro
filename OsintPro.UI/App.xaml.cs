using System;
using System.Diagnostics;
using System.Windows;
using OsintPro.UI.Services;
using Sentry;

namespace OsintPro.UI
{
    public partial class App : Application
    {
        public App()
        {
            this.DispatcherUnhandledException += (s, e) =>
            {
                SentrySdk.CaptureException(e.Exception);
                KillBackend();
                MessageBox.Show($"Помилка: {e.Exception.Message}\n\n{e.Exception.InnerException?.Message}",
                                "КРАШ ПРОГРАМИ", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    SentrySdk.CaptureException(ex);
                    MessageBox.Show($"Системна помилка: {ex?.Message}\n\n{ex?.InnerException?.Message}",
                                    "КРАШ ПРОГРАМИ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                KillBackend();
            };

            AppDomain.CurrentDomain.ProcessExit += (s, e) => KillBackend();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            AppSettings.Load();
            SentryService.InitializeFromSettings();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            KillBackend();
            base.OnExit(e);
        }

        private void KillBackend()
        {
            try
            {
                System.Threading.Tasks.Task.Run(async () => await BrowserManager.CloseAllAsync()).Wait(2000);
            }
            catch { }

            try
            {
                foreach (var process in Process.GetProcessesByName("chrome"))
                {
                    try
                    {
                        if (process.MainModule?.FileName.Contains("ms-playwright") == true)
                            process.Kill();
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}