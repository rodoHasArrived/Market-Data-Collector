namespace MarketDataCollector.Application.Subscriptions.Models;

/// <summary>
/// Result of a bulk CSV import operation.
/// </summary>
public sealed record BulkImportResult(
    /// <summary>Number of symbols successfully imported.</summary>
    int SuccessCount,

    /// <summary>Number of symbols that failed to import.</summary>
    int FailureCount,

    /// <summary>Number of symbols skipped (already exist).</summary>
    int SkippedCount,

    /// <summary>Details of any errors encountered.</summary>
    ImportError[] Errors,

    /// <summary>Symbols that were successfully imported.</summary>
    string[] ImportedSymbols,

    /// <summary>Total processing time in milliseconds.</summary>
    long ProcessingTimeMs
);

/// <summary>
/// Error encountered during import.
/// </summary>
public sealed record ImportError(
    /// <summary>Line number in the CSV (1-based).</summary>
    int LineNumber,

    /// <summary>Symbol that caused the error (if parseable).</summary>
    string? Symbol,

    /// <summary>Error message.</summary>
    string Message
);

/// <summary>
/// Options for CSV import operation.
/// </summary>
public sealed record BulkImportOptions(
    /// <summary>Whether to skip symbols that already exist.</summary>
    bool SkipExisting = true,

    /// <summary>Whether to update existing symbols with new settings.</summary>
    bool UpdateExisting = false,

    /// <summary>Whether the CSV has a header row.</summary>
    bool HasHeader = true,

    /// <summary>Default values to apply if not specified in CSV.</summary>
    ImportDefaults? Defaults = null,

    /// <summary>Whether to validate symbols before importing.</summary>
    bool ValidateSymbols = true
);

/// <summary>
/// Default values for imported symbols.
/// </summary>
public sealed record ImportDefaults(
    bool SubscribeTrades = true,
    bool SubscribeDepth = true,
    int DepthLevels = 10,
    string SecurityType = "STK",
    string Exchange = "SMART",
    string Currency = "USD"
);

/// <summary>
/// Options for CSV export operation.
/// </summary>
public sealed record BulkExportOptions(
    /// <summary>Whether to include a header row.</summary>
    bool IncludeHeader = true,

    /// <summary>Columns to include in export (null = all).</summary>
    string[]? Columns = null,

    /// <summary>Filter to specific symbols.</summary>
    string[]? FilterSymbols = null,

    /// <summary>Include metadata if available.</summary>
    bool IncludeMetadata = false
);

/// <summary>
/// CSV column mappings for import/export.
/// </summary>
public static class CsvColumns
{
    public const string Symbol = "Symbol";
    public const string SubscribeTrades = "SubscribeTrades";
    public const string SubscribeDepth = "SubscribeDepth";
    public const string DepthLevels = "DepthLevels";
    public const string SecurityType = "SecurityType";
    public const string Exchange = "Exchange";
    public const string Currency = "Currency";
    public const string PrimaryExchange = "PrimaryExchange";
    public const string LocalSymbol = "LocalSymbol";
    public const string TradingClass = "TradingClass";
    public const string ConId = "ConId";

    // Metadata columns (optional)
    public const string Name = "Name";
    public const string Sector = "Sector";
    public const string Industry = "Industry";
    public const string MarketCap = "MarketCap";

    public static readonly string[] Required = { Symbol };

    public static readonly string[] Standard =
    {
        Symbol, SubscribeTrades, SubscribeDepth, DepthLevels,
        SecurityType, Exchange, Currency, PrimaryExchange
    };

    public static readonly string[] Full =
    {
        Symbol, SubscribeTrades, SubscribeDepth, DepthLevels,
        SecurityType, Exchange, Currency, PrimaryExchange,
        LocalSymbol, TradingClass, ConId
    };

    public static readonly string[] WithMetadata =
    {
        Symbol, Name, Sector, Industry, MarketCap,
        SubscribeTrades, SubscribeDepth, DepthLevels,
        SecurityType, Exchange, Currency, PrimaryExchange
    };
}
