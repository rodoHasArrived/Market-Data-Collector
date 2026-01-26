using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarketDataCollector.Uwp.Models;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.ViewModels;

/// <summary>
/// ViewModel for the main dashboard page.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ConfigService _configService;
    private readonly StatusService _statusService;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionStatusText = "Disconnected";

    [ObservableProperty]
    private string _lastUpdateText = "No status available";

    [ObservableProperty]
    private long _publishedCount;

    [ObservableProperty]
    private long _droppedCount;

    [ObservableProperty]
    private long _integrityCount;

    [ObservableProperty]
    private long _historicalBarsCount;

    [ObservableProperty]
    private string _selectedDataSource = "IB";

    [ObservableProperty]
    private string _providerDescription = "Interactive Brokers - TWS/Gateway connection for L2 depth and trades";

    [ObservableProperty]
    private string _configPath = string.Empty;

    [ObservableProperty]
    private string _symbolCount = "(0 symbols)";

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<string> DataSources { get; } = new() { "IB", "Alpaca" };

    public ObservableCollection<SymbolViewModel> Symbols { get; } = new();

    public MainViewModel()
    {
        _configService = new ConfigService();
        _statusService = new StatusService();
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            await LoadConfigAsync();
            await RefreshStatusAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadConfigAsync()
    {
        var config = await _configService.LoadConfigAsync();
        if (config != null)
        {
            SelectedDataSource = config.DataSource ?? "IB";
            ConfigPath = _configService.ConfigPath;

            Symbols.Clear();
            if (config.Symbols != null)
            {
                foreach (var symbol in config.Symbols)
                {
                    Symbols.Add(new SymbolViewModel(symbol));
                }
            }
            SymbolCount = $"({Symbols.Count} symbols)";
        }

        UpdateProviderDescription();
    }

    partial void OnSelectedDataSourceChanged(string value)
    {
        UpdateProviderDescription();
    }

    private void UpdateProviderDescription()
    {
        ProviderDescription = SelectedDataSource switch
        {
            "Alpaca" => "Alpaca - WebSocket streaming for trades and quotes",
            _ => "Interactive Brokers - TWS/Gateway connection for L2 depth and trades"
        };
    }

    [RelayCommand]
    private async Task RefreshStatusAsync()
    {
        var status = await _statusService.GetStatusAsync();
        if (status != null)
        {
            IsConnected = status.IsConnected;
            ConnectionStatusText = status.IsConnected ? "Connected" : "Disconnected";
            LastUpdateText = $"Last update: {status.TimestampUtc:yyyy-MM-dd HH:mm:ss} UTC";

            if (status.Metrics != null)
            {
                PublishedCount = status.Metrics.Published;
                DroppedCount = status.Metrics.Dropped;
                IntegrityCount = status.Metrics.Integrity;
                HistoricalBarsCount = status.Metrics.HistoricalBars;
            }
        }
        else
        {
            IsConnected = false;
            ConnectionStatusText = "No Status";
            LastUpdateText = "Start collector with --http-port 8080";
        }
    }

    [RelayCommand]
    private async Task SaveDataSourceAsync()
    {
        await _configService.SaveDataSourceAsync(SelectedDataSource);
    }

    [RelayCommand]
    private void NavigateToSymbols()
    {
        // Navigation handled by MainPage
    }

    [RelayCommand]
    private void NavigateToBackfill()
    {
        // Navigation handled by MainPage
    }
}

/// <summary>
/// ViewModel for displaying symbol information.
/// </summary>
public class SymbolViewModel
{
    public string Symbol { get; }
    public bool SubscribeTrades { get; }
    public bool SubscribeDepth { get; }
    public int DepthLevels { get; }
    public string? Exchange { get; }
    public string? LocalSymbol { get; }

    public string TradesText => SubscribeTrades ? "Trades" : "-";
    public string DepthText => SubscribeDepth ? "Depth" : "-";

    public SymbolViewModel(SymbolConfig config)
    {
        Symbol = config.Symbol ?? string.Empty;
        SubscribeTrades = config.SubscribeTrades;
        SubscribeDepth = config.SubscribeDepth;
        DepthLevels = config.DepthLevels;
        Exchange = config.Exchange;
        LocalSymbol = config.LocalSymbol;
    }
}
