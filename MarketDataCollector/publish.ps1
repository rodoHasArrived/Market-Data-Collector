# MarketDataCollector - Cross-platform Single Executable Build Script (PowerShell)
# Usage: .\publish.ps1 [-Platform <platform>] [-Project <project>]
# Examples:
#   .\publish.ps1                              # Build all platforms, all projects
#   .\publish.ps1 -Platform linux-x64          # Build only Linux x64
#   .\publish.ps1 -Platform win-x64 -Project collector  # Build only Windows x64, collector only

[CmdletBinding()]
param(
    [ValidateSet("all", "win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64")]
    [string]$Platform = "all",

    [ValidateSet("all", "collector", "ui")]
    [string]$Project = "all",

    [string]$Version = "1.0.0",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$OutputDir = "./dist",

    [switch]$Help
)

$ErrorActionPreference = "Stop"

# Script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

# Configuration
$AllPlatforms = @("win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64")
$CollectorProject = "src/MarketDataCollector/MarketDataCollector.csproj"
$UiProject = "src/MarketDataCollector.Ui/MarketDataCollector.Ui.csproj"

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] " -ForegroundColor Blue -NoNewline
    Write-Host $Message
}

function Write-Success {
    param([string]$Message)
    Write-Host "[SUCCESS] " -ForegroundColor Green -NoNewline
    Write-Host $Message
}

function Write-Warning {
    param([string]$Message)
    Write-Host "[WARNING] " -ForegroundColor Yellow -NoNewline
    Write-Host $Message
}

function Write-Error {
    param([string]$Message)
    Write-Host "[ERROR] " -ForegroundColor Red -NoNewline
    Write-Host $Message
}

function Show-Help {
    Write-Host @"
MarketDataCollector - Single Executable Build Script (PowerShell)

Usage: .\publish.ps1 [-Platform <platform>] [-Project <project>]

Parameters:
  -Platform     Target platform (default: all)
                  all         Build for all platforms
                  win-x64     Windows x64
                  win-arm64   Windows ARM64
                  linux-x64   Linux x64
                  linux-arm64 Linux ARM64
                  osx-x64     macOS x64 (Intel)
                  osx-arm64   macOS ARM64 (Apple Silicon)

  -Project      Target project (default: all)
                  all        Build both projects
                  collector  Build only MarketDataCollector
                  ui         Build only MarketDataCollector.Ui

  -Version      Version number (default: 1.0.0)
  -Configuration Build configuration (default: Release)
  -OutputDir    Output directory (default: ./dist)
  -Help         Show this help message

Examples:
  .\publish.ps1                                    # Build all platforms, all projects
  .\publish.ps1 -Platform linux-x64                # Build Linux x64 only
  .\publish.ps1 -Platform win-x64 -Project collector  # Build Windows collector only
  .\publish.ps1 -Version 2.0.0                     # Build with custom version
"@
}

function Publish-Project {
    param(
        [string]$ProjectPath,
        [string]$RuntimeId,
        [string]$ProjectName,
        [string]$OutputSubDir
    )

    $outputPath = Join-Path $OutputDir $RuntimeId $OutputSubDir

    Write-Info "Publishing $ProjectName for $RuntimeId..."

    dotnet publish $ProjectPath `
        -c $Configuration `
        -r $RuntimeId `
        -o $outputPath `
        -p:Version=$Version `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:PublishTrimmed=true `
        -p:EnableCompressionInSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:IncludeAllContentForSelfExtract=true

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to publish $ProjectName for $RuntimeId"
    }

    # Copy configuration file
    $configFile = Join-Path $ScriptDir "appsettings.json"
    if (Test-Path $configFile) {
        Copy-Item $configFile -Destination $outputPath
    }

    Write-Success "Published $ProjectName for $RuntimeId -> $outputPath"
}

function New-Package {
    param([string]$RuntimeId)

    $outputPath = Join-Path $OutputDir $RuntimeId
    $packageName = "MarketDataCollector-$Version-$RuntimeId"

    if (Test-Path $outputPath) {
        Write-Info "Creating package for $RuntimeId..."

        $archivePath = Join-Path $OutputDir "$packageName.zip"

        if (Test-Path $archivePath) {
            Remove-Item $archivePath -Force
        }

        Compress-Archive -Path $outputPath -DestinationPath $archivePath
        Write-Success "Created $archivePath"
    }
}

# Main script
if ($Help) {
    Show-Help
    exit 0
}

# Determine target platforms
$TargetPlatforms = if ($Platform -eq "all") { $AllPlatforms } else { @($Platform) }

# Clean output directory
Write-Info "Cleaning output directory: $OutputDir"
if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

# Build projects
Write-Info "Building MarketDataCollector v$Version ($Configuration)"
Write-Info "Target platforms: $($TargetPlatforms -join ', ')"
Write-Info "Target projects: $Project"
Write-Host ""

foreach ($rid in $TargetPlatforms) {
    Write-Info "=== Building for $rid ==="

    if ($Project -eq "all" -or $Project -eq "collector") {
        Publish-Project -ProjectPath $CollectorProject -RuntimeId $rid -ProjectName "MarketDataCollector" -OutputSubDir "collector"
    }

    if ($Project -eq "all" -or $Project -eq "ui") {
        Publish-Project -ProjectPath $UiProject -RuntimeId $rid -ProjectName "MarketDataCollector.Ui" -OutputSubDir "ui"
    }

    # Create package
    New-Package -RuntimeId $rid

    Write-Host ""
}

# Summary
Write-Success "=== Build Complete ==="
Write-Host ""
Write-Info "Output directory: $OutputDir"
Write-Host ""

# List outputs
Get-ChildItem $OutputDir -Recurse -Filter "MarketDataCollector*" |
    Where-Object { $_.Extension -in @(".exe", "", ".zip", ".tar.gz") } |
    Select-Object -First 20 |
    ForEach-Object { Write-Host "  $($_.FullName)" }

Write-Host ""
Write-Info "To run the collector:"
Write-Host "  $OutputDir\win-x64\collector\MarketDataCollector.exe"
