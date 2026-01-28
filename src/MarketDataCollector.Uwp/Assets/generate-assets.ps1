<#
.SYNOPSIS
    Generate PNG assets from SVG source files for Market Data Collector UWP app.

.DESCRIPTION
    This script converts SVG source files to PNG at various scales required
    by Windows UWP applications. Requires Inkscape or ImageMagick.

.PARAMETER Tool
    The conversion tool to use: 'inkscape' or 'magick' (ImageMagick)

.EXAMPLE
    .\generate-assets.ps1 -Tool inkscape
#>

param(
    [ValidateSet('inkscape', 'magick')]
    [string]$Tool = 'inkscape'
)

$ErrorActionPreference = 'Stop'
$SourceDir = Join-Path $PSScriptRoot 'Source'

# Asset definitions: [BaseName, BaseWidth, BaseHeight, Scales[]]
$Assets = @(
    @{ Name = 'Square44x44Logo'; Width = 44; Height = 44; Scales = @(100, 125, 150, 200, 400) },
    @{ Name = 'Square150x150Logo'; Width = 150; Height = 150; Scales = @(100, 125, 150, 200, 400) },
    @{ Name = 'LargeTile'; Width = 310; Height = 310; Scales = @(100, 125, 150, 200, 400) },
    @{ Name = 'Wide310x150Logo'; Width = 310; Height = 150; Scales = @(100, 125, 150, 200, 400) },
    @{ Name = 'SmallTile'; Width = 71; Height = 71; Scales = @(100, 125, 150, 200, 400) },
    @{ Name = 'StoreLogo'; Width = 50; Height = 50; Scales = @(100, 125, 150, 200, 400) },
    @{ Name = 'SplashScreen'; Width = 620; Height = 300; Scales = @(100, 125, 150, 200, 400) },
    @{ Name = 'BadgeLogo'; Width = 24; Height = 24; Scales = @(100) },
    @{ Name = 'AppIcon'; Width = 256; Height = 256; Scales = @(100) }
)

# Target sizes for taskbar icons
$TargetSizes = @(16, 24, 32, 48, 256)

function Convert-SvgToPng {
    param(
        [string]$SvgPath,
        [string]$PngPath,
        [int]$Width,
        [int]$Height
    )

    if ($Tool -eq 'inkscape') {
        & inkscape $SvgPath --export-width=$Width --export-height=$Height --export-filename=$PngPath
    }
    else {
        & magick convert -background none -density 300 $SvgPath -resize "${Width}x${Height}" $PngPath
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Failed to convert $SvgPath"
    }
    else {
        Write-Host "Created: $PngPath" -ForegroundColor Green
    }
}

Write-Host "Generating PNG assets from SVG sources..." -ForegroundColor Cyan
Write-Host "Using tool: $Tool" -ForegroundColor Yellow
Write-Host ""

# Generate scaled assets
foreach ($asset in $Assets) {
    $svgPath = Join-Path $SourceDir "$($asset.Name).svg"

    if (-not (Test-Path $svgPath)) {
        Write-Warning "Source not found: $svgPath"
        continue
    }

    foreach ($scale in $asset.Scales) {
        $width = [math]::Round($asset.Width * $scale / 100)
        $height = [math]::Round($asset.Height * $scale / 100)

        if ($scale -eq 100 -and $asset.Scales.Count -eq 1) {
            # Single scale asset (like BadgeLogo, AppIcon)
            $pngPath = Join-Path $PSScriptRoot "$($asset.Name).png"
        }
        else {
            $pngPath = Join-Path $PSScriptRoot "$($asset.Name).scale-$scale.png"
        }

        Convert-SvgToPng -SvgPath $svgPath -PngPath $pngPath -Width $width -Height $height
    }

    # Also generate base .png without scale suffix for some assets
    if ($asset.Name -in @('Square44x44Logo', 'Square150x150Logo', 'StoreLogo')) {
        $pngPath = Join-Path $PSScriptRoot "$($asset.Name).png"
        Convert-SvgToPng -SvgPath $svgPath -PngPath $pngPath -Width $asset.Width -Height $asset.Height
    }
}

# Generate target size icons
$targetSvg = Join-Path $SourceDir 'Square44x44Logo.svg'
$unplatedSvg = Join-Path $SourceDir 'Square44x44Logo_altform-unplated.svg'

if (Test-Path $targetSvg) {
    Write-Host ""
    Write-Host "Generating target size icons..." -ForegroundColor Cyan

    foreach ($size in $TargetSizes) {
        # Plated version
        $pngPath = Join-Path $PSScriptRoot "Square44x44Logo.targetsize-$size.png"
        Convert-SvgToPng -SvgPath $targetSvg -PngPath $pngPath -Width $size -Height $size

        # Unplated version (if source exists)
        if (Test-Path $unplatedSvg) {
            $pngPath = Join-Path $PSScriptRoot "Square44x44Logo.targetsize-${size}_altform-unplated.png"
            Convert-SvgToPng -SvgPath $unplatedSvg -PngPath $pngPath -Width $size -Height $size
        }
    }
}

# Generate contrast assets
$contrastAssets = Get-ChildItem -Path $SourceDir -Filter '*_contrast-*.svg' -ErrorAction SilentlyContinue

if ($contrastAssets) {
    Write-Host ""
    Write-Host "Generating high contrast assets..." -ForegroundColor Cyan

    foreach ($svg in $contrastAssets) {
        $baseName = $svg.BaseName
        $pngPath = Join-Path $PSScriptRoot "$baseName.png"

        # Determine size from base name
        if ($baseName -match 'Square44x44') {
            Convert-SvgToPng -SvgPath $svg.FullName -PngPath $pngPath -Width 44 -Height 44
        }
        elseif ($baseName -match 'AppIcon') {
            Convert-SvgToPng -SvgPath $svg.FullName -PngPath $pngPath -Width 256 -Height 256
        }
    }
}

Write-Host ""
Write-Host "Asset generation complete!" -ForegroundColor Green
