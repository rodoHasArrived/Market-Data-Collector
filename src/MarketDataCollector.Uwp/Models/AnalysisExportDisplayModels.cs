namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Display model for analysis export templates.
/// </summary>
public sealed class TemplateDisplay
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ExportTemplate Template { get; set; } = new();
}
