# Visual Design Guide

## Design System Overview

The Market Data Collector desktop UI uses a console-inspired dark theme optimized for financial data display and long viewing sessions.

---

## Color Palette

### Background Colors

```
┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│   ████████  ConsoleBackgroundDark   #0d1117                    │
│   ████████  Primary app background, nav pane                    │
│                                                                 │
│   ████████  ConsoleBackgroundMedium #161b22                    │
│   ████████  Cards, panels, elevated surfaces                    │
│                                                                 │
│   ████████  ConsoleBackgroundLight  #21262d                    │
│   ████████  Hover states, active items                          │
│                                                                 │
│   ████████  ConsoleBorderColor      #30363d                    │
│   ████████  Borders, dividers, separators                       │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Text Colors

```
┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│   ConsoleTextPrimary    #e6edf3  ← Primary text, headings       │
│   ConsoleTextSecondary  #8b949e  ← Descriptions, labels         │
│   ConsoleTextMuted      #6e7681  ← Disabled, placeholder        │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Accent Colors

```
┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│   ████  ConsoleAccentGreen   #3fb950  Success, connected        │
│   ████  ConsoleAccentBlue    #58a6ff  Info, links, focus        │
│   ████  ConsoleAccentPurple  #a371f7  Primary accent            │
│   ████  ConsoleAccentRed     #f85149  Error, danger, alerts     │
│   ████  ConsoleAccentOrange  #d29922  Warning, caution          │
│   ████  ConsoleAccentCyan    #39c5cf  Highlight, special        │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Trading Colors

```
┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│   ████  BullishGreen   #3fb950  Price up, buy side              │
│   ████  BearishRed     #f85149  Price down, sell side           │
│   ████  NeutralGray    #8b949e  Unchanged, neutral              │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## Typography

### Font Families

| Usage | Font | Fallback |
|-------|------|----------|
| UI Text | Segoe UI Variable | Segoe UI |
| Monospace / Data | Cascadia Code | Consolas, Courier New |
| Numbers / Prices | Cascadia Mono | Consolas |

### Type Scale

```
┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│   HeaderLarge    28px  SemiBold   Page titles                   │
│   HeaderMedium   22px  SemiBold   Section headers               │
│   HeaderSmall    18px  SemiBold   Card titles                   │
│   BodyLarge      16px  Normal     Primary content               │
│   Body           14px  Normal     Standard text                 │
│   BodySmall      12px  Normal     Captions, labels              │
│   Caption        10px  Normal     Timestamps, metadata          │
│                                                                 │
│   Monospace Sizes:                                               │
│   MetricValue    32px  SemiBold   Large metric display          │
│   PriceDisplay   24px  SemiBold   Price values                  │
│   DataCell       13px  Normal     Table data                    │
│   TerminalText   12px  Normal     Log output                    │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## Spacing System

### Base Unit: 4px

```
┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│   xxs    4px   Tight spacing, inline elements                   │
│   xs     8px   Icon margins, compact padding                    │
│   sm    12px   Standard padding within cards                    │
│   md    16px   Content gaps, card padding                       │
│   lg    24px   Section spacing                                  │
│   xl    32px   Page margins, major sections                     │
│   xxl   48px   Hero spacing, page tops                          │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Card Padding

```
┌───────────────────────────────────────────────────────┐
│                     16px (md)                         │
│  ┌─────────────────────────────────────────────────┐  │
│  │                                                 │  │
│  │   Card Content                                  │  │ 16px
│  │                                                 │  │
│  └─────────────────────────────────────────────────┘  │
│                                                       │
└───────────────────────────────────────────────────────┘
```

---

## Border Radius

```
┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│   None      0px   Terminal output, code blocks                  │
│   Small     4px   Buttons, badges, tags                         │
│   Medium    6px   Cards, panels                                 │
│   Large     8px   Dialogs, modals                               │
│   Full     99px   Pills, circular indicators                    │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## Elevation (Shadows)

```
┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│   Level 0   No shadow (flat surface)                            │
│                                                                 │
│   Level 1   0 2px 4px rgba(0,0,0,0.2)                          │
│             Cards, panels                                       │
│                                                                 │
│   Level 2   0 4px 8px rgba(0,0,0,0.25)                         │
│             Dropdowns, menus                                    │
│                                                                 │
│   Level 3   0 8px 16px rgba(0,0,0,0.3)                         │
│             Dialogs, popovers                                   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## Component Styles

### Buttons

```
┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│   ┌─────────────┐  Primary / Accent Button                      │
│   │   Connect   │  Background: #a371f7 (AccentPurple)           │
│   └─────────────┘  Text: #ffffff                                │
│                    Hover: #b485f8                               │
│                                                                 │
│   ┌─────────────┐  Secondary Button                             │
│   │   Cancel    │  Background: #21262d (BackgroundLight)        │
│   └─────────────┘  Border: #30363d                              │
│                    Text: #e6edf3                                │
│                                                                 │
│   ┌─────────────┐  Danger Button                                │
│   │   Delete    │  Background: #f85149 (AccentRed)              │
│   └─────────────┘  Text: #ffffff                                │
│                                                                 │
│   ┌─────────────┐  Ghost Button                                 │
│   │   More...   │  Background: transparent                      │
│   └─────────────┘  Text: #58a6ff (AccentBlue)                   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Status Badges

```
┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│   ● LIVE         Green (#3fb950) - Active streaming             │
│   ● CONNECTED    Green (#3fb950) - Provider connected           │
│   ○ DISCONNECTED Red (#f85149) - Not connected                  │
│   ◐ CONNECTING   Orange (#d29922) - In progress                 │
│   ◌ IDLE         Gray (#6e7681) - No activity                   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Provider Tags

```
┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│   ┌────┐  IB        Red background (#f85149)                    │
│   └────┘                                                        │
│                                                                 │
│   ┌────────┐  ALPACA   Purple background (#a371f7)              │
│   └────────┘                                                    │
│                                                                 │
│   ┌─────────┐  POLYGON  Blue background (#58a6ff)               │
│   └─────────┘                                                   │
│                                                                 │
│   ┌──────┐  NYSE     Cyan background (#39c5cf)                  │
│   └──────┘                                                      │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## Layout Patterns

### Dashboard Grid

```
┌────────────────────────────────────────────────────────────────────┐
│ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐│
│ │ Events       │ │ Symbols      │ │ Uptime       │ │ Data Rate    ││
│ │   1,234,567  │ │       42     │ │   14:32:15   │ │  1.2 MB/s    ││
│ └──────────────┘ └──────────────┘ └──────────────┘ └──────────────┘│
│                                                                    │
│ ┌────────────────────────────────────┐ ┌──────────────────────────┐│
│ │                                    │ │  Recent Activity         ││
│ │     Live Price Chart               │ │  ─────────────────────   ││
│ │                                    │ │  10:42:15  Trade AAPL    ││
│ │     [═══════════════════════]      │ │  10:42:14  Quote SPY     ││
│ │                                    │ │  10:42:13  LOB update    ││
│ └────────────────────────────────────┘ └──────────────────────────┘│
│                                                                    │
│ ┌──────────────────────────────────────────────────────────────────┐
│ │  Symbol Status Grid                                              │
│ │  ┌──────┬──────────┬────────┬─────────┬──────────┐              │
│ │  │ Sym  │ Last     │ Chg    │ Volume  │ Status   │              │
│ │  ├──────┼──────────┼────────┼─────────┼──────────┤              │
│ │  │ AAPL │ 185.42   │ +1.23% │ 45.2M   │ ● LIVE   │              │
│ │  │ SPY  │ 478.91   │ +0.45% │ 82.1M   │ ● LIVE   │              │
│ │  └──────┴──────────┴────────┴─────────┴──────────┘              │
│ └──────────────────────────────────────────────────────────────────┘
└────────────────────────────────────────────────────────────────────┘
```

### Settings Form

```
┌────────────────────────────────────────────────────────────────────┐
│  Settings                                                          │
│  ════════════════════════════════════════════════════════════════  │
│                                                                    │
│  General                                                           │
│  ─────────────────────────────────────────────────────────────────│
│                                                                    │
│  Theme                                                             │
│  ┌──────────────────────────────────────────────────────┐         │
│  │  ◉ Dark    ○ Light    ○ System                       │         │
│  └──────────────────────────────────────────────────────┘         │
│                                                                    │
│  Auto-connect on startup                                           │
│  ┌────┐                                                           │
│  │ ✓  │  Enable automatic connection when app starts               │
│  └────┘                                                           │
│                                                                    │
│  ─────────────────────────────────────────────────────────────────│
│  Data Providers                                                    │
│  ─────────────────────────────────────────────────────────────────│
│                                                                    │
│  Active Provider                                                   │
│  ┌─────────────────────────────────────────────── ▼ ┐             │
│  │  Interactive Brokers                              │             │
│  └───────────────────────────────────────────────────┘             │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

---

## Animation Guidelines

### Transition Durations

| Type | Duration | Easing |
|------|----------|--------|
| Micro-interactions | 100ms | ease-out |
| State changes | 200ms | ease-in-out |
| Panel slides | 300ms | ease-out |
| Page transitions | 350ms | ease-in-out |

### Loading States

```
┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│   Skeleton Loading:                                              │
│   ┌─────────────────────────────────────────┐                   │
│   │ ████████████████░░░░░░░░░░░░░░░░░░░░░░░ │  Shimmer effect   │
│   └─────────────────────────────────────────┘                   │
│                                                                 │
│   Progress Ring:                                                 │
│      ◠◡◠  (Indeterminate spinner)                               │
│                                                                 │
│   Progress Bar:                                                  │
│   [██████████████░░░░░░░░░░░░░░░░░░░░] 45%                      │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## Accessibility

### Color Contrast

All text/background combinations meet WCAG 2.1 AA standards:

| Foreground | Background | Ratio |
|------------|------------|-------|
| #e6edf3 | #0d1117 | 13.5:1 ✓ |
| #8b949e | #0d1117 | 6.2:1 ✓ |
| #3fb950 | #0d1117 | 7.8:1 ✓ |
| #f85149 | #0d1117 | 5.4:1 ✓ |

### Focus Indicators

```
┌───────────────────────────────────────────┐
│                                           │
│   ┌─────────────┐                         │
│   │   Button    │  ← 2px #58a6ff outline  │
│   └─────────────┘    on focus             │
│                                           │
└───────────────────────────────────────────┘
```

### Keyboard Navigation

- Tab order follows visual layout (top-left to bottom-right)
- Escape closes dialogs and menus
- Arrow keys navigate within components
- Enter activates focused elements
- Space toggles checkboxes and switches

---

## Icon Guidelines

### Icon Sizes

| Context | Size |
|---------|------|
| Navigation menu | 20px |
| Buttons | 16px |
| Inline text | 14px |
| Status indicators | 12px |

### Icon Set

The app uses **Segoe Fluent Icons** for consistency with Windows 11:

```
Common Icons:
  Home             Dashboard
  Settings         Configuration
  ArrowSync        Refresh / Sync
  CloudDownload    Backfill data
  Database         Storage
  Plug             Providers
  Star             Favorites
  Warning          Alerts
  CheckMark        Success
  Dismiss          Close / Cancel
  ChevronRight     Navigate forward
  ChevronDown      Expand
```

---

## Dark Mode Considerations

1. **Avoid pure black** - Use #0d1117 instead of #000000 for softer contrast
2. **Reduce white intensity** - Use #e6edf3 instead of #ffffff for text
3. **Subtle elevation** - Use slightly lighter backgrounds for raised surfaces
4. **Muted colors** - Accent colors are slightly desaturated for comfort
5. **High contrast mode** - Support Windows high contrast themes
