namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Represents an export history item.
/// </summary>
public sealed class ExportHistoryItem
{
    public string Timestamp { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string SymbolCount { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
}
