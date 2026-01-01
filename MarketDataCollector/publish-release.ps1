#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build and package Market Data Collector for release distribution

.DESCRIPTION
    Creates platform-specific single-executable releases ready for distribution.
    Packages include executable, configuration files, and documentation.

.PARAMETER Platform
    Target platform(s) to build. Options: win-x64, linux-x64, osx-x64, osx-arm64, all

.PARAMETER Configuration
    Build configuration (Release or Debug)

.PARAMETER OutputDir
    Output directory for release packages

.EXAMPLE
    .\publish-release.ps1 -Platform all

.EXAMPLE
    .\publish-release.ps1 -Platform win-x64 -Configuration Release
#>

param(
    [ValidateSet("win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64", "all")]
    [string]$Platform = "all",
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [string]$OutputDir = ".\publish\releases",
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

# Banner
Write-Host @"
╔══════════════════════════════════════════════════════════════════════╗
║        Market Data Collector - Release Builder                      ║
║                    Version: $Version                                 ║
╚══════════════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

$ProjectPath = "src/MarketDataCollector/MarketDataCollector.csproj"

# Platform configurations
$Platforms = if ($Platform -eq "all") {
    @("win-x64", "linux-x64", "osx-x64", "osx-arm64")
} else {
    @($Platform)
}

# Ensure output directory exists
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

Write-Host "`nBuild Configuration:" -ForegroundColor Yellow
Write-Host "  Project: $ProjectPath" -ForegroundColor Gray
Write-Host "  Configuration: $Configuration" -ForegroundColor Gray
Write-Host "  Version: $Version" -ForegroundColor Gray
Write-Host "  Platforms: $($Platforms -join ', ')" -ForegroundColor Gray
Write-Host "  Output: $OutputDir" -ForegroundColor Gray
Write-Host ""

foreach ($TargetPlatform in $Platforms) {
    Write-Host "Building for $TargetPlatform..." -ForegroundColor Yellow

    $PlatformOutputDir = Join-Path $OutputDir $TargetPlatform
    $TempBuildDir = Join-Path $OutputDir "temp_$TargetPlatform"

    # Clean previous build
    if (Test-Path $TempBuildDir) {
        Remove-Item $TempBuildDir -Recurse -Force
    }

    # Build
    Write-Host "  [1/5] Compiling..." -ForegroundColor Cyan
    dotnet publish $ProjectPath `
        -c $Configuration `
        -r $TargetPlatform `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:PublishTrimmed=true `
        -p:PublishReadyToRun=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:IncludeAllContentForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:Version=$Version `
        -o $TempBuildDir `
        --nologo

    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ✗ Build failed for $TargetPlatform" -ForegroundColor Red
        continue
    }

    Write-Host "  ✓ Build complete" -ForegroundColor Green

    # Create package structure
    Write-Host "  [2/5] Creating package..." -ForegroundColor Cyan

    $PackageDir = Join-Path $OutputDir "package_$TargetPlatform"
    if (Test-Path $PackageDir) {
        Remove-Item $PackageDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $PackageDir -Force | Out-Null

    # Copy executable
    $ExeName = if ($TargetPlatform -like "win-*") { "MarketDataCollector.exe" } else { "MarketDataCollector" }
    Copy-Item (Join-Path $TempBuildDir $ExeName) $PackageDir -Force

    # Copy documentation
    Write-Host "  [3/5] Adding documentation..." -ForegroundColor Cyan
    Copy-Item "README.md" $PackageDir -Force -ErrorAction SilentlyContinue
    Copy-Item "HELP.md" $PackageDir -Force -ErrorAction SilentlyContinue
    Copy-Item "LICENSE" $PackageDir -Force -ErrorAction SilentlyContinue

    # Copy docs folder
    if (Test-Path "docs") {
        Copy-Item "docs" $PackageDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    # Copy configuration files
    Write-Host "  [4/5] Adding configuration files..." -ForegroundColor Cyan
    Copy-Item "appsettings.sample.json" $PackageDir -Force -ErrorAction SilentlyContinue

    # Create README for the package
    $PackageReadme = @"
# Market Data Collector v$Version

## Quick Start

### Windows:
``````
MarketDataCollector.exe --ui
``````

### Linux/macOS:
``````bash
chmod +x MarketDataCollector
./MarketDataCollector --ui
``````

Then open: http://localhost:8080

## First-Time Setup

1. Copy ``appsettings.sample.json`` to ``appsettings.json``
2. Edit ``appsettings.json`` with your data provider settings
3. Or use the web dashboard to configure everything

## Documentation

- **HELP.md** - Complete user guide
- **README.md** - Project overview
- **docs/GETTING_STARTED.md** - Step-by-step setup
- **docs/CONFIGURATION.md** - Configuration reference
- **docs/TROUBLESHOOTING.md** - Common issues

## Command Line

``````bash
MarketDataCollector --help           # Show all options
MarketDataCollector --ui             # Start web dashboard
MarketDataCollector --selftest       # Run self-tests
``````

## Support

- GitHub: https://github.com/rodoHasArrived/Test
- Issues: https://github.com/rodoHasArrived/Test/issues
- Documentation: ./docs/

---

Built on: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Platform: $TargetPlatform
Version: $Version
"@

    Set-Content -Path (Join-Path $PackageDir "QUICKSTART.md") -Value $PackageReadme

    # Create archive
    Write-Host "  [5/5] Creating archive..." -ForegroundColor Cyan

    $ArchiveName = "MarketDataCollector-$TargetPlatform-v$Version"
    $ArchivePath = if ($TargetPlatform -like "win-*") {
        "$OutputDir\$ArchiveName.zip"
    } else {
        "$OutputDir\$ArchiveName.tar.gz"
    }

    # Remove existing archive
    if (Test-Path $ArchivePath) {
        Remove-Item $ArchivePath -Force
    }

    # Create archive
    if ($TargetPlatform -like "win-*") {
        Compress-Archive -Path "$PackageDir\*" -DestinationPath $ArchivePath -CompressionLevel Optimal
    } else {
        # For Linux/macOS, use tar if available
        if (Get-Command tar -ErrorAction SilentlyContinue) {
            Push-Location $PackageDir
            tar -czf $ArchivePath *
            Pop-Location
        } else {
            # Fallback to zip for cross-platform compatibility
            $ArchivePath = "$OutputDir\$ArchiveName.zip"
            Compress-Archive -Path "$PackageDir\*" -DestinationPath $ArchivePath -CompressionLevel Optimal
        }
    }

    # Cleanup temp directories
    Remove-Item $TempBuildDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $PackageDir -Recurse -Force -ErrorAction SilentlyContinue

    $ArchiveSize = (Get-Item $ArchivePath).Length / 1MB
    Write-Host "  ✓ Package created: $ArchivePath ($([math]::Round($ArchiveSize, 2)) MB)" -ForegroundColor Green
    Write-Host ""
}

# Summary
Write-Host @"
╔══════════════════════════════════════════════════════════════════════╗
║                    Build Complete! ✓                                 ║
╚══════════════════════════════════════════════════════════════════════╝

Release packages created in: $OutputDir

Archives:
"@ -ForegroundColor Cyan

Get-ChildItem $OutputDir -Filter "*.zip", "*.tar.gz" -Recurse | ForEach-Object {
    $size = [math]::Round($_.Length / 1MB, 2)
    Write-Host "  $($_.Name) ($size MB)" -ForegroundColor Gray
}

Write-Host "`nNext Steps:" -ForegroundColor Yellow
Write-Host "  1. Test the builds on target platforms" -ForegroundColor Gray
Write-Host "  2. Create a GitHub release" -ForegroundColor Gray
Write-Host "  3. Upload the archives to the release" -ForegroundColor Gray
Write-Host "  4. Update installer scripts if needed" -ForegroundColor Gray
Write-Host ""
