#!/usr/bin/env pwsh
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Host "== Market Data Collector: Desktop Dev Bootstrap ==" -ForegroundColor Cyan

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../../..")
Set-Location $repoRoot

function Test-Command([string]$Name) {
    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function Info([string]$Message) { Write-Host "[INFO] $Message" -ForegroundColor Gray }
function Ok([string]$Message) { Write-Host "[OK]   $Message" -ForegroundColor Green }
function Warn([string]$Message) { Write-Host "[WARN] $Message" -ForegroundColor Yellow }

if (-not (Test-Command "dotnet")) {
    throw "dotnet SDK is required. Install .NET 9 SDK before running desktop development workflows."
}

$dotnetVersion = (& dotnet --version).Trim()
Info "Detected .NET SDK: $dotnetVersion"
Ok "dotnet command available"

$onWindows = $IsWindows -or ($env:OS -eq 'Windows_NT')
if (-not $onWindows) {
    Warn "Non-Windows environment detected. WPF/UWP app builds are skipped by design."
    Warn "Use this script on Windows for full desktop validation."
}

$wpfProject = "src/MarketDataCollector.Wpf/MarketDataCollector.Wpf.csproj"
$uwpProject = "src/MarketDataCollector.Uwp/MarketDataCollector.Uwp.csproj"

Info "Restoring desktop projects"
& dotnet restore $wpfProject | Out-Host
if ($LASTEXITCODE -ne 0) { throw "WPF restore failed." }
Ok "WPF restore succeeded"

if ($onWindows) {
    & dotnet restore $uwpProject -r win-x64 | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Warn "UWP restore failed. Verify Windows SDK + WinUI workloads are installed."
    } else {
        Ok "UWP restore succeeded"
    }
}

Info "Building WPF smoke target"
& dotnet build $wpfProject -c Debug --no-restore | Out-Host
if ($LASTEXITCODE -ne 0) { throw "WPF smoke build failed." }
Ok "WPF smoke build succeeded"

if ($onWindows) {
    Info "Attempting UWP smoke build (legacy)"
    & dotnet build $uwpProject -c Debug -r win-x64 --no-restore | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Warn "UWP smoke build failed. Run ./scripts/dev/diagnose-uwp-xaml.ps1 for targeted diagnostics."
    } else {
        Ok "UWP smoke build succeeded"
    }
}

Write-Host "" 
Write-Host "Desktop bootstrap checks complete." -ForegroundColor Green
Write-Host "Next commands:" -ForegroundColor Cyan
Write-Host "  make build-wpf"
Write-Host "  make test-desktop-services"
Write-Host "  make uwp-xaml-diagnose"
