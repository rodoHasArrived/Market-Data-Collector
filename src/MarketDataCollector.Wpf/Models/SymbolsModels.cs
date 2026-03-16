namespace MarketDataCollector.Wpf.Models;

/// <summary>
/// Symbol view model for the symbols page list and edit form.
/// </summary>
public sealed class SymbolViewModel
{
    public bool IsSelected { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public bool SubscribeTrades { get; set; }
    public bool SubscribeDepth { get; set; }
    public int DepthLevels { get; set; } = 10;
    public string Exchange { get; set; } = "SMART";
    public string? LocalSymbol { get; set; }
    public string SecurityType { get; set; } = "STK";
    public decimal? Strike { get; set; }
    public string? Right { get; set; }
    public string? LastTradeDateOrContractMonth { get; set; }
    public string? OptionStyle { get; set; }
    public int? Multiplier { get; set; }

    public string TradesText => SubscribeTrades ? "On" : "Off";
    public string DepthText => SubscribeDepth ? "On" : "Off";
    public string StatusText => SubscribeTrades || SubscribeDepth ? "Active" : "Inactive";
}

/// <summary>
/// Display model for a watchlist entry in the watchlists sidebar.
/// </summary>
public sealed class WatchlistInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SymbolCount { get; set; } = string.Empty;
    public string Color { get; set; } = "#58A6FF";
    public bool IsPinned { get; set; }
}
