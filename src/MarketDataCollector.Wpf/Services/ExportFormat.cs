namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Supported export formats for sampling/alignment outputs.
/// </summary>
public enum ExportFormat
{
    Csv,
    Parquet,
    Json,
    JsonLines,
    Excel,
    Hdf5,
    Feather
}
