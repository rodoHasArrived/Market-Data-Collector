using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Domain.Models;

namespace MarketDataCollector.Infrastructure.Utilities;

/// <summary>
/// Normalizes market data updates from different providers into a consistent format.
/// Handles symbol casing/trimming, timestamp UTC conversion, and aggressor side inference.
///
/// This normalizer sits between provider adapters and domain collectors to ensure
/// all data entering the pipeline is uniform regardless of the upstream source.
/// </summary>
public sealed class ProviderDataNormalizer
{
    /// <summary>
    /// Normalizes a trade update: uppercases symbol, ensures UTC timestamp,
    /// and maps provider-specific aggressor representations.
    /// </summary>
    public MarketTradeUpdate NormalizeTrade(MarketTradeUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        var normalizedSymbol = NormalizeSymbol(update.Symbol);
        var normalizedTimestamp = NormalizeTimestamp(update.Timestamp);
        var normalizedAggressor = NormalizeAggressor(update.Aggressor);

        // Return original if nothing changed (avoid allocation)
        if (normalizedSymbol == update.Symbol
            && normalizedTimestamp.Equals(update.Timestamp) && normalizedTimestamp.Offset == update.Timestamp.Offset
            && normalizedAggressor == update.Aggressor)
        {
            return update;
        }

        return update with
        {
            Symbol = normalizedSymbol,
            Timestamp = normalizedTimestamp,
            Aggressor = normalizedAggressor
        };
    }

    /// <summary>
    /// Normalizes a quote update: uppercases symbol and ensures UTC timestamp.
    /// </summary>
    public MarketQuoteUpdate NormalizeQuote(MarketQuoteUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        var normalizedSymbol = NormalizeSymbol(update.Symbol);
        var normalizedTimestamp = NormalizeTimestamp(update.Timestamp);

        if (normalizedSymbol == update.Symbol
            && normalizedTimestamp.Equals(update.Timestamp) && normalizedTimestamp.Offset == update.Timestamp.Offset)
        {
            return update;
        }

        return update with
        {
            Symbol = normalizedSymbol,
            Timestamp = normalizedTimestamp
        };
    }

    /// <summary>
    /// Normalizes a depth update: uppercases symbol and ensures UTC timestamp.
    /// </summary>
    public MarketDepthUpdate NormalizeDepth(MarketDepthUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        var normalizedSymbol = NormalizeSymbol(update.Symbol);
        var normalizedTimestamp = NormalizeTimestamp(update.Timestamp);

        if (normalizedSymbol == update.Symbol
            && normalizedTimestamp.Equals(update.Timestamp) && normalizedTimestamp.Offset == update.Timestamp.Offset)
        {
            return update;
        }

        return update with
        {
            Symbol = normalizedSymbol,
            Timestamp = normalizedTimestamp
        };
    }

    /// <summary>
    /// Normalizes a symbol to a canonical form: uppercase, trimmed, whitespace removed.
    /// Returns the original string if already normalized to avoid allocation.
    /// </summary>
    /// <remarks>
    /// Provider inconsistencies addressed:
    /// - Alpaca sends symbols as-is from JSON (usually uppercase but not guaranteed)
    /// - Polygon uses "sym" field which can vary in casing
    /// - IB passes symbols from ticker map (may contain leading/trailing spaces)
    /// - StockSharp passes SecurityId.SecurityCode (casing varies by connector)
    /// </remarks>
    internal static string NormalizeSymbol(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return string.Empty;

        var trimmed = symbol.AsSpan().Trim();

        // Check if already normalized (all uppercase, no surrounding whitespace)
        bool alreadyNormalized = trimmed.Length == symbol.Length;
        if (alreadyNormalized)
        {
            foreach (var c in trimmed)
            {
                if (char.IsLetter(c) && !char.IsUpper(c))
                {
                    alreadyNormalized = false;
                    break;
                }
            }
        }

        if (alreadyNormalized)
            return symbol;

        return trimmed.ToString().ToUpperInvariant();
    }

    /// <summary>
    /// Ensures a timestamp is expressed in UTC. If the offset is non-zero,
    /// converts to UTC equivalent. If the offset is zero, returns as-is.
    /// </summary>
    /// <remarks>
    /// Provider inconsistencies addressed:
    /// - Alpaca: ISO 8601 strings that may carry timezone offset (e.g., "2024-01-15T14:30:00-05:00")
    /// - Polygon: Unix milliseconds (always UTC, offset=0)
    /// - IB: Unix seconds (always UTC) or DateTimeOffset.UtcNow fallback
    /// - StockSharp: msg.ServerTime which may carry exchange-local offset
    /// - NYSE: Mixed formats from REST API vs WebSocket
    /// </remarks>
    internal static DateTimeOffset NormalizeTimestamp(DateTimeOffset timestamp)
    {
        // Already UTC - no conversion needed
        if (timestamp.Offset == TimeSpan.Zero)
            return timestamp;

        // Convert to UTC, preserving the actual instant in time
        return new DateTimeOffset(timestamp.UtcDateTime, TimeSpan.Zero);
    }

    /// <summary>
    /// Normalizes aggressor side values. Currently validates enum range.
    /// Invalid or out-of-range values are mapped to Unknown.
    /// </summary>
    /// <remarks>
    /// Provider inconsistencies addressed:
    /// - Alpaca: Always sends Unknown (no side info available)
    /// - Polygon: Maps CTA condition codes 29-33 to Sell, rest Unknown (no Buy mapping from conditions)
    /// - IB: Provides aggressor via tickType in some callbacks, Unknown in others
    /// - StockSharp: Full Buy/Sell/Unknown via Sides enum conversion
    ///
    /// Note: The actual BBO-based inference (price &gt;= ask =&gt; Buy, price &lt;= bid =&gt; Sell)
    /// is handled downstream in TradeDataCollector when aggressor is Unknown and BBO is available.
    /// </remarks>
    internal static AggressorSide NormalizeAggressor(AggressorSide side)
    {
        return side switch
        {
            AggressorSide.Unknown => AggressorSide.Unknown,
            AggressorSide.Buy => AggressorSide.Buy,
            AggressorSide.Sell => AggressorSide.Sell,
            _ => AggressorSide.Unknown // Map undefined enum values to Unknown
        };
    }
}
