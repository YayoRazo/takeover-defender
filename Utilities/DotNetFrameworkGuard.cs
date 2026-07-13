using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Windows;

namespace TakeoverDefender.Utilities
{
    internal static class DotNetFrameworkGuard
    {
        // .NET Framework 4.8 minimum Release DWORD, per Microsoft's official detection
        // table (learn.microsoft.com/dotnet/framework/migration-guide/how-to-determine-which-versions-are-installed).
        // Check is >=, not ==: later OS builds report higher Release values for the same 4.8.
        internal const int MinRelease = 528040;
        private const string OfflineInstallerUrl = "https://go.microsoft.com/fwlink/?linkid=2088631";

        internal static bool IsInstalled(int release) => release >= MinRelease;

        internal static void EnsureInstalledOrExit()
        {
            int release = RegistryHelp.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full", "Release", 0);
            if (IsInstalled(release))
                return;

            var choice = MessageBox.Show(
                "Takeover Defender requires .NET Framework 4.8 or later, which is not installed on this computer.\n\n" +
                "Download and install it now? (~120 MB, needs internet and Administrator rights)",
                "Takeover Defender - .NET Framework Required",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (choice != MessageBoxResult.Yes)
                Environment.Exit(1);

            InstallAndRelaunch();
        }

        private static void InstallAndRelaunch()
        {
            string installerPath = Path.Combine(Path.GetTempPath(), "NDP48-x86-x64-AllOS-ENU.exe");
            try
            {
                using (var client = new WebClient())
                    client.DownloadFile(OfflineInstallerUrl, installerPath);

                var psi = new ProcessStartInfo(installerPath, "/q /norestart") { UseShellExecute = true };
                using Process installer = Process.Start(psi);
                installer.WaitForExit();

                // 0 = success. 3010/1641 = success, reboot required (still usable per Microsoft's docs).
                if (installer.ExitCode == 0 || installer.ExitCode == 3010 || installer.ExitCode == 1641)
                {
                    var relaunch = new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName) { UseShellExecute = true };
                    Process.Start(relaunch);
                    Environment.Exit(0);
                }

                MessageBox.Show(
                    $".NET Framework installation did not complete (exit code {installer.ExitCode}).\n\n" +
                    $"Takeover Defender cannot run without it. Install it manually from:\n{OfflineInstallerUrl}",
                    "Takeover Defender - Installation Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $".NET Framework installation was interrupted: {ex.Message}\n\n" +
                    $"Install it manually from:\n{OfflineInstallerUrl}",
                    "Takeover Defender - Installation Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
            finally
            {
                try { File.Delete(installerPath); } catch { }
            }
        }
    }
}
