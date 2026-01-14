using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using Windows.System;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for advanced data analysis export functionality.
/// </summary>
public sealed partial class AnalysisExportPage : Page
{
    private readonly AnalysisExportService _exportService;
    private string? _outputPath;

    public AnalysisExportPage()
    {
        this.InitializeComponent();
        _exportService = AnalysisExportService.Instance;
        Loaded += AnalysisExportPage_Loaded;
    }

    private async void AnalysisExportPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadTemplatesAsync();
    }

    private async System.Threading.Tasks.Task LoadTemplatesAsync()
    {
        var templates = await _exportService.GetExportTemplatesAsync();
        TemplatesList.ItemsSource = templates.Select(t => new TemplateDisplay
        {
            Name = t.Name,
            Description = t.Description,
            Template = t
        }).ToList();
    }

    private void SetDateRange_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string range) return;

        ToDatePicker.Date = DateTimeOffset.Now;

        FromDatePicker.Date = range switch
        {
            "30" => DateTimeOffset.Now.AddDays(-30),
            "90" => DateTimeOffset.Now.AddDays(-90),
            "ytd" => new DateTimeOffset(DateTimeOffset.Now.Year, 1, 1, 0, 0, 0, TimeSpan.Zero),
            "all" => null,
            _ => DateTimeOffset.Now.AddDays(-30)
        };
    }

    private async void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add("*");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            _outputPath = folder.Path;
            OutputPathBox.Text = folder.Path;
        }
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        ExportProgress.IsActive = true;
        ExportButton.IsEnabled = false;
        ExportStatusText.Text = "Exporting...";

        try
        {
            var options = BuildExportOptions();
            var result = await _exportService.ExportAsync(options);

            if (result.Success)
            {
                ShowExportResults(result);
                ExportStatusText.Text = "Export completed";
            }
            else
            {
                ExportStatusText.Text = result.Error ?? "Export failed";
                ExportResultsCard.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            ExportStatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            ExportProgress.IsActive = false;
            ExportButton.IsEnabled = true;
        }
    }

    private AnalysisExportOptions BuildExportOptions()
    {
        var includeFields = new List<string>();
        if (IncludeTimestampCheck.IsChecked == true) includeFields.Add("Timestamp");
        if (IncludeSymbolCheck.IsChecked == true) includeFields.Add("Symbol");
        if (IncludePriceCheck.IsChecked == true) includeFields.Add("Price");
        if (IncludeVolumeCheck.IsChecked == true) includeFields.Add("Volume");
        if (IncludeBidAskCheck.IsChecked == true) { includeFields.Add("BidPrice"); includeFields.Add("AskPrice"); }
        if (IncludeSpreadCheck.IsChecked == true) includeFields.Add("Spread");
        if (IncludeSideCheck.IsChecked == true) includeFields.Add("Side");
        if (IncludeExchangeCheck.IsChecked == true) includeFields.Add("Exchange");
        if (IncludeSequenceCheck.IsChecked == true) includeFields.Add("Sequence");
        if (IncludeConditionsCheck.IsChecked == true) includeFields.Add("Conditions");

        return new AnalysisExportOptions
        {
            Symbols = string.IsNullOrWhiteSpace(SymbolsBox.Text)
                ? null
                : SymbolsBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            FromDate = FromDatePicker.Date?.Date is DateTime from ? DateOnly.FromDateTime(from) : null,
            ToDate = ToDatePicker.Date?.Date is DateTime to ? DateOnly.FromDateTime(to) : null,
            Format = Enum.Parse<ExportFormat>((FormatCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Parquet"),
            Aggregation = Enum.Parse<DataAggregation>((AggregationCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Tick"),
            Compression = Enum.Parse<CompressionType>((CompressionCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "None"),
            Timezone = (TimezoneCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString(),
            IncludeFields = includeFields.ToArray(),
            IncludeMetadata = IncludeMetadataCheck.IsChecked == true,
            OutputPath = _outputPath,
            FileName = string.IsNullOrWhiteSpace(FileNameBox.Text) ? null : FileNameBox.Text,
            SplitBySymbol = SplitBySymbolCheck.IsChecked == true
        };
    }

    private void ShowExportResults(AnalysisExportResult result)
    {
        ExportResultsCard.Visibility = Visibility.Visible;

        ExportResultIcon.Glyph = result.Success ? "\uE73E" : "\uEA39";
        ExportResultIcon.Foreground = new SolidColorBrush(
            result.Success ? Windows.UI.Color.FromArgb(255, 72, 187, 120) : Windows.UI.Color.FromArgb(255, 245, 101, 101));

        RowsExportedText.Text = result.RowsExported.ToString("N0");
        FilesCreatedText.Text = result.FilesCreated.Count.ToString();
        ExportSizeText.Text = FormatBytes(result.BytesWritten);

        _outputPath = result.OutputPath;
    }

    private async void OpenOutput_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_outputPath))
        {
            await Launcher.LaunchFolderPathAsync(_outputPath);
        }
    }

    private void UseTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not TemplateDisplay display) return;

        var template = display.Template;

        // Apply template settings
        FormatCombo.SelectedIndex = (int)template.Format;
        AggregationCombo.SelectedIndex = (int)template.Aggregation;
        IncludeMetadataCheck.IsChecked = template.IncludeMetadata;

        // Reset field checkboxes and set based on template
        IncludeTimestampCheck.IsChecked = template.IncludeFields?.Contains("Timestamp") ?? true;
        IncludeSymbolCheck.IsChecked = template.IncludeFields?.Contains("Symbol") ?? true;
        IncludePriceCheck.IsChecked = template.IncludeFields?.Contains("Price") ?? true;
        IncludeVolumeCheck.IsChecked = template.IncludeFields?.Contains("Volume") ?? true;
        IncludeBidAskCheck.IsChecked = template.IncludeFields?.Contains("BidPrice") ?? false;
        IncludeSpreadCheck.IsChecked = template.IncludeFields?.Contains("Spread") ?? false;
        IncludeSideCheck.IsChecked = template.IncludeFields?.Contains("Side") ?? false;
        IncludeExchangeCheck.IsChecked = template.IncludeFields?.Contains("Exchange") ?? false;
        IncludeSequenceCheck.IsChecked = template.IncludeFields?.Contains("Sequence") ?? false;
    }

    private async void ExportQualityReport_Click(object sender, RoutedEventArgs e)
    {
        var options = new QualityReportOptions
        {
            Symbols = string.IsNullOrWhiteSpace(SymbolsBox.Text)
                ? null
                : SymbolsBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            FromDate = FromDatePicker.Date?.Date is DateTime from ? DateOnly.FromDateTime(from) : null,
            ToDate = ToDatePicker.Date?.Date is DateTime to ? DateOnly.FromDateTime(to) : null,
            IncludeCharts = true,
            Format = "HTML"
        };

        ExportProgress.IsActive = true;
        ExportStatusText.Text = "Generating quality report...";

        try
        {
            var result = await _exportService.GenerateQualityReportAsync(options);
            if (result.Success)
            {
                ExportStatusText.Text = "Report generated";
                if (!string.IsNullOrEmpty(result.ReportPath))
                {
                    await Launcher.LaunchUriAsync(new Uri(result.ReportPath));
                }
            }
            else
            {
                ExportStatusText.Text = result.Error ?? "Failed to generate report";
            }
        }
        finally
        {
            ExportProgress.IsActive = false;
        }
    }

    private async void ExportOrderFlow_Click(object sender, RoutedEventArgs e)
    {
        var options = new OrderFlowExportOptions
        {
            Symbols = string.IsNullOrWhiteSpace(SymbolsBox.Text)
                ? null
                : SymbolsBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            FromDate = FromDatePicker.Date?.Date is DateTime from ? DateOnly.FromDateTime(from) : null,
            ToDate = ToDatePicker.Date?.Date is DateTime to ? DateOnly.FromDateTime(to) : null,
            Metrics = new[] { "VWAP", "Imbalance", "BuySellRatio", "CumulativeDelta" },
            OutputPath = _outputPath
        };

        ExportProgress.IsActive = true;
        ExportStatusText.Text = "Exporting order flow...";

        try
        {
            var result = await _exportService.ExportOrderFlowAsync(options);
            if (result.Success)
            {
                ShowExportResults(result);
                ExportStatusText.Text = "Order flow exported";
            }
            else
            {
                ExportStatusText.Text = result.Error ?? "Export failed";
            }
        }
        finally
        {
            ExportProgress.IsActive = false;
        }
    }

    private async void ExportIntegrityEvents_Click(object sender, RoutedEventArgs e)
    {
        var options = new IntegrityExportOptions
        {
            Symbols = string.IsNullOrWhiteSpace(SymbolsBox.Text)
                ? null
                : SymbolsBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            FromDate = FromDatePicker.Date?.Date is DateTime from ? DateOnly.FromDateTime(from) : null,
            ToDate = ToDatePicker.Date?.Date is DateTime to ? DateOnly.FromDateTime(to) : null,
            OutputPath = _outputPath
        };

        ExportProgress.IsActive = true;
        ExportStatusText.Text = "Exporting integrity events...";

        try
        {
            var result = await _exportService.ExportIntegrityEventsAsync(options);
            if (result.Success)
            {
                ShowExportResults(result);
                ExportStatusText.Text = "Integrity events exported";
            }
            else
            {
                ExportStatusText.Text = result.Error ?? "Export failed";
            }
        }
        finally
        {
            ExportProgress.IsActive = false;
        }
    }

    private async void CreateResearchPackage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Create Research Package",
            Content = new TextBox
            {
                PlaceholderText = "Package name",
                Header = "Name"
            },
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && dialog.Content is TextBox nameBox)
        {
            var options = new ResearchPackageOptions
            {
                Name = nameBox.Text ?? "research-package",
                Symbols = string.IsNullOrWhiteSpace(SymbolsBox.Text)
                    ? null
                    : SymbolsBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                FromDate = FromDatePicker.Date?.Date is DateTime from ? DateOnly.FromDateTime(from) : null,
                ToDate = ToDatePicker.Date?.Date is DateTime to ? DateOnly.FromDateTime(to) : null,
                OutputPath = _outputPath
            };

            ExportProgress.IsActive = true;
            ExportStatusText.Text = "Creating research package...";

            try
            {
                var packageResult = await _exportService.CreateResearchPackageAsync(options);
                if (packageResult.Success)
                {
                    ExportStatusText.Text = $"Package created: {FormatBytes(packageResult.SizeBytes)}";
                }
                else
                {
                    ExportStatusText.Text = packageResult.Error ?? "Failed to create package";
                }
            }
            finally
            {
                ExportProgress.IsActive = false;
            }
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }
}

public class TemplateDisplay
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ExportTemplate Template { get; set; } = new();
}
