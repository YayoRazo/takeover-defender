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

## Antivirus and browser warnings (expected)

Takeover Defender is an **unsigned executable whose only job is to disable Windows
Defender**. Because of that, antivirus engines and browsers **will flag it** — this
is expected and unavoidable for any tool of this kind, and it is **not** a sign of a
real infection:

- **Windows Defender / other AVs** classify Defender-disabling tools as `HackTool`,
  `PUA` (Potentially Unwanted Application) or a generic trojan. It is a heuristic
  **false positive**: the "malicious behavior" the engine detects *is* the feature
  the tool intentionally performs.
- **SmartScreen / browsers** (Edge, Chrome, Firefox) show *"This file is not
  commonly downloaded and may be dangerous"* and block the download, because the
  `.exe` is unsigned and has no download reputation.

A VirusTotal scan of the release build confirms it: most engines flag it as
`HackTool`/`PUA` — see the
[report for this build](https://www.virustotal.com/gui/file/0e09359d4dded06f457dc1949a2a5eaf95c8e645b7f0e58d3fd52a89243fa16c).

### Running it despite the warnings

1. Download the `.exe`. In the browser warning pick *Keep* → *Keep anyway*, or use a
   download path SmartScreen doesn't gate.
2. If Defender quarantines the file before you can run it, **add an exclusion**
   first: *Windows Security → Virus & threat protection → Manage settings →
   Exclusions → Add an exclusion → File* → select `TakeoverDefender.exe`.
3. Run it as Administrator.

> Yes, this is a chicken-and-egg: to disable Defender you may first have to tell
> Defender to trust the disabler. That is inherent to what the tool does.

### Reducing the warnings

- **Self-sign** the executable with a code-signing certificate (removes most
  SmartScreen reputation warnings; AV heuristic detection usually remains).
- Submit the binary to Microsoft (and other AV vendors) as a false positive through
  their submission portals so the detection signatures get updated.

## Build from source

Clone the repo, then:

```bash
git clone https://github.com/YayoRazo/takeover-defender.git
cd takeover-defender
```

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
dotnet build TakeoverDefender.csproj -c Release        # x64
dotnet build TakeoverDefender.csproj -c Release_x86    # x86 (32-bit)
dotnet build TakeoverDefender.sln -c Debug             # Debug (both projects)
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
├── CHANGELOG.md                # version history
├── .gitignore
├── .gitattributes              # LF for the bash hook, binaries
├── TakeoverDefender.sln        # open/build/test both projects
├── packages.lock.json          # locked NuGraph for reproducible restore
├── TakeoverDefender.csproj
├── app.manifest              # requireAdministrator, supportedOS Win8/8.1/10
├── app.ico
├── App.xaml / App.xaml.cs    # OS/admin guard, single-instance, stale-task cleanup
├── MainWindow.xaml / .cs     # UI: status, take-over/restore, log
├── Properties/AssemblyInfo.cs  # 3-digit version source (x.y.z)
├── .githooks/
│   ├── pre-commit            # auto-sets the version patch from the commit count
│   └── set-patch.ps1
├── .github/workflows/release.yml  # tag v* -> build x64/x86 -> GitHub release
├── scripts/
│   ├── install-sdk.ps1
│   ├── bump-version.ps1      # major|minor|x.y.z, -DryRun
│   ├── disable-defender-safemode.ps1
│   └── enable-defender-safemode.ps1
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

## Versioning

The version is a single 3-digit semver (`x.y.z`) kept in `Properties/AssemblyInfo.cs` (`AssemblyVersion`, `AssemblyFileVersion`, `AssemblyInformationalVersion`).

- The **patch** (3rd digit) is owned by the pre-commit hook and set automatically to `git commit count + 1`. Enable it once per clone:
  ```powershell
  git config core.hooksPath .githooks
  ```
- **major / minor** are bumped explicitly:
  ```powershell
  .\scripts\bump-version.ps1 minor            # 1.2.7 -> 1.3.0
  .\scripts\bump-version.ps1 major            # 1.2.7 -> 2.0.0
  .\scripts\bump-version.ps1 2.5.0 -DryRun    # preview
  ```
  Then update `CHANGELOG.md`, commit (`chore(release): bump version to x.y.z`).

### Releasing

1. `.\scripts\bump-version.ps1 minor` (or `major`).
2. Move `## Unreleased` entries in `CHANGELOG.md` under a dated `## x.y.z` section.
3. Commit, then tag and push:
   ```powershell
   git commit -am "chore(release): bump version to x.y.z"
   git tag vx.y.z
   git push origin main --tags
   ```
4. The `release` workflow builds x64 + x86, runs tests, and publishes a GitHub Release with `TakeoverDefender-x64.exe` and `TakeoverDefender-x86.exe`.

## License

MIT — see [LICENSE](LICENSE).
