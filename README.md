# Takeover Defender

Take full control of Windows Defender — disable and re-enable at will.

Windows Defender became a full antivirus starting with Windows 8. This tool targets Windows 8 and later. Windows 7 and earlier are not supported.

## Compatibility

| Feature | Windows 8/8.1 | Windows 10/11 |
|---------|:-------------:|:-------------:|
| Registry policies | Yes | Yes |
| Service control | Yes | Yes |
| Executable blocking | Yes | Yes |
| Set-MpPreference (PowerShell) | Yes | Yes |
| Tamper Protection handling | N/A | Yes |
| x86 (32-bit) | Yes | Yes |
| x64 (64-bit) | Yes | Yes |

## What TAKE OVER actually changes

Disabling is not a single switch. The **TAKE OVER** operation performs, in order:

1. **Tamper Protection** (Windows 10+) — temporarily set to OFF so the rest can take.
2. **Defender services** — stops and disables `WinDefend`, `SecurityHealthService`, `WdFilter`, `WdBoot`, `wscsvc`, and (on Win10+) the modern-protection/driver set.
3. **Registry policies** — writes `DisableAntiSpyware`/`DisableRealtimeMonitoring`/etc. under `SOFTWARE\Policies\Microsoft\Windows Defender` and related keys, plus Security Center `AntiVirusOverride`/`FirewallOverride`, and firewall-profile `DisableNotifications` for Domain/Private/Standard profiles.
4. **MpPreference** — `Set-MpPreference` to turn off real-time, behavior, IOAV, script, archive, IPS scanning and sample submission.
5. **Executable blocking** — takes ownership of `MsMpEng.exe`, `NisSrv.exe`, `MpCmdRun.exe`, `MpDefenderCoreService.exe`, `smartscreen.exe`, etc., and renames them so they cannot launch.
6. **Folder cleanup** — clears Defender scan history / workspace / support folders.
7. **SmartScreen** — disables SmartScreen for Explorer, Edge phishing filter, and web content evaluation.

**RESTORE** reverses all of the above, restoring each service to the start-type it had before TAKE OVER (captured at disable time) and re-enabling Tamper Protection only if TAKE OVER disabled it.

Privileged work (service control, ownership/rename, tamper registry writes) runs as **SYSTEM** through short-lived scheduled tasks named `Tkd_*` that are deleted immediately after use; any stale `Tkd_*` tasks are also cleaned up at startup.

## After TAKE OVER: the residual MsMpEng.exe process

**TAKE OVER fully disables Defender's protection** — real-time scanning, behavior monitoring, IOAV, antivirus and antispyware engines are all OFF. It will **not** scan your files, and it will **not interfere with developing software or compiling code** (this is the part that used to slow things down).

On modern Windows 10/11 (and especially Insider builds), the **`MsMpEng.exe` (Antimalware Service Executable) process stays loaded** even after TAKE OVER. This is by OS design: `WinDefend` is a Protected Process Light (PPL) service, and the OS blocks `sc config`/`sc stop`/registry writes/ownership-takeover against it even when running as SYSTEM. What you get is:

- **CPU: ~0%** — the process is idle (all scanning engines are off).
- **RAM: ~80–100 MB** — just the cost of keeping the process loaded.
- **No interference** — no scanning, no real-time hooks, no slowdown when building/compiling.

So for the common goal ("stop Defender from interfering with my dev work"), TAKE OVER alone is sufficient and the lingering process is harmless.

### If you want the process gone entirely

To stop `WinDefend`/`MsMpEng.exe` from running at all (and survive a reboot), the service must be disabled from **Safe Mode** — the only context where the SCM allows reconfiguring the protected service. Two helper scripts are provided:

1. Boot into **Safe Mode** (e.g. `msconfig` → *Boot* → *Safe boot* → *Minimal* → restart).
2. Run as Administrator:
   ```powershell
   PowerShell -ExecutionPolicy Bypass -File .\scripts\disable-defender-safemode.ps1
   ```
   It sets every Defender service to `start= disabled`.
3. Undo Safe Mode boot (`msconfig` → uncheck *Safe boot*) and reboot normally. `WinDefend` will be **Stopped** and `MsMpEng.exe` will **not** run.

Reversible with `.\scripts\enable-defender-safemode.ps1` (also run in Safe Mode).

## Build from source

Copy or clone the source into a `takeover-defender/` directory (if you have a git remote, `git clone <your-remote-url>`), then:

### 1. Install build tools

**Windows 10 / 11** — run PowerShell as Administrator:

```powershell
.\scripts\install-sdk.ps1
```

Or manually: `winget install Microsoft.DotNet.SDK.8`

**Windows 8 / 8.1** — run PowerShell as Administrator:

```powershell
.\scripts\install-sdk.ps1
```

Downloads and installs the .NET Framework 4.8 Developer Pack (Authenticode signature verified before running). No winget required.

### 2. Build

**Windows 10+ (with .NET SDK):**

```bash
dotnet build -c Release        # x64
dotnet build -c Release_x86    # x86 (32-bit)
dotnet build -c Debug          # Debug
```

**Windows 8/8.1 (with .NET Framework MSBuild):**

```powershell
# 64-bit
& "$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe" TakeoverDefender.csproj /p:Configuration=Debug /p:Platform=x64

# 32-bit
& "$env:SystemRoot\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe" TakeoverDefender.csproj /p:Configuration=Debug /p:Platform=x86
```

### 3. Tests

Tests are **Windows-only**: they read the live registry, the real `WinDefend` service, and the Defender directory. Run on a Windows machine with Defender present:

```bash
dotnet test
```

## Usage

1. Run `TakeoverDefender.exe` as **Administrator**
2. Click **TAKE OVER** — disables Defender
3. Click **RESTORE** — re-enables Defender

Tamper Protection on Windows 10+ is automatically disabled before changes and restored after.

## Requirements

- Windows 8 or later
- Administrator privileges
- .NET Framework 4.8

## Project structure

```
takeover-defender/
├── README.md
├── LICENSE
├── .gitignore
├── TakeoverDefender.sln        # open/build/test both projects
├── packages.lock.json          # locked NuGraph for reproducible restore
├── TakeoverDefender.csproj
├── app.manifest              # requireAdministrator, supportedOS Win8/8.1/10
├── app.ico
├── App.xaml / App.xaml.cs    # OS/admin guard, single-instance, stale-task cleanup
├── MainWindow.xaml / .cs     # UI: status, take-over/restore, log
├── scripts/install-sdk.ps1
├── Properties/AssemblyInfo.cs
├── Utilities/
│   ├── DefenderManager.cs    # disable/enable orchestration, state detection
│   ├── CommandExecutor.cs    # process + SYSTEM-via-scheduled-task runner
│   ├── PathLocator.cs        # paths, exe targets, OS detection
│   └── RegistryHelp.cs       # registry read/write helpers
└── tests/TakeoverDefender.Tests/
    ├── CoreTests.cs          # registry, path, command, XML-escape, state logic
    ├── WindowsTests.cs       # live Windows compatibility checks
    └── TakeoverDefender.Tests.csproj
```

## License

MIT — see [LICENSE](LICENSE).
