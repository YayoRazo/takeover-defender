<#
.SYNOPSIS
    Fully dismantles Windows Defender by disabling all of its services.
    MUST be run in Safe Mode (the only context where the protected WinDefend
    service can be reconfigured on modern/Insider Windows builds).

    Why Safe Mode: in normal mode, WinDefend is a Protected Process Light (PPL)
    service; `sc config`, direct registry writes, and registry take-ownership
    are all blocked by the OS. In Safe Mode the service does not run, so the SCM
    allows reconfiguring it. After this runs once in Safe Mode and the machine
    is rebooted normally, WinDefend (and MsMpEng.exe) will not start.

.NOTES
    Reversible with enable-defender-safemode.ps1 (also run in Safe Mode).
#>

$ErrorActionPreference = 'Continue'

if (-not [Environment]::IsWindows) { throw 'Windows only.' }

# Defender / Security Center service set. start=4 (disabled).
$services = @(
    'WinDefend','SecurityHealthService','wscsvc',
    'WdFilter','WdBoot','WdNisDrv','WdNisSvc',
    'Sense','SgrmAgent','SgrmBroker',
    'MDCoreSvc','MsSecCore','MsSecFlt','MsSecWfp',
    'webthreatdefsvc','webthreatdefusersvc'
)

Write-Host '=== Disabling Defender services (start= disabled, then stop) ===' -ForegroundColor Cyan
foreach ($s in $services) {
    $exists = $null -ne (Get-Service -Name $s -ErrorAction SilentlyContinue)
    if (-not $exists) {
        Write-Host "  [skip] $s (not present on this build)" -ForegroundColor DarkGray
        continue
    }
    $null = & sc.exe config $s start= disabled 2>&1
    $cfgCode = $LASTEXITCODE
    $null = & sc.exe stop $s 2>&1
    $startType = (Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\$s" -ErrorAction SilentlyContinue).Start
    $verdict = if ($startType -eq 4) { 'DISABLED' } else { "still Start=$startType" }
    $color = if ($startType -eq 4) { 'Green' } else { 'Red' }
    Write-Host ("  {0,-24} sc config exit={1}  -> {2}" -f $s, $cfgCode, $verdict) -ForegroundColor $color
}

Write-Host ''
Write-Host '=== Done ===' -ForegroundColor Green
Write-Host 'Now:' -ForegroundColor Yellow
Write-Host '  1. Undo Safe Mode boot (msconfig -> Boot -> uncheck Safe boot, OR: bcdedit /deletevalue safeboot)'
Write-Host '  2. Reboot normally'
Write-Host '  3. WinDefend / MsMpEng.exe should NOT start. Verify with: (Get-Service WinDefend).Status   and   Get-Process MsMpEng'
