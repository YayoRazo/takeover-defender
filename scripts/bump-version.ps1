<#
.SYNOPSIS
    Bump the major or minor version of Takeover Defender.

    The pre-commit hook (.githooks/pre-commit) owns the PATCH digit (set to the git
    commit count + 1 on every commit). This script handles major / minor bumps (and
    exact x.y.z) and syncs the new 3-digit semver into Properties\AssemblyInfo.cs:
      AssemblyVersion              -> major.minor.patch   (CLR binding identity)
      AssemblyFileVersion          -> major.minor.patch
      AssemblyInformationalVersion -> major.minor.patch   (canonical / product version)

    The version is ALWAYS three digits (x.y.z). Patch is reset to 0 by this script;
    the next commit's pre-commit hook replaces it with the commit count. Do NOT bump
    the patch manually.

.EXAMPLE
    .\scripts\bump-version.ps1 minor            # 0.1.0 -> 0.2.0
    .\scripts\bump-version.ps1 major            # 0.1.0 -> 1.0.0
    .\scripts\bump-version.ps1 0.3.0            # set exact version
    .\scripts\bump-version.ps1 minor -DryRun    # preview only
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)][string]$Level,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$assemblyPath = Join-Path $root 'Properties\AssemblyInfo.cs'

if (-not (Test-Path $assemblyPath)) {
    throw 'Properties\AssemblyInfo.cs not found - run from the repo root.'
}
if (-not $Level) {
    Write-Host 'Usage: .\scripts\bump-version.ps1 [-DryRun] <major|minor|x.y.z>'
    Write-Host '  major   bump major, reset minor+patch to 0.0'
    Write-Host '  minor   bump minor, reset patch to 0'
    Write-Host '  x.y.z   set exact version (patch reset to 0; hook sets the real patch)'
    exit 1
}

function Get-CurrentVersion {
    param([string]$Path)
    foreach ($line in Get-Content $Path) {
        if ($line -match '\[assembly:\s*AssemblyInformationalVersion\("(\d+)\.(\d+)\.(\d+)"\)]') {
            return @{ Major = [int]$Matches[1]; Minor = [int]$Matches[2]; Patch = [int]$Matches[3] }
        }
    }
    throw 'AssemblyInformationalVersion not found in AssemblyInfo.cs.'
}

function Invoke-Bump {
    param([hashtable]$Current, [string]$Lvl)
    if ($Lvl -match '^(\d+)\.(\d+)\.(\d+)$') {
        return @{ Major = [int]$Matches[1]; Minor = [int]$Matches[2]; Patch = 0 }
    }
    if ($Lvl -eq 'major') { return @{ Major = $Current.Major + 1; Minor = 0; Patch = 0 } }
    if ($Lvl -eq 'minor') { return @{ Major = $Current.Major; Minor = $Current.Minor + 1; Patch = 0 } }
    throw "unknown bump level: $Lvl (use major, minor, or x.y.z)"
}

$current = Get-CurrentVersion -Path $assemblyPath
$old = "$($current.Major).$($current.Minor).$($current.Patch)"
$new = Invoke-Bump -Current $current -Lvl $Level
$newVer = "$($new.Major).$($new.Minor).$($new.Patch)"

Write-Host "Bump version: $old -> $newVer" -ForegroundColor Cyan

if ($DryRun) {
    Write-Host "  [dry-run] would update Properties\AssemblyInfo.cs to $newVer (3-digit semver):" -ForegroundColor DarkGray
    Write-Host "    AssemblyVersion(""              $newVer"")" -ForegroundColor DarkGray
    Write-Host "    AssemblyFileVersion(""          $newVer"")" -ForegroundColor DarkGray
    Write-Host "    AssemblyInformationalVersion("" $newVer"")" -ForegroundColor DarkGray
    Write-Host '  Dry run - no files changed. Drop -DryRun to apply.' -ForegroundColor DarkGray
    return
}

$lines = Get-Content $assemblyPath
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '\[assembly:\s*AssemblyVersion\(') {
        $lines[$i] = "[assembly: AssemblyVersion(`"$newVer`")]"
    }
    elseif ($lines[$i] -match '\[assembly:\s*AssemblyFileVersion\(') {
        $lines[$i] = "[assembly: AssemblyFileVersion(`"$newVer`")]"
    }
    elseif ($lines[$i] -match '\[assembly:\s*AssemblyInformationalVersion\(') {
        $lines[$i] = "[assembly: AssemblyInformationalVersion(`"$newVer`")]"
    }
}
Set-Content -Path $assemblyPath -Value $lines -Encoding UTF8

Write-Host "Version bumped to $newVer in Properties\AssemblyInfo.cs." -ForegroundColor Green
Write-Host ''
Write-Host 'Next steps:' -ForegroundColor DarkGray
Write-Host "  1. Review:   git diff Properties\AssemblyInfo.cs" -ForegroundColor DarkGray
Write-Host '  2. Update CHANGELOG.md (move Unreleased under a dated section)' -ForegroundColor DarkGray
Write-Host "  3. Commit:   git commit -m `"chore(release): bump version to $newVer`"" -ForegroundColor DarkGray
Write-Host "  4. Tag + push release: git tag v$newVer && git push origin v$newVer" -ForegroundColor DarkGray
Write-Host '  (the pre-commit hook sets the patch to the commit count automatically.)' -ForegroundColor DarkGray
