using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Represents an exchange trading session.
/// </summary>
public sealed class ExchangeSession
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public SolidColorBrush StatusColor { get; set; } = new SolidColorBrush(Colors.Gray);
    public string PreMarket { get; set; } = string.Empty;
    public string RegularHours { get; set; } = string.Empty;
    public string PostMarket { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
    public bool CollectData { get; set; }
}

/// <summary>
/// Represents a holiday entry.
/// </summary>
public sealed class HolidayEntry
{
    public string Date { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public SolidColorBrush TypeColor { get; set; } = new SolidColorBrush(Colors.Gray);
}
