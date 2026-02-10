using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MarketDataCollector.Contracts.Export;
using MarketDataCollector.Uwp.Services;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Export presets management page for creating, editing, and using export configurations.
/// Implements Feature Refinement #69 - Archive Export Presets.
/// </summary>
public sealed partial class ExportPresetsPage : Page
{
    private readonly ExportPresetService _presetService;
    private readonly NotificationService _notificationService;
    private ExportPreset? _editingPreset;
    private bool _isNewPreset;

    public ExportPresetsPage()
    {
        this.InitializeComponent();
        _presetService = ExportPresetService.Instance;
        _notificationService = NotificationService.Instance;

        _presetService.PresetsChanged += PresetService_PresetsChanged;

        Loaded += ExportPresetsPage_Loaded;
        Unloaded += ExportPresetsPage_Unloaded;
    }

    private void ExportPresetsPage_Loaded(object sender, RoutedEventArgs e)
    {
        _ = LoadDataAsync();
    }

    private void ExportPresetsPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _presetService.PresetsChanged -= PresetService_PresetsChanged;
    }

    private void PresetService_PresetsChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => _ = LoadDataAsync());
    }

    private async Task LoadDataAsync()
    {
        try
        {
            await _presetService.InitializeAsync();

            // Populate quick export combo
            QuickExportPresetCombo.ItemsSource = _presetService.Presets;
            if (_presetService.Presets.Count > 0)
            {
                QuickExportPresetCombo.SelectedIndex = 0;
            }

            // Populate built-in presets
            var builtInPresets = _presetService.Presets.Where(p => p.IsBuiltIn).ToList();
            BuiltInPresetsGrid.ItemsSource = builtInPresets;

            // Populate custom presets
            var customPresets = _presetService.Presets.Where(p => !p.IsBuiltIn).ToList();
            CustomPresetsList.ItemsSource = customPresets;
            CustomPresetCount.Text = $"({customPresets.Count})";
            NoCustomPresetsText.Visibility = customPresets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            CustomPresetsList.Visibility = customPresets.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("ExportPresetsPage error loading data", ex);
        }
    }

    #region Event Handlers

    private void CreatePreset_Click(object sender, RoutedEventArgs e)
    {
        _isNewPreset = true;
        _editingPreset = null;
        EditorTitle.Text = "Create Preset";
        ClearEditorFields();
        PresetEditorSection.Visibility = Visibility.Visible;
    }

    private async void ImportPresets_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".json");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                var importedCount = await _presetService.ImportPresetsAsync(file.Path);
                await _notificationService.NotifyAsync(
                    "Import Successful",
                    $"Imported {importedCount} preset(s)",
                    NotificationType.Success);
            }
        }
        catch (Exception ex)
        {
            await _notificationService.NotifyErrorAsync("Import Failed", ex.Message);
        }
    }

    private async void QuickExport_Click(object sender, RoutedEventArgs e)
    {
        if (QuickExportPresetCombo.SelectedItem is ExportPreset preset)
        {
            await UsePresetAsync(preset.Id);
        }
    }

    private async void UsePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string presetId)
        {
            await UsePresetAsync(presetId);
        }
    }

    private async Task UsePresetAsync(string presetId)
    {
        try
        {
            var preset = _presetService.GetPreset(presetId);
            if (preset == null)
            {
                await _notificationService.NotifyErrorAsync("Error", "Preset not found");
                return;
            }

            // Record usage
            await _presetService.RecordPresetUsageAsync(presetId);

            // Navigate to export page with preset settings or start export
            // For now, show a confirmation
            var (startDate, endDate) = ExportPresetService.GetDateRange(preset.Filters);

            var dialog = new ContentDialog
            {
                Title = "Start Export",
                Content = $"Export using '{preset.Name}' preset?\n\n" +
                          $"Format: {preset.Format}\n" +
                          $"Date Range: {startDate:d} to {endDate:d}\n" +
                          $"Destination: {ExportPresetService.ExpandPathTemplate(preset.Destination)}",
                PrimaryButtonText = "Export",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await _notificationService.NotifyAsync(
                    "Export Started",
                    $"Exporting with '{preset.Name}' preset",
                    NotificationType.Info);

                // In a real implementation, this would trigger the actual export
                // through BatchExportSchedulerService or similar
            }
        }
        catch (Exception ex)
        {
            await _notificationService.NotifyErrorAsync("Export Failed", ex.Message);
        }
    }

    private async void DuplicatePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string presetId)
        {
            var preset = _presetService.GetPreset(presetId);
            if (preset == null) return;

            var dialog = new ContentDialog
            {
                Title = "Duplicate Preset",
                PrimaryButtonText = "Create",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var nameBox = new TextBox
            {
                Header = "New Preset Name",
                Text = $"{preset.Name} (Copy)",
                SelectionStart = 0,
                SelectionLength = preset.Name.Length
            };

            dialog.Content = nameBox;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(nameBox.Text))
            {
                await _presetService.DuplicatePresetAsync(presetId, nameBox.Text);
                await _notificationService.NotifyAsync(
                    "Preset Duplicated",
                    $"Created '{nameBox.Text}'",
                    NotificationType.Success);
            }
        }
    }

    private void EditPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string presetId)
        {
            var preset = _presetService.GetPreset(presetId);
            if (preset == null || preset.IsBuiltIn) return;

            _isNewPreset = false;
            _editingPreset = preset;
            EditorTitle.Text = "Edit Preset";
            PopulateEditorFields(preset);
            PresetEditorSection.Visibility = Visibility.Visible;
        }
    }

    private async void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string presetId)
        {
            var preset = _presetService.GetPreset(presetId);
            if (preset == null || preset.IsBuiltIn) return;

            var dialog = new ContentDialog
            {
                Title = "Delete Preset",
                Content = $"Are you sure you want to delete '{preset.Name}'? This cannot be undone.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await _presetService.DeletePresetAsync(presetId);
                await _notificationService.NotifyAsync(
                    "Preset Deleted",
                    $"Deleted '{preset.Name}'",
                    NotificationType.Info);
            }
        }
    }

    private async void ExportSelectedPresets_Click(object sender, RoutedEventArgs e)
    {
        var customPresets = _presetService.Presets.Where(p => !p.IsBuiltIn).ToList();
        if (customPresets.Count == 0)
        {
            await _notificationService.NotifyAsync(
                "No Presets",
                "Create custom presets first before exporting",
                NotificationType.Info);
            return;
        }

        try
        {
            var picker = new FolderPicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                var presetIds = customPresets.Select(p => p.Id).ToArray();
                var filePath = await _presetService.ExportPresetsAsync(presetIds, folder.Path);
                await _notificationService.NotifyAsync(
                    "Export Complete",
                    $"Exported {presetIds.Length} preset(s) to {System.IO.Path.GetFileName(filePath)}",
                    NotificationType.Success);
            }
        }
        catch (Exception ex)
        {
            await _notificationService.NotifyErrorAsync("Export Failed", ex.Message);
        }
    }

    private async void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PresetNameBox.Text))
        {
            await _notificationService.NotifyErrorAsync("Validation Error", "Please enter a preset name");
            return;
        }

        try
        {
            var preset = _isNewPreset ? new ExportPreset() : _editingPreset ?? new ExportPreset();

            preset.Name = PresetNameBox.Text.Trim();
            preset.Description = PresetDescriptionBox.Text?.Trim();
            preset.Format = GetSelectedFormat();
            preset.Compression = GetSelectedCompression();
            preset.Destination = DestinationBox.Text?.Trim() ?? "";
            preset.FilenamePattern = FilenamePatternBox.Text?.Trim() ?? "{symbol}_{date}.{format}";
            preset.Filters = new ExportPresetFilters
            {
                DateRangeType = GetSelectedDateRange(),
                SessionFilter = (SessionFilterCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "All",
                EventTypes = ParseCommaSeparated(EventTypesBox.Text),
                Symbols = ParseCommaSeparated(SymbolsBox.Text)
            };
            preset.IncludeDataDictionary = IncludeDataDictionaryCheck.IsChecked == true;
            preset.IncludeLoaderScript = IncludeLoaderScriptCheck.IsChecked == true;
            preset.OverwriteExisting = OverwriteExistingCheck.IsChecked == true;
            preset.NotifyOnComplete = NotifyOnCompleteCheck.IsChecked == true;
            preset.PostExportHook = PostExportHookBox.Text?.Trim();
            preset.ScheduleEnabled = ScheduleEnabledToggle.IsOn;
            preset.Schedule = ScheduleEnabledToggle.IsOn ? ScheduleCronBox.Text?.Trim() : null;

            if (_isNewPreset)
            {
                await _presetService.CreatePresetAsync(
                    preset.Name,
                    preset.Description,
                    preset.Format,
                    preset.Destination);

                // Update with full settings
                var newPreset = _presetService.GetPresetByName(preset.Name);
                if (newPreset != null)
                {
                    newPreset.Compression = preset.Compression;
                    newPreset.FilenamePattern = preset.FilenamePattern;
                    newPreset.Filters = preset.Filters;
                    newPreset.IncludeDataDictionary = preset.IncludeDataDictionary;
                    newPreset.IncludeLoaderScript = preset.IncludeLoaderScript;
                    newPreset.OverwriteExisting = preset.OverwriteExisting;
                    newPreset.NotifyOnComplete = preset.NotifyOnComplete;
                    newPreset.PostExportHook = preset.PostExportHook;
                    newPreset.ScheduleEnabled = preset.ScheduleEnabled;
                    newPreset.Schedule = preset.Schedule;
                    await _presetService.UpdatePresetAsync(newPreset);
                }

                await _notificationService.NotifyAsync(
                    "Preset Created",
                    $"Created '{preset.Name}'",
                    NotificationType.Success);
            }
            else
            {
                await _presetService.UpdatePresetAsync(preset);
                await _notificationService.NotifyAsync(
                    "Preset Updated",
                    $"Updated '{preset.Name}'",
                    NotificationType.Success);
            }

            PresetEditorSection.Visibility = Visibility.Collapsed;
            _editingPreset = null;
        }
        catch (Exception ex)
        {
            await _notificationService.NotifyErrorAsync("Save Failed", ex.Message);
        }
    }

    private void CancelEdit_Click(object sender, RoutedEventArgs e)
    {
        PresetEditorSection.Visibility = Visibility.Collapsed;
        _editingPreset = null;
    }

    #endregion

    #region Helper Methods

    private void ClearEditorFields()
    {
        PresetNameBox.Text = "";
        PresetDescriptionBox.Text = "";
        FormatCombo.SelectedIndex = 0;
        CompressionCombo.SelectedIndex = 1; // Snappy
        DestinationBox.Text = "";
        FilenamePatternBox.Text = "{symbol}_{date}.{format}";
        DateRangeCombo.SelectedIndex = 2; // Last Week
        SessionFilterCombo.SelectedIndex = 0; // All
        EventTypesBox.Text = "";
        SymbolsBox.Text = "";
        IncludeDataDictionaryCheck.IsChecked = true;
        IncludeLoaderScriptCheck.IsChecked = true;
        OverwriteExistingCheck.IsChecked = false;
        NotifyOnCompleteCheck.IsChecked = false;
        PostExportHookBox.Text = "";
        ScheduleEnabledToggle.IsOn = false;
        ScheduleCronBox.Text = "";
    }

    private void PopulateEditorFields(ExportPreset preset)
    {
        PresetNameBox.Text = preset.Name;
        PresetDescriptionBox.Text = preset.Description ?? "";
        SetComboByTag(FormatCombo, preset.Format.ToString());
        SetComboByTag(CompressionCombo, preset.Compression.ToString());
        DestinationBox.Text = preset.Destination;
        FilenamePatternBox.Text = preset.FilenamePattern;
        SetComboByTag(DateRangeCombo, preset.Filters.DateRangeType.ToString());
        SetComboByTag(SessionFilterCombo, preset.Filters.SessionFilter);
        EventTypesBox.Text = string.Join(", ", preset.Filters.EventTypes);
        SymbolsBox.Text = string.Join(", ", preset.Filters.Symbols);
        IncludeDataDictionaryCheck.IsChecked = preset.IncludeDataDictionary;
        IncludeLoaderScriptCheck.IsChecked = preset.IncludeLoaderScript;
        OverwriteExistingCheck.IsChecked = preset.OverwriteExisting;
        NotifyOnCompleteCheck.IsChecked = preset.NotifyOnComplete;
        PostExportHookBox.Text = preset.PostExportHook ?? "";
        ScheduleEnabledToggle.IsOn = preset.ScheduleEnabled;
        ScheduleCronBox.Text = preset.Schedule ?? "";
    }

    private static void SetComboByTag(ComboBox combo, string? tag)
    {
        if (string.IsNullOrEmpty(tag)) return;

        foreach (var item in combo.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag?.ToString() == tag)
            {
                combo.SelectedItem = item;
                return;
            }
        }
    }

    private ExportPresetFormat GetSelectedFormat()
    {
        var tag = (FormatCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Parquet";
        return Enum.TryParse<ExportPresetFormat>(tag, out var format) ? format : ExportPresetFormat.Parquet;
    }

    private ExportPresetCompression GetSelectedCompression()
    {
        var tag = (CompressionCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Snappy";
        return Enum.TryParse<ExportPresetCompression>(tag, out var compression) ? compression : ExportPresetCompression.Snappy;
    }

    private DateRangeType GetSelectedDateRange()
    {
        var tag = (DateRangeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "LastWeek";
        return Enum.TryParse<DateRangeType>(tag, out var dateRange) ? dateRange : DateRangeType.LastWeek;
    }

    private static string[] ParseCommaSeparated(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Array.Empty<string>();

        return input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    #endregion
}
