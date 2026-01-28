# Market Data Collector - UWP Assets

This directory contains all visual assets for the Market Data Collector Windows desktop application.

## Directory Structure

```
Assets/
├── Source/              # SVG source files for generating raster assets
│   ├── AppIcon.svg      # Main application icon (256x256)
│   ├── Square44x44Logo.svg
│   ├── Square44x44Logo_altform-unplated.svg
│   ├── Square150x150Logo.svg
│   ├── StoreLogo.svg
│   ├── SmallTile.svg
│   ├── LargeTile.svg
│   ├── Wide310x150Logo.svg
│   ├── SplashScreen.svg
│   ├── BadgeLogo.svg
│   └── *_contrast-*.svg  # High contrast theme variants
├── Icons/               # UI icons for in-app use
│   ├── Streaming*.svg   # Real-time data indicators
│   ├── Connection*.svg  # Connection status icons
│   ├── Data*.svg        # Data type icons (Trade, Quote, OrderBook, Bar)
│   ├── Provider*.svg    # Data provider icons (Alpaca, Polygon, IB)
│   ├── Status*.svg      # System status indicators
│   └── Action*.svg      # Action buttons (Backfill, Export, Archive)
├── *.png                # Generated raster assets (various scales)
└── README.md            # This file
```

## Asset Types

### Application Icons

| Asset | Base Size | Scales | Purpose |
|-------|-----------|--------|---------|
| `Square44x44Logo` | 44x44 | 100, 125, 150, 200, 400 | Taskbar, Start menu |
| `Square150x150Logo` | 150x150 | 100, 125, 150, 200, 400 | Medium tile |
| `LargeTile` | 310x310 | 100, 125, 150, 200, 400 | Large tile |
| `Wide310x150Logo` | 310x150 | 100, 125, 150, 200, 400 | Wide tile |
| `SmallTile` | 71x71 | 100, 125, 150, 200, 400 | Small tile |
| `StoreLogo` | 50x50 | 100, 125, 150, 200, 400 | Microsoft Store |
| `SplashScreen` | 620x300 | 100, 125, 150, 200, 400 | App startup |
| `BadgeLogo` | 24x24 | 100 | Lock screen notifications |

### Target Size Icons (Taskbar/Start)

Target size icons are used for unplated display:

| Size | File Pattern |
|------|--------------|
| 16px | `Square44x44Logo.targetsize-16.png` |
| 24px | `Square44x44Logo.targetsize-24.png` |
| 32px | `Square44x44Logo.targetsize-32.png` |
| 48px | `Square44x44Logo.targetsize-48.png` |
| 256px | `Square44x44Logo.targetsize-256.png` |

### High Contrast Assets

For Windows accessibility modes:

- `*_contrast-white.svg` - White foreground for dark backgrounds
- `*_contrast-black.svg` - Black foreground for light backgrounds

## Design Guidelines

### Color Palette

| Color | Hex | Usage |
|-------|-----|-------|
| Primary Green | `#00ff88` | Chart lines, positive indicators |
| Secondary Green | `#00e676` | Gradients, accents |
| Primary Blue | `#2196f3` | Volume data, secondary charts |
| Background Dark | `#1a3656` | Icon backgrounds (light end) |
| Background Darker | `#0a1a2e` | Icon backgrounds (dark end) |
| Warning Yellow | `#ffca28` | Warning states |
| Error Red | `#ff5252` | Error states, bearish indicators |
| White | `#ffffff` | Text, high contrast |

### Design Elements

1. **Chart Motif**: The primary visual element is an upward-trending line chart representing market data
2. **Data Points**: Circle markers at significant points
3. **Grid Lines**: Subtle grid suggesting financial charts
4. **Gradient Fills**: Area fills under chart lines for depth
5. **Glow Effects**: Subtle glow on key elements for modern feel

## Generating Raster Assets

### Using Inkscape (CLI)

```bash
# Generate all scale variants
for scale in 100 125 150 200 400; do
  size=$((150 * scale / 100))
  inkscape Source/Square150x150Logo.svg \
    --export-width=$size \
    --export-height=$size \
    --export-filename=Square150x150Logo.scale-$scale.png
done
```

### Using ImageMagick

```bash
# Convert SVG to PNG at specific size
convert -background none -density 300 Source/AppIcon.svg -resize 256x256 AppIcon.png
```

### Using rsvg-convert

```bash
# High quality SVG to PNG conversion
rsvg-convert -w 256 -h 256 Source/AppIcon.svg > AppIcon.png
```

## UI Icons Usage

The `Icons/` folder contains SVG icons designed for use within the application:

### Streaming Indicators
- `StreamingActive.svg` - Animated-ready pulse rings (green)
- `StreamingInactive.svg` - Muted pulse rings (gray)

### Connection Status
- `ConnectionConnected.svg` - Signal bars with checkmark
- `ConnectionDisconnected.svg` - Muted bars with X

### Data Types
- `DataTrade.svg` - Trade/execution data
- `DataQuote.svg` - Bid/Ask quote data
- `DataOrderBook.svg` - L2 market depth
- `DataBar.svg` - OHLC candlestick data

### Provider Logos
- `ProviderAlpaca.svg` - Alpaca Markets
- `ProviderPolygon.svg` - Polygon.io
- `ProviderIB.svg` - Interactive Brokers
- `ProviderGeneric.svg` - Generic data provider

### Status Indicators
- `StatusHealthy.svg` - System healthy (green shield)
- `StatusWarning.svg` - Warning state (yellow triangle)
- `StatusError.svg` - Error state (red circle)

### Actions
- `ActionBackfill.svg` - Historical data retrieval
- `ActionExport.svg` - Data export
- `ActionArchive.svg` - Archive management

## Using Icons in XAML

```xml
<!-- Method 1: Image from SVG (requires Win2D or similar) -->
<Image Source="ms-appx:///Assets/Icons/StreamingActive.svg" Width="24" Height="24"/>

<!-- Method 2: Convert to PathGeometry for vector rendering -->
<PathIcon Data="M12,2 L21,7 L21,17 L12,22 L3,17 L3,7 Z"/>

<!-- Method 3: Use as ImageBrush -->
<Rectangle Width="24" Height="24">
    <Rectangle.Fill>
        <ImageBrush ImageSource="ms-appx:///Assets/Icons/StatusHealthy.svg"/>
    </Rectangle.Fill>
</Rectangle>
```

## Adding New Assets

1. Create SVG source in `Source/` folder
2. Follow the established color palette and design patterns
3. Generate PNG variants at required scales
4. Update `Package.appxmanifest` if adding new tile types
5. Add entry to this README

## Requirements

- SVG files should use viewBox for proper scaling
- All gradients and filters must have unique IDs
- Text should use system-safe fonts (Segoe UI, Arial)
- Icons should be legible at smallest intended size

---

*Last Updated: 2026-01-28*
