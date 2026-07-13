# Building from source

Clone the repo, then:

```bash
git clone https://github.com/YayoRazo/takeover-defender.git
cd takeover-defender
```

## 1. Install build tools

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

## 2. Build

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

## 3. Tests

Tests are **Windows-only**: they read the live registry, the real `WinDefend` service, and the Defender directory. Run on a Windows machine with Defender present:

```bash
dotnet test
```

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
