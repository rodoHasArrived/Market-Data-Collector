# =============================================================================
# Market Data Collector - Build Diagnostics Script (PowerShell)
# =============================================================================
#
# Enhanced diagnostic tool for troubleshooting build and restore issues,
# with specific support for the Windows Desktop Application.
#
# Usage:
#   .\scripts\diagnostics\diagnose-build.ps1              # Run all diagnostics
#   .\scripts\diagnostics\diagnose-build.ps1 -Action restore      # Diagnose restore only
#   .\scripts\diagnostics\diagnose-build.ps1 -Action build        # Diagnose build only
#   .\scripts\diagnostics\diagnose-build.ps1 -Action desktop      # Desktop-specific diagnostics
#   .\scripts\diagnostics\diagnose-build.ps1 -Action clean        # Clean and diagnose
#   .\scripts\diagnostics\diagnose-build.ps1 -Action environment  # Environment check only
#
# =============================================================================

param(
    [Parameter(Position = 0)]
    [ValidateSet("all", "restore", "build", "desktop", "clean", "environment")]
    [string]$Action = "all",

    [switch]$Verbose,

    [switch]$NoNotify,

    [string]$OutputFormat = "console"
)

# Script directory setup
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = (Resolve-Path (Join-Path $ScriptDir "..\..")).Path
$LogDir = Join-Path $ProjectRoot "diagnostic-logs"
$LibDir = Join-Path $ProjectRoot "scripts\lib"

# Import build notification module if available
$notificationModule = Join-Path $LibDir "BuildNotification.psm1"
if (Test-Path $notificationModule) {
    Import-Module $notificationModule -Force
    $useNotificationModule = $true
    Initialize-BuildNotification -EnableToast (-not $NoNotify) -Verbose $Verbose
}
else {
    $useNotificationModule = $false
}

# Diagnostic results collection
$script:DiagnosticResults = @{
    Environment = @{}
    Checks = @()
    Warnings = @()
    Errors = @()
    Suggestions = @()
    StartTime = Get-Date
}

# Colors
$ColorInfo = "Cyan"
$ColorSuccess = "Green"
$ColorWarning = "Yellow"
$ColorError = "Red"

function Print-Header {
    if ($useNotificationModule) {
        Show-BuildHeader -Title "Market Data Collector - Build Diagnostics" -Subtitle "Comprehensive Build Environment Analysis"
    }
    else {
        Write-Host ""
        Write-Host "╔══════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
        Write-Host "║         Market Data Collector - Build Diagnostics                    ║" -ForegroundColor Cyan
        Write-Host "╚══════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
        Write-Host ""
    }
}

function Print-Section {
    param([string]$Title)
    if ($useNotificationModule) {
        Show-BuildSection -Title $Title
    }
    else {
        Write-Host ""
        Write-Host "┌─ $Title " -ForegroundColor Yellow -NoNewline
        Write-Host ("─" * (68 - $Title.Length)) -ForegroundColor DarkYellow
    }
}

function Print-Info {
    param([string]$Message)
    Write-Host "  [INFO] $Message" -ForegroundColor $ColorInfo
}

function Print-Success {
    param([string]$Message)
    Write-Host "  [✓] $Message" -ForegroundColor $ColorSuccess
}

function Print-Warning {
    param([string]$Message)
    Write-Host "  [⚠] $Message" -ForegroundColor $ColorWarning
    $script:DiagnosticResults.Warnings += $Message
}

function Print-Error {
    param([string]$Message)
    Write-Host "  [✗] $Message" -ForegroundColor $ColorError
    $script:DiagnosticResults.Errors += $Message
}

function Print-Detail {
    param([string]$Message)
    Write-Host "      $Message" -ForegroundColor Gray
}

function Add-Check {
    param(
        [string]$Name,
        [string]$Status,
        [string]$Value = "",
        [string]$Details = ""
    )
    $script:DiagnosticResults.Checks += @{
        Name = $Name
        Status = $Status
        Value = $Value
        Details = $Details
        Timestamp = Get-Date
    }
}

function Setup-LogDir {
    if (-not (Test-Path $LogDir)) {
        New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
    }
    Print-Info "Diagnostic logs will be saved to: $LogDir"
}

function Check-Environment {
    Print-Section "Environment Analysis"

    # Operating System
    $os = [System.Environment]::OSVersion
    $script:DiagnosticResults.Environment["OS"] = "$($os.Platform) $($os.Version)"
    Print-Info "Operating System: $($os.Platform) $($os.Version)"

    # Windows Version (detailed)
    if ($env:OS -eq "Windows_NT") {
        try {
            $winVer = (Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion")
            $buildNumber = $winVer.CurrentBuild
            $displayVersion = $winVer.DisplayVersion
            $script:DiagnosticResults.Environment["WindowsBuild"] = $buildNumber
            Print-Info "Windows Build: $buildNumber ($displayVersion)"

            # Check if Windows 10 19041+ (required for WinUI 3)
            if ([int]$buildNumber -lt 19041) {
                Print-Warning "Windows build $buildNumber is below minimum (19041) for WinUI 3"
                $script:DiagnosticResults.Suggestions += "Update Windows to version 2004 (build 19041) or later for WinUI 3 support"
            }
            else {
                Print-Success "Windows build meets WinUI 3 requirements"
            }
            Add-Check -Name "Windows Build" -Status $(if ([int]$buildNumber -ge 19041) { "Pass" } else { "Warning" }) -Value $buildNumber
        }
        catch {
            Print-Warning "Could not determine Windows version details"
        }
    }

    # CPU Architecture
    $arch = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture
    $script:DiagnosticResults.Environment["Architecture"] = $arch.ToString()
    Print-Info "CPU Architecture: $arch"
    Add-Check -Name "Architecture" -Status "Info" -Value $arch.ToString()

    # Memory
    try {
        $totalMemGB = [math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB, 2)
        $script:DiagnosticResults.Environment["Memory"] = "${totalMemGB} GB"
        Print-Info "Total Memory: ${totalMemGB} GB"

        if ($totalMemGB -lt 8) {
            Print-Warning "Less than 8 GB RAM may cause build performance issues"
        }
        Add-Check -Name "Memory" -Status $(if ($totalMemGB -ge 8) { "Pass" } else { "Warning" }) -Value "${totalMemGB} GB"
    }
    catch {
        Print-Warning "Could not determine system memory"
    }

    # Disk Space
    try {
        $drive = (Get-Item $ProjectRoot).PSDrive
        $freeSpaceGB = [math]::Round($drive.Free / 1GB, 2)
        $script:DiagnosticResults.Environment["DiskFree"] = "${freeSpaceGB} GB"
        Print-Info "Free Disk Space: ${freeSpaceGB} GB on $($drive.Name):"

        if ($freeSpaceGB -lt 10) {
            Print-Warning "Less than 10 GB free disk space may cause issues"
            $script:DiagnosticResults.Suggestions += "Free up disk space - builds require at least 5 GB, recommended 10 GB+"
        }
        Add-Check -Name "Disk Space" -Status $(if ($freeSpaceGB -ge 10) { "Pass" } else { "Warning" }) -Value "${freeSpaceGB} GB"
    }
    catch {
        Print-Warning "Could not determine free disk space"
    }
}

function Check-DotNet {
    Print-Section ".NET SDK Analysis"

    try {
        $dotnetVersion = dotnet --version
        $script:DiagnosticResults.Environment["DotNetVersion"] = $dotnetVersion
        Print-Success ".NET SDK version: $dotnetVersion"
        Add-Check -Name ".NET SDK" -Status "Pass" -Value $dotnetVersion

        # Check for .NET 9.0 (required for the project)
        if (-not $dotnetVersion.StartsWith("9.")) {
            Print-Warning "Project requires .NET 9.0, found $dotnetVersion"
            $script:DiagnosticResults.Suggestions += "Install .NET 9.0 SDK from https://dotnet.microsoft.com/download/dotnet/9.0"
        }

        # List installed SDKs
        Write-Host ""
        Print-Info "Installed .NET SDKs:"
        $sdks = dotnet --list-sdks
        $sdks | ForEach-Object { Print-Detail $_ }
        $script:DiagnosticResults.Environment["InstalledSDKs"] = $sdks -join "; "

        # Check for required workloads
        Write-Host ""
        Print-Info "Checking .NET workloads..."
        $workloads = dotnet workload list 2>$null
        if ($workloads) {
            $workloads | ForEach-Object { Print-Detail $_ }
        }

        # List installed runtimes
        Write-Host ""
        Print-Info "Installed .NET Runtimes:"
        $runtimes = dotnet --list-runtimes
        $windowsRuntimes = $runtimes | Where-Object { $_ -like "*WindowsDesktop*" -or $_ -like "*Windows*" }
        if ($windowsRuntimes) {
            $windowsRuntimes | ForEach-Object { Print-Detail $_ }
            Print-Success "Windows Desktop runtime found"
            Add-Check -Name "Windows Desktop Runtime" -Status "Pass"
        }
        else {
            Print-Warning "Windows Desktop runtime not found"
            Add-Check -Name "Windows Desktop Runtime" -Status "Warning"
        }
    }
    catch {
        Print-Error ".NET SDK not found. Please install .NET SDK 9.0 or later."
        Add-Check -Name ".NET SDK" -Status "Fail" -Details "Not installed"
        $script:DiagnosticResults.Suggestions += "Install .NET 9.0 SDK from https://dotnet.microsoft.com/download/dotnet/9.0"
        return $false
    }

    return $true
}

function Check-NuGetSources {
    Print-Section "NuGet Configuration"

    Print-Info "Configured NuGet sources:"
    $sources = dotnet nuget list source
    $sources | ForEach-Object { Print-Detail $_ }

    # Check for nuget.org
    if ($sources -match "nuget.org") {
        Print-Success "nuget.org source configured"
        Add-Check -Name "NuGet.org Source" -Status "Pass"
    }
    else {
        Print-Warning "nuget.org source not found - may have package resolution issues"
        Add-Check -Name "NuGet.org Source" -Status "Warning"
        $script:DiagnosticResults.Suggestions += "Add nuget.org source: dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org"
    }

    # Check NuGet cache
    $nugetCachePath = Join-Path $env:USERPROFILE ".nuget\packages"
    if (Test-Path $nugetCachePath) {
        $cacheSize = (Get-ChildItem -Path $nugetCachePath -Recurse -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
        $cacheSizeGB = [math]::Round($cacheSize / 1GB, 2)
        Print-Info "NuGet cache size: ${cacheSizeGB} GB"
        Print-Detail "Location: $nugetCachePath"
    }
}

function Check-WindowsSDK {
    Print-Section "Windows SDK Analysis"

    # Check Windows Kits
    $windowsKitsPath = "C:\Program Files (x86)\Windows Kits\10"
    if (Test-Path $windowsKitsPath) {
        Print-Success "Windows SDK found at: $windowsKitsPath"
        Add-Check -Name "Windows SDK" -Status "Pass"

        # List installed SDK versions
        $sdkVersionsPath = Join-Path $windowsKitsPath "Include"
        if (Test-Path $sdkVersionsPath) {
            $versions = Get-ChildItem -Path $sdkVersionsPath -Directory | Sort-Object Name -Descending
            Print-Info "Installed SDK versions:"
            $versions | Select-Object -First 5 | ForEach-Object { Print-Detail $_.Name }
            $script:DiagnosticResults.Environment["WindowsSDKVersions"] = ($versions.Name -join ", ")
        }
    }
    else {
        Print-Warning "Windows SDK not found at default location"
        Print-Detail "This is OK - Windows App SDK from NuGet will be used instead"
        Add-Check -Name "Windows SDK" -Status "Info" -Details "Using Windows App SDK from NuGet"
    }

    # Check Visual Studio Build Tools
    $vswherePath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswherePath) {
        Print-Info "Checking Visual Studio installations..."
        $vsInstalls = & $vswherePath -all -prerelease -format json | ConvertFrom-Json
        if ($vsInstalls) {
            foreach ($vs in $vsInstalls) {
                Print-Detail "$($vs.displayName) - $($vs.installationVersion)"
            }
            Add-Check -Name "Visual Studio" -Status "Pass" -Value $vsInstalls[0].displayName
        }
    }
    else {
        Print-Info "Visual Studio not detected (Build Tools may still be available)"
    }

    # Check MSBuild
    try {
        $msbuildPath = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe 2>$null | Select-Object -First 1
        if ($msbuildPath) {
            Print-Success "MSBuild found: $msbuildPath"
            Add-Check -Name "MSBuild" -Status "Pass"
        }
        else {
            Print-Info "MSBuild not found via vswhere (dotnet build will be used)"
        }
    }
    catch {
        Print-Info "MSBuild check skipped"
    }
}

function Check-DesktopProject {
    Print-Section "Desktop Project Analysis"

    $desktopProjectPath = Join-Path $ProjectRoot "src\MarketDataCollector.Uwp\MarketDataCollector.Uwp.csproj"

    if (-not (Test-Path $desktopProjectPath)) {
        Print-Error "Desktop project not found: $desktopProjectPath"
        Add-Check -Name "Desktop Project" -Status "Fail" -Details "Project file not found"
        return $false
    }

    Print-Success "Desktop project found"
    Add-Check -Name "Desktop Project" -Status "Pass"

    # Parse project file
    try {
        [xml]$projectXml = Get-Content $desktopProjectPath
        $propertyGroups = $projectXml.Project.PropertyGroup

        # Extract key properties
        $targetFramework = ($propertyGroups | Where-Object { $_.TargetFramework }).TargetFramework | Select-Object -First 1
        $outputType = ($propertyGroups | Where-Object { $_.OutputType }).OutputType | Select-Object -First 1
        $useWinUI = ($propertyGroups | Where-Object { $_.UseWinUI }).UseWinUI | Select-Object -First 1
        $platforms = ($propertyGroups | Where-Object { $_.Platforms }).Platforms | Select-Object -First 1

        Print-Info "Project Configuration:"
        Print-Detail "Target Framework: $targetFramework"
        Print-Detail "Output Type: $outputType"
        Print-Detail "Uses WinUI: $useWinUI"
        Print-Detail "Platforms: $platforms"

        # Validate target framework
        if ($targetFramework -and $targetFramework -match "net9\.0-windows") {
            Print-Success "Target framework is .NET 9.0 Windows"
            Add-Check -Name "Target Framework" -Status "Pass" -Value $targetFramework
        }
        elseif ($targetFramework) {
            Print-Warning "Unexpected target framework: $targetFramework"
            Add-Check -Name "Target Framework" -Status "Warning" -Value $targetFramework
        }

        # Check package references
        Write-Host ""
        Print-Info "Package References:"
        $packages = $projectXml.Project.ItemGroup.PackageReference
        foreach ($pkg in $packages) {
            if ($pkg.Include) {
                Print-Detail "$($pkg.Include) v$($pkg.Version)"
            }
        }

        # Check for Windows App SDK
        $windowsAppSdk = $packages | Where-Object { $_.Include -eq "Microsoft.WindowsAppSDK" }
        if ($windowsAppSdk) {
            Print-Success "Windows App SDK: v$($windowsAppSdk.Version)"
            Add-Check -Name "Windows App SDK" -Status "Pass" -Value $windowsAppSdk.Version

            # Check version compatibility
            $version = [version]($windowsAppSdk.Version -replace "-.*", "")
            if ($version.Major -lt 1 -or ($version.Major -eq 1 -and $version.Minor -lt 6)) {
                Print-Warning "Windows App SDK 1.6+ required for .NET 9 compatibility"
                $script:DiagnosticResults.Suggestions += "Update Windows App SDK to 1.6 or later for .NET 9 support"
            }
        }
        else {
            Print-Warning "Windows App SDK reference not found"
            Add-Check -Name "Windows App SDK" -Status "Warning" -Details "Not referenced"
        }
    }
    catch {
        Print-Warning "Could not parse project file: $($_.Exception.Message)"
    }

    return $true
}

function Diagnose-Restore {
    Print-Section "NuGet Restore Diagnostics"

    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $logFile = Join-Path $LogDir "restore-diagnostic-$timestamp.log"

    Push-Location $ProjectRoot

    try {
        Print-Info "Running dotnet restore with diagnostic logging..."
        Print-Detail "This may take a few minutes..."

        $verbosity = if ($Verbose) { "detailed" } else { "normal" }

        # Run restore with diagnostic verbosity
        $restoreOutput = dotnet restore /p:EnableWindowsTargeting=true -v $verbosity 2>&1 | Tee-Object -FilePath $logFile

        if ($LASTEXITCODE -eq 0) {
            Print-Success "Restore completed successfully"
            Add-Check -Name "NuGet Restore" -Status "Pass"
        }
        else {
            Print-Error "Restore failed! Exit code: $LASTEXITCODE"
            Add-Check -Name "NuGet Restore" -Status "Fail" -Details "Exit code: $LASTEXITCODE"

            # Show last 20 lines of log
            Write-Host ""
            Print-Info "Last 20 lines of diagnostic log:"
            Get-Content $logFile -Tail 20 | ForEach-Object { Print-Detail $_ }

            # Analyze errors
            $errors = $restoreOutput | Select-String -Pattern "error" -CaseSensitive:$false
            if ($errors) {
                Write-Host ""
                Print-Info "Detected errors:"
                $errors | Select-Object -First 10 | ForEach-Object { Print-Error $_.Line }
            }

            return $false
        }

        # Check for warnings in the log
        $warnings = Select-String -Path $logFile -Pattern "warning" -AllMatches -CaseSensitive:$false
        if ($warnings.Count -gt 0) {
            Print-Warning "Found $($warnings.Count) warning(s) in restore output"
            Print-Detail "Review with: Select-String -Path '$logFile' -Pattern warning"
        }

        Print-Success "Diagnostic log saved to: $logFile"
        return $true
    }
    finally {
        Pop-Location
    }
}

function Diagnose-Build {
    Print-Section "Build Diagnostics"

    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $logFile = Join-Path $LogDir "build-diagnostic-$timestamp.log"

    Push-Location $ProjectRoot

    try {
        Print-Info "Running dotnet build with diagnostic logging..."

        $verbosity = if ($Verbose) { "detailed" } else { "normal" }

        # Run build with diagnostic verbosity
        $buildOutput = dotnet build -c Release /p:EnableWindowsTargeting=true -v $verbosity 2>&1 | Tee-Object -FilePath $logFile

        if ($LASTEXITCODE -eq 0) {
            Print-Success "Build completed successfully"
            Add-Check -Name "Solution Build" -Status "Pass"
        }
        else {
            Print-Error "Build failed! Exit code: $LASTEXITCODE"
            Add-Check -Name "Solution Build" -Status "Fail" -Details "Exit code: $LASTEXITCODE"

            # Show last 30 lines of log
            Write-Host ""
            Print-Info "Last 30 lines of diagnostic log:"
            Get-Content $logFile -Tail 30 | ForEach-Object { Print-Detail $_ }

            # Analyze build errors
            $errors = $buildOutput | Select-String -Pattern "error [A-Z]+\d+:" -CaseSensitive:$false
            if ($errors) {
                Write-Host ""
                Print-Info "Build errors detected:"
                $errors | Select-Object -First 10 | ForEach-Object { Print-Error $_.Line }
            }

            return $false
        }

        # Check for warnings
        $warnings = Select-String -Path $logFile -Pattern "warning" -AllMatches -CaseSensitive:$false
        if ($warnings.Count -gt 0) {
            Print-Warning "Found $($warnings.Count) warning(s) in build output"
        }

        Print-Success "Diagnostic log saved to: $logFile"
        return $true
    }
    finally {
        Pop-Location
    }
}

function Diagnose-DesktopBuild {
    Print-Section "Desktop Application Build Diagnostics"

    $desktopProjectPath = Join-Path $ProjectRoot "src\MarketDataCollector.Uwp\MarketDataCollector.Uwp.csproj"
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $logFile = Join-Path $LogDir "desktop-build-diagnostic-$timestamp.log"

    Push-Location $ProjectRoot

    try {
        Print-Info "Building Desktop Application with diagnostics..."

        $verbosity = if ($Verbose) { "detailed" } else { "normal" }

        # Restore first
        Print-Info "Step 1: Restoring dependencies..."
        $restoreResult = dotnet restore $desktopProjectPath -r win-x64 -v $verbosity 2>&1
        if ($LASTEXITCODE -ne 0) {
            Print-Error "Desktop restore failed"
            Add-Check -Name "Desktop Restore" -Status "Fail"
            $restoreResult | Out-File -FilePath $logFile
            return $false
        }
        Print-Success "Restore completed"
        Add-Check -Name "Desktop Restore" -Status "Pass"

        # Build
        Print-Info "Step 2: Building application..."
        $buildOutput = dotnet build $desktopProjectPath -c Release -r win-x64 -v $verbosity 2>&1 | Tee-Object -FilePath $logFile

        if ($LASTEXITCODE -eq 0) {
            Print-Success "Desktop build completed successfully"
            Add-Check -Name "Desktop Build" -Status "Pass"

            # Get build output info
            $outputDir = Join-Path (Split-Path -Parent $desktopProjectPath) "bin\Release\net9.0-windows10.0.19041.0\win-x64"
            if (Test-Path $outputDir) {
                $exePath = Join-Path $outputDir "MarketDataCollector.Desktop.exe"
                if (Test-Path $exePath) {
                    $exeInfo = Get-Item $exePath
                    Print-Info "Executable built: $($exeInfo.Name)"
                    Print-Detail "Size: $([math]::Round($exeInfo.Length / 1MB, 2)) MB"
                    Print-Detail "Location: $outputDir"
                }
            }
        }
        else {
            Print-Error "Desktop build failed! Exit code: $LASTEXITCODE"
            Add-Check -Name "Desktop Build" -Status "Fail" -Details "Exit code: $LASTEXITCODE"

            # Analyze specific desktop build errors
            Write-Host ""
            Print-Info "Analyzing build errors..."

            # Common WinUI/XAML errors
            $xamlErrors = $buildOutput | Select-String -Pattern "XamlCompiler|WinUI|XAML" -CaseSensitive:$false
            if ($xamlErrors) {
                Print-Warning "XAML/WinUI related errors detected:"
                $xamlErrors | Select-Object -First 5 | ForEach-Object { Print-Detail $_.Line }
                $script:DiagnosticResults.Suggestions += "XAML errors may require Windows App SDK update or Visual Studio components"
            }

            # Windows App SDK errors
            $sdkErrors = $buildOutput | Select-String -Pattern "WindowsAppSDK|Microsoft\.Windows\." -CaseSensitive:$false
            if ($sdkErrors) {
                Print-Warning "Windows App SDK related errors detected:"
                $sdkErrors | Select-Object -First 5 | ForEach-Object { Print-Detail $_.Line }
                $script:DiagnosticResults.Suggestions += "Try updating Windows App SDK: dotnet add package Microsoft.WindowsAppSDK --version 1.6.250108002"
            }

            # Show last 20 lines
            Write-Host ""
            Print-Info "Last 20 lines of build output:"
            Get-Content $logFile -Tail 20 | ForEach-Object { Print-Detail $_ }

            return $false
        }

        Print-Success "Desktop diagnostic log saved to: $logFile"
        return $true
    }
    finally {
        Pop-Location
    }
}

function Clean-Solution {
    Print-Section "Clean Solution"

    Push-Location $ProjectRoot

    try {
        Print-Info "Cleaning solution..."
        dotnet clean 2>&1 | Out-Null

        if ($LASTEXITCODE -eq 0) {
            Print-Success "Clean completed successfully"
        }
        else {
            Print-Warning "Clean encountered issues"
        }

        # Offer to clear NuGet cache
        Write-Host ""
        $response = Read-Host "  Do you want to clear NuGet cache? [y/N]"
        if ($response -eq "y" -or $response -eq "Y") {
            Print-Info "Clearing NuGet cache..."
            dotnet nuget locals all --clear
            Print-Success "NuGet cache cleared"
        }
    }
    finally {
        Pop-Location
    }
}

function Show-LogSummary {
    Print-Section "Diagnostic Log Summary"

    if (Test-Path $LogDir) {
        $logs = Get-ChildItem -Path $LogDir -File | Sort-Object LastWriteTime -Descending
        if ($logs.Count -gt 0) {
            Print-Info "Recent diagnostic logs:"
            $logs | Select-Object -First 10 | ForEach-Object {
                $size = "{0:N1} KB" -f ($_.Length / 1KB)
                Print-Detail "$($_.Name) ($size) - $($_.LastWriteTime.ToString('yyyy-MM-dd HH:mm'))"
            }

            Write-Host ""
            Print-Info "Useful commands:"
            Print-Detail "View log: Get-Content '$LogDir\<filename>'"
            Print-Detail "Search errors: Select-String -Path '$LogDir\<filename>' -Pattern 'error'"
            Print-Detail "Search warnings: Select-String -Path '$LogDir\<filename>' -Pattern 'warning'"
        }
        else {
            Print-Info "No diagnostic logs found"
        }
    }
    else {
        Print-Info "No diagnostic logs directory"
    }
}

function Show-DiagnosticSummary {
    $duration = (Get-Date) - $script:DiagnosticResults.StartTime

    if ($useNotificationModule) {
        Show-BuildSummary -Success ($script:DiagnosticResults.Errors.Count -eq 0)
    }

    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║                    Diagnostic Summary                                ║" -ForegroundColor Cyan
    Write-Host "╚══════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""

    # Checks summary
    $passCount = ($script:DiagnosticResults.Checks | Where-Object { $_.Status -eq "Pass" }).Count
    $warnCount = ($script:DiagnosticResults.Checks | Where-Object { $_.Status -eq "Warning" }).Count
    $failCount = ($script:DiagnosticResults.Checks | Where-Object { $_.Status -eq "Fail" }).Count
    $totalChecks = $script:DiagnosticResults.Checks.Count

    Write-Host "  Checks: " -ForegroundColor White -NoNewline
    Write-Host "$passCount passed" -ForegroundColor Green -NoNewline
    if ($warnCount -gt 0) {
        Write-Host ", $warnCount warnings" -ForegroundColor Yellow -NoNewline
    }
    if ($failCount -gt 0) {
        Write-Host ", $failCount failed" -ForegroundColor Red -NoNewline
    }
    Write-Host " / $totalChecks total" -ForegroundColor Gray

    Write-Host "  Duration: " -ForegroundColor White -NoNewline
    Write-Host "$([math]::Round($duration.TotalSeconds, 1)) seconds" -ForegroundColor Cyan

    # Warnings
    if ($script:DiagnosticResults.Warnings.Count -gt 0) {
        Write-Host ""
        Write-Host "  Warnings:" -ForegroundColor Yellow
        $script:DiagnosticResults.Warnings | ForEach-Object { Write-Host "    ⚠ $_" -ForegroundColor Yellow }
    }

    # Errors
    if ($script:DiagnosticResults.Errors.Count -gt 0) {
        Write-Host ""
        Write-Host "  Errors:" -ForegroundColor Red
        $script:DiagnosticResults.Errors | ForEach-Object { Write-Host "    ✗ $_" -ForegroundColor Red }
    }

    # Suggestions
    if ($script:DiagnosticResults.Suggestions.Count -gt 0) {
        Write-Host ""
        Write-Host "  Recommendations:" -ForegroundColor Cyan
        $script:DiagnosticResults.Suggestions | ForEach-Object { Write-Host "    💡 $_" -ForegroundColor White }
    }

    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
}

# Main execution
Print-Header
Setup-LogDir

$success = $true

switch ($Action) {
    "environment" {
        Check-Environment
        Check-DotNet
        Check-NuGetSources
        Check-WindowsSDK
        Check-DesktopProject
    }
    "restore" {
        Check-NuGetSources
        $success = Diagnose-Restore
    }
    "build" {
        $success = Diagnose-Build
    }
    "desktop" {
        Check-Environment
        Check-DotNet
        Check-WindowsSDK
        Check-DesktopProject
        $success = Diagnose-DesktopBuild
    }
    "clean" {
        Clean-Solution
        $success = Diagnose-Restore
        if ($success) {
            $success = Diagnose-Build
        }
    }
    default {
        Check-Environment
        Check-DotNet
        Check-NuGetSources
        Check-WindowsSDK
        Check-DesktopProject
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
Show-DiagnosticSummary

if (-not $success) {
    exit 1
}
