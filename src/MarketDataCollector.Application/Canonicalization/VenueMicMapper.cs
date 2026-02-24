using System.Text.Json;
using Serilog;

namespace MarketDataCollector.Application.Canonicalization;

/// <summary>
/// Normalizes freeform venue/exchange identifiers to ISO 10383 MIC (Market Identifier Code) values.
/// Maps provider-specific venue formats (numeric IDs, text abbreviations, routing names) to
/// standard 4-character MIC codes.
/// </summary>
public sealed class VenueMicMapper
{
    private readonly Dictionary<(string Provider, string RawVenue), string> _map;

    public VenueMicMapper(Dictionary<(string Provider, string RawVenue), string> mappings)
    {
        _map = mappings ?? throw new ArgumentNullException(nameof(mappings));
    }

    /// <summary>
    /// Attempts to map a raw venue string from a specific provider to an ISO 10383 MIC code.
    /// Returns <c>null</c> if no mapping exists.
    /// </summary>
    public string? TryMapVenue(string? rawVenue, string provider)
    {
        if (string.IsNullOrWhiteSpace(rawVenue))
            return null;

        var key = (provider.ToUpperInvariant(), rawVenue);
        if (_map.TryGetValue(key, out var mic))
            return mic;

        // Try provider-agnostic lookup (raw venue might already be a MIC)
        var agnosticKey = ("*", rawVenue.ToUpperInvariant());
        if (_map.TryGetValue(agnosticKey, out var agnosticMic))
            return agnosticMic;

        return null;
    }

    /// <summary>
    /// Gets the total number of mappings loaded.
    /// </summary>
    public int MappingCount => _map.Count;

    /// <summary>
    /// Creates a <see cref="VenueMicMapper"/> from a JSON configuration file.
    /// Expected format:
    /// <code>
    /// {
    ///   "version": 1,
    ///   "mappings": [
    ///     { "provider": "ALPACA", "rawVenue": "V", "mic": "XNAS" },
    ///     ...
    ///   ]
    /// }
    /// </code>
    /// </summary>
    public static VenueMicMapper LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Log.ForContext<VenueMicMapper>()
                .Warning("Venue mapping file not found at {Path}, using built-in defaults", filePath);
            return CreateDefault();
        }

        var json = File.ReadAllText(filePath);
        return LoadFromJson(json);
    }

    /// <summary>
    /// Creates a <see cref="VenueMicMapper"/> from a JSON string.
    /// </summary>
    public static VenueMicMapper LoadFromJson(string json)
    {
        var doc = JsonDocument.Parse(json);
        var mappings = new Dictionary<(string Provider, string RawVenue), string>();

        if (doc.RootElement.TryGetProperty("mappings", out var mappingsElement))
        {
            foreach (var entry in mappingsElement.EnumerateArray())
            {
                var provider = entry.GetProperty("provider").GetString()?.ToUpperInvariant();
                var rawVenue = entry.GetProperty("rawVenue").GetString();
                var mic = entry.GetProperty("mic").GetString();

                if (provider is null || rawVenue is null || mic is null)
                    continue;

                mappings[(provider, rawVenue)] = mic;
            }
        }

        return new VenueMicMapper(mappings);
    }

    /// <summary>
    /// Creates a mapper with built-in default mappings for known providers.
    /// Based on the provider field audit in the deterministic canonicalization design doc.
    /// </summary>
    public static VenueMicMapper CreateDefault()
    {
        var mappings = new Dictionary<(string Provider, string RawVenue), string>
        {
            // Alpaca text venue identifiers
            [("ALPACA", "V")] = "XNAS",
            [("ALPACA", "NASDAQ")] = "XNAS",
            [("ALPACA", "P")] = "ARCX",
            [("ALPACA", "NYSE_ARCA")] = "ARCX",
            [("ALPACA", "N")] = "XNYS",
            [("ALPACA", "NYSE")] = "XNYS",
            [("ALPACA", "A")] = "XASE",
            [("ALPACA", "AMEX")] = "XASE",
            [("ALPACA", "Z")] = "BATS",
            [("ALPACA", "BATS")] = "BATS",
            [("ALPACA", "B")] = "XBOS",
            [("ALPACA", "X")] = "XPHL",
            [("ALPACA", "J")] = "EDGA",
            [("ALPACA", "K")] = "EDGX",
            [("ALPACA", "IEX")] = "IEXG",
            [("ALPACA", "M")] = "XCHI",
            [("ALPACA", "W")] = "XCBO",

            // Polygon numeric exchange IDs
            [("POLYGON", "1")] = "XNYS",
            [("POLYGON", "2")] = "XASE",
            [("POLYGON", "3")] = "ARCX",
            [("POLYGON", "4")] = "XNAS",
            [("POLYGON", "5")] = "XBOS",
            [("POLYGON", "6")] = "XPHL",
            [("POLYGON", "7")] = "BATY",
            [("POLYGON", "8")] = "BATS",
            [("POLYGON", "9")] = "IEXG",
            [("POLYGON", "10")] = "EDGX",
            [("POLYGON", "11")] = "EDGA",
            [("POLYGON", "12")] = "XCHI",
            [("POLYGON", "14")] = "FINN",
            [("POLYGON", "15")] = "XCBO",
            [("POLYGON", "16")] = "MEMX",
            [("POLYGON", "17")] = "MIHI",
            [("POLYGON", "19")] = "LTSE",

            // IB TWS routing names
            [("IB", "ISLAND")] = "XNAS",
            [("IB", "ARCA")] = "ARCX",
            [("IB", "NYSE")] = "XNYS",
            [("IB", "AMEX")] = "XASE",
            [("IB", "BATS")] = "BATS",
            [("IB", "EDGX")] = "EDGX",
            [("IB", "EDGA")] = "EDGA",
            [("IB", "BYX")] = "BATY",
            [("IB", "IEX")] = "IEXG",
            [("IB", "CHX")] = "XCHI",
            [("IB", "CBOE")] = "XCBO",
            [("IB", "MEMX")] = "MEMX",
            [("IB", "PHLX")] = "XPHL",
            [("IB", "BEX")] = "XBOS",
            // SMART is IB-specific best-execution router, not a real exchange
            [("IB", "SMART")] = "SMART",

            // Provider-agnostic pass-through for already-standard MIC codes
            [("*", "XNYS")] = "XNYS",
            [("*", "XNAS")] = "XNAS",
            [("*", "ARCX")] = "ARCX",
            [("*", "XASE")] = "XASE",
            [("*", "BATS")] = "BATS",
            [("*", "BATY")] = "BATY",
            [("*", "IEXG")] = "IEXG",
            [("*", "EDGX")] = "EDGX",
            [("*", "EDGA")] = "EDGA",
            [("*", "XCHI")] = "XCHI",
            [("*", "XCBO")] = "XCBO",
            [("*", "XBOS")] = "XBOS",
            [("*", "XPHL")] = "XPHL",
            [("*", "MEMX")] = "MEMX",
            [("*", "MIHI")] = "MIHI",
            [("*", "FINN")] = "FINN",
            [("*", "LTSE")] = "LTSE",
        };

        return new VenueMicMapper(mappings);
    }
}
