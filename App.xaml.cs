using System;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using TakeoverDefender.Utilities;

namespace TakeoverDefender
{
    public partial class App : Application
    {
        private static Mutex _singleInstanceMutex;
        public App()
        {
            DispatcherUnhandledException += (s, e) =>
            {
                MessageBox.Show($"Unhandled error: {e.Exception.Message}", "Takeover Defender", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
                Environment.Exit(1);
            };
        }

        private void App_Startup(object sender, StartupEventArgs e)
        {
            var os = Environment.OSVersion.Version;
            if (os.Major < 6 || (os.Major == 6 && os.Minor < 2))
            {
                MessageBox.Show(
                    "This software requires Windows 8 or later.\n\n" +
                    "Windows Defender on Windows 7 and earlier is an anti-spyware only.\n" +
                    "Full antivirus capabilities were introduced in Windows 8.",
                    "Takeover Defender - Unsupported Version",
                    MessageBoxButton.OK, MessageBoxImage.Stop);
                Environment.Exit(1);
            }

            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                MessageBox.Show("This application requires Administrator privileges.\nPlease run as Administrator.", "Takeover Defender", MessageBoxButton.OK, MessageBoxImage.Warning);
                Environment.Exit(1);
            }

            CheckSingleInstance();
            CommandExecutor.CleanupStaleSystemTasks();

            new MainWindow().Show();
        }

        private static void CheckSingleInstance()
        {
            bool createdNew;
            _singleInstanceMutex = new Mutex(initiallyOwned: true, name: @"Global\TakeoverDefender_SingleInstance", createdNew: out createdNew);
            if (!createdNew)
            {
                MessageBox.Show("Another instance is already running.", "Takeover Defender", MessageBoxButton.OK, MessageBoxImage.Information);
                Environment.Exit(0);
            }
        }
    }
}
