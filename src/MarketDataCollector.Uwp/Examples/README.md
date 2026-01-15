# Desktop UI Examples

This directory contains examples and documentation for the Market Data Collector desktop UI visual elements and components.

## Contents

- **[VisualDesignGuide.md](VisualDesignGuide.md)** - Color palette, typography, spacing, and design tokens
- **[ComponentExamples.xaml](ComponentExamples.xaml)** - XAML component library with all reusable elements
- **[ChartExamples.xaml](ChartExamples.xaml)** - Data visualization and charting components
- **[StatusIndicatorExamples.xaml](StatusIndicatorExamples.xaml)** - Connection status, health indicators, badges
- **[CardLayoutExamples.xaml](CardLayoutExamples.xaml)** - Card-based layouts for metrics and data display
- **[DataGridExamples.xaml](DataGridExamples.xaml)** - Table and data grid patterns
- **[NotificationExamples.xaml](NotificationExamples.xaml)** - Toast notifications and alerts

## Design Philosophy

The Market Data Collector desktop UI follows these principles:

1. **Console-Inspired Aesthetics** - Dark theme with terminal-like visual language
2. **Information Density** - Display maximum relevant data without clutter
3. **Real-Time Updates** - Visual feedback for streaming data and status changes
4. **Accessibility** - High contrast ratios and keyboard navigation support
5. **Consistency** - Unified component library across all pages

## Quick Start

To use these components in your pages:

```xaml
<!-- Reference the style dictionary -->
<Page.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="ms-appx:///Styles/AppStyles.xaml"/>
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Page.Resources>

<!-- Use a metric card -->
<Border Style="{StaticResource MetricCardStyle}">
    <StackPanel>
        <TextBlock Text="Events Published" Style="{StaticResource MetricLabelStyle}"/>
        <TextBlock Text="1,234,567" Style="{StaticResource MetricValueStyle}"/>
    </StackPanel>
</Border>
```

## Screenshot Gallery

See individual example files for visual representations of each component.
