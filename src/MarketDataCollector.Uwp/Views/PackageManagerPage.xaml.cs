using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for managing portable data packages - create, import, and validate.
/// </summary>
public sealed partial class PackageManagerPage : Page
{
    private readonly PortablePackagerService _packagerService;
    private string? _importPath;
    private string? _validatePath;

    public PackageManagerPage()
    {
        this.InitializeComponent();
        _packagerService = PortablePackagerService.Instance;
        Loaded += PackageManagerPage_Loaded;
    }

    private async void PackageManagerPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadRecentPackagesAsync();
    }

    private async System.Threading.Tasks.Task LoadRecentPackagesAsync()
    {
        try
        {
            var packages = await _packagerService.GetRecentPackagesAsync();
            if (packages.Count > 0)
            {
                RecentPackagesList.ItemsSource = packages.Select(p => new PackageDisplayInfo
                {
                    Name = p.Name,
                    Path = p.Path,
                    SizeText = FormatBytes(p.SizeBytes),
                    DateText = p.CreatedAt.ToString("g")
                }).ToList();
                NoRecentPackagesText.Visibility = Visibility.Collapsed;
            }
            else
            {
                NoRecentPackagesText.Visibility = Visibility.Visible;
            }
        }
        catch
        {
            NoRecentPackagesText.Visibility = Visibility.Visible;
        }
    }

    private async void CreatePackage_Click(object sender, RoutedEventArgs e)
    {
        var name = PackageNameBox.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            CreateStatusText.Text = "Please enter a package name";
            return;
        }

        CreateProgress.IsActive = true;
        CreatePackageButton.IsEnabled = false;
        CreateStatusText.Text = "Creating package...";

        try
        {
            var options = new PackageCreationOptions
            {
                Name = name,
                Description = PackageDescriptionBox.Text,
                FromDate = CreateFromDate.Date?.Date is DateTime from ? DateOnly.FromDateTime(from) : null,
                ToDate = CreateToDate.Date?.Date is DateTime to ? DateOnly.FromDateTime(to) : null,
                Symbols = string.IsNullOrWhiteSpace(PackageSymbolsBox.Text)
                    ? null
                    : PackageSymbolsBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                IncludeTrades = IncludeTradesCheck.IsChecked == true,
                IncludeQuotes = IncludeQuotesCheck.IsChecked == true,
                IncludeBars = IncludeBarsCheck.IsChecked == true,
                IncludeLOB = IncludeLOBCheck.IsChecked == true,
                IncludeMetadata = IncludeMetadataCheck.IsChecked == true,
                GenerateChecksums = GenerateChecksumsCheck.IsChecked == true,
                Compression = (CompressionCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "zstd"
            };

            var result = await _packagerService.CreatePackageAsync(options);

            if (result.Success)
            {
                CreateStatusText.Text = $"Package created: {result.PackagePath}";
                await LoadRecentPackagesAsync();
            }
            else
            {
                CreateStatusText.Text = result.Error ?? "Failed to create package";
            }
        }
        catch (Exception ex)
        {
            CreateStatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            CreateProgress.IsActive = false;
            CreatePackageButton.IsEnabled = true;
        }
    }

    private async void BrowseImport_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add(".zip");
        picker.FileTypeFilter.Add(".mdcpkg");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            _importPath = file.Path;
            ImportPathBox.Text = file.Path;
            ImportPackageButton.IsEnabled = true;

            await ShowPackageInfoAsync(file.Path);
        }
    }

    private async void ImportPackage_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_importPath)) return;

        ImportProgress.IsActive = true;
        ImportPackageButton.IsEnabled = false;
        ImportStatusText.Text = "Importing...";

        try
        {
            var options = new PackageImportOptions
            {
                PackagePath = _importPath,
                ValidateFirst = ValidateBeforeImportCheck.IsChecked == true,
                OverwriteExisting = OverwriteExistingCheck.IsChecked == true
            };

            var result = await _packagerService.ImportPackageAsync(options);

            if (result.Success)
            {
                ImportStatusText.Text = $"Imported {result.FilesImported} files";
            }
            else
            {
                ImportStatusText.Text = result.Error ?? "Import failed";
            }
        }
        catch (Exception ex)
        {
            ImportStatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            ImportProgress.IsActive = false;
            ImportPackageButton.IsEnabled = true;
        }
    }

    private async void BrowseValidate_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add(".zip");
        picker.FileTypeFilter.Add(".mdcpkg");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            _validatePath = file.Path;
            ValidatePathBox.Text = file.Path;
            ValidatePackageButton.IsEnabled = true;
        }
    }

    private async void ValidatePackage_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_validatePath)) return;

        ValidateProgress.IsActive = true;
        ValidatePackageButton.IsEnabled = false;

        try
        {
            var result = await _packagerService.ValidatePackageAsync(_validatePath);
            ShowValidationResults(result);
        }
        catch (Exception ex)
        {
            ShowValidationError(ex.Message);
        }
        finally
        {
            ValidateProgress.IsActive = false;
            ValidatePackageButton.IsEnabled = true;
        }
    }

    private void ShowValidationResults(PackageValidationResult result)
    {
        ValidationResultsCard.Visibility = Visibility.Visible;

        ValidationResultIcon.Glyph = result.IsValid ? "\uE73E" : "\uEA39";
        ValidationResultIcon.Foreground = new SolidColorBrush(
            result.IsValid ? Windows.UI.Color.FromArgb(255, 72, 187, 120) : Windows.UI.Color.FromArgb(255, 245, 101, 101));

        ValidFilesCountText.Text = result.ValidFileCount.ToString();
        CorruptFilesCountText.Text = result.CorruptFileCount.ToString();
        MissingFilesCountText.Text = result.MissingFileCount.ToString();
        TotalSizeText.Text = FormatBytes(result.TotalSizeBytes);

        if (result.Issues.Count > 0)
        {
            ValidationIssuesList.ItemsSource = result.Issues.Select(i => new ValidationIssueDisplay
            {
                Message = i.Message,
                Icon = i.Severity == "Error" ? "\uEA39" : "\uE7BA",
                IconColor = new SolidColorBrush(
                    i.Severity == "Error"
                        ? Windows.UI.Color.FromArgb(255, 245, 101, 101)
                        : Windows.UI.Color.FromArgb(255, 237, 137, 54))
            }).ToList();
            ValidationIssuesList.Visibility = Visibility.Visible;
        }
        else
        {
            ValidationIssuesList.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowValidationError(string error)
    {
        ValidationResultsCard.Visibility = Visibility.Visible;
        ValidationResultIcon.Glyph = "\uEA39";
        ValidationResultIcon.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 245, 101, 101));
        ValidFilesCountText.Text = "0";
        CorruptFilesCountText.Text = "0";
        MissingFilesCountText.Text = "0";
        TotalSizeText.Text = "0 B";

        ValidationIssuesList.ItemsSource = new[] { new ValidationIssueDisplay
        {
            Message = error,
            Icon = "\uEA39",
            IconColor = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 245, 101, 101))
        }};
        ValidationIssuesList.Visibility = Visibility.Visible;
    }

    private async System.Threading.Tasks.Task ShowPackageInfoAsync(string path)
    {
        try
        {
            var info = await _packagerService.GetPackageInfoAsync(path);
            if (info != null)
            {
                PackageInfoCard.Visibility = Visibility.Visible;
                InfoPackageName.Text = info.Name;
                InfoCreatedDate.Text = info.CreatedAt.ToString("g");
                InfoDateRange.Text = $"{info.FromDate:d} - {info.ToDate:d}";
                InfoSymbolCount.Text = info.SymbolCount.ToString();
                InfoFileCount.Text = info.FileCount.ToString();
                InfoTotalSize.Text = FormatBytes(info.TotalSizeBytes);
            }
        }
        catch
        {
            PackageInfoCard.Visibility = Visibility.Collapsed;
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

public class PackageDisplayInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string SizeText { get; set; } = string.Empty;
    public string DateText { get; set; } = string.Empty;
}

public class ValidationIssueDisplay
{
    public string Message { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public SolidColorBrush? IconColor { get; set; }
}
