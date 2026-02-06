namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Supported export formats.
/// Mirrors MarketDataCollector.Storage.Export.ExportFormat from the core project.
/// </summary>
public enum ExportFormat
{
    /// <summary>Apache Parquet - columnar format for analytics.</summary>
    Parquet,
    /// <summary>Comma-separated values.</summary>
    Csv,
    /// <summary>JSON Lines - one JSON object per line.</summary>
    Jsonl,
    /// <summary>QuantConnect Lean native format.</summary>
    Lean,
    /// <summary>Microsoft Excel XLSX format.</summary>
    Xlsx,
    /// <summary>SQL statements (INSERT or COPY).</summary>
    Sql
}
