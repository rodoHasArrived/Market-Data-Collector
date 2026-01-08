#Requires -Version 5.1
<#
.SYNOPSIS
    Market Data Collector - Debug Bundle Collector
    Gathers all diagnostic information into a shareable bundle

.DESCRIPTION
    Collects system information, .NET SDK details, Docker info, Git status,
    project structure, dependencies, and logs into a compressed archive
    for sharing with support or attaching to issue reports.

.PARAMETER OutputDir
    Output directory (default: ./debug-bundle-TIMESTAMP)

.PARAMETER NoLogs
    Skip log file collection

.PARAMETER NoConfig
    Skip configuration files (for privacy)

.PARAMETER IncludeData
    Include sample data files (warning: may be large)

.PARAMETER Verbose
    Show detailed progress

.EXAMPLE
    .\collect-debug.ps1
    Collect full debug bundle

.EXAMPLE
    .\collect-debug.ps1 -NoConfig
    Collect bundle without configuration files
#>

param(
    [string]$OutputDir = "",
    [switch]$NoLogs,
    [switch]$NoConfig,
    [switch]$IncludeData,
    [switch]$Verbose
)

$ErrorActionPreference = "Continue"

# Script paths
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $ScriptDir)

# Timestamp for bundle name
$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"

# Set default output directory
if (-not $OutputDir) {
    $OutputDir = Join-Path $ProjectRoot "debug-bundle-$Timestamp"
}

#region Output Functions

function Write-Header {
    param([string]$Text)
    Write-Host ""
    Write-Host ("=" * 60) -ForegroundColor Cyan
    Write-Host "  $Text" -ForegroundColor Cyan
    Write-Host ("=" * 60) -ForegroundColor Cyan
    Write-Host ""
}

function Write-Step {
    param([string]$Text)
    Write-Host "[>] $Text" -ForegroundColor Blue
}

function Write-Success {
    param([string]$Text)
    Write-Host "  [OK] $Text" -ForegroundColor Green
}

function Write-Skip {
    param([string]$Text)
    Write-Host "  [--] $Text (skipped)" -ForegroundColor DarkGray
}

function Write-VerboseInfo {
    param([string]$Text)
    if ($Verbose) {
        Write-Host "      $Text" -ForegroundColor DarkGray
    }
}

#endregion

#region Sanitization

function Invoke-Sanitize {
    param([string]$FilePath)

    if (Test-Path $FilePath) {
        $content = Get-Content $FilePath -Raw
        $patterns = @(
            @{ Pattern = '("*[Kk]ey[Ii]d"*\s*[=:]\s*)"[^"]*"'; Replace = '$1"***REDACTED***"' }
            @{ Pattern = '("*[Ss]ecret[Kk]ey"*\s*[=:]\s*)"[^"]*"'; Replace = '$1"***REDACTED***"' }
            @{ Pattern = '("*[Aa]pi[Kk]ey"*\s*[=:]\s*)"[^"]*"'; Replace = '$1"***REDACTED***"' }
            @{ Pattern = '("*[Pp]assword"*\s*[=:]\s*)"[^"]*"'; Replace = '$1"***REDACTED***"' }
            @{ Pattern = '("*[Cc]onnection[Ss]tring"*\s*[=:]\s*)"[^"]*"'; Replace = '$1"***REDACTED***"' }
        )

        foreach ($p in $patterns) {
            $content = $content -replace $p.Pattern, $p.Replace
        }

        Set-Content -Path $FilePath -Value $content
    }
}

#endregion

#region Collection Functions

function Get-SystemInfo {
    Write-Step "Collecting system information..."

    $infoFile = Join-Path $OutputDir "system-info.txt"

    $info = @"
=== System Information ===
Collected: $(Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ" -AsUTC)

--- Operating System ---
$([System.Environment]::OSVersion.VersionString)
Platform: $([System.Environment]::OSVersion.Platform)

--- PowerShell ---
Version: $($PSVersionTable.PSVersion)
Edition: $($PSVersionTable.PSEdition)

--- Computer ---
Name: $env:COMPUTERNAME
User: $env:USERNAME
Domain: $env:USERDOMAIN

--- Memory ---
$(Get-CimInstance Win32_OperatingSystem | Select-Object @{N='Total (GB)';E={[math]::Round($_.TotalVisibleMemorySize/1MB,2)}}, @{N='Free (GB)';E={[math]::Round($_.FreePhysicalMemory/1MB,2)}} | Format-List | Out-String)

--- Disk Space ---
$(Get-PSDrive -PSProvider FileSystem | Where-Object { $_.Used -gt 0 } | Select-Object Name, @{N='Used (GB)';E={[math]::Round($_.Used/1GB,2)}}, @{N='Free (GB)';E={[math]::Round($_.Free/1GB,2)}} | Format-Table | Out-String)

--- Environment Variables (filtered) ---
$(Get-ChildItem Env: | Where-Object { $_.Name -match '^(DOTNET|NUGET|PATH|HOME|USER|USERNAME)' } | Sort-Object Name | Format-Table Name, Value -Wrap | Out-String)

"@

    Set-Content -Path $infoFile -Value $info
    Write-Success "System info collected"
}

function Get-DotNetInfo {
    Write-Step "Collecting .NET SDK information..."

    $dotnetFile = Join-Path $OutputDir "dotnet-info.txt"

    $info = @"
=== .NET SDK Information ===

"@

    $dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnetCmd) {
        $info += @"
--- SDK Version ---
$(& dotnet --version 2>$null)

--- Installed SDKs ---
$(& dotnet --list-sdks 2>$null | Out-String)

--- Installed Runtimes ---
$(& dotnet --list-runtimes 2>$null | Out-String)

--- NuGet Sources ---
$(& dotnet nuget list source 2>$null | Out-String)

--- Workloads ---
$(& dotnet workload list 2>$null | Out-String)

"@
    }
    else {
        $info += "dotnet CLI not found`n"
    }

    Set-Content -Path $dotnetFile -Value $info
    Write-Success ".NET SDK info collected"
}

function Get-DockerInfo {
    Write-Step "Collecting Docker information..."

    $dockerFile = Join-Path $OutputDir "docker-info.txt"

    $info = @"
=== Docker Information ===

"@

    $dockerCmd = Get-Command docker -ErrorAction SilentlyContinue
    if ($dockerCmd) {
        try {
            $info += @"
--- Docker Version ---
$(& docker version 2>$null | Out-String)

--- Docker Info ---
$(& docker info 2>$null | Out-String)

--- Docker Images ---
$(& docker images --format "table {{.Repository}}`t{{.Tag}}`t{{.Size}}" 2>$null | Select-Object -First 20 | Out-String)

--- Docker Containers ---
$(& docker ps -a --format "table {{.Names}}`t{{.Status}}`t{{.Image}}" 2>$null | Select-Object -First 20 | Out-String)

"@
        }
        catch {
            $info += "Error getting Docker info: $_`n"
        }
    }
    else {
        $info += "Docker not installed`n"
    }

    Set-Content -Path $dockerFile -Value $info
    Write-Success "Docker info collected"
}

function Get-GitInfo {
    Write-Step "Collecting Git information..."

    $gitFile = Join-Path $OutputDir "git-info.txt"

    $info = @"
=== Git Information ===

"@

    $gitCmd = Get-Command git -ErrorAction SilentlyContinue
    if ($gitCmd) {
        Push-Location $ProjectRoot
        try {
            $gitDir = & git rev-parse --git-dir 2>$null
            if ($LASTEXITCODE -eq 0) {
                $info += @"
--- Current Branch ---
$(& git rev-parse --abbrev-ref HEAD 2>$null)

--- Current Commit ---
$(& git log -1 --format="%H%n%s%n%an <%ae>%n%ai" 2>$null)

--- Recent Commits ---
$(& git log --oneline -20 2>$null | Out-String)

--- Status ---
$(& git status --short 2>$null | Out-String)

--- Remotes ---
$(& git remote -v 2>$null | Out-String)

--- Branches ---
$(& git branch -a 2>$null | Out-String)

"@
            }
            else {
                $info += "Not a Git repository`n"
            }
        }
        finally {
            Pop-Location
        }
    }
    else {
        $info += "Git not installed`n"
    }

    Set-Content -Path $gitFile -Value $info
    Write-Success "Git info collected"
}

function Get-ProjectInfo {
    Write-Step "Collecting project information..."

    $projectFile = Join-Path $OutputDir "project-info.txt"

    $slnPath = Join-Path $ProjectRoot "MarketDataCollector.sln"
    $buildPropsPath = Join-Path $ProjectRoot "Directory.Build.props"

    $info = @"
=== Project Information ===

--- Solution File ---
$(if (Test-Path $slnPath) { Get-Content $slnPath -TotalCount 50 | Out-String } else { "Solution file not found" })

--- Directory.Build.props ---
$(if (Test-Path $buildPropsPath) { Get-Content $buildPropsPath | Out-String } else { "File not found" })

--- Project Files ---
$(Get-ChildItem -Path (Join-Path $ProjectRoot "src") -Recurse -Include "*.csproj","*.fsproj" -ErrorAction SilentlyContinue | Select-Object -First 20 | ForEach-Object { $_.FullName.Replace($ProjectRoot, ".") } | Out-String)

--- Directory Structure ---
$(Get-ChildItem -Path $ProjectRoot -Directory -Depth 1 | Where-Object { $_.Name -notmatch '^(node_modules|\.git|bin|obj)$' } | Select-Object -First 50 | ForEach-Object { $_.FullName.Replace($ProjectRoot, ".") } | Out-String)

"@

    Set-Content -Path $projectFile -Value $info
    Write-Success "Project info collected"
}

function Get-DependencyInfo {
    Write-Step "Collecting dependency information..."

    $depsFile = Join-Path $OutputDir "dependencies.txt"

    $csprojPath = Join-Path $ProjectRoot "src\MarketDataCollector\MarketDataCollector.csproj"

    $info = @"
=== Dependency Information ===

--- Package References (Main Project) ---
$(if (Test-Path $csprojPath) { Select-String -Path $csprojPath -Pattern "PackageReference" | ForEach-Object { $_.Line.Trim() } | Out-String } else { "Main project file not found" })

--- Dependency Graph (if available) ---
"@

    $objPath = Join-Path $ProjectRoot "src\MarketDataCollector\obj"
    $assetsPath = Join-Path $objPath "project.assets.json"

    if (Test-Path $assetsPath) {
        $size = (Get-Item $assetsPath).Length / 1KB
        $info += "project.assets.json exists (size: $([math]::Round($size, 1)) KB)`n"
    }
    else {
        $info += "project.assets.json not found (run dotnet restore)`n"
    }

    Set-Content -Path $depsFile -Value $info
    Write-Success "Dependency info collected"
}

function Get-ConfigFiles {
    if ($NoConfig) {
        Write-Skip "Configuration files"
        return
    }

    Write-Step "Collecting configuration files..."

    $configDir = Join-Path $OutputDir "config"
    New-Item -ItemType Directory -Path $configDir -Force | Out-Null

    $appSettings = Join-Path $ProjectRoot "config\appsettings.json"
    $sampleSettings = Join-Path $ProjectRoot "config\appsettings.sample.json"
    $dockerCompose = Join-Path $ProjectRoot "deploy\docker\docker-compose.yml"

    if (Test-Path $appSettings) {
        Copy-Item $appSettings -Destination $configDir
        Invoke-Sanitize (Join-Path $configDir "appsettings.json")
        Write-VerboseInfo "Copied and sanitized appsettings.json"
    }

    if (Test-Path $sampleSettings) {
        Copy-Item $sampleSettings -Destination $configDir
        Write-VerboseInfo "Copied appsettings.sample.json"
    }

    if (Test-Path $dockerCompose) {
        Copy-Item $dockerCompose -Destination $configDir
        Write-VerboseInfo "Copied docker-compose.yml"
    }

    Write-Success "Configuration files collected (sanitized)"
}

function Get-Logs {
    if ($NoLogs) {
        Write-Skip "Log files"
        return
    }

    Write-Step "Collecting log files..."

    $logsDir = Join-Path $OutputDir "logs"
    New-Item -ItemType Directory -Path $logsDir -Force | Out-Null

    $logCount = 0
    $cutoffDate = (Get-Date).AddDays(-7)

    # Collect diagnostic logs
    $diagLogsPath = Join-Path $ProjectRoot "diagnostic-logs"
    if (Test-Path $diagLogsPath) {
        Get-ChildItem -Path $diagLogsPath -Filter "*.log" |
        Where-Object { $_.LastWriteTime -gt $cutoffDate } |
        ForEach-Object {
            Copy-Item $_.FullName -Destination $logsDir
            Invoke-Sanitize (Join-Path $logsDir $_.Name)
            $logCount++
        }
    }

    # Collect application logs
    $appLogsPath = Join-Path $ProjectRoot "logs"
    if (Test-Path $appLogsPath) {
        Get-ChildItem -Path $appLogsPath -Filter "*.log" |
        Where-Object { $_.LastWriteTime -gt $cutoffDate } |
        ForEach-Object {
            Copy-Item $_.FullName -Destination $logsDir
            Invoke-Sanitize (Join-Path $logsDir $_.Name)
            $logCount++
        }
    }

    Write-Success "Collected $logCount log file(s)"
}

function Get-BuildOutput {
    Write-Step "Running build diagnostics..."

    $buildFile = Join-Path $OutputDir "build-output.txt"

    $info = @"
=== Build Diagnostic Output ===
Timestamp: $(Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ" -AsUTC)

--- Restore ---
"@

    Push-Location $ProjectRoot
    try {
        $restoreOutput = & dotnet restore -v detailed 2>&1 | Select-Object -Last 100 | Out-String
        $info += $restoreOutput

        $info += "`n--- Build ---`n"
        $buildOutput = & dotnet build --no-restore -v minimal 2>&1 | Select-Object -Last 200 | Out-String
        $info += $buildOutput
    }
    catch {
        $info += "Error during build: $_`n"
    }
    finally {
        Pop-Location
    }

    Set-Content -Path $buildFile -Value $info
    Write-Success "Build diagnostics collected"
}

function Invoke-Doctor {
    Write-Step "Running environment doctor..."

    $doctorFile = Join-Path $OutputDir "doctor-output.txt"
    $doctorScript = Join-Path $ScriptDir "doctor.ps1"

    if (Test-Path $doctorScript) {
        try {
            $output = & $doctorScript -Verbose 2>&1 | Out-String
            Set-Content -Path $doctorFile -Value $output
            Write-Success "Doctor diagnostics collected"
        }
        catch {
            Set-Content -Path $doctorFile -Value "Error running doctor: $_"
            Write-VerboseInfo "Doctor script failed: $_"
        }
    }
    else {
        Set-Content -Path $doctorFile -Value "Doctor script not found"
        Write-Skip "Doctor script not found"
    }
}

function New-Bundle {
    Write-Step "Creating archive..."

    # Create manifest
    $manifestFile = Join-Path $OutputDir "MANIFEST.txt"
    $manifest = @"
=== Debug Bundle Manifest ===
Created: $(Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ" -AsUTC)
Project: Market Data Collector

Contents:
$(Get-ChildItem -Path $OutputDir -Recurse -File | ForEach-Object { $_.FullName.Replace($OutputDir, ".") } | Sort-Object | Out-String)
"@
    Set-Content -Path $manifestFile -Value $manifest

    # Create archive
    $archiveName = "debug-bundle-$Timestamp.zip"
    $archivePath = Join-Path $ProjectRoot $archiveName

    Compress-Archive -Path $OutputDir -DestinationPath $archivePath -Force

    $size = [math]::Round((Get-Item $archivePath).Length / 1KB, 1)
    Write-Success "Created $archiveName ($size KB)"

    # Clean up directory
    Remove-Item -Path $OutputDir -Recurse -Force

    Write-Host ""
    Write-Host "Debug bundle created: $archivePath" -ForegroundColor Green
    Write-Host ""
    Write-Host "To share this bundle:"
    Write-Host "  1. Review the contents for any sensitive information"
    Write-Host "  2. Upload to a secure location or attach to your issue report"
    Write-Host ""
}

#endregion

#region Main

function Main {
    Write-Header "MarketDataCollector Debug Bundle Collector"

    # Create output directory
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    Write-Host "Collecting diagnostics to: $OutputDir"
    Write-Host ""

    # Collect all information
    Get-SystemInfo
    Get-DotNetInfo
    Get-DockerInfo
    Get-GitInfo
    Get-ProjectInfo
    Get-DependencyInfo
    Get-ConfigFiles
    Get-Logs
    Invoke-Doctor
    Get-BuildOutput

    # Create archive
    New-Bundle
}

Main

#endregion
