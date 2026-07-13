<#
.SYNOPSIS
    Sets the patch (3rd) digit of the version to the given commit count.
    Called by .githooks/pre-commit. Keeps major.minor, updates all three attributes.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][int]$Patch)

$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$assemblyPath = Join-Path $root 'Properties\AssemblyInfo.cs'
if (-not (Test-Path $assemblyPath)) { throw "AssemblyInfo.cs not found at $assemblyPath" }

$major = $minor = $null
foreach ($line in Get-Content $assemblyPath) {
    if ($line -match '\[assembly:\s*AssemblyInformationalVersion\("(\d+)\.(\d+)\.\d+"\)]') {
        $major = [int]$Matches[1]; $minor = [int]$Matches[2]; break
    }
}
if ($null -eq $major) { throw 'Could not parse AssemblyInformationalVersion major.minor.' }

$newVer = "$major.$minor.$Patch"

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
Write-Host "[set-patch] version -> $newVer"
