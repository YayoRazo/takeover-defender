using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace TakeoverDefender.Utilities
{
    internal static class PathLocator
    {
        internal static string FindExecutable(params string[] names)
        {
            if (names == null || names.Length == 0)
                return string.Empty;

            foreach (string name in names)
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                if (Path.IsPathRooted(name) && File.Exists(name))
                    return name;

                string[] extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE")
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string ext in extensions)
                {
                    string fullName = Path.HasExtension(name) ? name : name + ext;
                    string path = FindInPath(fullName);
                    if (!string.IsNullOrEmpty(path))
                        return path;
                }
            }

            return string.Empty;
        }

        private static string FindInPath(string fileName)
        {
            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            string[] dirs = pathEnv.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);

            HashSet<string> searchDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string d in dirs)
            {
                string trimmed = d.Trim().Trim('"');
                if (!string.IsNullOrEmpty(trimmed))
                    searchDirs.Add(trimmed);
            }

            if (!string.IsNullOrEmpty(Environment.SystemDirectory))
                searchDirs.Add(Environment.SystemDirectory);

            string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrEmpty(windir))
            {
                searchDirs.Add(Path.Combine(windir, "System32"));
                searchDirs.Add(Path.Combine(windir, "SysWOW64"));
            }

            foreach (string dir in searchDirs)
            {
                try
                {
                    string candidate = Path.Combine(dir, fileName);
                    if (File.Exists(candidate))
                        return candidate;
                }
                catch { }
            }

            return string.Empty;
        }

        internal static readonly string SystemDrive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System));
        internal static readonly string ProgramFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        internal static readonly string ProgramFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        internal static readonly string SystemDirectory = Environment.SystemDirectory;
        internal static readonly string PowerShell = FindExecutable("pwsh.exe", "powershell.exe");
        internal static readonly string CommandShell = FindExecutable("cmd.exe");
        internal static readonly string BcdEdit = FindExecutable("bcdedit.exe");

        internal static bool Is64BitOS => Environment.Is64BitOperatingSystem;
        internal static bool IsWow64 => Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess;

        internal static bool IsWindows10Plus => Environment.OSVersion.Version.Major >= 10;

        internal static bool IsSafeMode =>
            RegistryHelp.KeyExists(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\SafeBoot\Option");

        internal static string WindowsDefenderDir =>
            Path.Combine(ProgramFiles, "Windows Defender");

        internal static string DefenderPlatform =>
            Path.Combine(SystemDrive, "ProgramData", "Microsoft", "Windows Defender", "Platform");

        private static List<(string Normal, string Block)> _exeTargetsCache;

        internal static List<(string Normal, string Block)> GetDefenderExeTargets()
        {
            if (_exeTargetsCache != null)
                return _exeTargetsCache;

            var targets = new List<(string, string)>
            {
                (Path.Combine(WindowsDefenderDir, "MsMpEng.exe"), Path.Combine(WindowsDefenderDir, "BlockAntimalware.exe")),
                (Path.Combine(WindowsDefenderDir, "NisSrv.exe"), Path.Combine(WindowsDefenderDir, "BlockNisSrv.exe")),
                (Path.Combine(WindowsDefenderDir, "MpCmdRun.exe"), Path.Combine(WindowsDefenderDir, "BlockMpCmdRun.exe")),
                (Path.Combine(WindowsDefenderDir, "mpextms.exe"), Path.Combine(WindowsDefenderDir, "Blockmpextms.exe")),
            };

            if (IsWindows10Plus)
            {
                targets.Add((Path.Combine(WindowsDefenderDir, "MpDefenderCoreService.exe"), Path.Combine(WindowsDefenderDir, "BlockAntimalwareCore.exe")));
                targets.Add((Path.Combine(WindowsDefenderDir, "MpCopyAccelerator.exe"), Path.Combine(WindowsDefenderDir, "BlockMpCopyAccelerator.exe")));
            }

            string smartscreen = Path.Combine(SystemDirectory, "smartscreen.exe");
            if (File.Exists(smartscreen))
                targets.Add((smartscreen, Path.Combine(SystemDirectory, "BlockSS.exe")));

            _exeTargetsCache = targets;
            return targets;
        }

        internal static readonly string[] FoldersToClean = new[]
        {
            Path.Combine(SystemDrive, "ProgramData", "Microsoft", "Windows Defender", "Scans", "History"),
            Path.Combine(SystemDrive, "ProgramData", "Microsoft", "Windows Defender", "Scans", "Workspace"),
            Path.Combine(SystemDrive, "ProgramData", "Microsoft", "Windows Defender", "Support"),
        };
    }
}
