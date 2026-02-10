namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Simple model for symbols in the welcome wizard.
/// </summary>
public sealed class WelcomeSymbolItem
{
    public string Symbol { get; set; } = string.Empty;
    public bool SubscribeTrades { get; set; }
    public bool SubscribeDepth { get; set; }
    public bool SubscribeQuotes { get; set; }
    public string SubscriptionText { get; set; } = string.Empty;
}
