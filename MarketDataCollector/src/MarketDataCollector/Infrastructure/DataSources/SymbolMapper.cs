using System.Collections.Concurrent;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Infrastructure.DataSources;

/// <summary>
/// Handles symbol format differences between data sources.
/// Maps canonical symbols to source-specific formats and vice versa.
/// </summary>
public interface ISymbolMapper
{
    /// <summary>
    /// Maps a canonical symbol to the format used by a specific source.
    /// </summary>
    /// <param name="canonicalSymbol">The canonical symbol (e.g., "BRK.B").</param>
    /// <param name="sourceId">The data source ID (e.g., "yahoo", "ib").</param>
    /// <returns>The source-specific symbol format.</returns>
    string MapToSource(string canonicalSymbol, string sourceId);

    /// <summary>
    /// Maps a source-specific symbol back to the canonical format.
    /// </summary>
    /// <param name="sourceSymbol">The source-specific symbol.</param>
    /// <param name="sourceId">The data source ID.</param>
    /// <returns>The canonical symbol.</returns>
    string MapFromSource(string sourceSymbol, string sourceId);

    /// <summary>
    /// Gets all known aliases for a canonical symbol.
    /// </summary>
    /// <param name="canonicalSymbol">The canonical symbol.</param>
    /// <returns>List of aliases including source-specific formats.</returns>
    IReadOnlyList<string> GetAllAliases(string canonicalSymbol);

    /// <summary>
    /// Registers a custom mapping for a symbol.
    /// </summary>
    void RegisterMapping(string canonicalSymbol, string sourceId, string sourceSymbol);

    /// <summary>
    /// Gets the default market for symbols without explicit market designation.
    /// </summary>
    string DefaultMarket { get; }
}

/// <summary>
/// Default implementation of ISymbolMapper with configurable mappings.
/// </summary>
public sealed class SymbolMapper : ISymbolMapper
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _toSourceMappings;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _fromSourceMappings;
    private readonly ILogger _log;

    /// <inheritdoc />
    public string DefaultMarket { get; }

    /// <summary>
    /// Creates a new SymbolMapper with the provided configuration.
    /// </summary>
    public SymbolMapper(SymbolMappingConfig? config = null, ILogger? logger = null)
    {
        _log = logger ?? LoggingSetup.ForContext<SymbolMapper>();
        DefaultMarket = config?.DefaultMarket ?? "US";

        _toSourceMappings = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>(
            StringComparer.OrdinalIgnoreCase);
        _fromSourceMappings = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>(
            StringComparer.OrdinalIgnoreCase);

        // Load configured mappings
        if (config?.Mappings != null)
        {
            foreach (var (sourceId, mappings) in config.Mappings)
            {
                var sourceMappings = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var reverseMappings = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var (canonical, sourceSymbol) in mappings)
                {
                    sourceMappings[canonical] = sourceSymbol;
                    reverseMappings[sourceSymbol] = canonical;
                }

                _toSourceMappings[sourceId] = sourceMappings;
                _fromSourceMappings[sourceId] = reverseMappings;
            }
        }

        _log.Debug("SymbolMapper initialized with {Count} source mappings", _toSourceMappings.Count);
    }

    /// <inheritdoc />
    public string MapToSource(string canonicalSymbol, string sourceId)
    {
        if (string.IsNullOrWhiteSpace(canonicalSymbol))
            return canonicalSymbol;

        // Check for explicit mapping first
        if (_toSourceMappings.TryGetValue(sourceId, out var sourceMappings) &&
            sourceMappings.TryGetValue(canonicalSymbol, out var mappedSymbol))
        {
            return mappedSymbol;
        }

        // Apply default transformations based on source
        return ApplyDefaultToSourceTransform(canonicalSymbol, sourceId);
    }

    /// <inheritdoc />
    public string MapFromSource(string sourceSymbol, string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceSymbol))
            return sourceSymbol;

        // Check for explicit reverse mapping first
        if (_fromSourceMappings.TryGetValue(sourceId, out var reverseMappings) &&
            reverseMappings.TryGetValue(sourceSymbol, out var canonical))
        {
            return canonical;
        }

        // Apply default reverse transformations based on source
        return ApplyDefaultFromSourceTransform(sourceSymbol, sourceId);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetAllAliases(string canonicalSymbol)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { canonicalSymbol };

        // Add all source-specific formats
        foreach (var (sourceId, _) in _toSourceMappings)
        {
            var sourceSymbol = MapToSource(canonicalSymbol, sourceId);
            if (!string.Equals(sourceSymbol, canonicalSymbol, StringComparison.OrdinalIgnoreCase))
            {
                aliases.Add(sourceSymbol);
            }
        }

        // Also add common transformations
        var defaultTransforms = new[]
        {
            canonicalSymbol.Replace(".", "-"),  // BRK.B -> BRK-B
            canonicalSymbol.Replace(".", " "),  // BRK.B -> BRK B
            canonicalSymbol.Replace("-", "."),  // BRK-B -> BRK.B
            canonicalSymbol.Replace(" ", "."),  // BRK B -> BRK.B
        };

        foreach (var transform in defaultTransforms)
        {
            if (!string.Equals(transform, canonicalSymbol, StringComparison.OrdinalIgnoreCase))
            {
                aliases.Add(transform);
            }
        }

        return aliases.ToList();
    }

    /// <inheritdoc />
    public void RegisterMapping(string canonicalSymbol, string sourceId, string sourceSymbol)
    {
        var sourceMappings = _toSourceMappings.GetOrAdd(
            sourceId,
            _ => new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        sourceMappings[canonicalSymbol] = sourceSymbol;

        var reverseMappings = _fromSourceMappings.GetOrAdd(
            sourceId,
            _ => new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        reverseMappings[sourceSymbol] = canonicalSymbol;

        _log.Debug("Registered mapping: {Canonical} -> {Source} ({SourceId})",
            canonicalSymbol, sourceSymbol, sourceId);
    }

    #region Default Transformations

    private static string ApplyDefaultToSourceTransform(string symbol, string sourceId)
    {
        return sourceId.ToLowerInvariant() switch
        {
            // Yahoo Finance uses dashes for class shares
            "yahoo" => symbol.Replace(".", "-"),

            // Interactive Brokers uses spaces for class shares
            "ib" => symbol.Replace(".", " "),

            // Alpaca generally uses the canonical format
            "alpaca" => symbol,

            // Polygon uses the canonical format
            "polygon" => symbol,

            // Stooq uses dots (canonical)
            "stooq" => symbol,

            // Default: return as-is
            _ => symbol
        };
    }

    private static string ApplyDefaultFromSourceTransform(string symbol, string sourceId)
    {
        return sourceId.ToLowerInvariant() switch
        {
            // Yahoo Finance: convert dashes back to dots
            "yahoo" => symbol.Replace("-", "."),

            // Interactive Brokers: convert spaces back to dots
            "ib" => symbol.Replace(" ", "."),

            // Others: return as-is
            _ => symbol
        };
    }

    #endregion

    #region Static Helpers

    /// <summary>
    /// Creates a SymbolMapper with common stock symbol mappings pre-configured.
    /// </summary>
    public static SymbolMapper CreateWithCommonMappings(string defaultMarket = "US")
    {
        var config = new SymbolMappingConfig
        {
            DefaultMarket = defaultMarket,
            Enabled = true,
            Mappings = new Dictionary<string, Dictionary<string, string>>
            {
                ["yahoo"] = new()
                {
                    ["BRK.A"] = "BRK-A",
                    ["BRK.B"] = "BRK-B",
                    ["BF.A"] = "BF-A",
                    ["BF.B"] = "BF-B",
                    // Add common indices
                    ["^GSPC"] = "^GSPC",  // S&P 500
                    ["^DJI"] = "^DJI",    // Dow Jones
                    ["^IXIC"] = "^IXIC",  // NASDAQ
                    ["^VIX"] = "^VIX",    // VIX
                },
                ["ib"] = new()
                {
                    ["BRK.A"] = "BRK A",
                    ["BRK.B"] = "BRK B",
                    ["BF.A"] = "BF A",
                    ["BF.B"] = "BF B",
                },
                ["stooq"] = new()
                {
                    // Stooq uses lowercase with exchange suffix for non-US
                    ["AAPL"] = "aapl.us",
                    ["MSFT"] = "msft.us",
                }
            }
        };

        return new SymbolMapper(config);
    }

    #endregion
}

#region Configuration

/// <summary>
/// Configuration for symbol mapping.
/// </summary>
public sealed record SymbolMappingConfig
{
    /// <summary>
    /// Whether symbol mapping is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Default market for symbols without explicit market designation.
    /// </summary>
    public string DefaultMarket { get; init; } = "US";

    /// <summary>
    /// Explicit symbol mappings per source.
    /// Key: sourceId, Value: Dictionary of canonical -> source-specific symbols.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>>? Mappings { get; init; }
}

#endregion

#region Extensions

/// <summary>
/// Extension methods for symbol mapping.
/// </summary>
public static class SymbolMapperExtensions
{
    /// <summary>
    /// Maps a list of canonical symbols to source-specific formats.
    /// </summary>
    public static IEnumerable<string> MapToSource(
        this ISymbolMapper mapper,
        IEnumerable<string> canonicalSymbols,
        string sourceId)
    {
        return canonicalSymbols.Select(s => mapper.MapToSource(s, sourceId));
    }

    /// <summary>
    /// Maps a list of source-specific symbols back to canonical format.
    /// </summary>
    public static IEnumerable<string> MapFromSource(
        this ISymbolMapper mapper,
        IEnumerable<string> sourceSymbols,
        string sourceId)
    {
        return sourceSymbols.Select(s => mapper.MapFromSource(s, sourceId));
    }

    /// <summary>
    /// Tries to find the canonical symbol for any of the given aliases.
    /// </summary>
    public static string? TryResolveCanonical(
        this ISymbolMapper mapper,
        IEnumerable<string> possibleSymbols,
        string sourceId)
    {
        foreach (var symbol in possibleSymbols)
        {
            var canonical = mapper.MapFromSource(symbol, sourceId);
            if (!string.IsNullOrWhiteSpace(canonical))
                return canonical;
        }
        return null;
    }
}

#endregion
