using System.Collections.Frozen;
using System.Text.Json;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Contracts.Domain.Enums;
using Serilog;

namespace MarketDataCollector.Application.Canonicalization;

/// <summary>
/// Maps provider-specific raw trade condition codes to canonical <see cref="CanonicalTradeCondition"/> values.
/// Loaded from <c>config/condition-codes.json</c> at startup.
/// Thread-safe after initialization (uses frozen dictionary).
/// </summary>
public sealed class ConditionCodeMapper
{
    private readonly ILogger _log = LoggingSetup.ForContext<ConditionCodeMapper>();

    /// <summary>
    /// Lookup: (PROVIDER, rawCode) -> CanonicalTradeCondition.
    /// Frozen after load for zero-allocation reads on the hot path.
    /// </summary>
    private readonly FrozenDictionary<(string Provider, string RawCode), CanonicalTradeCondition> _map;

    /// <summary>
    /// Version of the mapping table (from the JSON file).
    /// </summary>
    public int Version { get; }

    private ConditionCodeMapper(
        FrozenDictionary<(string Provider, string RawCode), CanonicalTradeCondition> map,
        int version)
    {
        _map = map;
        Version = version;
    }

    /// <summary>
    /// Loads the condition code mapping from a JSON file.
    /// </summary>
    public static ConditionCodeMapper LoadFromFile(string path)
    {
        if (!File.Exists(path))
        {
            Log.Warning("Condition code mapping file not found at {Path}, using empty mappings", path);
            return new ConditionCodeMapper(
                FrozenDictionary<(string, string), CanonicalTradeCondition>.Empty, 0);
        }

        var json = File.ReadAllText(path);
        return LoadFromJson(json);
    }

    /// <summary>
    /// Loads the condition code mapping from a JSON string.
    /// </summary>
    public static ConditionCodeMapper LoadFromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var version = root.TryGetProperty("version", out var vProp) ? vProp.GetInt32() : 0;
        var dict = new Dictionary<(string, string), CanonicalTradeCondition>();

        if (root.TryGetProperty("mappings", out var mappings))
        {
            foreach (var providerProp in mappings.EnumerateObject())
            {
                var provider = providerProp.Name.ToUpperInvariant();
                foreach (var codeProp in providerProp.Value.EnumerateObject())
                {
                    var rawCode = codeProp.Name;
                    var canonicalName = codeProp.Value.GetString();
                    if (canonicalName is not null &&
                        Enum.TryParse<CanonicalTradeCondition>(canonicalName, ignoreCase: true, out var canonical))
                    {
                        dict[(provider, rawCode)] = canonical;
                    }
                }
            }
        }

        return new ConditionCodeMapper(dict.ToFrozenDictionary(), version);
    }

    /// <summary>
    /// Maps raw provider condition codes to canonical conditions.
    /// Returns both the canonical and raw arrays for auditability.
    /// </summary>
    /// <param name="provider">Provider name (e.g., "ALPACA", "POLYGON").</param>
    /// <param name="rawConditions">Raw condition codes from the provider.</param>
    /// <returns>Tuple of canonical conditions and the original raw conditions.</returns>
    public (CanonicalTradeCondition[] Canonical, string[] Raw) MapConditions(
        string provider, string[]? rawConditions)
    {
        if (rawConditions is null or { Length: 0 })
            return ([], []);

        var upperProvider = provider.ToUpperInvariant();
        var canonical = new CanonicalTradeCondition[rawConditions.Length];

        for (var i = 0; i < rawConditions.Length; i++)
        {
            canonical[i] = _map.TryGetValue((upperProvider, rawConditions[i]), out var mapped)
                ? mapped
                : CanonicalTradeCondition.Unknown;
        }

        return (canonical, rawConditions);
    }

    /// <summary>
    /// Tries to map a single raw condition code to its canonical equivalent.
    /// </summary>
    public CanonicalTradeCondition MapSingle(string provider, string rawCode)
    {
        return _map.TryGetValue((provider.ToUpperInvariant(), rawCode), out var mapped)
            ? mapped
            : CanonicalTradeCondition.Unknown;
    }

    /// <summary>
    /// Gets the total number of mappings loaded.
    /// </summary>
    public int MappingCount => _map.Count;
}
