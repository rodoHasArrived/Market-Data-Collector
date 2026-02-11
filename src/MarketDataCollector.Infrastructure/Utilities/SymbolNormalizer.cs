namespace MarketDataCollector.Infrastructure.Utilities;

/// <summary>
/// Centralized symbol normalization utilities used across all data providers.
/// Consolidates duplicate normalization logic from individual providers.
/// </summary>
public static class SymbolNormalizer
{
    /// <summary>
    /// Normalizes a symbol to standard format (uppercase, trimmed).
    /// </summary>
    /// <param name="symbol">Raw symbol input.</param>
    /// <returns>Normalized symbol string.</returns>
    public static string Normalize(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return string.Empty;

        return symbol.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Normalizes a symbol for Tiingo API (replaces dots with hyphens).
    /// Tiingo uses hyphens instead of dots for preferred shares (e.g., BRK-B instead of BRK.B).
    /// </summary>
    /// <param name="symbol">Raw symbol input.</param>
    /// <returns>Tiingo-normalized symbol string.</returns>
    public static string NormalizeForTiingo(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return string.Empty;

        return symbol.Trim().ToUpperInvariant().Replace(".", "-");
    }

    /// <summary>
    /// Normalizes a symbol for Yahoo Finance API.
    /// Handles special characters for different exchanges.
    /// </summary>
    /// <param name="symbol">Raw symbol input.</param>
    /// <returns>Yahoo Finance-normalized symbol string.</returns>
    public static string NormalizeForYahoo(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return string.Empty;

        return symbol.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Normalizes a symbol for Polygon.io API.
    /// </summary>
    /// <param name="symbol">Raw symbol input.</param>
    /// <returns>Polygon-normalized symbol string.</returns>
    public static string NormalizeForPolygon(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return string.Empty;

        return symbol.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Normalizes a symbol for Alpaca API.
    /// </summary>
    /// <param name="symbol">Raw symbol input.</param>
    /// <returns>Alpaca-normalized symbol string.</returns>
    public static string NormalizeForAlpaca(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return string.Empty;

        return symbol.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Validates that a symbol string is not null or whitespace.
    /// </summary>
    /// <param name="symbol">Symbol to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool IsValid(string? symbol)
    {
        return !string.IsNullOrWhiteSpace(symbol);
    }

    /// <summary>
    /// Validates and normalizes a symbol, returning null if invalid.
    /// </summary>
    /// <param name="symbol">Symbol to validate and normalize.</param>
    /// <returns>Normalized symbol or null if invalid.</returns>
    public static string? TryNormalize(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return null;

        return Normalize(symbol);
    }
}
