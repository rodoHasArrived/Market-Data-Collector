using System.Text.Json;
using MarketDataCollector.Contracts.Domain.Enums;
using Serilog;

namespace MarketDataCollector.Application.Canonicalization;

/// <summary>
/// Maps provider-specific trade condition codes to canonical <see cref="CanonicalTradeCondition"/> values.
/// Loaded from a JSON configuration file at startup.
/// </summary>
public sealed class ConditionCodeMapper
{
    private readonly ILogger _log = Log.ForContext<ConditionCodeMapper>();
    private readonly Dictionary<(string Provider, string RawCode), CanonicalTradeCondition> _map;

    public ConditionCodeMapper(Dictionary<(string Provider, string RawCode), CanonicalTradeCondition> mappings)
    {
        _map = mappings ?? throw new ArgumentNullException(nameof(mappings));
    }

    /// <summary>
    /// Maps raw provider condition codes to canonical codes.
    /// Returns both the canonical and raw arrays for auditability.
    /// </summary>
    public (CanonicalTradeCondition[] Canonical, string[] Raw) MapConditions(
        string provider, string[]? rawConditions)
    {
        if (rawConditions is null || rawConditions.Length == 0)
            return ([], []);

        var canonical = new CanonicalTradeCondition[rawConditions.Length];
        for (var i = 0; i < rawConditions.Length; i++)
        {
            var key = (provider.ToUpperInvariant(), rawConditions[i]);
            canonical[i] = _map.TryGetValue(key, out var mapped)
                ? mapped
                : CanonicalTradeCondition.Unknown;
        }

        return (canonical, rawConditions);
    }

    /// <summary>
    /// Checks whether a given raw code has a known canonical mapping for the provider.
    /// </summary>
    public bool HasMapping(string provider, string rawCode)
    {
        return _map.ContainsKey((provider.ToUpperInvariant(), rawCode));
    }

    /// <summary>
    /// Gets the total number of mappings loaded.
    /// </summary>
    public int MappingCount => _map.Count;

    /// <summary>
    /// Creates a <see cref="ConditionCodeMapper"/> from a JSON configuration file.
    /// Expected format:
    /// <code>
    /// {
    ///   "version": 1,
    ///   "mappings": [
    ///     { "provider": "ALPACA", "rawCode": "@", "canonical": "Regular" },
    ///     ...
    ///   ]
    /// }
    /// </code>
    /// </summary>
    public static ConditionCodeMapper LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Log.ForContext<ConditionCodeMapper>()
                .Warning("Condition code mapping file not found at {Path}, using built-in defaults", filePath);
            return CreateDefault();
        }

        var json = File.ReadAllText(filePath);
        return LoadFromJson(json);
    }

    /// <summary>
    /// Creates a <see cref="ConditionCodeMapper"/> from a JSON string.
    /// </summary>
    public static ConditionCodeMapper LoadFromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var mappings = new Dictionary<(string Provider, string RawCode), CanonicalTradeCondition>();

        if (doc.RootElement.TryGetProperty("mappings", out var mappingsElement))
        {
            foreach (var entry in mappingsElement.EnumerateArray())
            {
                var provider = entry.GetProperty("provider").GetString()?.ToUpperInvariant();
                var rawCode = entry.GetProperty("rawCode").GetString();
                var canonicalStr = entry.GetProperty("canonical").GetString();

                if (provider is null || rawCode is null || canonicalStr is null)
                    continue;

                if (Enum.TryParse<CanonicalTradeCondition>(canonicalStr, ignoreCase: true, out var canonical))
                {
                    mappings[(provider, rawCode)] = canonical;
                }
            }
        }

        return new ConditionCodeMapper(mappings);
    }

    /// <summary>
    /// Creates a mapper with built-in default mappings for known providers.
    /// </summary>
    public static ConditionCodeMapper CreateDefault()
    {
        var mappings = new Dictionary<(string Provider, string RawCode), CanonicalTradeCondition>
        {
            // Alpaca CTA plan codes
            [("ALPACA", "@")] = CanonicalTradeCondition.Regular,
            [("ALPACA", "T")] = CanonicalTradeCondition.FormT_ExtendedHours,
            [("ALPACA", "I")] = CanonicalTradeCondition.Intermarket_Sweep,
            [("ALPACA", "X")] = CanonicalTradeCondition.CrossTrade,
            [("ALPACA", "O")] = CanonicalTradeCondition.OpeningPrint,
            [("ALPACA", "6")] = CanonicalTradeCondition.ClosingPrint,
            [("ALPACA", "4")] = CanonicalTradeCondition.DerivativelyPriced,
            [("ALPACA", "H")] = CanonicalTradeCondition.Halted,

            // Polygon SEC numeric codes
            [("POLYGON", "0")] = CanonicalTradeCondition.Regular,
            [("POLYGON", "12")] = CanonicalTradeCondition.FormT_ExtendedHours,
            [("POLYGON", "37")] = CanonicalTradeCondition.OddLot,
            [("POLYGON", "14")] = CanonicalTradeCondition.Intermarket_Sweep,
            [("POLYGON", "15")] = CanonicalTradeCondition.OpeningPrint,
            [("POLYGON", "16")] = CanonicalTradeCondition.ClosingPrint,
            [("POLYGON", "22")] = CanonicalTradeCondition.AveragePrice,
            [("POLYGON", "29")] = CanonicalTradeCondition.SellerInitiated,
            [("POLYGON", "30")] = CanonicalTradeCondition.SellerDownExempt,
            [("POLYGON", "38")] = CanonicalTradeCondition.CrossTrade,
            [("POLYGON", "40")] = CanonicalTradeCondition.DerivativelyPriced,
            [("POLYGON", "52")] = CanonicalTradeCondition.CorrectedConsolidated,
            [("POLYGON", "53")] = CanonicalTradeCondition.Contingent,

            // IB text-based condition codes
            [("IB", "RegularTrade")] = CanonicalTradeCondition.Regular,
            [("IB", "OddLot")] = CanonicalTradeCondition.OddLot,
            [("IB", "FormT")] = CanonicalTradeCondition.FormT_ExtendedHours,
            [("IB", "IntermarketSweep")] = CanonicalTradeCondition.Intermarket_Sweep,
            [("IB", "OpeningPrint")] = CanonicalTradeCondition.OpeningPrint,
            [("IB", "ClosingPrint")] = CanonicalTradeCondition.ClosingPrint,
            [("IB", "DerivativelyPriced")] = CanonicalTradeCondition.DerivativelyPriced,
            [("IB", "Halted")] = CanonicalTradeCondition.Halted,
        };

        return new ConditionCodeMapper(mappings);
    }
}
