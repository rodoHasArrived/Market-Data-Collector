#Requires -Version 5.1
<#
.SYNOPSIS
    Market Data Collector - Build Doctor
    Comprehensive system health check for build environment

.DESCRIPTION
    Validates the development environment for building the Market Data Collector project.
    Checks .NET SDK, Docker, Git, NuGet sources, disk space, and project structure.

.PARAMETER Quick
    Run quick checks only (skip slow operations like network tests)

.PARAMETER Json
    Output results as JSON for programmatic consumption

.PARAMETER Fix
    Attempt to auto-fix issues where possible

.PARAMETER Verbose
    Show detailed output

.EXAMPLE
    .\doctor.ps1
    Run full diagnostic check

.EXAMPLE
    .\doctor.ps1 -Quick
    Run quick diagnostic check

.EXAMPLE
    .\doctor.ps1 -Fix
    Run diagnostics and attempt to fix issues
#>

param(
    [switch]$Quick,
    [switch]$Json,
    [switch]$Fix,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

# Script paths
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $ScriptDir)

# Counters
$script:PassCount = 0
$script:WarnCount = 0
$script:FailCount = 0

# Results array for JSON output
$script:Results = @()

#region Output Functions

function Write-Header {
    param([string]$Text)
    if (-not $Json) {
        Write-Host ""
        Write-Host ("=" * 60) -ForegroundColor Cyan
        Write-Host "  $Text" -ForegroundColor Cyan
        Write-Host ("=" * 60) -ForegroundColor Cyan
        Write-Host ""
    }
}

function Write-Section {
    param([string]$Text)
    if (-not $Json) {
        Write-Host ""
        Write-Host $Text -ForegroundColor White -NoNewline
        Write-Host ""
        Write-Host ("-" * 50) -ForegroundColor DarkGray
    }
}

function Write-CheckPass {
    param(
        [string]$Name,
        [string]$Detail = ""
    )
    $script:PassCount++
    $script:Results += @{
        name   = $Name
        status = "pass"
        detail = $Detail
    }
    if (-not $Json) {
        Write-Host "  " -NoNewline
        Write-Host "[OK]" -ForegroundColor Green -NoNewline
        Write-Host " $Name " -NoNewline
        Write-Host $Detail -ForegroundColor DarkGray
    }
}

function Write-CheckWarn {
    param(
        [string]$Name,
        [string]$Detail = ""
    )
    $script:WarnCount++
    $script:Results += @{
        name   = $Name
        status = "warn"
        detail = $Detail
    }
    if (-not $Json) {
        Write-Host "  " -NoNewline
        Write-Host "[!!]" -ForegroundColor Yellow -NoNewline
        Write-Host " $Name " -NoNewline
        Write-Host $Detail -ForegroundColor Yellow
    }
}

function Write-CheckFail {
    param(
        [string]$Name,
        [string]$Detail = "",
        [string]$FixHint = ""
    )
    $script:FailCount++
    $script:Results += @{
        name   = $Name
        status = "fail"
        detail = $Detail
        fix    = $FixHint
    }
    if (-not $Json) {
        Write-Host "  " -NoNewline
        Write-Host "[X]" -ForegroundColor Red -NoNewline
        Write-Host " $Name " -NoNewline
        Write-Host $Detail -ForegroundColor Red
        if ($FixHint) {
            Write-Host "      Fix: $FixHint" -ForegroundColor DarkGray
        }
    }
}

function Write-Verbose-Check {
    param([string]$Text)
    if ($Verbose -and -not $Json) {
        Write-Host "      $Text" -ForegroundColor DarkGray
    }
}

#endregion

#region Version Comparison

function Compare-Version {
    param(
        [string]$Current,
        [string]$Required
    )
    try {
        $currentParts = $Current.Split('.') | ForEach-Object { [int]$_ }
        $requiredParts = $Required.Split('.') | ForEach-Object { [int]$_ }

        for ($i = 0; $i -lt [Math]::Max($currentParts.Length, $requiredParts.Length); $i++) {
            $c = if ($i -lt $currentParts.Length) { $currentParts[$i] } else { 0 }
            $r = if ($i -lt $requiredParts.Length) { $requiredParts[$i] } else { 0 }
            if ($c -gt $r) { return 1 }
            if ($c -lt $r) { return -1 }
        }
        return 0
    }
    catch {
        return -1
    }
}

#endregion

#region Check Functions

function Test-DotNet {
    Write-Section ".NET SDK"

    $dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnetCmd) {
        Write-CheckFail ".NET SDK" "Not installed" "Install from https://dot.net/download"
        return
    }

    try {
        $version = & dotnet --version 2>$null
        $required = "9.0.0"

        if ((Compare-Version $version $required) -ge 0) {
            Write-CheckPass ".NET SDK $version" "(required: $required+)"
        }
        else {
            Write-CheckFail ".NET SDK $version" "(required: $required+)" "Update .NET SDK to 9.0+"
        }

        if ($Verbose) {
            Write-Verbose-Check "Installed SDKs:"
            $sdks = & dotnet --list-sdks 2>$null
            foreach ($sdk in $sdks) {
                Write-Verbose-Check "  $sdk"
            }
        }
    }
    catch {
        Write-CheckFail ".NET SDK" "Error checking version: $_"
    }
}

function Test-Docker {
    Write-Section "Docker"

    $dockerCmd = Get-Command docker -ErrorAction SilentlyContinue
    if (-not $dockerCmd) {
        Write-CheckWarn "Docker" "Not installed (optional for native builds)"
        return
    }

    try {
        $versionOutput = & docker --version 2>$null
        $version = if ($versionOutput -match '(\d+\.\d+\.\d+)') { $Matches[1] } else { "unknown" }

        $dockerInfo = & docker info 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-CheckPass "Docker $version" "(daemon running)"
        }
        else {
            Write-CheckWarn "Docker $version" "(daemon not running)"
        }

        # Check Docker Compose
        $composeVersion = $null
        try {
            $composeOutput = & docker compose version 2>$null
            if ($LASTEXITCODE -eq 0 -and $composeOutput -match '(\d+\.\d+\.\d+)') {
                $composeVersion = $Matches[1]
            }
        }
        catch {}

        if (-not $composeVersion) {
            try {
                $composeOutput = & docker-compose --version 2>$null
                if ($composeOutput -match '(\d+\.\d+\.\d+)') {
                    $composeVersion = $Matches[1]
                }
            }
            catch {}
        }

        if ($composeVersion) {
            Write-CheckPass "Docker Compose $composeVersion" ""
        }
        else {
            Write-CheckWarn "Docker Compose" "Not installed"
        }
    }
    catch {
        Write-CheckWarn "Docker" "Error checking: $_"
    }
}

function Test-Git {
    Write-Section "Git"

    $gitCmd = Get-Command git -ErrorAction SilentlyContinue
    if (-not $gitCmd) {
        Write-CheckFail "Git" "Not installed" "Install from https://git-scm.com"
        return
    }

    try {
        $versionOutput = & git --version 2>$null
        $version = if ($versionOutput -match '(\d+\.\d+\.\d+)') { $Matches[1] } else { "unknown" }
        Write-CheckPass "Git $version" ""

        # Check if in a git repo
        Push-Location $ProjectRoot
        try {
            $gitDir = & git rev-parse --git-dir 2>$null
            if ($LASTEXITCODE -eq 0) {
                $branch = & git rev-parse --abbrev-ref HEAD 2>$null
                $status = & git status --porcelain 2>$null
                $changeCount = ($status | Measure-Object).Count

                if ($changeCount -eq 0) {
                    Write-CheckPass "Repository" "branch: $branch (clean)"
                }
                else {
                    Write-CheckWarn "Repository" "branch: $branch ($changeCount uncommitted changes)"
                }
            }
        }
        finally {
            Pop-Location
        }
    }
    catch {
        Write-CheckWarn "Git" "Error checking: $_"
    }
}

function Test-NuGet {
    Write-Section "NuGet Configuration"

    try {
        $sources = & dotnet nuget list source 2>$null
        $sourceCount = ($sources | Where-Object { $_ -match '^\s*\d+\.' } | Measure-Object).Count

        if ($sourceCount -gt 0) {
            Write-CheckPass "NuGet sources" "$sourceCount configured"
            if ($Verbose) {
                foreach ($line in $sources | Where-Object { $_ -match '^\s*\d+\.' }) {
                    Write-Verbose-Check $line.Trim()
                }
            }
        }
        else {
            Write-CheckWarn "NuGet sources" "No sources configured"
        }

        # Check nuget.org connectivity (skip in quick mode)
        if (-not $Quick) {
            try {
                $response = Invoke-WebRequest -Uri "https://api.nuget.org/v3/index.json" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
                if ($response.StatusCode -eq 200) {
                    Write-CheckPass "nuget.org" "Reachable"
                }
            }
            catch {
                Write-CheckWarn "nuget.org" "Not reachable (may affect restore)"
            }
        }
    }
    catch {
        Write-CheckWarn "NuGet" "Error checking: $_"
    }
}

function Test-DiskSpace {
    Write-Section "Disk Space"

    try {
        $drive = (Get-Item $ProjectRoot).PSDrive.Name
        $driveInfo = Get-PSDrive -Name $drive -ErrorAction SilentlyContinue

        if ($driveInfo) {
            $freeGB = [math]::Round($driveInfo.Free / 1GB, 1)

            if ($freeGB -ge 20) {
                Write-CheckPass "Disk space" "${freeGB}GB available"
            }
            elseif ($freeGB -ge 5) {
                Write-CheckWarn "Disk space" "${freeGB}GB available (recommend 20GB+)"
            }
            else {
                Write-CheckFail "Disk space" "${freeGB}GB available" "Free up disk space (need 5GB+ minimum)"
            }
        }
    }
    catch {
        Write-CheckWarn "Disk space" "Unable to check: $_"
    }
}

function Test-ProjectStructure {
    Write-Section "Project Structure"

    # Check solution file
    $slnPath = Join-Path $ProjectRoot "MarketDataCollector.sln"
    if (Test-Path $slnPath) {
        Write-CheckPass "Solution file" "MarketDataCollector.sln"
    }
    else {
        Write-CheckFail "Solution file" "Not found" "Ensure you're in the project root"
    }

    # Check main project
    $csprojPath = Join-Path $ProjectRoot "src\MarketDataCollector\MarketDataCollector.csproj"
    if (Test-Path $csprojPath) {
        Write-CheckPass "Main project" "src/MarketDataCollector/MarketDataCollector.csproj"
    }
    else {
        Write-CheckFail "Main project" "Not found" ""
    }

    # Check Directory.Build.props
    $buildPropsPath = Join-Path $ProjectRoot "Directory.Build.props"
    if (Test-Path $buildPropsPath) {
        $content = Get-Content $buildPropsPath -Raw
        if ($content -match "EnableWindowsTargeting") {
            Write-CheckPass "Directory.Build.props" "EnableWindowsTargeting configured"
        }
        else {
            Write-CheckWarn "Directory.Build.props" "EnableWindowsTargeting not set"
        }
    }
    else {
        Write-CheckWarn "Directory.Build.props" "Not found (may cause cross-platform issues)"
    }

    # Check config
    $configPath = Join-Path $ProjectRoot "config\appsettings.json"
    $samplePath = Join-Path $ProjectRoot "config\appsettings.sample.json"

    if (Test-Path $configPath) {
        Write-CheckPass "Configuration" "config/appsettings.json"
    }
    else {
        if (Test-Path $samplePath) {
            Write-CheckWarn "Configuration" "appsettings.json not found (template available)"
            if ($Fix) {
                Copy-Item $samplePath $configPath
                Write-CheckPass "Configuration" "Created from template"
            }
        }
        else {
            Write-CheckWarn "Configuration" "No config files found"
        }
    }
}

function Test-Dependencies {
    Write-Section "Dependencies"

    if ($Quick) {
        Write-Verbose-Check "Skipping dependency check in quick mode"
        return
    }

    $objPath = Join-Path $ProjectRoot "src\MarketDataCollector\obj"
    $needsRestore = -not (Test-Path $objPath)

    if ($needsRestore) {
        Write-CheckWarn "NuGet packages" "Not restored (run 'dotnet restore')"
        if ($Fix) {
            Write-Host "      Running dotnet restore..." -ForegroundColor DarkGray
            Push-Location $ProjectRoot
            try {
                & dotnet restore -v q 2>$null
                if ($LASTEXITCODE -eq 0) {
                    Write-CheckPass "NuGet packages" "Restored successfully"
                }
                else {
                    Write-CheckFail "NuGet packages" "Restore failed" "Check network and NuGet sources"
                }
            }
            finally {
                Pop-Location
            }
        }
    }
    else {
        Write-CheckPass "NuGet packages" "Restored"
    }
}

function Test-EnvironmentVariables {
    Write-Section "Environment Variables"

    $credVars = @("ALPACA__KEYID", "ALPACA__SECRETKEY", "NYSE__APIKEY", "TIINGO__APIKEY")
    $foundCreds = 0

    foreach ($var in $credVars) {
        $value = [Environment]::GetEnvironmentVariable($var)
        if ($value) {
            $foundCreds++
            Write-Verbose-Check "$var`: configured"
        }
    }

    if ($foundCreds -gt 0) {
        Write-CheckPass "API credentials" "$foundCreds provider(s) configured"
    }
    else {
        Write-CheckWarn "API credentials" "No provider credentials found in environment"
    }

    $dotnetEnv = [Environment]::GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    if ($dotnetEnv) {
        Write-CheckPass "DOTNET_ENVIRONMENT" $dotnetEnv
    }
}

function Test-Ports {
    Write-Section "Network Ports"

    if ($Quick) {
        Write-Verbose-Check "Skipping port check in quick mode"
        return
    }

    $ports = @{
        8080 = "Web Dashboard"
        5000 = "API Gateway"
        7497 = "TWS Gateway"
        9090 = "Prometheus"
        3000 = "Grafana"
    }

    foreach ($port in $ports.Keys) {
        $desc = $ports[$port]
        try {
            $connection = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
            if ($connection) {
                Write-CheckWarn "Port $port ($desc)" "In use"
            }
            else {
                Write-Verbose-Check "Port $port ($desc): Available"
            }
        }
        catch {
            Write-Verbose-Check "Port $port ($desc): Unable to check"
        }
    }
}

function Write-Summary {
    if ($Json) {
        $output = @{
            timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
            passed    = $script:PassCount
            warnings  = $script:WarnCount
            failures  = $script:FailCount
            results   = $script:Results
        }
        $output | ConvertTo-Json -Depth 10
    }
    else {
        Write-Host ""
        Write-Host ("=" * 60) -ForegroundColor Cyan
        Write-Host "  " -NoNewline
        Write-Host "$($script:PassCount) passed" -ForegroundColor Green -NoNewline
        Write-Host ", " -NoNewline
        Write-Host "$($script:WarnCount) warnings" -ForegroundColor Yellow -NoNewline
        Write-Host ", " -NoNewline
        Write-Host "$($script:FailCount) failures" -ForegroundColor Red
        Write-Host ("=" * 60) -ForegroundColor Cyan
        Write-Host ""

        if ($script:FailCount -gt 0) {
            Write-Host "Build environment has critical issues. Please fix failures before building." -ForegroundColor Red
            exit 1
        }
        elseif ($script:WarnCount -gt 0) {
            Write-Host "Build environment ready with some warnings." -ForegroundColor Yellow
        }
        else {
            Write-Host "Build environment is healthy!" -ForegroundColor Green
        }
    }
}

#endregion

#region Main

function Main {
    Write-Header "MarketDataCollector Build Doctor"

    Test-DotNet
    Test-Docker
    Test-Git
    Test-NuGet
    Test-DiskSpace
    Test-ProjectStructure
    Test-Dependencies
    Test-EnvironmentVariables
    Test-Ports

    Write-Summary
}

Main

#endregion
