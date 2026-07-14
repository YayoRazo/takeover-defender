using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using TakeoverDefender.Utilities;

namespace TakeoverDefender
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            SetWindowIcon();
            Loaded += OnLoaded;
        }

        private void SetWindowIcon()
        {
            try
            {
                Icon = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/app.ico", UriKind.Absolute));
            }
            catch { }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await RefreshStatus();
        }

        private async Task RefreshStatus()
        {
            await Task.Run(() =>
            {
                Dispatcher.Invoke(() =>
                {
                    StatusTitle.Text = "Checking status...";
                    StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#a6adc8"));
                    StatusDetail.Text = "";
                });

                var state = DefenderManager.DefenderState.Unknown;

                try { state = DefenderManager.GetCurrentState(); }
                catch { }

                Dispatcher.Invoke(() =>
                {
                    switch (state)
                    {
                        case DefenderManager.DefenderState.Active:
                            StatusTitle.Text = "Defender is ACTIVE";
                            StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#a6e3a1"));
                            StatusDetail.Text = "Real-time protection, antimalware, and services are running.";
                            break;

                        case DefenderManager.DefenderState.Inactive:
                            StatusTitle.Text = "Defender appears INACTIVE";
                            StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ef4444"));
                            StatusDetail.Text = "Real-time protection and antimalware appear disabled.";
                            break;

                        default:
                            StatusTitle.Text = "Status: Unknown";
                            StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f9e2af"));
                            StatusDetail.Text = "Could not determine Defender state.";
                            break;
                    }
                });
            });
        }

        private async void BtnTakeOver_Click(object sender, RoutedEventArgs e)
        {
            BtnTakeOver.IsEnabled = false;
            BtnRestore.IsEnabled = false;
            TxtWarning.Visibility = Visibility.Collapsed;
            TxtLog.Text = "";
            Progress.Visibility = Visibility.Visible;

            Log("=== TAKEOVER: Disabling Windows Defender ===\n");
            if (PathLocator.IsSafeMode)
                Log("Safe Mode detected - Defender services will be disabled permanently this run.\n");

            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        if (DefenderManager.IsTamperProtectionEnabled())
                        {
                            Log("[0] Tamper Protection is ON - temporarily disabling to allow changes...");
                            if (!DefenderManager.DisableTamperProtection())
                            {
                                DefenderManager.OpenTamperProtectionSettings();
                                throw new InvalidOperationException(
                                    "Could not disable Tamper Protection automatically (the registry key is protected). " +
                                    "Opened Windows Security for you - turn Tamper Protection off there, then retry.");
                            }
                            Log("  Tamper Protection disabled.\n");
                        }

                        Log("[1/6] Stopping Defender services...");
                        int svcFail = DefenderManager.DeactivatePart1_Services();
                        LogResult("Services stopped and disabled.", svcFail);

                        Log("[2/6] Writing disable registry policies...");
                        int regFail = DefenderManager.DeactivatePart2_Registry();
                        LogResult("Registry policies applied.", regFail);

                        Log("[3/6] Configuring MpPreference via PowerShell...");
                        bool mpOk = DefenderManager.DeactivatePart3_MpPreference();
                        Log(mpOk ? "  MpPreference configured.\n" : "  WARNING: MpPreference step reported a failure.\n");

                        Log("[4/6] Blocking Defender executables...");
                        int exeFail = DefenderManager.DeactivatePart4_BlockExecutables();
                        LogResult("Executables blocked.", exeFail);

                        Log("[5/6] Cleaning Defender folders...");
                        DefenderManager.DeactivatePart5_CleanFolders();
                        Log("  Folders cleaned.\n");

                        Log("[6/6] Disabling SmartScreen...");
                        int ssFail = DefenderManager.DeactivatePart6_SmartScreen();
                        LogResult("SmartScreen disabled.", ssFail);

                        Log("=== COMPLETED: Windows Defender is now under your control ===\n");
                        Log("Real-time Protection, behavior monitoring, antivirus and antispyware are OFF.");
                        Log("Defender will no longer scan files or interfere with compiling/developing.\n");

                        if (PathLocator.IsSafeMode)
                        {
                            Log("Since this ran in Safe Mode, the services were disabled permanently.");
                            Log("Undo Safe Mode boot and reboot normally - WinDefend/MsMpEng.exe will not start again.");
                        }
                        else
                        {
                            Log("NOTE: MsMpEng.exe (Antimalware Service Executable) may stay loaded.");
                            Log("On modern Windows it is a Protected Process the OS keeps alive, but with");
                            Log("all engines off it uses ~0% CPU and only ~80-100 MB of idle RAM - it does");
                            Log("NOT scan and will NOT slow you down.");
                            Log("To remove that process entirely (survives reboot), boot into Safe Mode");
                            Log("and click TAKE OVER again here - no script needed.");
                        }
                        Log("");

                        Dispatcher.Invoke(() =>
                        {
                            TxtWarning.Visibility = Visibility.Visible;
                            TxtWarning.Text = "Defender protection is OFF. Some changes may require a restart; MsMpEng.exe may stay loaded but is idle.";
                        });
                    }
                    catch (Exception ex)
                    {
                        Log($"\nERROR: {ex.Message}");
                        Dispatcher.Invoke(() => MessageBox.Show($"Error: {ex.Message}", "Takeover Defender",
                            MessageBoxButton.OK, MessageBoxImage.Error));
                    }
                });
            }
            finally
            {
                Progress.Visibility = Visibility.Collapsed;
            }

            await RefreshStatus();
            BtnTakeOver.IsEnabled = true;
            BtnRestore.IsEnabled = true;
        }

        private async void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            BtnTakeOver.IsEnabled = false;
            BtnRestore.IsEnabled = false;
            TxtWarning.Visibility = Visibility.Collapsed;
            TxtLog.Text = "";
            Progress.Visibility = Visibility.Visible;

            Log("=== RESTORE: Re-enabling Windows Defender ===\n");

            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        Log("[0] Restoring Tamper Protection if it was disabled by Takeover...");
                        DefenderManager.RestoreTamperIfDisabled();
                        Log("  Tamper Protection handled.\n");

                        Log("[1/5] Restoring Defender executables...");
                        int exeFail = DefenderManager.ActivatePart1_RestoreExecutables();
                        LogResult("Executables restored.", exeFail);

                        Log("[2/5] Enabling Defender services...");
                        int svcFail = DefenderManager.ActivatePart2_Services();
                        LogResult("Services enabled.", svcFail);

                        Log("[3/5] Removing disable registry policies...");
                        int regFail = DefenderManager.ActivatePart3_Registry();
                        LogResult("Registry policies removed.", regFail);

                        Log("[4/5] Configuring MpPreference via PowerShell...");
                        bool mpOk = DefenderManager.ActivatePart4_MpPreference();
                        Log(mpOk ? "  MpPreference restored.\n" : "  WARNING: MpPreference step reported a failure.\n");

                        Log("[5/5] Re-enabling SmartScreen...");
                        int ssFail = DefenderManager.ActivatePart5_SmartScreen();
                        LogResult("SmartScreen enabled.", ssFail);

                        Log("=== COMPLETED: Windows Defender has been restored ===\n");
                        Log("Real-time Protection is ON.");
                        Log("All Defender services are re-enabled.");
                    }
                    catch (Exception ex)
                    {
                        Log($"\nERROR: {ex.Message}");
                        Dispatcher.Invoke(() => MessageBox.Show($"Error: {ex.Message}", "Takeover Defender",
                            MessageBoxButton.OK, MessageBoxImage.Error));
                    }
                });
            }
            finally
            {
                Progress.Visibility = Visibility.Collapsed;
            }

            await RefreshStatus();
            BtnTakeOver.IsEnabled = true;
            BtnRestore.IsEnabled = true;
        }

        private void Log(string message)
        {
            Dispatcher.Invoke(() =>
            {
                TxtLog.AppendText(message.TrimEnd('\n', '\r') + Environment.NewLine);
                TxtLog.ScrollToEnd();
            });
        }

        private void LogResult(string successMessage, int failures)
        {
            Log(failures > 0
                ? $"  {successMessage} WARNING: {failures} item(s) reported a failure.\n"
                : $"  {successMessage}\n");
        }
    }
}
