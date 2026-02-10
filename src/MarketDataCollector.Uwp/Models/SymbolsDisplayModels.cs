using Microsoft.UI.Xaml.Media;
using MarketDataCollector.Uwp.Services;
using MarketDataCollector.Uwp.ViewModels;

namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Enhanced symbol view model with selection and status properties.
/// </summary>
public sealed class EnhancedSymbolViewModel : SymbolViewModel
{
    public bool IsSelected { get; set; }
    public string StatusText => SubscribeTrades || SubscribeDepth ? "Active" : "Inactive";

    public SolidColorBrush StatusBackground => SubscribeTrades || SubscribeDepth
        ? BrushRegistry.SuccessBackground
        : BrushRegistry.SubtleBackground;

    public SolidColorBrush TradesStatusColor => SubscribeTrades
        ? BrushRegistry.Success
        : BrushRegistry.Inactive;

    public SolidColorBrush DepthStatusColor => SubscribeDepth
        ? BrushRegistry.Success
        : BrushRegistry.Inactive;

    public EnhancedSymbolViewModel(SymbolConfig config) : base(config)
    {
    }

    public SymbolConfig ToSymbolConfig()
    {
        return new SymbolConfig
        {
            Symbol = Symbol,
            SubscribeTrades = SubscribeTrades,
            SubscribeDepth = SubscribeDepth,
            DepthLevels = DepthLevels,
            Exchange = Exchange,
            LocalSymbol = LocalSymbol,
            SecurityType = SecurityType ?? "STK",
            Currency = "USD",
            Strike = Strike,
            Right = Right,
            LastTradeDateOrContractMonth = LastTradeDateOrContractMonth,
            OptionStyle = OptionStyle,
            Multiplier = Multiplier
        };
    }
}

/// <summary>
/// Watchlist information model.
/// </summary>
public sealed class WatchlistInfo
{
    public string Name { get; set; } = string.Empty;
    public string SymbolCount { get; set; } = string.Empty;
}
