using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.MultiProvider;

/// <summary>
/// Manages provider-specific symbol mappings.
/// Different providers may use different symbols for the same security.
/// E.g., "BRK.B" (Yahoo) vs "BRK B" (IB) vs "BRK/B" (Alpaca)
/// </summary>
public sealed class ProviderSymbolMappingService
{
    private readonly ILogger _log = LoggingSetup.ForContext<ProviderSymbolMappingService>();
    private readonly ConcurrentDictionary<string, SymbolMappingEntry> _mappings = new();
    private readonly string? _persistencePath;
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public ProviderSymbolMappingService(string? persistencePath = null)
    {
        _persistencePath = persistencePath;
    }

    /// <summary>
    /// Gets all symbol mappings.
    /// </summary>
    public IReadOnlyDictionary<string, SymbolMappingEntry> Mappings => _mappings;

    /// <summary>
    /// Loads mappings from persistence.
    /// </summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_persistencePath) || !File.Exists(_persistencePath))
            return;

        try
        {
            var json = await File.ReadAllTextAsync(_persistencePath, ct);
            var entries = JsonSerializer.Deserialize<SymbolMappingEntry[]>(json, s_jsonOptions);
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    _mappings.TryAdd(entry.CanonicalSymbol.ToUpperInvariant(), entry);
                }
            }
            _log.Information("Loaded {Count} symbol mappings from {Path}", _mappings.Count, _persistencePath);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to load symbol mappings from {Path}", _persistencePath);
        }
    }

    /// <summary>
    /// Saves mappings to persistence.
    /// </summary>
    public async Task SaveAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_persistencePath))
            return;

        try
        {
            var entries = _mappings.Values.ToArray();
            var json = JsonSerializer.Serialize(entries, s_jsonOptions);
            var dir = Path.GetDirectoryName(_persistencePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(_persistencePath, json, ct);
            _log.Information("Saved {Count} symbol mappings to {Path}", entries.Length, _persistencePath);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to save symbol mappings to {Path}", _persistencePath);
        }
    }

    /// <summary>
    /// Adds or updates a symbol mapping.
    /// </summary>
    public void SetMapping(SymbolMappingEntry entry)
    {
        if (entry is null) throw new ArgumentNullException(nameof(entry));
        _mappings[entry.CanonicalSymbol.ToUpperInvariant()] = entry;
        _log.Debug("Set mapping for {Symbol}: {Mappings}",
            entry.CanonicalSymbol,
            string.Join(", ", entry.ProviderSymbols.Select(kv => $"{kv.Key}={kv.Value}")));
    }

    /// <summary>
    /// Adds a provider-specific symbol to an existing mapping.
    /// </summary>
    public void AddProviderSymbol(string canonicalSymbol, DataSourceKind provider, string providerSymbol)
    {
        var key = canonicalSymbol.ToUpperInvariant();
        _mappings.AddOrUpdate(
            key,
            _ => new SymbolMappingEntry(
                canonicalSymbol,
                new Dictionary<DataSourceKind, string> { { provider, providerSymbol } }),
            (_, existing) =>
            {
                var updated = new Dictionary<DataSourceKind, string>(existing.ProviderSymbols)
                {
                    [provider] = providerSymbol
                };
                return existing with { ProviderSymbols = updated };
            });
    }

    /// <summary>
    /// Gets the provider-specific symbol for a canonical symbol.
    /// </summary>
    public string GetProviderSymbol(string canonicalSymbol, DataSourceKind provider)
    {
        var key = canonicalSymbol.ToUpperInvariant();
        if (_mappings.TryGetValue(key, out var entry) &&
            entry.ProviderSymbols.TryGetValue(provider, out var providerSymbol))
        {
            return providerSymbol;
        }
        // Default: return the canonical symbol as-is
        return canonicalSymbol;
    }

    /// <summary>
    /// Gets the canonical symbol from a provider-specific symbol.
    /// </summary>
    public string? GetCanonicalSymbol(string providerSymbol, DataSourceKind provider)
    {
        foreach (var entry in _mappings.Values)
        {
            if (entry.ProviderSymbols.TryGetValue(provider, out var symbol) &&
                symbol.Equals(providerSymbol, StringComparison.OrdinalIgnoreCase))
            {
                return entry.CanonicalSymbol;
            }
        }
        return null;
    }

    /// <summary>
    /// Gets all provider symbols for a canonical symbol.
    /// </summary>
    public Dictionary<DataSourceKind, string> GetAllProviderSymbols(string canonicalSymbol)
    {
        var key = canonicalSymbol.ToUpperInvariant();
        if (_mappings.TryGetValue(key, out var entry))
        {
            return new Dictionary<DataSourceKind, string>(entry.ProviderSymbols);
        }
        return new Dictionary<DataSourceKind, string>();
    }

    /// <summary>
    /// Removes a symbol mapping entirely.
    /// </summary>
    public bool RemoveMapping(string canonicalSymbol)
    {
        return _mappings.TryRemove(canonicalSymbol.ToUpperInvariant(), out _);
    }

    /// <summary>
    /// Removes a provider-specific symbol from a mapping.
    /// </summary>
    public bool RemoveProviderSymbol(string canonicalSymbol, DataSourceKind provider)
    {
        var key = canonicalSymbol.ToUpperInvariant();
        if (_mappings.TryGetValue(key, out var entry))
        {
            var updated = new Dictionary<DataSourceKind, string>(entry.ProviderSymbols);
            if (updated.Remove(provider))
            {
                if (updated.Count == 0)
                    _mappings.TryRemove(key, out _);
                else
                    _mappings[key] = entry with { ProviderSymbols = updated };
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Imports mappings from a CSV file.
    /// Expected format: CanonicalSymbol,IB,Alpaca,Polygon,Yahoo
    /// </summary>
    public async Task<int> ImportFromCsvAsync(string csvPath, CancellationToken ct = default)
    {
        if (!File.Exists(csvPath))
            throw new FileNotFoundException("CSV file not found", csvPath);

        var lines = await File.ReadAllLinesAsync(csvPath, ct);
        var count = 0;
        var headerParsed = false;
        var providerColumns = new Dictionary<int, DataSourceKind>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',');
            if (!headerParsed)
            {
                // Parse header
                for (var i = 1; i < parts.Length; i++)
                {
                    var header = parts[i].Trim();
                    if (Enum.TryParse<DataSourceKind>(header, true, out var provider))
                    {
                        providerColumns[i] = provider;
                    }
                }
                headerParsed = true;
                continue;
            }

            if (parts.Length < 2) continue;

            var canonicalSymbol = parts[0].Trim();
            if (string.IsNullOrWhiteSpace(canonicalSymbol)) continue;

            var providerSymbols = new Dictionary<DataSourceKind, string>();
            foreach (var (colIndex, provider) in providerColumns)
            {
                if (colIndex < parts.Length)
                {
                    var providerSymbol = parts[colIndex].Trim();
                    if (!string.IsNullOrWhiteSpace(providerSymbol))
                    {
                        providerSymbols[provider] = providerSymbol;
                    }
                }
            }

            if (providerSymbols.Count > 0)
            {
                SetMapping(new SymbolMappingEntry(canonicalSymbol, providerSymbols));
                count++;
            }
        }

        _log.Information("Imported {Count} symbol mappings from CSV", count);
        return count;
    }

    /// <summary>
    /// Exports mappings to a CSV file.
    /// </summary>
    public async Task ExportToCsvAsync(string csvPath, CancellationToken ct = default)
    {
        var providers = Enum.GetValues<DataSourceKind>();
        var lines = new List<string>
        {
            "CanonicalSymbol," + string.Join(",", providers.Select(p => p.ToString()))
        };

        foreach (var entry in _mappings.Values.OrderBy(e => e.CanonicalSymbol))
        {
            var values = new List<string> { entry.CanonicalSymbol };
            foreach (var provider in providers)
            {
                values.Add(entry.ProviderSymbols.TryGetValue(provider, out var s) ? s : "");
            }
            lines.Add(string.Join(",", values));
        }

        var dir = Path.GetDirectoryName(csvPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllLinesAsync(csvPath, lines, ct);
        _log.Information("Exported {Count} symbol mappings to CSV", _mappings.Count);
    }

    /// <summary>
    /// Gets known symbol transformations for common cases.
    /// </summary>
    public static string TransformSymbol(string symbol, DataSourceKind fromProvider, DataSourceKind toProvider)
    {
        // Handle common transformations
        // BRK.B (Yahoo) -> BRK B (IB) -> BRK/B (some providers)
        var result = symbol;

        // Normalize first (remove special characters)
        var normalized = symbol.Replace(".", " ").Replace("/", " ").Replace("-", " ");

        return toProvider switch
        {
            DataSourceKind.IB => normalized.Replace("  ", " "), // IB uses spaces
            DataSourceKind.Alpaca => symbol.Replace(" ", "."), // Alpaca uses dots
            DataSourceKind.Polygon => symbol.Replace(" ", "."), // Polygon uses dots
            _ => symbol
        };
    }
}

/// <summary>
/// Represents a symbol mapping entry.
/// </summary>
public sealed record SymbolMappingEntry(
    /// <summary>
    /// The canonical (normalized) symbol used internally.
    /// </summary>
    string CanonicalSymbol,

    /// <summary>
    /// Provider-specific symbol mappings.
    /// </summary>
    IReadOnlyDictionary<DataSourceKind, string> ProviderSymbols,

    /// <summary>
    /// Optional FIGI identifier.
    /// </summary>
    string? Figi = null,

    /// <summary>
    /// Optional ISIN identifier.
    /// </summary>
    string? Isin = null,

    /// <summary>
    /// Optional CUSIP identifier.
    /// </summary>
    string? Cusip = null,

    /// <summary>
    /// Security name.
    /// </summary>
    string? Name = null,

    /// <summary>
    /// Security type (STK, OPT, FUT, etc.).
    /// </summary>
    string SecurityType = "STK",

    /// <summary>
    /// Primary exchange.
    /// </summary>
    string? PrimaryExchange = null,

    /// <summary>
    /// Currency.
    /// </summary>
    string Currency = "USD",

    /// <summary>
    /// Additional metadata.
    /// </summary>
    IReadOnlyDictionary<string, string>? Metadata = null
);

/// <summary>
/// Symbol mapping UI model for the dashboard.
/// </summary>
public sealed record SymbolMappingViewModel(
    string CanonicalSymbol,
    string? IbSymbol,
    string? AlpacaSymbol,
    string? PolygonSymbol,
    string? YahooSymbol,
    string? Name,
    string SecurityType,
    string? Figi
);

/// <summary>
/// Request to add or update a symbol mapping.
/// </summary>
public sealed record SymbolMappingRequest(
    string CanonicalSymbol,
    string? IbSymbol = null,
    string? AlpacaSymbol = null,
    string? PolygonSymbol = null,
    string? Name = null,
    string? Figi = null
);
