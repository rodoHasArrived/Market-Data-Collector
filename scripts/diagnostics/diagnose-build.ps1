# =============================================================================
# Market Data Collector - Build Diagnostics Script (PowerShell)
# =============================================================================
#
# This script helps diagnose build and restore issues by running dotnet
# commands with diagnostic logging enabled.
#
# Usage:
#   .\scripts\diagnose-build.ps1              # Run all diagnostics
#   .\scripts\diagnose-build.ps1 -Action restore      # Diagnose restore only
#   .\scripts\diagnose-build.ps1 -Action build        # Diagnose build only
#   .\scripts\diagnose-build.ps1 -Action clean        # Clean and diagnose
#
# =============================================================================

param(
    [Parameter(Position = 0)]
    [ValidateSet("all", "restore", "build", "clean")]
    [string]$Action = "all"
)

# Script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$LogDir = Join-Path $ProjectRoot "diagnostic-logs"

# Colors
$ColorInfo = "Cyan"
$ColorSuccess = "Green"
$ColorWarning = "Yellow"
$ColorError = "Red"

function Print-Header {
    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║         Market Data Collector - Build Diagnostics                    ║" -ForegroundColor Cyan
    Write-Host "╚══════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""
}

function Print-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor $ColorInfo
}

function Print-Success {
    param([string]$Message)
    Write-Host "[SUCCESS] $Message" -ForegroundColor $ColorSuccess
}

function Print-Warning {
    param([string]$Message)
    Write-Host "[WARNING] $Message" -ForegroundColor $ColorWarning
}

function Print-Error {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor $ColorError
}

function Setup-LogDir {
    if (-not (Test-Path $LogDir)) {
        New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
    }
    Print-Info "Diagnostic logs will be saved to: $LogDir"
}

function Check-DotNet {
    Print-Info "Checking .NET SDK..."
    
    try {
        $dotnetVersion = dotnet --version
        Print-Success ".NET SDK version: $dotnetVersion"
        
        # List installed SDKs
        Print-Info "Installed .NET SDKs:"
        dotnet --list-sdks | ForEach-Object { Write-Host "  $_" }
        Write-Host ""
    }
    catch {
        Print-Error ".NET SDK not found. Please install .NET SDK 8.0 or later."
        exit 1
    }
}

function Check-NuGetSources {
    Print-Info "Checking NuGet sources..."
    dotnet nuget list source
    Write-Host ""
}

function Diagnose-Restore {
    Print-Info "Running dotnet restore with diagnostic logging..."
    
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $logFile = Join-Path $LogDir "restore-diagnostic-$timestamp.log"
    
    Push-Location $ProjectRoot
    
    try {
        # Run restore with diagnostic verbosity
        dotnet restore /p:EnableWindowsTargeting=true -v diag > $logFile 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Print-Success "Restore completed successfully"
        }
        else {
            Print-Error "Restore failed! Check log file: $logFile"
            
            # Show last 50 lines of log
            Print-Info "Last 50 lines of diagnostic log:"
            Get-Content $logFile -Tail 50 | ForEach-Object { Write-Host "  $_" }
            return $false
        }
        
        # Check for warnings in the log
        $warningCount = 0
        if (Test-Path $logFile) {
            $warningCount = (Select-String -Path $logFile -Pattern "warning" -AllMatches -CaseSensitive:$false).Count
        }
        if ($warningCount -gt 0) {
            Print-Warning "Found $warningCount warning(s) in restore output"
            Print-Info "To view warnings: Select-String -Path $logFile -Pattern warning -CaseSensitive:`$false"
        }
        
        Print-Success "Diagnostic log saved to: $logFile"
        Write-Host ""
        return $true
    }
    finally {
        Pop-Location
    }
}

function Diagnose-Build {
    Print-Info "Running dotnet build with diagnostic logging..."
    
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $logFile = Join-Path $LogDir "build-diagnostic-$timestamp.log"
    
    Push-Location $ProjectRoot
    
    try {
        # Run build with diagnostic verbosity
        dotnet build -c Release /p:EnableWindowsTargeting=true -v diag > $logFile 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Print-Success "Build completed successfully"
        }
        else {
            Print-Error "Build failed! Check log file: $logFile"
            
            # Show last 50 lines of log
            Print-Info "Last 50 lines of diagnostic log:"
            Get-Content $logFile -Tail 50 | ForEach-Object { Write-Host "  $_" }
            return $false
        }
        
        # Check for warnings in the log
        $warningCount = 0
        if (Test-Path $logFile) {
            $warningCount = (Select-String -Path $logFile -Pattern "warning" -AllMatches -CaseSensitive:$false).Count
        }
        if ($warningCount -gt 0) {
            Print-Warning "Found $warningCount warning(s) in build output"
            Print-Info "To view warnings: Select-String -Path $logFile -Pattern warning -CaseSensitive:`$false"
        }
        
        Print-Success "Diagnostic log saved to: $logFile"
        Write-Host ""
        return $true
    }
    finally {
        Pop-Location
    }
}

function Clean-Solution {
    Print-Info "Cleaning solution..."
    
    Push-Location $ProjectRoot
    
    try {
        dotnet clean
        
        if ($LASTEXITCODE -eq 0) {
            Print-Success "Clean completed successfully"
        }
        else {
            Print-Warning "Clean encountered issues"
        }
        
        # Ask to clear NuGet cache
        $response = Read-Host "Do you want to clear NuGet cache? [y/N]"
        if ($response -eq "y" -or $response -eq "Y") {
            Print-Info "Clearing NuGet cache..."
            dotnet nuget locals all --clear
            Print-Success "NuGet cache cleared"
        }
        Write-Host ""
    }
    finally {
        Pop-Location
    }
}

function Show-LogSummary {
    Print-Info "Diagnostic log summary:"
    Write-Host ""
    
    if (Test-Path $LogDir) {
        $logs = Get-ChildItem -Path $LogDir -File
        if ($logs.Count -gt 0) {
            $logs | ForEach-Object {
                $size = "{0:N2} KB" -f ($_.Length / 1KB)
                Write-Host "  $($_.Name) ($size)"
            }
            Write-Host ""
            Print-Info "To view a log file: Get-Content $LogDir\<filename>"
            Print-Info "To search for errors: Select-String -Path $LogDir\<filename> -Pattern error"
            Print-Info "To search for warnings: Select-String -Path $LogDir\<filename> -Pattern warning"
        }
        else {
            Print-Info "No diagnostic logs found"
        }
    }
    else {
        Print-Info "No diagnostic logs found"
    }
    Write-Host ""
}

# Main execution
Print-Header

Setup-LogDir
Check-DotNet

$success = $true

switch ($Action) {
    "restore" {
        Check-NuGetSources
        $success = Diagnose-Restore
    }
    "build" {
        $success = Diagnose-Build
    }
    "clean" {
        Clean-Solution
        $success = Diagnose-Restore
        if ($success) {
            $success = Diagnose-Build
        }
    }
    default {
        Check-NuGetSources
        $success = Diagnose-Restore
        if ($success) {
            $success = Diagnose-Build
        }
        else {
            Print-Error "Skipping build due to restore failure"
        }
    }
}

Show-LogSummary

Write-Host "╔══════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║                    Diagnostics Complete                              ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan

if (-not $success) {
    exit 1
}
