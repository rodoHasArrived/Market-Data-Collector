#!/usr/bin/env pwsh
<#
.SYNOPSIS
    One-click installer for Market Data Collector (Windows)

.DESCRIPTION
    Downloads, extracts, and sets up Market Data Collector with a single command.
    Creates desktop shortcuts, sets up configuration, and optionally adds to PATH.

.PARAMETER InstallPath
    Installation directory (default: C:\Program Files\MarketDataCollector)

.PARAMETER AddToPath
    Add MarketDataCollector to system PATH

.PARAMETER CreateShortcut
    Create desktop shortcut

.EXAMPLE
    .\install.ps1

.EXAMPLE
    .\install.ps1 -InstallPath "C:\Tools\MarketDataCollector" -AddToPath -CreateShortcut
#>

param(
    [string]$InstallPath = "$env:LOCALAPPDATA\MarketDataCollector",
    [switch]$AddToPath = $true,
    [switch]$CreateShortcut = $true,
    [string]$Version = "latest"
)

$ErrorActionPreference = "Stop"

# Banner
Write-Host @"
╔══════════════════════════════════════════════════════════════════════╗
║            Market Data Collector - Windows Installer                 ║
║                        Version: $Version                             ║
╚══════════════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

# Configuration
$GitHubRepo = "rodoHasArrived/Test"
$DownloadUrl = if ($Version -eq "latest") {
    "https://github.com/$GitHubRepo/releases/latest/download/MarketDataCollector-win-x64.zip"
} else {
    "https://github.com/$GitHubRepo/releases/download/$Version/MarketDataCollector-win-x64.zip"
}

$TempDir = Join-Path $env:TEMP "MarketDataCollector-Install"
$ZipFile = Join-Path $TempDir "MarketDataCollector.zip"

try {
    # Step 1: Create temp directory
    Write-Host "`n[1/7] Creating temporary directory..." -ForegroundColor Yellow
    if (Test-Path $TempDir) {
        Remove-Item $TempDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $TempDir -Force | Out-Null
    Write-Host "✓ Temporary directory created: $TempDir" -ForegroundColor Green

    # Step 2: Download
    Write-Host "`n[2/7] Downloading Market Data Collector..." -ForegroundColor Yellow
    Write-Host "URL: $DownloadUrl" -ForegroundColor Gray

    try {
        Invoke-WebRequest -Uri $DownloadUrl -OutFile $ZipFile -UseBasicParsing
        Write-Host "✓ Download complete" -ForegroundColor Green
    } catch {
        Write-Host "✗ Download failed: $_" -ForegroundColor Red
        Write-Host "`nNote: If the release doesn't exist yet, please build from source:" -ForegroundColor Yellow
        Write-Host "  git clone https://github.com/$GitHubRepo.git" -ForegroundColor Gray
        Write-Host "  cd Test/MarketDataCollector" -ForegroundColor Gray
        Write-Host "  .\publish.ps1" -ForegroundColor Gray
        throw
    }

    # Step 3: Extract
    Write-Host "`n[3/7] Extracting files..." -ForegroundColor Yellow
    if (Test-Path $InstallPath) {
        Write-Host "⚠ Installation directory exists. Backing up..." -ForegroundColor Yellow
        $BackupPath = "$InstallPath.backup.$(Get-Date -Format 'yyyyMMdd-HHmmss')"
        Move-Item $InstallPath $BackupPath -Force
        Write-Host "  Backup created: $BackupPath" -ForegroundColor Gray
    }

    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
    Expand-Archive -Path $ZipFile -DestinationPath $InstallPath -Force
    Write-Host "✓ Files extracted to: $InstallPath" -ForegroundColor Green

    # Step 4: Set up configuration
    Write-Host "`n[4/7] Setting up configuration..." -ForegroundColor Yellow
    $ConfigPath = Join-Path $InstallPath "appsettings.json"
    $SampleConfigPath = Join-Path $InstallPath "appsettings.sample.json"

    if (-not (Test-Path $ConfigPath)) {
        if (Test-Path $SampleConfigPath) {
            Copy-Item $SampleConfigPath $ConfigPath
            Write-Host "✓ Configuration file created from sample" -ForegroundColor Green
        } else {
            # Create minimal config
            $MinimalConfig = @{
                DataRoot = "data"
                Compress = $false
                DataSource = "Alpaca"
                Symbols = @()
            } | ConvertTo-Json -Depth 10

            Set-Content -Path $ConfigPath -Value $MinimalConfig
            Write-Host "✓ Default configuration file created" -ForegroundColor Green
        }
    } else {
        Write-Host "✓ Existing configuration preserved" -ForegroundColor Green
    }

    # Step 5: Create data directory
    Write-Host "`n[5/7] Creating data directory..." -ForegroundColor Yellow
    $DataPath = Join-Path $InstallPath "data"
    New-Item -ItemType Directory -Path $DataPath -Force | Out-Null
    Write-Host "✓ Data directory created: $DataPath" -ForegroundColor Green

    # Step 6: Add to PATH (optional)
    if ($AddToPath) {
        Write-Host "`n[6/7] Adding to system PATH..." -ForegroundColor Yellow

        $UserPath = [Environment]::GetEnvironmentVariable("Path", "User")
        if ($UserPath -notlike "*$InstallPath*") {
            $NewPath = "$UserPath;$InstallPath"
            [Environment]::SetEnvironmentVariable("Path", $NewPath, "User")
            $env:Path = "$env:Path;$InstallPath"
            Write-Host "✓ Added to user PATH" -ForegroundColor Green
            Write-Host "  (You may need to restart your terminal)" -ForegroundColor Gray
        } else {
            Write-Host "✓ Already in PATH" -ForegroundColor Green
        }
    } else {
        Write-Host "`n[6/7] Skipping PATH setup (use -AddToPath to enable)" -ForegroundColor Gray
    }

    # Step 7: Create desktop shortcut (optional)
    if ($CreateShortcut) {
        Write-Host "`n[7/7] Creating desktop shortcut..." -ForegroundColor Yellow

        $WshShell = New-Object -ComObject WScript.Shell
        $Desktop = [Environment]::GetFolderPath("Desktop")
        $ShortcutPath = Join-Path $Desktop "Market Data Collector.lnk"

        $Shortcut = $WshShell.CreateShortcut($ShortcutPath)
        $Shortcut.TargetPath = "powershell.exe"
        $Shortcut.Arguments = "-NoExit -Command `"cd '$InstallPath'; .\MarketDataCollector.exe --ui`""
        $Shortcut.WorkingDirectory = $InstallPath
        $Shortcut.Description = "Market Data Collector - Web Dashboard"
        $Shortcut.IconLocation = Join-Path $InstallPath "MarketDataCollector.exe"
        $Shortcut.Save()

        Write-Host "✓ Desktop shortcut created" -ForegroundColor Green
    } else {
        Write-Host "`n[7/7] Skipping shortcut creation (use -CreateShortcut to enable)" -ForegroundColor Gray
    }

    # Cleanup
    Write-Host "`nCleaning up..." -ForegroundColor Yellow
    Remove-Item $TempDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "✓ Cleanup complete" -ForegroundColor Green

    # Success message
    Write-Host @"

╔══════════════════════════════════════════════════════════════════════╗
║                    Installation Complete! ✓                          ║
╚══════════════════════════════════════════════════════════════════════╝

Installation Directory: $InstallPath

Quick Start:
  1. Open a new terminal (to pick up PATH changes)
  2. Run: MarketDataCollector --ui
  3. Open browser to: http://localhost:8080

Or click the desktop shortcut: "Market Data Collector"

Documentation:
  - User Guide: $InstallPath\HELP.md
  - Configuration: $InstallPath\appsettings.json
  - Getting Started: $InstallPath\docs\GETTING_STARTED.md

Next Steps:
  1. Configure your data provider (IB or Alpaca)
  2. Add symbols to track
  3. Start collecting data!

For help: MarketDataCollector --help

"@ -ForegroundColor Cyan

} catch {
    Write-Host "`n✗ Installation failed: $_" -ForegroundColor Red
    Write-Host "`nTroubleshooting:" -ForegroundColor Yellow
    Write-Host "  1. Check internet connection" -ForegroundColor Gray
    Write-Host "  2. Verify GitHub releases are available" -ForegroundColor Gray
    Write-Host "  3. Try running as Administrator" -ForegroundColor Gray
    Write-Host "  4. Check antivirus isn't blocking download" -ForegroundColor Gray
    Write-Host "`nFor manual installation, see: https://github.com/$GitHubRepo/releases" -ForegroundColor Gray

    exit 1
}
