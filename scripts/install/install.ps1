<#
.SYNOPSIS
    Market Data Collector - Installation Script for Windows

.DESCRIPTION
    This script automates the installation and setup of Market Data Collector on Windows.

.PARAMETER Mode
    Installation mode: Docker, Native, Check, or Uninstall

.EXAMPLE
    .\install.ps1
    Interactive installation

.EXAMPLE
    .\install.ps1 -Mode Docker
    Docker-based installation

.EXAMPLE
    .\install.ps1 -Mode Native
    Native .NET installation
#>

param(
    [Parameter(Position = 0)]
    [ValidateSet("Docker", "Native", "Desktop", "Check", "Uninstall", "Help")]
    [string]$Mode = ""
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = (Resolve-Path (Join-Path $ScriptDir "..\..")).Path

# Colors
function Write-ColorOutput($ForegroundColor) {
    $fc = $host.UI.RawUI.ForegroundColor
    $host.UI.RawUI.ForegroundColor = $ForegroundColor
    if ($args) {
        Write-Output $args
    }
    $host.UI.RawUI.ForegroundColor = $fc
}

function Write-Info($message) { Write-Host "[INFO] $message" -ForegroundColor Blue }
function Write-Success($message) { Write-Host "[SUCCESS] $message" -ForegroundColor Green }
function Write-Warning($message) { Write-Host "[WARNING] $message" -ForegroundColor Yellow }
function Write-Error($message) { Write-Host "[ERROR] $message" -ForegroundColor Red }

function Show-Header {
    Write-Host ""
    Write-Host "======================================================================" -ForegroundColor Cyan
    Write-Host "           Market Data Collector - Installation Script                " -ForegroundColor Cyan
    Write-Host "                         Version 1.1.0                                " -ForegroundColor Cyan
    Write-Host "======================================================================" -ForegroundColor Cyan
    Write-Host ""
}

function Show-Help {
    Show-Header
    Write-Host "Usage: .\install.ps1 [-Mode <mode>]"
    Write-Host ""
    Write-Host "Modes:"
    Write-Host "  Docker     Install using Docker (recommended for production)"
    Write-Host "  Native     Install using native .NET SDK (CLI)"
    Write-Host "  Desktop    Build and install Windows Desktop App (WinUI 3)"
    Write-Host "  Check      Check prerequisites only"
    Write-Host "  Uninstall  Remove Docker containers and images"
    Write-Host "  Help       Show this help message"
    Write-Host ""
    Write-Host "Examples:"
    Write-Host "  .\install.ps1                    # Interactive installation"
    Write-Host "  .\install.ps1 -Mode Docker       # Quick Docker installation"
    Write-Host "  .\install.ps1 -Mode Native       # Native .NET installation"
    Write-Host ""
}

function Test-Command($command) {
    try {
        Get-Command $command -ErrorAction Stop | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

function Test-Prerequisites {
    Write-Info "Checking prerequisites..."

    $missing = @()

    # Check Docker
    if (Test-Command "docker") {
        $dockerVersion = docker --version
        Write-Success "Docker: $dockerVersion"
    }
    else {
        Write-Warning "Docker: Not installed"
        $missing += "docker"
    }

    # Check Docker Compose
    if (Test-Command "docker") {
        try {
            docker compose version | Out-Null
            Write-Success "Docker Compose: Available"
        }
        catch {
            Write-Warning "Docker Compose: Not available"
            $missing += "docker-compose"
        }
    }

    # Check .NET SDK
    if (Test-Command "dotnet") {
        $dotnetVersion = dotnet --version
        Write-Success ".NET SDK: $dotnetVersion"

        if ([version]$dotnetVersion -lt [version]"8.0") {
            Write-Warning ".NET SDK version 8.0+ recommended (found: $dotnetVersion)"
        }
    }
    else {
        Write-Warning ".NET SDK: Not installed"
        $missing += "dotnet"
    }

    # Check Git
    if (Test-Command "git") {
        $gitVersion = git --version
        Write-Success "Git: $gitVersion"
    }
    else {
        Write-Warning "Git: Not installed"
        $missing += "git"
    }

    Write-Host ""

    if ($missing.Count -eq 0) {
        Write-Success "All prerequisites are installed!"
        return $true
    }
    else {
        Write-Warning "Missing prerequisites: $($missing -join ', ')"
        return $false
    }
}

function Show-Prerequisites-Suggestions {
    Write-Host ""
    Write-Info "Installation suggestions for Windows:"
    Write-Host ""
    Write-Host "Using winget (Windows Package Manager):" -ForegroundColor Yellow
    Write-Host "  winget install -e --id Docker.DockerDesktop"
    Write-Host "  winget install -e --id Microsoft.DotNet.SDK.8"
    Write-Host "  winget install -e --id Git.Git"
    Write-Host ""
    Write-Host "Using Chocolatey:" -ForegroundColor Yellow
    Write-Host "  choco install docker-desktop dotnet-sdk git -y"
    Write-Host ""
    Write-Host "Manual downloads:" -ForegroundColor Yellow
    Write-Host "  Docker Desktop: https://www.docker.com/products/docker-desktop"
    Write-Host "  .NET SDK 8.0:   https://dotnet.microsoft.com/download/dotnet/8.0"
    Write-Host "  Git:            https://git-scm.com/download/win"
    Write-Host ""
}

function Setup-Config {
    Write-Info "Setting up configuration..."

    $configPath = Join-Path $RepoRoot "config\appsettings.json"
    $samplePath = Join-Path $RepoRoot "config\appsettings.sample.json"

    if (-not (Test-Path $configPath)) {
        if (Test-Path $samplePath) {
            Copy-Item $samplePath $configPath
            Write-Success "Created appsettings.json from template"
            Write-Warning "Remember to edit appsettings.json with your API credentials"
        }
        else {
            Write-Error "appsettings.sample.json not found"
            return $false
        }
    }
    else {
        Write-Info "appsettings.json already exists, skipping..."
    }

    # Create data directory
    $dataDir = Join-Path $RepoRoot "data"
    $logsDir = Join-Path $RepoRoot "logs"

    if (-not (Test-Path $dataDir)) { New-Item -ItemType Directory -Path $dataDir | Out-Null }
    if (-not (Test-Path $logsDir)) { New-Item -ItemType Directory -Path $logsDir | Out-Null }

    Write-Success "Created data and logs directories"
    return $true
}

function Install-Docker {
    Write-Info "Installing with Docker..."

    Push-Location $RepoRoot

    try {
        # Build image
        Write-Info "Building Docker image..."
        docker build -t marketdatacollector:latest .

        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to build Docker image"
            return
        }

        Write-Success "Docker image built successfully"

        # Setup config
        if (-not (Setup-Config)) { return }

        # Start container
        Write-Info "Starting container..."
        docker compose up -d

        if ($LASTEXITCODE -eq 0) {
            Write-Success "Container started successfully"
            Write-Host ""
            Write-Host "======================================================================" -ForegroundColor Green
            Write-Host "                    Installation Complete!                            " -ForegroundColor Green
            Write-Host "======================================================================" -ForegroundColor Green
            Write-Host "  Dashboard:   http://localhost:8080" -ForegroundColor White
            Write-Host "  Metrics:     http://localhost:8080/metrics" -ForegroundColor White
            Write-Host "  Status:      http://localhost:8080/status" -ForegroundColor White
            Write-Host "  Health:      http://localhost:8080/health" -ForegroundColor White
            Write-Host "======================================================================" -ForegroundColor Green
            Write-Host "  View logs:   docker compose logs -f" -ForegroundColor Gray
            Write-Host "  Stop:        docker compose down" -ForegroundColor Gray
            Write-Host "  Restart:     docker compose restart" -ForegroundColor Gray
            Write-Host "======================================================================" -ForegroundColor Green
        }
        else {
            Write-Error "Failed to start container"
        }
    }
    finally {
        Pop-Location
    }
}

function Install-Native {
    Write-Info "Installing with native .NET..."

    if (-not (Test-Command "dotnet")) {
        Write-Error ".NET SDK is required for native installation"
        Show-Prerequisites-Suggestions
        return
    }

    Push-Location $RepoRoot

    try {
        $projectPath = Join-Path $RepoRoot "src\MarketDataCollector\MarketDataCollector.csproj"

        # Restore and build
        Write-Info "Restoring dependencies..."
        dotnet restore $projectPath

        Write-Info "Building project..."
        dotnet build $projectPath -c Release

        if ($LASTEXITCODE -ne 0) {
            Write-Error "Build failed"
            return
        }

        Write-Success "Build completed successfully"

        # Setup config
        if (-not (Setup-Config)) { return }

        # Run tests
        Write-Info "Running self-tests..."
        dotnet run --project $projectPath --configuration Release -- --selftest

        Write-Host ""
        Write-Host "======================================================================" -ForegroundColor Green
        Write-Host "                    Installation Complete!                            " -ForegroundColor Green
        Write-Host "======================================================================" -ForegroundColor Green
        Write-Host "  Start dashboard:" -ForegroundColor White
        Write-Host "    dotnet run --project src\MarketDataCollector\MarketDataCollector.csproj -- --ui" -ForegroundColor Gray
        Write-Host "" -ForegroundColor White
        Write-Host "  Or use the publish script for a standalone executable:" -ForegroundColor White
        Write-Host "    .\publish.ps1 win-x64" -ForegroundColor Gray
        Write-Host "    .\publish\win-x64\MarketDataCollector.exe --ui" -ForegroundColor Gray
        Write-Host "======================================================================" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}

function Install-Desktop {
    Write-Info "Installing Windows Desktop Application..."

    if (-not (Test-Command "dotnet")) {
        Write-Error ".NET SDK is required for Desktop installation"
        Show-Prerequisites-Suggestions
        return
    }

    Push-Location $RepoRoot

    try {
        $desktopProjectPath = Join-Path $RepoRoot "src\MarketDataCollector.Uwp\MarketDataCollector.Uwp.csproj"
        $outputPath = Join-Path $RepoRoot "dist\win-x64\desktop"

        # Restore and build
        Write-Info "Restoring dependencies..."
        dotnet restore $desktopProjectPath

        Write-Info "Building Windows Desktop App..."
        dotnet publish $desktopProjectPath `
            -c Release `
            -r win-x64 `
            -o $outputPath `
            --self-contained true `
            -p:WindowsPackageType=None `
            -p:PublishReadyToRun=true

        if ($LASTEXITCODE -ne 0) {
            Write-Error "Build failed"
            return
        }

        Write-Success "Build completed successfully"

        # Setup config
        if (-not (Setup-Config)) { return }

        # Copy config to output
        $configFile = Join-Path $RepoRoot "config\appsettings.json"
        $sampleConfigFile = Join-Path $RepoRoot "config\appsettings.sample.json"

        if (Test-Path $configFile) {
            Copy-Item $configFile -Destination $outputPath
        }
        if (Test-Path $sampleConfigFile) {
            Copy-Item $sampleConfigFile -Destination $outputPath
        }

        # Create data directory
        $dataDir = Join-Path $outputPath "data"
        if (-not (Test-Path $dataDir)) {
            New-Item -ItemType Directory -Path $dataDir | Out-Null
        }

        Write-Host ""
        Write-Host "======================================================================" -ForegroundColor Green
        Write-Host "           Windows Desktop App Installation Complete!                 " -ForegroundColor Green
        Write-Host "======================================================================" -ForegroundColor Green
        Write-Host "  Location:  $outputPath" -ForegroundColor White
        Write-Host "" -ForegroundColor White
        Write-Host "  To run the app:" -ForegroundColor White
        Write-Host "    $outputPath\MarketDataCollector.Desktop.exe" -ForegroundColor Gray
        Write-Host "" -ForegroundColor White
        Write-Host "  Or create a shortcut to the executable on your desktop." -ForegroundColor White
        Write-Host "======================================================================" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}

function Uninstall-Docker {
    Write-Info "Uninstalling Docker containers and images..."

    Push-Location $RepoRoot

    try {
        # Stop containers
        Write-Info "Stopping containers..."
        docker compose down 2>$null

        # Remove image
        Write-Info "Removing Docker image..."
        docker rmi marketdatacollector:latest 2>$null

        Write-Success "Uninstallation complete"
        Write-Warning "Data directory (.\data) was preserved. Remove manually if needed."
    }
    finally {
        Pop-Location
    }
}

function Show-InteractiveMenu {
    Show-Header

    Test-Prerequisites | Out-Null
    Write-Host ""

    Write-Host "Choose installation method:" -ForegroundColor Yellow
    Write-Host "  1) Docker (recommended for production)"
    Write-Host "  2) Native .NET SDK (CLI application)"
    Write-Host "  3) Windows Desktop App (WinUI 3 - recommended for Windows)"
    Write-Host "  4) Check prerequisites only"
    Write-Host "  5) Exit"
    Write-Host ""

    $choice = Read-Host "Enter choice [1-5]"

    switch ($choice) {
        "1" { Install-Docker }
        "2" { Install-Native }
        "3" { Install-Desktop }
        "4" {
            Test-Prerequisites | Out-Null
            Show-Prerequisites-Suggestions
        }
        "5" {
            Write-Host "Exiting..."
            exit 0
        }
        default {
            Write-Error "Invalid choice"
            exit 1
        }
    }
}

# Main
switch ($Mode) {
    "Docker" {
        Show-Header
        Test-Prerequisites | Out-Null
        Install-Docker
    }
    "Native" {
        Show-Header
        Test-Prerequisites | Out-Null
        Install-Native
    }
    "Desktop" {
        Show-Header
        Test-Prerequisites | Out-Null
        Install-Desktop
    }
    "Check" {
        Show-Header
        Test-Prerequisites | Out-Null
        Show-Prerequisites-Suggestions
    }
    "Uninstall" {
        Show-Header
        Uninstall-Docker
    }
    "Help" {
        Show-Help
    }
    "" {
        Show-InteractiveMenu
    }
}
