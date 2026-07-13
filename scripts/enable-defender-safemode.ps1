<#
.SYNOPSIS
    Re-enables Windows Defender services (undo of disable-defender-safemode.ps1).
    Run in Safe Mode for the same reason: WinDefend can only be reconfigured there.
#>

$ErrorActionPreference = 'Continue'

# Known-good start types: boot drivers=0, auto services=2, demand services=3.
$target = @{
    'WinDefend'             = 2   # auto
    'SecurityHealthService' = 2   # auto
    'wscsvc'                = 2   # auto
    'webthreatdefusersvc'   = 2   # auto
    'WdFilter'              = 0   # boot
    'WdBoot'                = 0   # boot
    'WdNisDrv'              = 3   # demand
    'WdNisSvc'              = 3   # demand
    'Sense'                 = 3   # demand
    'SgrmAgent'             = 3   # demand
    'SgrmBroker'            = 3   # demand
    'MDCoreSvc'             = 3   # demand
    'MsSecCore'             = 3   # demand
    'MsSecFlt'              = 3   # demand
    'MsSecWfp'              = 3   # demand
    'webthreatdefsvc'       = 3   # demand
}

$scStart = @{ 0 = 'boot'; 2 = 'auto'; 3 = 'demand' }

Write-Host '=== Re-enabling Defender services ===' -ForegroundColor Cyan
foreach ($s in $target.Keys) {
    if ($null -eq (Get-Service -Name $s -ErrorAction SilentlyContinue)) {
        Write-Host "  [skip] $s (not present)" -ForegroundColor DarkGray
        continue
    }
    $st = $scStart[$target[$s]]
    $null = & sc.exe config $s start= $st 2>&1
    $startType = (Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\$s" -ErrorAction SilentlyContinue).Start
    $color = if ($startType -eq $target[$s]) { 'Green' } else { 'Red' }
    Write-Host ("  {0,-24} -> Start={1}" -f $s, $startType) -ForegroundColor $color
}

Write-Host ''
Write-Host 'Done. Undo Safe Mode boot and reboot normally; Defender will start again.' -ForegroundColor Green
