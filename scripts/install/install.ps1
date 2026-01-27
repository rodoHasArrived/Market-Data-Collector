<#
.SYNOPSIS
    Market Data Collector - Installation Script for Windows

.DESCRIPTION
    This script automates the installation and setup of Market Data Collector on Windows.
    Features enhanced debugging output, progress tracking, and Windows toast notifications.

.PARAMETER Mode
    Installation mode: Docker, Native, Desktop, Check, Uninstall, or Help

.PARAMETER Verbose
    Enable verbose logging output

.PARAMETER NoNotify
    Disable Windows toast notifications

.PARAMETER LogPath
    Custom path for the installation log file

.EXAMPLE
    .\install.ps1
    Interactive installation

.EXAMPLE
    .\install.ps1 -Mode Docker
    Docker-based installation

.EXAMPLE
    .\install.ps1 -Mode Desktop -Verbose
    Windows Desktop installation with verbose output

.EXAMPLE
    .\install.ps1 -Mode Native -NoNotify
    Native .NET installation without notifications
#>

param(
    [Parameter(Position = 0)]
    [ValidateSet("Docker", "Native", "Desktop", "Check", "Uninstall", "Help")]
    [string]$Mode = "",

    [switch]$Verbose,

    [switch]$NoNotify,

    [string]$LogPath = ""
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = (Resolve-Path (Join-Path $ScriptDir "..\..")).Path
$LibDir = Join-Path (Split-Path -Parent $ScriptDir) "lib"

# Import the build notification module
$notificationModule = Join-Path $LibDir "BuildNotification.psm1"
if (Test-Path $notificationModule) {
    Import-Module $notificationModule -Force
    $useNotificationModule = $true
}
else {
    $useNotificationModule = $false
}

# Fallback functions if module not available
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
function Write-Warn($message) { Write-Host "[WARNING] $message" -ForegroundColor Yellow }
function Write-Err($message) { Write-Host "[ERROR] $message" -ForegroundColor Red }

function Show-Header {
    if ($useNotificationModule) {
        Show-BuildHeader -Title "Market Data Collector - Installation Script" -Subtitle "Version 1.2.0 - Enhanced Debugging Edition"
    }
    else {
        Write-Host ""
        Write-Host "======================================================================" -ForegroundColor Cyan
        Write-Host "           Market Data Collector - Installation Script                " -ForegroundColor Cyan
        Write-Host "                         Version 1.2.0                                " -ForegroundColor Cyan
        Write-Host "======================================================================" -ForegroundColor Cyan
        Write-Host ""
    }
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
    if ($useNotificationModule) {
        Show-BuildSection -Title "Windows Desktop Application Installation"
        Initialize-BuildNotification -EnableToast (-not $NoNotify) -Verbose $Verbose
    }
    else {
        Write-Info "Installing Windows Desktop Application..."
    }

    if (-not (Test-Command "dotnet")) {
        if ($useNotificationModule) {
            Show-BuildError -Error ".NET SDK is required for Desktop installation" `
                -Suggestion "Install .NET SDK 9.0 or later from https://dotnet.microsoft.com/download" `
                -Details @(
                    "The Windows Desktop App requires .NET 9.0 SDK",
                    "Windows App SDK 1.6+ is also required (installed via NuGet)"
                )
        }
        else {
            Write-Err ".NET SDK is required for Desktop installation"
        }
        Show-Prerequisites-Suggestions
        return
    }

    Push-Location $RepoRoot
    $buildSuccess = $false

    try {
        $desktopProjectPath = Join-Path $RepoRoot "src\MarketDataCollector.Uwp\MarketDataCollector.Uwp.csproj"
        $outputPath = Join-Path $RepoRoot "dist\win-x64\msix"
        $diagnosticLogDir = Join-Path $RepoRoot "diagnostic-logs"

        # Ensure diagnostic log directory exists
        if (-not (Test-Path $diagnosticLogDir)) {
            New-Item -ItemType Directory -Path $diagnosticLogDir -Force | Out-Null
        }

        $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"

        # ==================== STEP 1: Environment Check ====================
        if ($useNotificationModule) {
            Start-BuildStep -Name "Environment Verification" -Description "Checking build environment"
        }
        else {
            Write-Info "Verifying build environment..."
        }

        # Check .NET version
        $dotnetVersion = dotnet --version
        $dotnetSdkList = dotnet --list-sdks
        if ($useNotificationModule) {
            Update-BuildProgress -Message ".NET SDK version: $dotnetVersion"
            Update-BuildProgress -Message "Available SDKs: $($dotnetSdkList -join ', ')"
        }
        else {
            Write-Info ".NET SDK version: $dotnetVersion"
        }

        # Check for Windows SDK
        $windowsSdkPath = "C:\Program Files (x86)\Windows Kits\10"
        $hasWindowsSdk = Test-Path $windowsSdkPath
        if ($useNotificationModule) {
            if ($hasWindowsSdk) {
                Update-BuildProgress -Message "Windows SDK: Found at $windowsSdkPath"
            }
            else {
                Update-BuildProgress -Message "Windows SDK: Not found (will use Windows App SDK from NuGet)"
            }
            Complete-BuildStep -Success $true -Message "Environment verified"
        }
        else {
            if ($hasWindowsSdk) {
                Write-Success "Windows SDK found"
            }
        }

        # ==================== STEP 2: Clean Previous Build ====================
        if ($useNotificationModule) {
            Start-BuildStep -Name "Clean Previous Build" -Description "Removing old build artifacts"
        }
        else {
            Write-Info "Cleaning previous build..."
        }

        if (Test-Path $outputPath) {
            Remove-Item -Path $outputPath -Recurse -Force
            if ($useNotificationModule) {
                Update-BuildProgress -Message "Removed previous build at $outputPath"
            }
        }

        # Clean obj/bin directories for the project
        $objPath = Join-Path (Split-Path -Parent $desktopProjectPath) "obj"
        $binPath = Join-Path (Split-Path -Parent $desktopProjectPath) "bin"
        if (Test-Path $objPath) {
            Remove-Item -Path $objPath -Recurse -Force -ErrorAction SilentlyContinue
            if ($useNotificationModule) {
                Update-BuildProgress -Message "Cleaned obj directory"
            }
        }

        if ($useNotificationModule) {
            Complete-BuildStep -Success $true -Message "Clean completed"
        }

        # ==================== STEP 3: Restore Dependencies ====================
        if ($useNotificationModule) {
            Start-BuildStep -Name "Restore Dependencies" -Description "Restoring NuGet packages"
        }
        else {
            Write-Info "Restoring dependencies..."
        }

        $restoreLogFile = Join-Path $diagnosticLogDir "desktop-restore-$timestamp.log"
        $restoreArgs = @(
            "restore"
            $desktopProjectPath
            "-r", "win-x64"
            "-v", "normal"
        )

        if ($Verbose) {
            $restoreArgs += @("-v", "detailed")
        }

        if ($useNotificationModule) {
            Update-BuildProgress -Message "Running: dotnet $($restoreArgs -join ' ')"
        }

        $restoreOutput = & dotnet $restoreArgs 2>&1 | Tee-Object -FilePath $restoreLogFile
        $restoreExitCode = $LASTEXITCODE

        if ($restoreExitCode -ne 0) {
            if ($useNotificationModule) {
                Complete-BuildStep -Success $false -Message "Restore failed"
                Show-BuildError -Error "Failed to restore NuGet packages" `
                    -LogFile $restoreLogFile `
                    -Suggestion "Check network connectivity and NuGet source configuration" `
                    -Details @(
                        "Exit code: $restoreExitCode",
                        "Check log file for detailed error messages",
                        "Run 'dotnet nuget list source' to verify NuGet sources"
                    )
            }
            else {
                Write-Err "Restore failed. Check log: $restoreLogFile"
            }
            return
        }

        # Count restored packages
        $packageCount = ($restoreOutput | Select-String -Pattern "Restored" -AllMatches).Count
        if ($useNotificationModule) {
            Update-BuildProgress -Message "Restored $packageCount package reference(s)"
            Complete-BuildStep -Success $true -Message "Dependencies restored"
        }
        else {
            Write-Success "Dependencies restored ($packageCount packages)"
        }

        # ==================== STEP 4: Build Application ====================
        if ($useNotificationModule) {
            Start-BuildStep -Name "Build Application" -Description "Compiling Windows Desktop App"
        }
        else {
            Write-Info "Building Windows Desktop App..."
        }

        $buildLogFile = Join-Path $diagnosticLogDir "desktop-build-$timestamp.log"
        $publishLogFile = Join-Path $diagnosticLogDir "desktop-publish-$timestamp.log"

        # Build first to catch compilation errors
        $buildArgs = @(
            "build"
            $desktopProjectPath
            "-c", "Release"
            "-r", "win-x64"
            "--no-restore"
            "-p:Platform=x64"
        )

        if ($Verbose) {
            $buildArgs += @("-v", "detailed")
        }
        else {
            $buildArgs += @("-v", "normal")
        }

        if ($useNotificationModule) {
            Update-BuildProgress -Message "Compiling C# code..."
            Update-BuildProgress -Message "Target: win-x64 | Config: Release"
        }

        $buildOutput = & dotnet $buildArgs 2>&1 | Tee-Object -FilePath $buildLogFile
        $buildExitCode = $LASTEXITCODE

        # Extract warnings and errors
        $buildWarnings = $buildOutput | Select-String -Pattern "warning [A-Z]+\d+:" -AllMatches
        $buildErrors = $buildOutput | Select-String -Pattern "error [A-Z]+\d+:" -AllMatches

        if ($buildExitCode -ne 0) {
            if ($useNotificationModule) {
                Complete-BuildStep -Success $false -Message "Build failed"

                $errorDetails = @("Exit code: $buildExitCode")
                if ($buildErrors.Count -gt 0) {
                    $errorDetails += "Errors found: $($buildErrors.Count)"
                    # Show first few errors
                    $buildErrors | Select-Object -First 5 | ForEach-Object {
                        $errorDetails += "  $_"
                    }
                }

                Show-BuildError -Error "Build compilation failed" `
                    -LogFile $buildLogFile `
                    -Suggestion "Review the build errors above and fix the code issues" `
                    -Details $errorDetails
            }
            else {
                Write-Err "Build failed. Check log: $buildLogFile"
                if ($buildErrors.Count -gt 0) {
                    Write-Host "Errors:" -ForegroundColor Red
                    $buildErrors | Select-Object -First 5 | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
                }
            }
            return
        }

        if ($buildWarnings.Count -gt 0) {
            if ($useNotificationModule) {
                Show-BuildWarning -Message "Build completed with $($buildWarnings.Count) warning(s)" `
                    -Suggestion "Review warnings in: $buildLogFile"
            }
            else {
                Write-Warn "Build completed with $($buildWarnings.Count) warning(s)"
            }
        }

        if ($useNotificationModule) {
            Update-BuildProgress -Message "Build succeeded, proceeding to publish..."
            Complete-BuildStep -Success $true -Message "Compilation successful"
        }
        else {
            Write-Success "Build completed"
        }

        # ==================== STEP 5: Publish Application ====================
        if ($useNotificationModule) {
            Start-BuildStep -Name "Publish Application" -Description "Creating MSIX package"
        }
        else {
            Write-Info "Publishing MSIX package..."
        }

        $appInstallerUri = $env:MDC_APPINSTALLER_URI
        $certPfxPath = $env:MDC_SIGNING_CERT_PFX
        $certPassword = $env:MDC_SIGNING_CERT_PASSWORD
        $publishArgs = @(
            "publish"
            $desktopProjectPath
            "-c", "Release"
            "-r", "win-x64"
            "--self-contained", "true"
            "-p:WindowsPackageType=MSIX"
            "-p:PublishReadyToRun=true"
            "-p:Platform=x64"
            "-p:AppxPackageDir=$outputPath\\"
        )

        if (-not [string]::IsNullOrWhiteSpace($appInstallerUri)) {
            $publishArgs += @(
                "-p:GenerateAppInstallerFile=true"
                "-p:AppInstallerUri=$appInstallerUri"
                "-p:AppInstallerCheckForUpdateFrequency=OnApplicationRun"
                "-p:AppInstallerUpdateFrequency=1"
            )
        }

        if (-not [string]::IsNullOrWhiteSpace($certPfxPath)) {
            $publishArgs += @(
                "-p:PackageCertificateKeyFile=$certPfxPath"
                "-p:PackageCertificatePassword=$certPassword"
            )
        }
        else {
            $publishArgs += "-p:GenerateTemporaryStoreCertificate=true"
        }

        if ($Verbose) {
            $publishArgs += @("-v", "detailed")
        }
        else {
            $publishArgs += @("-v", "normal")
        }

        if ($useNotificationModule) {
            Update-BuildProgress -Message "Publishing MSIX package..."
            Update-BuildProgress -Message "ReadyToRun: Enabled (faster startup)"
        }

        $publishOutput = & dotnet $publishArgs 2>&1 | Tee-Object -FilePath $publishLogFile
        $publishExitCode = $LASTEXITCODE

        if ($publishExitCode -ne 0) {
            if ($useNotificationModule) {
                Complete-BuildStep -Success $false -Message "Publish failed"
                Show-BuildError -Error "Failed to publish application" `
                    -LogFile $publishLogFile `
                    -Suggestion "Check if all required Windows SDK components are installed" `
                    -Details @(
                        "Exit code: $publishExitCode",
                        "The publish step creates the final executable",
                        "Ensure Windows App SDK dependencies are available"
                    )
            }
            else {
                Write-Err "Publish failed. Check log: $publishLogFile"
            }
            return
        }

        if ($useNotificationModule) {
            Complete-BuildStep -Success $true -Message "Application published"
        }
        else {
            Write-Success "Publish completed"
        }

        # ==================== STEP 6: Verify Installation ====================
        if ($useNotificationModule) {
            Start-BuildStep -Name "Verify Installation" -Description "Checking MSIX output"
        }
        else {
            Write-Info "Verifying MSIX output..."
        }

        $msixPackages = Get-ChildItem -Path $outputPath -Filter "*.msix*" -File -ErrorAction SilentlyContinue
        if (-not $msixPackages) {
            if ($useNotificationModule) {
                Complete-BuildStep -Success $false -Message "MSIX package not found"
                Show-BuildError -Error "MSIX package not found at expected location" `
                    -Suggestion "Check build output and ensure the project packaged correctly" `
                    -Details @(
                        "Expected MSIX/MSIXBundle in: $outputPath",
                        "Check publish output directory: $outputPath"
                    )
            }
            else {
                Write-Err "MSIX package not found in: $outputPath"
            }
            return
        }

        $packageCount = $msixPackages.Count
        $totalSize = "{0:N2} MB" -f (($msixPackages | Measure-Object -Property Length -Sum).Sum / 1MB)

        if ($useNotificationModule) {
            Update-BuildProgress -Message "Packages: $packageCount ($totalSize total)"
            Complete-BuildStep -Success $true -Message "Installation verified"
        }
        else {
            Write-Success "MSIX package(s) verified: $packageCount"
        }

        $buildSuccess = $true

        # Show final summary
        if ($useNotificationModule) {
            Show-BuildSummary -Success $true -OutputPath $outputPath
        }

        Write-Host ""
        Write-Host "======================================================================" -ForegroundColor Green
        Write-Host "           Windows Desktop App Installation Complete!                 " -ForegroundColor Green
        Write-Host "======================================================================" -ForegroundColor Green
        Write-Host ""
        Write-Host "  Location:  " -ForegroundColor White -NoNewline
        Write-Host $outputPath -ForegroundColor Cyan
        Write-Host "  Packages:  " -ForegroundColor White -NoNewline
        Write-Host "$packageCount MSIX file(s)" -ForegroundColor Cyan
        Write-Host "  Size:      " -ForegroundColor White -NoNewline
        Write-Host "$totalSize total" -ForegroundColor Gray
        Write-Host ""
        if (-not [string]::IsNullOrWhiteSpace($certPfxPath)) {
            Write-Host "  Signing:   " -ForegroundColor White -NoNewline
            Write-Host "Signed with $certPfxPath" -ForegroundColor Gray
        }
        else {
            Write-Host "  Signing:   " -ForegroundColor White -NoNewline
            Write-Host "Temporary dev certificate used" -ForegroundColor Gray
        }
        Write-Host ""
        Write-Host "  Guidance:  " -ForegroundColor White -NoNewline
        Write-Host "docs/guides/msix-packaging.md" -ForegroundColor Gray
        Write-Host ""
        Write-Host "  Build logs saved to:" -ForegroundColor DarkGray
        Write-Host "    $diagnosticLogDir" -ForegroundColor Gray
        Write-Host ""
        Write-Host "  Or create a shortcut to the executable on your desktop." -ForegroundColor DarkGray
        Write-Host "======================================================================" -ForegroundColor Green
    }
    catch {
        if ($useNotificationModule) {
            Show-BuildError -Error "Unexpected error during installation" `
                -Details @(
                    "Exception: $($_.Exception.Message)",
                    "Line: $($_.InvocationInfo.ScriptLineNumber)"
                ) `
                -Suggestion "Check the diagnostic logs for more details"
            Show-BuildSummary -Success $false
        }
        else {
            Write-Err "Unexpected error: $($_.Exception.Message)"
        }
        throw
    }
    finally {
        Pop-Location
        if (-not $buildSuccess -and $useNotificationModule) {
            Write-Host ""
            Write-Host "  Troubleshooting Resources:" -ForegroundColor Yellow
            Write-Host "    • Run diagnostic script: .\scripts\diagnostics\diagnose-build.ps1 -Action desktop" -ForegroundColor Gray
            Write-Host "    • Check .NET SDK: dotnet --info" -ForegroundColor Gray
            Write-Host "    • Verify Windows SDK: Get-ItemProperty 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows Kits\Installed Roots'" -ForegroundColor Gray
            Write-Host "    • Documentation: docs/guides/troubleshooting.md" -ForegroundColor Gray
            Write-Host ""
        }
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
