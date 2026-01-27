namespace MarketDataCollector.Infrastructure.Utilities;

/// <summary>
/// Centralized symbol normalization utilities to eliminate duplicate implementations
/// across providers. Each provider was implementing NormalizeSymbol() separately.
/// </summary>
public static class SymbolNormalization
{
    /// <summary>
    /// Standard symbol normalization: uppercase and trim whitespace.
    /// Used by: Alpaca, Polygon, Finnhub, Alpha Vantage, Yahoo Finance, Stooq, Nasdaq Data Link
    /// </summary>
    public static string Normalize(string symbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol, nameof(symbol));
        return symbol.ToUpperInvariant().Trim();
    }

    /// <summary>
    /// Tiingo-specific normalization: replaces dots with dashes.
    /// Tiingo uses dashes instead of dots for class shares (e.g., BRK-A instead of BRK.A)
    /// </summary>
    public static string NormalizeForTiingo(string symbol)
    {
        return Normalize(symbol).Replace(".", "-");
    }

    /// <summary>
    /// Yahoo Finance-specific normalization for international symbols.
    /// Adds exchange suffix if not present (e.g., .L for London, .T for Tokyo)
    /// </summary>
    public static string NormalizeForYahoo(string symbol, string? exchangeSuffix = null)
    {
        var normalized = Normalize(symbol);

        if (!string.IsNullOrEmpty(exchangeSuffix) && !normalized.Contains('.'))
        {
            return $"{normalized}.{exchangeSuffix.TrimStart('.')}";
        }

        return normalized;
    }

    /// <summary>
    /// Stooq-specific normalization for Polish market symbols.
    /// Stooq uses lowercase symbols and specific market suffixes.
    /// </summary>
    public static string NormalizeForStooq(string symbol)
    {
        // Stooq uses lowercase for some markets
        return symbol.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// OpenFIGI-specific normalization.
    /// OpenFIGI expects uppercase symbols without special characters.
    /// </summary>
    public static string NormalizeForOpenFigi(string symbol)
    {
        var normalized = Normalize(symbol);
        // Remove common suffixes that OpenFIGI doesn't recognize
        if (normalized.Contains('.'))
        {
            normalized = normalized.Split('.')[0];
        }
        return normalized;
    }

    /// <summary>
    /// Validates that a symbol meets basic requirements.
    /// </summary>
    public static bool IsValidSymbol(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return false;

        // Symbols should be alphanumeric with optional dots, dashes, or underscores
        return symbol.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' || c == ' ');
    }

    /// <summary>
    /// Batch normalize multiple symbols.
    /// </summary>
    public static IReadOnlyList<string> NormalizeMany(IEnumerable<string> symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols, nameof(symbols));
        return symbols.Where(s => !string.IsNullOrWhiteSpace(s))
                      .Select(Normalize)
                      .Distinct()
                      .ToList();
    }
}
