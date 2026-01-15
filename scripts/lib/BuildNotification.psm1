# =============================================================================
# Market Data Collector - Build Notification Module
# =============================================================================
#
# Provides build progress tracking, Windows toast notifications, and detailed
# logging for the Windows desktop application build process.
#
# Usage:
#   Import-Module .\scripts\lib\BuildNotification.psm1
#   Initialize-BuildNotification -EnableToast $true
#   Start-BuildStep "Building Desktop App"
#   Complete-BuildStep -Success $true
#
# =============================================================================

# Module-level state
$script:BuildState = @{
    StartTime = $null
    CurrentStep = $null
    Steps = @()
    LogPath = $null
    EnableToast = $true
    EnableSound = $true
    Verbose = $false
}

# ANSI color codes for terminal output
$script:Colors = @{
    Reset = "`e[0m"
    Bold = "`e[1m"
    Red = "`e[91m"
    Green = "`e[92m"
    Yellow = "`e[93m"
    Blue = "`e[94m"
    Magenta = "`e[95m"
    Cyan = "`e[96m"
    White = "`e[97m"
    Gray = "`e[90m"
}

# Progress bar characters
$script:ProgressChars = @{
    Full = '█'
    Empty = '░'
    Spinner = @('⠋', '⠙', '⠹', '⠸', '⠼', '⠴', '⠦', '⠧', '⠇', '⠏')
}

<#
.SYNOPSIS
    Initializes the build notification system.
.PARAMETER EnableToast
    Enable Windows toast notifications (default: true on Windows).
.PARAMETER EnableSound
    Enable notification sounds (default: true).
.PARAMETER Verbose
    Enable verbose logging output.
.PARAMETER LogPath
    Path to save build log file.
#>
function Initialize-BuildNotification {
    [CmdletBinding()]
    param(
        [bool]$EnableToast = $true,
        [bool]$EnableSound = $true,
        [bool]$Verbose = $false,
        [string]$LogPath = ""
    )

    $script:BuildState.StartTime = Get-Date
    $script:BuildState.Steps = @()
    $script:BuildState.EnableToast = $EnableToast -and ($env:OS -eq "Windows_NT")
    $script:BuildState.EnableSound = $EnableSound
    $script:BuildState.Verbose = $Verbose

    if ($LogPath) {
        $script:BuildState.LogPath = $LogPath
        $logDir = Split-Path -Parent $LogPath
        if (-not (Test-Path $logDir)) {
            New-Item -ItemType Directory -Path $logDir -Force | Out-Null
        }
    }
    else {
        $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
        $script:BuildState.LogPath = Join-Path $env:TEMP "mdc-build-$timestamp.log"
    }

    Write-BuildLog "Build notification initialized at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-BuildLog "Toast notifications: $($script:BuildState.EnableToast)"
    Write-BuildLog "Sound: $($script:BuildState.EnableSound)"
}

<#
.SYNOPSIS
    Writes a message to the build log file.
#>
function Write-BuildLog {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Message,
        [ValidateSet("INFO", "SUCCESS", "WARNING", "ERROR", "DEBUG")]
        [string]$Level = "INFO"
    )

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"
    $logEntry = "[$timestamp] [$Level] $Message"

    if ($script:BuildState.LogPath) {
        Add-Content -Path $script:BuildState.LogPath -Value $logEntry -ErrorAction SilentlyContinue
    }

    if ($script:BuildState.Verbose -or $Level -in @("ERROR", "WARNING")) {
        $color = switch ($Level) {
            "SUCCESS" { "Green" }
            "WARNING" { "Yellow" }
            "ERROR" { "Red" }
            "DEBUG" { "Gray" }
            default { "White" }
        }
        Write-Host $logEntry -ForegroundColor $color
    }
}

<#
.SYNOPSIS
    Displays a styled header banner.
#>
function Show-BuildHeader {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Title,
        [string]$Subtitle = ""
    )

    $width = 72
    $border = "═" * $width

    Write-Host ""
    Write-Host "╔$border╗" -ForegroundColor Cyan
    Write-Host "║$($Title.PadLeft(($width + $Title.Length) / 2).PadRight($width))║" -ForegroundColor Cyan
    if ($Subtitle) {
        Write-Host "║$($Subtitle.PadLeft(($width + $Subtitle.Length) / 2).PadRight($width))║" -ForegroundColor DarkCyan
    }
    Write-Host "╚$border╝" -ForegroundColor Cyan
    Write-Host ""

    Write-BuildLog "=== $Title ===" -Level "INFO"
    if ($Subtitle) {
        Write-BuildLog $Subtitle -Level "INFO"
    }
}

<#
.SYNOPSIS
    Displays a styled section header.
#>
function Show-BuildSection {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Title
    )

    Write-Host ""
    Write-Host "┌─ $Title " -ForegroundColor Yellow -NoNewline
    Write-Host ("─" * (68 - $Title.Length)) -ForegroundColor DarkYellow

    Write-BuildLog "--- $Title ---" -Level "INFO"
}

<#
.SYNOPSIS
    Starts a new build step with progress tracking.
#>
function Start-BuildStep {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Name,
        [string]$Description = ""
    )

    $step = @{
        Name = $Name
        Description = $Description
        StartTime = Get-Date
        EndTime = $null
        Status = "Running"
        Messages = @()
    }

    $script:BuildState.CurrentStep = $step
    $script:BuildState.Steps += $step

    $stepNumber = $script:BuildState.Steps.Count

    Write-Host ""
    Write-Host "  [$stepNumber] " -ForegroundColor DarkGray -NoNewline
    Write-Host "⏳ " -ForegroundColor Yellow -NoNewline
    Write-Host $Name -ForegroundColor White -NoNewline
    if ($Description) {
        Write-Host " - $Description" -ForegroundColor DarkGray -NoNewline
    }
    Write-Host ""

    Write-BuildLog "STEP START: $Name" -Level "INFO"
    if ($Description) {
        Write-BuildLog "  Description: $Description" -Level "DEBUG"
    }
}

<#
.SYNOPSIS
    Updates the current build step with a progress message.
#>
function Update-BuildProgress {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Message,
        [int]$PercentComplete = -1
    )

    if ($script:BuildState.CurrentStep) {
        $script:BuildState.CurrentStep.Messages += $Message
    }

    $prefix = "      "

    if ($PercentComplete -ge 0) {
        $progressBar = Get-ProgressBar -Percent $PercentComplete -Width 20
        Write-Host "$prefix$progressBar " -NoNewline
    }
    else {
        Write-Host "$prefix→ " -ForegroundColor DarkGray -NoNewline
    }

    Write-Host $Message -ForegroundColor Gray
    Write-BuildLog "  $Message" -Level "DEBUG"
}

<#
.SYNOPSIS
    Completes the current build step.
#>
function Complete-BuildStep {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [bool]$Success,
        [string]$Message = ""
    )

    if (-not $script:BuildState.CurrentStep) {
        return
    }

    $step = $script:BuildState.CurrentStep
    $step.EndTime = Get-Date
    $step.Status = if ($Success) { "Success" } else { "Failed" }

    $duration = $step.EndTime - $step.StartTime
    $durationStr = Format-Duration -Duration $duration

    $stepNumber = $script:BuildState.Steps.Count
    $statusIcon = if ($Success) { "✓" } else { "✗" }
    $statusColor = if ($Success) { "Green" } else { "Red" }

    Write-Host "      $statusIcon " -ForegroundColor $statusColor -NoNewline

    if ($Message) {
        Write-Host $Message -ForegroundColor $statusColor -NoNewline
        Write-Host " " -NoNewline
    }

    Write-Host "($durationStr)" -ForegroundColor DarkGray

    $logLevel = if ($Success) { "SUCCESS" } else { "ERROR" }
    Write-BuildLog "STEP COMPLETE: $($step.Name) - $($step.Status) ($durationStr)" -Level $logLevel

    $script:BuildState.CurrentStep = $null
}

<#
.SYNOPSIS
    Shows a build step failure with detailed error information.
#>
function Show-BuildError {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Error,
        [string]$Suggestion = "",
        [string]$LogFile = "",
        [string[]]$Details = @()
    )

    Write-Host ""
    Write-Host "  ╔══════════════════════════════════════════════════════════════════╗" -ForegroundColor Red
    Write-Host "  ║ BUILD ERROR                                                       ║" -ForegroundColor Red
    Write-Host "  ╚══════════════════════════════════════════════════════════════════╝" -ForegroundColor Red
    Write-Host ""
    Write-Host "  ✗ $Error" -ForegroundColor Red

    if ($Details.Count -gt 0) {
        Write-Host ""
        Write-Host "  Details:" -ForegroundColor Yellow
        foreach ($detail in $Details) {
            Write-Host "    • $detail" -ForegroundColor Gray
        }
    }

    if ($Suggestion) {
        Write-Host ""
        Write-Host "  💡 Suggestion:" -ForegroundColor Cyan
        Write-Host "     $Suggestion" -ForegroundColor White
    }

    if ($LogFile -and (Test-Path $LogFile)) {
        Write-Host ""
        Write-Host "  📋 Log file:" -ForegroundColor DarkGray
        Write-Host "     $LogFile" -ForegroundColor Gray
    }

    Write-Host ""

    Write-BuildLog "BUILD ERROR: $Error" -Level "ERROR"
    foreach ($detail in $Details) {
        Write-BuildLog "  Detail: $detail" -Level "ERROR"
    }
    if ($Suggestion) {
        Write-BuildLog "  Suggestion: $Suggestion" -Level "INFO"
    }

    # Send toast notification for errors
    Send-BuildNotification -Title "Build Failed" -Message $Error -Type "Error"
}

<#
.SYNOPSIS
    Shows a warning message.
#>
function Show-BuildWarning {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Message,
        [string]$Suggestion = ""
    )

    Write-Host "  ⚠ " -ForegroundColor Yellow -NoNewline
    Write-Host $Message -ForegroundColor Yellow

    if ($Suggestion) {
        Write-Host "    💡 $Suggestion" -ForegroundColor DarkYellow
    }

    Write-BuildLog "WARNING: $Message" -Level "WARNING"
    if ($Suggestion) {
        Write-BuildLog "  Suggestion: $Suggestion" -Level "INFO"
    }
}

<#
.SYNOPSIS
    Shows the final build summary with all steps.
#>
function Show-BuildSummary {
    [CmdletBinding()]
    param(
        [bool]$Success = $true,
        [string]$OutputPath = ""
    )

    $totalDuration = (Get-Date) - $script:BuildState.StartTime
    $totalDurationStr = Format-Duration -Duration $totalDuration

    $successCount = ($script:BuildState.Steps | Where-Object { $_.Status -eq "Success" }).Count
    $failedCount = ($script:BuildState.Steps | Where-Object { $_.Status -eq "Failed" }).Count
    $totalSteps = $script:BuildState.Steps.Count

    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan

    if ($Success) {
        Write-Host "                        BUILD SUCCESSFUL                               " -ForegroundColor Green
    }
    else {
        Write-Host "                          BUILD FAILED                                 " -ForegroundColor Red
    }

    Write-Host "═══════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""

    # Step summary
    Write-Host "  Steps: " -ForegroundColor White -NoNewline
    Write-Host "$successCount passed" -ForegroundColor Green -NoNewline
    if ($failedCount -gt 0) {
        Write-Host ", $failedCount failed" -ForegroundColor Red -NoNewline
    }
    Write-Host " / $totalSteps total" -ForegroundColor Gray

    # Duration
    Write-Host "  Duration: " -ForegroundColor White -NoNewline
    Write-Host $totalDurationStr -ForegroundColor Cyan

    # Output path
    if ($OutputPath -and (Test-Path $OutputPath)) {
        Write-Host "  Output: " -ForegroundColor White -NoNewline
        Write-Host $OutputPath -ForegroundColor Gray

        $size = Get-FolderSize -Path $OutputPath
        Write-Host "  Size: " -ForegroundColor White -NoNewline
        Write-Host $size -ForegroundColor Gray
    }

    # Log file
    if ($script:BuildState.LogPath -and (Test-Path $script:BuildState.LogPath)) {
        Write-Host "  Log: " -ForegroundColor White -NoNewline
        Write-Host $script:BuildState.LogPath -ForegroundColor DarkGray
    }

    Write-Host ""

    # Detailed step breakdown
    if ($script:BuildState.Steps.Count -gt 0) {
        Write-Host "  Step Breakdown:" -ForegroundColor DarkGray
        foreach ($step in $script:BuildState.Steps) {
            $duration = if ($step.EndTime) { $step.EndTime - $step.StartTime } else { (Get-Date) - $step.StartTime }
            $durationStr = Format-Duration -Duration $duration
            $icon = switch ($step.Status) {
                "Success" { "✓" }
                "Failed" { "✗" }
                default { "○" }
            }
            $color = switch ($step.Status) {
                "Success" { "Green" }
                "Failed" { "Red" }
                default { "Yellow" }
            }

            Write-Host "    $icon " -ForegroundColor $color -NoNewline
            Write-Host $step.Name -ForegroundColor White -NoNewline
            Write-Host " ($durationStr)" -ForegroundColor DarkGray
        }
        Write-Host ""
    }

    Write-Host "═══════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""

    Write-BuildLog "BUILD SUMMARY" -Level "INFO"
    Write-BuildLog "  Total duration: $totalDurationStr" -Level "INFO"
    Write-BuildLog "  Steps: $successCount passed, $failedCount failed / $totalSteps total" -Level "INFO"
    Write-BuildLog "  Result: $(if ($Success) { 'SUCCESS' } else { 'FAILED' })" -Level $(if ($Success) { "SUCCESS" } else { "ERROR" })

    # Send toast notification
    if ($Success) {
        Send-BuildNotification -Title "Build Complete" -Message "Build finished successfully in $totalDurationStr" -Type "Success"
    }
}

<#
.SYNOPSIS
    Sends a Windows toast notification.
#>
function Send-BuildNotification {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Title,
        [Parameter(Mandatory)]
        [string]$Message,
        [ValidateSet("Info", "Success", "Warning", "Error")]
        [string]$Type = "Info"
    )

    if (-not $script:BuildState.EnableToast) {
        return
    }

    try {
        # Use PowerShell BurntToast module if available
        if (Get-Module -ListAvailable -Name BurntToast) {
            Import-Module BurntToast -ErrorAction SilentlyContinue
            $sound = switch ($Type) {
                "Success" { "Default" }
                "Warning" { "Alarm2" }
                "Error" { "Alarm" }
                default { "Default" }
            }
            New-BurntToastNotification -Text $Title, $Message -Sound $sound -ErrorAction SilentlyContinue
        }
        # Fallback to Windows native notification
        elseif ($env:OS -eq "Windows_NT") {
            Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue
            $balloonIcon = switch ($Type) {
                "Success" { [System.Windows.Forms.ToolTipIcon]::Info }
                "Warning" { [System.Windows.Forms.ToolTipIcon]::Warning }
                "Error" { [System.Windows.Forms.ToolTipIcon]::Error }
                default { [System.Windows.Forms.ToolTipIcon]::Info }
            }

            $notify = New-Object System.Windows.Forms.NotifyIcon
            $notify.Icon = [System.Drawing.SystemIcons]::Application
            $notify.BalloonTipIcon = $balloonIcon
            $notify.BalloonTipTitle = $Title
            $notify.BalloonTipText = $Message
            $notify.Visible = $true
            $notify.ShowBalloonTip(5000)

            # Cleanup after showing
            Start-Sleep -Milliseconds 100
            $notify.Dispose()
        }
    }
    catch {
        # Silently ignore notification errors
        Write-BuildLog "Failed to send notification: $_" -Level "DEBUG"
    }
}

<#
.SYNOPSIS
    Generates a text-based progress bar.
#>
function Get-ProgressBar {
    param(
        [int]$Percent,
        [int]$Width = 20
    )

    $filled = [math]::Floor($Width * $Percent / 100)
    $empty = $Width - $filled

    $bar = "[" + ($script:ProgressChars.Full * $filled) + ($script:ProgressChars.Empty * $empty) + "]"
    $percentStr = "{0,3}%" -f $Percent

    return "$bar $percentStr"
}

<#
.SYNOPSIS
    Formats a duration as a human-readable string.
#>
function Format-Duration {
    param(
        [TimeSpan]$Duration
    )

    if ($Duration.TotalSeconds -lt 1) {
        return "{0:N0}ms" -f $Duration.TotalMilliseconds
    }
    elseif ($Duration.TotalMinutes -lt 1) {
        return "{0:N1}s" -f $Duration.TotalSeconds
    }
    elseif ($Duration.TotalHours -lt 1) {
        return "{0:N0}m {1:N0}s" -f [math]::Floor($Duration.TotalMinutes), $Duration.Seconds
    }
    else {
        return "{0:N0}h {1:N0}m" -f [math]::Floor($Duration.TotalHours), $Duration.Minutes
    }
}

<#
.SYNOPSIS
    Gets the size of a folder in a human-readable format.
#>
function Get-FolderSize {
    param(
        [string]$Path
    )

    try {
        $size = (Get-ChildItem -Path $Path -Recurse -File | Measure-Object -Property Length -Sum).Sum

        if ($size -lt 1KB) {
            return "{0:N0} B" -f $size
        }
        elseif ($size -lt 1MB) {
            return "{0:N1} KB" -f ($size / 1KB)
        }
        elseif ($size -lt 1GB) {
            return "{0:N1} MB" -f ($size / 1MB)
        }
        else {
            return "{0:N2} GB" -f ($size / 1GB)
        }
    }
    catch {
        return "Unknown"
    }
}

<#
.SYNOPSIS
    Checks if a prerequisite is installed and logs the result.
#>
function Test-BuildPrerequisite {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Name,
        [Parameter(Mandatory)]
        [scriptblock]$TestScript,
        [string]$InstallInstructions = "",
        [bool]$Required = $true
    )

    Write-Host "  Checking $Name... " -ForegroundColor Gray -NoNewline

    try {
        $result = & $TestScript
        if ($result) {
            Write-Host "✓ " -ForegroundColor Green -NoNewline
            Write-Host $result -ForegroundColor DarkGray
            Write-BuildLog "Prerequisite check: $Name - OK ($result)" -Level "SUCCESS"
            return $true
        }
        else {
            throw "Not found"
        }
    }
    catch {
        $icon = if ($Required) { "✗" } else { "○" }
        $color = if ($Required) { "Red" } else { "Yellow" }
        $status = if ($Required) { "MISSING (required)" } else { "MISSING (optional)" }

        Write-Host "$icon " -ForegroundColor $color -NoNewline
        Write-Host $status -ForegroundColor $color

        if ($InstallInstructions) {
            Write-Host "    Install: " -ForegroundColor DarkGray -NoNewline
            Write-Host $InstallInstructions -ForegroundColor Gray
        }

        Write-BuildLog "Prerequisite check: $Name - $status" -Level $(if ($Required) { "ERROR" } else { "WARNING" })
        return $false
    }
}

<#
.SYNOPSIS
    Gets the path to the build log file.
#>
function Get-BuildLogPath {
    return $script:BuildState.LogPath
}

<#
.SYNOPSIS
    Gets the build state for external inspection.
#>
function Get-BuildState {
    return $script:BuildState.Clone()
}

# Export module functions
Export-ModuleMember -Function @(
    'Initialize-BuildNotification',
    'Write-BuildLog',
    'Show-BuildHeader',
    'Show-BuildSection',
    'Start-BuildStep',
    'Update-BuildProgress',
    'Complete-BuildStep',
    'Show-BuildError',
    'Show-BuildWarning',
    'Show-BuildSummary',
    'Send-BuildNotification',
    'Get-ProgressBar',
    'Format-Duration',
    'Get-FolderSize',
    'Test-BuildPrerequisite',
    'Get-BuildLogPath',
    'Get-BuildState'
)
