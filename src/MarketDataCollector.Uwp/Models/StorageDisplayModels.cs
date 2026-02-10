namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Model for symbol storage information display in the UI.
/// </summary>
public sealed class SymbolStorageDisplayInfo
{
    public string Symbol { get; set; } = string.Empty;
    public double Percentage { get; set; }
    public string Size { get; set; } = string.Empty;
    public string Files { get; set; } = string.Empty;
}
