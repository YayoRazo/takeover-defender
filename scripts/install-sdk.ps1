<#
.SYNOPSIS
    Installs the required SDK for building Takeover Defender.
    Requires Windows 8 or later.
#>

param()
$ErrorActionPreference = "Stop"

$os = [Environment]::OSVersion.Version
$major = $os.Major
$minor = $os.Minor
$isWin10Plus = ($major -gt 10) -or ($major -eq 10)

Write-Host "Detected: Windows $($major).$($minor)" -ForegroundColor Cyan

if ($major -lt 6 -or ($major -eq 6 -and $minor -lt 2)) {
    Write-Host "ERROR: Windows 7 and earlier are not supported." -ForegroundColor Red
    Write-Host "Windows Defender became a full antivirus starting with Windows 8." -ForegroundColor Red
    exit 1
}

if ($isWin10Plus) {

    Write-Host "[Windows 10+] Installing .NET SDK 8 via winget..." -ForegroundColor Green

    if (!(Get-Command winget -ErrorAction SilentlyContinue)) {
        Write-Host "ERROR: winget not found. Install App Installer from Microsoft Store." -ForegroundColor Red
        exit 1
    }

    winget install Microsoft.DotNet.SDK.8 --accept-source-agreements --accept-package-agreements

} else {

    Write-Host "[Windows 8/8.1] Installing .NET Framework 4.8 Developer Pack..." -ForegroundColor Green
    $url = "https://go.microsoft.com/fwlink/?linkid=2088517"
    $file = "$env:TEMP\ndp48-devpack-enu.exe"
    Write-Host "Downloading from $url ..."
    Invoke-WebRequest -Uri $url -OutFile $file -UseBasicParsing

    Write-Host "Verifying publisher signature..." -ForegroundColor Cyan
    $sig = Get-AuthenticodeSignature -FilePath $file
    if ($sig.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
        Write-Host "ERROR: Installer signature is not valid ($($sig.Status)). Aborting." -ForegroundColor Red
        Remove-Item $file -ErrorAction SilentlyContinue
        exit 1
    }
    $publisher = $sig.SignerCertificate.Subject
    if ($publisher -notlike "*Microsoft Corporation*") {
        Write-Host "ERROR: Installer is not signed by Microsoft Corporation: $publisher" -ForegroundColor Red
        Remove-Item $file -ErrorAction SilentlyContinue
        exit 1
    }
    Write-Host "Installer signed by: $publisher" -ForegroundColor Green

    Write-Host "Running installer..."
    Start-Process -FilePath $file -ArgumentList "/quiet /norestart" -Wait
    Remove-Item $file -ErrorAction SilentlyContinue

}

Write-Host ""
Write-Host "=== Installation complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "Build commands:" -ForegroundColor Cyan

if ($isWin10Plus) {
    Write-Host "  dotnet build -c Release"
    Write-Host "  dotnet build -c Release_x86"
    Write-Host "  dotnet test"
}
