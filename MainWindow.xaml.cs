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
                            StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f38ba8"));
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

            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        if (DefenderManager.IsTamperProtectionEnabled())
                        {
                            Log("[0] Tamper Protection is ON - temporarily disabling to allow changes...");
                            DefenderManager.DisableTamperProtection();
                            Log("  Tamper Protection disabled.\n");
                        }

                        Log("[1/6] Stopping Defender services...");
                        DefenderManager.DeactivatePart1_Services();
                        Log("  Services stopped and disabled.\n");

                        Log("[2/6] Writing disable registry policies...");
                        DefenderManager.DeactivatePart2_Registry();
                        Log("  Registry policies applied.\n");

                        Log("[3/6] Configuring MpPreference via PowerShell...");
                        bool mpOk = DefenderManager.DeactivatePart3_MpPreference();
                        Log(mpOk ? "  MpPreference configured.\n" : "  WARNING: MpPreference step reported a failure.\n");

                        Log("[4/6] Blocking Defender executables...");
                        DefenderManager.DeactivatePart4_BlockExecutables();
                        Log("  Executables blocked.\n");

                        Log("[5/6] Cleaning Defender folders...");
                        DefenderManager.DeactivatePart5_CleanFolders();
                        Log("  Folders cleaned.\n");

                        Log("[6/6] Disabling SmartScreen...");
                        DefenderManager.DeactivatePart6_SmartScreen();
                        Log("  SmartScreen disabled.\n");

                        Log("=== COMPLETED: Windows Defender is now under your control ===\n");
                        Log("System Antimalware (MsMpEng.exe) has been stopped.");
                        Log("Real-time Protection is OFF.");
                        Log("All Defender services are disabled.");
                        Log("");

                        Dispatcher.Invoke(() =>
                        {
                            TxtWarning.Visibility = Visibility.Visible;
                            TxtWarning.Text = "Note: Some changes may require a restart to fully take effect.";
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
                        DefenderManager.ActivatePart1_RestoreExecutables();
                        Log("  Executables restored.\n");

                        Log("[2/5] Enabling Defender services...");
                        DefenderManager.ActivatePart2_Services();
                        Log("  Services enabled.\n");

                        Log("[3/5] Removing disable registry policies...");
                        DefenderManager.ActivatePart3_Registry();
                        Log("  Registry policies removed.\n");

                        Log("[4/5] Configuring MpPreference via PowerShell...");
                        bool mpOk = DefenderManager.ActivatePart4_MpPreference();
                        Log(mpOk ? "  MpPreference restored.\n" : "  WARNING: MpPreference step reported a failure.\n");

                        Log("[5/5] Re-enabling SmartScreen...");
                        DefenderManager.ActivatePart5_SmartScreen();
                        Log("  SmartScreen enabled.\n");

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
    }
}
