namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Display model for search suggestions in the UI.
/// </summary>
public sealed class SearchSuggestionDisplay
{
    public string Text { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string NavigationTarget { get; set; } = string.Empty;

    public override string ToString() => $"{Text} ({Category})";
}
