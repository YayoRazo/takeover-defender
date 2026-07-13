using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceProcess;
using System.Text;
using Microsoft.Win32;

namespace TakeoverDefender.Utilities
{
    internal static class DefenderManager
    {
        private const int StartBoot = 0;
        private const int StartSystem = 1;
        private const int StartAuto = 2;
        private const int StartDemand = 3;
        private const int StartDisabled = 4;

        private static Dictionary<string, int> _services;
        private static readonly Dictionary<string, int> _originalStartTypes = new Dictionary<string, int>();
        private static bool _tamperDisabledByUs;
        private static int? _originalTamperValue;

        private static Dictionary<string, int> GetServices()
        {
            if (_services != null)
                return _services;

            _services = new Dictionary<string, int>
            {
                { "WinDefend", StartAuto },
                { "SecurityHealthService", StartAuto },
                { "Sense", StartDemand },
                { "SgrmAgent", StartDemand },
                { "SgrmBroker", StartDemand },
                { "WdFilter", StartBoot },
                { "WdBoot", StartBoot },
                { "WdNisDrv", StartDemand },
                { "WdNisSvc", StartDemand },
                { "wscsvc", StartAuto }
            };

            if (PathLocator.IsWindows10Plus)
            {
                _services["MDCoreSvc"] = StartDemand;
                _services["MsSecCore"] = StartDemand;
                _services["MsSecFlt"] = StartDemand;
                _services["MsSecWfp"] = StartDemand;
                _services["webthreatdefsvc"] = StartDemand;
                _services["webthreatdefusersvc"] = StartAuto;
            }

            return _services;
        }

        public enum DefenderState
        {
            Unknown,
            Active,
            Inactive
        }

        public static DefenderState GetCurrentState()
        {
            try
            {
                int? disableRealtime = RegistryHelp.GetValue<int?>(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection",
                    "DisableRealtimeMonitoring", null);

                int? disableAntiSpyware = RegistryHelp.GetValue<int?>(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows Defender",
                    "DisableAntiSpyware", null);

                bool serviceRunning = IsServiceRunning("WinDefend");
                return EvaluateState(disableRealtime, disableAntiSpyware, serviceRunning);
            }
            catch
            {
                return DefenderState.Unknown;
            }
        }

        internal static DefenderState EvaluateState(int? disableRealtime, int? disableAntiSpyware, bool serviceRunning)
        {
            if ((disableRealtime.HasValue && disableRealtime.Value == 1) ||
                (disableAntiSpyware.HasValue && disableAntiSpyware.Value == 1))
                return DefenderState.Inactive;

            return serviceRunning ? DefenderState.Active : DefenderState.Inactive;
        }

        private static bool IsServiceRunning(string name)
        {
            try
            {
                using ServiceController sc = new ServiceController(name);
                return sc.Status == ServiceControllerStatus.Running;
            }
            catch
            {
                return false;
            }
        }

        public static int DeactivatePart1_Services()
        {
            int failures = DisableDefenderServices();
            TerminateDefenderProcesses();
            return failures;
        }

        public static int DeactivatePart2_Registry()
        {
            return WriteDisableRegistryPolicies();
        }

        public static bool DeactivatePart3_MpPreference()
        {
            return SetMpPreferenceDisable();
        }

        public static int DeactivatePart4_BlockExecutables()
        {
            return BlockDefenderExecutables();
        }

        public static void DeactivatePart5_CleanFolders()
        {
            CleanDefenderFolders();
        }

        public static int DeactivatePart6_SmartScreen()
        {
            return SetSmartScreenOff();
        }

        public static int ActivatePart1_RestoreExecutables()
        {
            return RestoreDefenderExecutables();
        }

        public static int ActivatePart2_Services()
        {
            return EnableDefenderServices();
        }

        public static int ActivatePart3_Registry()
        {
            return RemoveDisableRegistryPolicies();
        }

        public static bool ActivatePart4_MpPreference()
        {
            return SetMpPreferenceEnable();
        }

        public static int ActivatePart5_SmartScreen()
        {
            return SetSmartScreenOn();
        }

        internal enum TamperStatus { Off, On, Unknown }

        private static TamperStatus TamperState(int val)
        {
            if (val == 0) return TamperStatus.Off;
            if (val == 1) return TamperStatus.On;
            return TamperStatus.Unknown;
        }

        public static bool IsTamperProtectionEnabled()
        {
            if (!PathLocator.IsWindows10Plus)
                return false;

            int val = RegistryHelp.GetValue<int>(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Features",
                "TamperProtection", 5);
            TamperStatus s = TamperState(val);
            return s == TamperStatus.On || s == TamperStatus.Unknown;
        }

        public static void DisableTamperProtection()
        {
            if (!PathLocator.IsWindows10Plus)
                return;

            int val = RegistryHelp.GetValue<int>(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Features",
                "TamperProtection", 5);
            if (TamperState(val) == TamperStatus.Off)
                return;

            _originalTamperValue = val;
            _tamperDisabledByUs = true;

            CommandExecutor.RunAsSystem(
                @"reg add ""HKLM\SOFTWARE\Microsoft\Windows Defender\Features"" /v TamperProtection /t REG_DWORD /d 0 /f & " +
                @"reg add ""HKLM\SOFTWARE\Microsoft\Windows Defender\Features"" /v TamperProtectionSource /t REG_DWORD /d 2 /f");
        }

        public static void EnableTamperProtection()
        {
            if (!PathLocator.IsWindows10Plus)
                return;

            CommandExecutor.RunAsSystem(
                @"reg add ""HKLM\SOFTWARE\Microsoft\Windows Defender\Features"" /v TamperProtection /t REG_DWORD /d 1 /f & " +
                @"reg add ""HKLM\SOFTWARE\Microsoft\Windows Defender\Features"" /v TamperProtectionSource /t REG_DWORD /d 2 /f");
        }

        public static void RestoreTamperIfDisabled()
        {
            if (!_tamperDisabledByUs)
                return;

            EnableTamperProtection();
            _tamperDisabledByUs = false;
            _originalTamperValue = null;
        }

        private static void TerminateDefenderProcesses()
        {
            string procs = "smartscreen.exe mpdefendercoreservice.exe msmpeng.exe securityhealthservice.exe " +
                "securityhealthsystray.exe securityhealthui.exe mssense.exe nissrv.exe " +
                "mdcoresvc.exe msseccore.exe mssecflt.exe mssecwfp.exe sgrmbroker.exe " +
                "wdboot.exe wdnissvc.exe mpcmdrun.exe mpcopyaccelerator.exe mpextms.exe " +
                "mpsigstub.exe mrt.exe configsecuritypolicy.exe sensedlpprocessor.exe " +
                "sensecm.exe senseir.exe sensendr.exe sensetvm.exe sensece.exe senseui.exe";

            CommandExecutor.RunAsSystem($"taskkill /f /im {procs.Replace(" ", " /im ")}");
        }

        private static bool SetMpPreferenceDisable()
        {
            string script = @"
Set-MpPreference -DisableIOAVProtection $true
Set-MpPreference -DisableRealtimeMonitoring $true
Set-MpPreference -DisableBehaviorMonitoring $true
Set-MpPreference -DisableBlockAtFirstSeen $true
Set-MpPreference -DisablePrivacyMode $true
Set-MpPreference -SignatureDisableUpdateOnStartupWithoutEngine $true
Set-MpPreference -DisableArchiveScanning $true
Set-MpPreference -DisableIntrusionPreventionSystem $true
Set-MpPreference -DisableScriptScanning $true
Set-MpPreference -SubmitSamplesConsent 2
Set-MpPreference -MAPSReporting 0
Set-MpPreference -PUAProtection Disabled
";
            int code = CommandExecutor.RunPowerShell(script);
            return code == 0;
        }

        private static bool SetMpPreferenceEnable()
        {
            string script = @"
Set-MpPreference -DisableIOAVProtection $false
Set-MpPreference -DisableRealtimeMonitoring $false
Set-MpPreference -DisableBehaviorMonitoring $false
Set-MpPreference -DisableBlockAtFirstSeen $false
Set-MpPreference -DisablePrivacyMode $false
Set-MpPreference -SignatureDisableUpdateOnStartupWithoutEngine $false
Set-MpPreference -DisableArchiveScanning $false
Set-MpPreference -DisableIntrusionPreventionSystem $false
Set-MpPreference -DisableScriptScanning $false
Set-MpPreference -SubmitSamplesConsent 1
Set-MpPreference -MAPSReporting 2
Set-MpPreference -PUAProtection Enabled
";
            int code = CommandExecutor.RunPowerShell(script);
            return code == 0;
        }

        private static int DisableDefenderServices()
        {
            CaptureOriginalStartTypes();
            var cmds = new StringBuilder();
            foreach (var svc in GetServices())
                cmds.Append($"sc stop \"{svc.Key}\" & sc config \"{svc.Key}\" start= disabled & ");
            CommandExecutor.RunAsSystem(cmds.ToString().TrimEnd(' ', '&'));

            int failures = 0;
            foreach (var svc in GetServices())
            {
                int start = RegistryHelp.GetValue<int>(
                    $@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\{svc.Key}", "Start", -1);
                if (start != StartDisabled) failures++;
            }
            return failures;
        }

        private static int EnableDefenderServices()
        {
            var cmds = new StringBuilder();
            foreach (var svc in GetServices())
            {
                int target = _originalStartTypes.TryGetValue(svc.Key, out int orig) ? orig : svc.Value;
                cmds.Append($"sc config \"{svc.Key}\" start= {StartTypeToSc(target)} & ");
                if (target == StartAuto)
                    cmds.Append($"sc start \"{svc.Key}\" & ");
            }
            CommandExecutor.RunAsSystem(cmds.ToString().TrimEnd(' ', '&'));

            int failures = 0;
            foreach (var svc in GetServices())
            {
                int start = RegistryHelp.GetValue<int>(
                    $@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\{svc.Key}", "Start", -1);
                if (start == StartDisabled || start < 0) failures++;
            }
            return failures;
        }

        private static void CaptureOriginalStartTypes()
        {
            _originalStartTypes.Clear();
            foreach (var svc in GetServices())
            {
                int start = RegistryHelp.GetValue<int>(
                    $@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\{svc.Key}", "Start", svc.Value);
                _originalStartTypes[svc.Key] = start;
            }
        }

        private static string StartTypeToSc(int start)
        {
            switch (start)
            {
                case StartBoot: return "boot";
                case StartSystem: return "system";
                case StartAuto: return "auto";
                case StartDemand: return "demand";
                case StartDisabled: return "disabled";
                default: return "demand";
            }
        }

        private static int WriteDisableRegistryPolicies()
        {
            int failures = 0;
            void W(string subkey, string name, object data)
            {
                if (!RegistryHelp.Write(Registry.LocalMachine, subkey, name, data, RegistryValueKind.DWord))
                    failures++;
            }

            const string P = @"SOFTWARE\Policies\Microsoft\Windows Defender";

            W(P, "DisableAntiSpyware", 1);
            W(P, "DisableAntiVirus", 1);
            W(P, "ServiceKeepAlive", 0);
            W(P, "AllowFastService", 0);

            const string RT = @"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection";
            W(RT, "DisableRealtimeMonitoring", 1);
            W(RT, "DisableBehaviorMonitoring", 1);
            W(RT, "DisableIOAVProtection", 1);
            W(RT, "DisableOnAccessProtection", 1);
            W(RT, "DisableScanOnRealtimeEnable", 1);
            W(RT, "DisableScriptScanning", 1);
            W(RT, "DisableIntrusionPreventionSystem", 1);
            W(RT, "DisableArchiveScanning", 1);
            W(RT, "DisableBlockAtFirstSeen", 1);
            W(RT, "DisablePrivacyMode", 1);

            W(@"SOFTWARE\Policies\Microsoft\Windows Defender\Features", "TamperProtection", 0);

            W(@"SOFTWARE\Policies\Microsoft\Windows Defender\MpEngine", "MpEnablePus", 0);
            W(@"SOFTWARE\Policies\Microsoft\Windows Defender\MpEngine", "MpCloudBlockLevel", 0);

            W(@"SOFTWARE\Policies\Microsoft\Windows Defender\Scan", "DisableHeuristics", 1);
            W(@"SOFTWARE\Policies\Microsoft\Windows Defender\Scan", "DisableEmailScanning", 1);
            W(@"SOFTWARE\Policies\Microsoft\Windows Defender\Scan", "DisableCatchupQuickScan", 1);
            W(@"SOFTWARE\Policies\Microsoft\Windows Defender\Scan", "DisableCatchupFullScan", 1);
            W(@"SOFTWARE\Policies\Microsoft\Windows Defender\Scan", "DisableArchiveScanning", 1);

            W(@"SOFTWARE\Policies\Microsoft\Windows Defender\Signature Updates", "DisableScheduledSignatureUpdateOnBattery", 1);
            W(@"SOFTWARE\Policies\Microsoft\Windows Defender\Signature Updates", "UpdateOnStartUp", 0);
            W(@"SOFTWARE\Policies\Microsoft\Windows Defender\Signature Updates", "DisableUpdateOnStartupWithoutEngine", 1);
            W(@"SOFTWARE\Policies\Microsoft\Windows Defender\Signature Updates", "DisableScanOnUpdate", 1);

            W(@"SOFTWARE\Policies\Microsoft\Windows Defender\Reporting", "DisableGenericRePorts", 1);
            W(@"SOFTWARE\Policies\Microsoft\Windows Defender\UX Configuration", "Notification_Suppress", 1);

            W(@"SOFTWARE\Policies\Microsoft\Windows Defender Security Center\Notifications", "DisableEnhancedNotifications", 1);
            W(@"SOFTWARE\Policies\Microsoft\Windows Defender Security Center\Notifications", "DisableNotifications", 1);

            W(@"SOFTWARE\Policies\Microsoft\Microsoft Antimalware", "DisableAntiSpyware", 1);
            W(@"SOFTWARE\Policies\Microsoft\Microsoft Antimalware", "DisableAntiVirus", 1);

            W(@"SOFTWARE\Microsoft\Security Center", "AntiVirusOverride", 1);
            W(@"SOFTWARE\Microsoft\Security Center", "FirewallOverride", 1);

            const string FW = @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy";
            W(FW + @"\DomainProfile", "DisableNotifications", 1);
            W(FW + @"\PrivateProfile", "DisableNotifications", 1);
            W(FW + @"\StandardProfile", "DisableNotifications", 1);

            return failures;
        }

        private static int RemoveDisableRegistryPolicies()
        {
            int failures = 0;
            const string FW = @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy";

            if (!RegistryHelp.DeleteFolder(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows Defender")) failures++;
            if (!RegistryHelp.DeleteFolder(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Microsoft Antimalware")) failures++;
            if (!RegistryHelp.DeleteValue(Registry.LocalMachine, @"SOFTWARE\Microsoft\Security Center", "AntiVirusOverride")) failures++;
            if (!RegistryHelp.DeleteValue(Registry.LocalMachine, @"SOFTWARE\Microsoft\Security Center", "FirewallOverride")) failures++;

            if (!RegistryHelp.Write(Registry.LocalMachine, FW + @"\DomainProfile", "DisableNotifications", 0, RegistryValueKind.DWord)) failures++;
            if (!RegistryHelp.Write(Registry.LocalMachine, FW + @"\PrivateProfile", "DisableNotifications", 0, RegistryValueKind.DWord)) failures++;
            if (!RegistryHelp.Write(Registry.LocalMachine, FW + @"\StandardProfile", "DisableNotifications", 0, RegistryValueKind.DWord)) failures++;

            return failures;
        }

        private static string OwnRename(string from, string toName)
        {
            return $"takeown /f \"{from}\" /a && icacls \"{from}\" /grant *S-1-5-32-544:F && rename \"{from}\" \"{toName}\" & ";
        }

        private static int BlockDefenderExecutables()
        {
            var targets = PathLocator.GetDefenderExeTargets();
            var cmds = new StringBuilder();
            var attempted = new List<string>();

            foreach (var (normal, block) in targets)
            {
                if (!File.Exists(normal))
                    continue;
                attempted.Add(normal);
                cmds.Append(OwnRename(normal, Path.GetFileName(block)));
            }

            string platformDir = PathLocator.DefenderPlatform;
            if (Directory.Exists(platformDir))
            {
                foreach (string versionDir in Directory.GetDirectories(platformDir))
                {
                    string msMpEng = Path.Combine(versionDir, "MsMpEng.exe");
                    if (File.Exists(msMpEng))
                    {
                        attempted.Add(msMpEng);
                        cmds.Append(OwnRename(msMpEng, "BlockAntimalware.exe"));
                    }
                }
            }

            if (cmds.Length > 0)
                CommandExecutor.RunAsSystem(cmds.ToString().TrimEnd(' ', '&'));

            int failures = 0;
            foreach (string f in attempted)
                if (File.Exists(f)) failures++;
            return failures;
        }

        private static int RestoreDefenderExecutables()
        {
            var targets = PathLocator.GetDefenderExeTargets();
            var cmds = new StringBuilder();
            var expectedNormals = new List<string>();

            foreach (var (normal, block) in targets)
            {
                if (!File.Exists(block))
                    continue;

                expectedNormals.Add(normal);
                if (File.Exists(normal))
                    cmds.Append($"takeown /f \"{normal}\" /a && del /f /q \"{normal}\" && ");

                cmds.Append(OwnRename(block, Path.GetFileName(normal)));
            }

            string platformDir = PathLocator.DefenderPlatform;
            if (Directory.Exists(platformDir))
            {
                foreach (string versionDir in Directory.GetDirectories(platformDir))
                {
                    string blocked = Path.Combine(versionDir, "BlockAntimalware.exe");
                    if (File.Exists(blocked))
                    {
                        expectedNormals.Add(Path.Combine(versionDir, "MsMpEng.exe"));
                        cmds.Append(OwnRename(blocked, "MsMpEng.exe"));
                    }
                }
            }

            if (cmds.Length > 0)
                CommandExecutor.RunAsSystem(cmds.ToString().TrimEnd(' ', '&'));

            int failures = 0;
            foreach (string f in expectedNormals)
                if (!File.Exists(f)) failures++;
            return failures;
        }

        private static void CleanDefenderFolders()
        {
            var cmds = new StringBuilder();
            foreach (string folder in PathLocator.FoldersToClean)
            {
                if (!Directory.Exists(folder))
                    continue;

                foreach (string file in Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly))
                    cmds.Append($"takeown /f \"{file}\" /a && del /f /q \"{file}\" & ");
            }

            if (cmds.Length > 0)
                CommandExecutor.RunAsSystem(cmds.ToString().TrimEnd(' ', '&'));
        }

        private static int SetSmartScreenOff()
        {
            int failures = 0;
            void W(string subkey, string name, object data, RegistryValueKind kind)
            {
                if (!RegistryHelp.Write(Registry.LocalMachine, subkey, name, data, kind)) failures++;
            }

            W(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "SmartScreenEnabled", "Off", RegistryValueKind.String);
            W(@"SOFTWARE\Policies\Microsoft\Windows\System", "EnableSmartScreen", 0, RegistryValueKind.DWord);
            W(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost", "EnableWebContentEvaluation", 0, RegistryValueKind.DWord);
            W(@"SOFTWARE\Policies\Microsoft\MicrosoftEdge\PhishingFilter", "EnabledV9", 0, RegistryValueKind.DWord);
            return failures;
        }

        private static int SetSmartScreenOn()
        {
            int failures = 0;
            if (!RegistryHelp.Write(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer",
                "SmartScreenEnabled", "On", RegistryValueKind.String)) failures++;
            if (!RegistryHelp.DeleteValue(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableSmartScreen")) failures++;
            if (!RegistryHelp.DeleteValue(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost", "EnableWebContentEvaluation")) failures++;
            if (!RegistryHelp.DeleteFolder(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\MicrosoftEdge\PhishingFilter")) failures++;
            return failures;
        }
    }
}
