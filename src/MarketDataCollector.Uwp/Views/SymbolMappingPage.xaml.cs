using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MarketDataCollector.Uwp.Services;
using Windows.Storage.Pickers;
using Windows.Storage;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// Page for managing provider-specific symbol mappings.
/// Allows users to view, edit, and test symbol transformations across different data providers.
/// </summary>
public sealed partial class SymbolMappingPage : Page
{
    private readonly SymbolMappingService _mappingService;
    private readonly ObservableCollection<SymbolMappingViewModel> _mappings;
    private readonly ObservableCollection<ProviderSymbolEntry> _providerEntries;
    private SymbolMappingViewModel? _selectedMapping;
    private bool _isNewMapping;

    public SymbolMappingPage()
    {
        this.InitializeComponent();
        _mappingService = SymbolMappingService.Instance;
        _mappings = new ObservableCollection<SymbolMappingViewModel>();
        _providerEntries = new ObservableCollection<ProviderSymbolEntry>();

        MappingsListView.ItemsSource = _mappings;
        ProviderSymbolsList.ItemsSource = _providerEntries;
        ProviderLegend.ItemsSource = SymbolMappingService.KnownProviders.Take(5);

        Loaded += SymbolMappingPage_Loaded;
    }

    private async void SymbolMappingPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadMappingsAsync();
    }

    private async Task LoadMappingsAsync()
    {
        await _mappingService.LoadAsync();
        RefreshMappingsList();
    }

    private void RefreshMappingsList(string? filter = null)
    {
        _mappings.Clear();

        var mappings = _mappingService.GetMappings();
        foreach (var mapping in mappings)
        {
            if (!string.IsNullOrWhiteSpace(filter) &&
                !mapping.CanonicalSymbol.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                !(mapping.DisplayName?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                continue;
            }

            _mappings.Add(new SymbolMappingViewModel(mapping));
        }

        UpdateMappingCount();
        UpdateEmptyState();
    }

    private void UpdateMappingCount()
    {
        MappingCountText.Text = $"{_mappings.Count} mapping{(_mappings.Count != 1 ? "s" : "")}";
    }

    private void UpdateEmptyState()
    {
        EmptyState.Visibility = _mappings.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        MappingsListView.Visibility = _mappings.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            RefreshMappingsList(sender.Text);
        }
    }

    private void MappingsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MappingsListView.SelectedItem is SymbolMappingViewModel vm)
        {
            _selectedMapping = vm;
            _isNewMapping = false;
            LoadMappingDetails(vm);
            ShowDetailsPanel(true);
        }
    }

    private void LoadMappingDetails(SymbolMappingViewModel vm)
    {
        CanonicalSymbolBox.Text = vm.CanonicalSymbol;
        DisplayNameBox.Text = vm.DisplayName ?? "";
        NotesBox.Text = vm.Notes ?? "";
        IsCustomToggle.IsOn = vm.IsCustomMapping;

        // Security Type
        foreach (ComboBoxItem item in SecurityTypeCombo.Items)
        {
            if (item.Tag?.ToString() == vm.SecurityType)
            {
                SecurityTypeCombo.SelectedItem = item;
                break;
            }
        }

        // Exchange
        ExchangeCombo.Text = vm.PrimaryExchange ?? "";

        // Identifiers
        FigiBox.Text = vm.Figi ?? "";
        IsinBox.Text = vm.Isin ?? "";
        CusipBox.Text = vm.Cusip ?? "";

        // Provider symbols
        LoadProviderEntries(vm);
    }

    private void LoadProviderEntries(SymbolMappingViewModel vm)
    {
        _providerEntries.Clear();

        foreach (var provider in SymbolMappingService.KnownProviders)
        {
            var customSymbol = vm.ProviderSymbols?.GetValueOrDefault(provider.Id) ?? "";
            var defaultSymbol = SymbolMappingService.ApplyDefaultTransform(vm.CanonicalSymbol, provider.Id);

            _providerEntries.Add(new ProviderSymbolEntry
            {
                ProviderId = provider.Id,
                ProviderName = provider.DisplayName,
                Symbol = customSymbol,
                DefaultSymbol = defaultSymbol
            });
        }
    }

    private void ShowDetailsPanel(bool show)
    {
        DetailsPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        NoSelectionPanel.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
    }

    private void AddMapping_Click(object sender, RoutedEventArgs e)
    {
        _isNewMapping = true;
        _selectedMapping = new SymbolMappingViewModel(new SymbolMapping
        {
            CanonicalSymbol = "",
            SecurityType = "STK",
            IsCustomMapping = true
        });

        MappingsListView.SelectedItem = null;
        ClearDetailsPanel();
        ShowDetailsPanel(true);
        CanonicalSymbolBox.Focus(FocusState.Programmatic);
    }

    private void ClearDetailsPanel()
    {
        CanonicalSymbolBox.Text = "";
        DisplayNameBox.Text = "";
        NotesBox.Text = "";
        IsCustomToggle.IsOn = true;
        SecurityTypeCombo.SelectedIndex = 0;
        ExchangeCombo.Text = "";
        FigiBox.Text = "";
        IsinBox.Text = "";
        CusipBox.Text = "";
        _providerEntries.Clear();

        // Add empty provider entries
        foreach (var provider in SymbolMappingService.KnownProviders)
        {
            _providerEntries.Add(new ProviderSymbolEntry
            {
                ProviderId = provider.Id,
                ProviderName = provider.DisplayName,
                Symbol = "",
                DefaultSymbol = ""
            });
        }
    }

    private async void SaveMapping_Click(object sender, RoutedEventArgs e)
    {
        var canonicalSymbol = CanonicalSymbolBox.Text?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(canonicalSymbol))
        {
            ShowInfoBar("Canonical symbol is required.", InfoBarSeverity.Warning);
            return;
        }

        var mapping = new SymbolMapping
        {
            CanonicalSymbol = canonicalSymbol,
            DisplayName = string.IsNullOrWhiteSpace(DisplayNameBox.Text) ? null : DisplayNameBox.Text.Trim(),
            SecurityType = (SecurityTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "STK",
            PrimaryExchange = string.IsNullOrWhiteSpace(ExchangeCombo.Text) ? null : ExchangeCombo.Text.Trim(),
            Figi = string.IsNullOrWhiteSpace(FigiBox.Text) ? null : FigiBox.Text.Trim(),
            Isin = string.IsNullOrWhiteSpace(IsinBox.Text) ? null : IsinBox.Text.Trim(),
            Cusip = string.IsNullOrWhiteSpace(CusipBox.Text) ? null : CusipBox.Text.Trim(),
            Notes = string.IsNullOrWhiteSpace(NotesBox.Text) ? null : NotesBox.Text.Trim(),
            IsCustomMapping = IsCustomToggle.IsOn,
            ProviderSymbols = new Dictionary<string, string>()
        };

        // Collect provider symbols (only non-empty custom values)
        foreach (var entry in _providerEntries)
        {
            if (!string.IsNullOrWhiteSpace(entry.Symbol))
            {
                mapping.ProviderSymbols[entry.ProviderId] = entry.Symbol.Trim();
            }
        }

        try
        {
            await _mappingService.AddOrUpdateMappingAsync(mapping);
            RefreshMappingsList(SearchBox.Text);

            // Select the saved mapping
            var savedVm = _mappings.FirstOrDefault(m =>
                string.Equals(m.CanonicalSymbol, canonicalSymbol, StringComparison.OrdinalIgnoreCase));
            if (savedVm != null)
            {
                MappingsListView.SelectedItem = savedVm;
            }

            ShowInfoBar($"Mapping for '{canonicalSymbol}' saved successfully.", InfoBarSeverity.Success);
            _isNewMapping = false;
        }
        catch (Exception ex)
        {
            ShowInfoBar($"Failed to save mapping: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void DeleteMapping_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMapping == null || _isNewMapping) return;

        var dialog = new ContentDialog
        {
            Title = "Delete Mapping",
            Content = $"Are you sure you want to delete the mapping for '{_selectedMapping.CanonicalSymbol}'?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            try
            {
                await _mappingService.RemoveMappingAsync(_selectedMapping.CanonicalSymbol);
                RefreshMappingsList(SearchBox.Text);
                ShowDetailsPanel(false);
                _selectedMapping = null;
                ShowInfoBar("Mapping deleted successfully.", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowInfoBar($"Failed to delete mapping: {ex.Message}", InfoBarSeverity.Error);
            }
        }
    }

    private void AutoFillProviders_Click(object sender, RoutedEventArgs e)
    {
        var canonicalSymbol = CanonicalSymbolBox.Text?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(canonicalSymbol))
        {
            ShowInfoBar("Enter a canonical symbol first.", InfoBarSeverity.Warning);
            return;
        }

        foreach (var entry in _providerEntries)
        {
            if (string.IsNullOrWhiteSpace(entry.Symbol))
            {
                entry.Symbol = SymbolMappingService.ApplyDefaultTransform(canonicalSymbol, entry.ProviderId);
                entry.DefaultSymbol = entry.Symbol;
            }
        }

        // Refresh the list
        var temp = _providerEntries.ToList();
        _providerEntries.Clear();
        foreach (var entry in temp)
        {
            _providerEntries.Add(entry);
        }
    }

    private void ResetProviderSymbol_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string providerId)
        {
            var entry = _providerEntries.FirstOrDefault(p => p.ProviderId == providerId);
            if (entry != null)
            {
                entry.Symbol = "";
                // Refresh to update UI
                var index = _providerEntries.IndexOf(entry);
                _providerEntries.RemoveAt(index);
                _providerEntries.Insert(index, entry);
            }
        }
    }

    private void TestMapping_Click(object sender, RoutedEventArgs e)
    {
        TestSymbolMapping();
    }

    private void TestSymbolBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            TestSymbolMapping();
        }
    }

    private void TestSymbolMapping()
    {
        var symbol = TestSymbolBox.Text?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return;
        }

        var results = _mappingService.TestMapping(symbol);
        TestResultsList.ItemsSource = results.ToList();
    }

    private async void ImportCsv_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".csv");

            // Get window handle for WinUI 3
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                var content = await FileIO.ReadTextAsync(file);
                var imported = await _mappingService.ImportFromCsvAsync(content);
                RefreshMappingsList();
                ShowInfoBar($"Imported {imported} mapping(s) from CSV.", InfoBarSeverity.Success);
            }
        }
        catch (Exception ex)
        {
            ShowInfoBar($"Failed to import CSV: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("CSV Files", new List<string> { ".csv" });
            picker.SuggestedFileName = $"symbol-mappings-{DateTime.Now:yyyyMMdd}";

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                var csv = _mappingService.ExportToCsv();
                await FileIO.WriteTextAsync(file, csv);
                ShowInfoBar($"Exported {_mappings.Count} mapping(s) to CSV.", InfoBarSeverity.Success);
            }
        }
        catch (Exception ex)
        {
            ShowInfoBar($"Failed to export CSV: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void ShowInfoBar(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;

        // Auto-close after delay
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += (s, e) =>
        {
            StatusInfoBar.IsOpen = false;
            timer.Stop();
        };
        timer.Start();
    }
}

/// <summary>
/// View model for displaying symbol mappings in the list.
/// </summary>
public class SymbolMappingViewModel
{
    private readonly SymbolMapping _mapping;

    public SymbolMappingViewModel(SymbolMapping mapping)
    {
        _mapping = mapping;
    }

    public string CanonicalSymbol => _mapping.CanonicalSymbol;
    public string? DisplayName => _mapping.DisplayName;
    public string SecurityType => _mapping.SecurityType;
    public string? PrimaryExchange => _mapping.PrimaryExchange;
    public string? Figi => _mapping.Figi;
    public string? Isin => _mapping.Isin;
    public string? Cusip => _mapping.Cusip;
    public string? Notes => _mapping.Notes;
    public bool IsCustomMapping => _mapping.IsCustomMapping;
    public Dictionary<string, string>? ProviderSymbols => _mapping.ProviderSymbols;

    public string ProviderCountText
    {
        get
        {
            var count = _mapping.ProviderSymbols?.Count(kv => !string.IsNullOrWhiteSpace(kv.Value)) ?? 0;
            return count > 0 ? $"{count} provider(s) configured" : "Using defaults";
        }
    }
}

/// <summary>
/// Entry for a provider-specific symbol in the details panel.
/// </summary>
public class ProviderSymbolEntry
{
    public string ProviderId { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string DefaultSymbol { get; set; } = string.Empty;
}
