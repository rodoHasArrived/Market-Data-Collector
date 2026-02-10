using System.Collections.Generic;
using System.Linq;

namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// View model for displaying symbol mappings in the list.
/// </summary>
public sealed class SymbolMappingViewModel
{
    private readonly SymbolMapping _mapping;

    public SymbolMappingViewModel(SymbolMapping mapping)
    {
        _mapping = mapping;
    }

    public string CanonicalSymbol => _mapping.CanonicalSymbol;
    public string? DisplayName => _mapping.DisplayName;
    public string SecurityType => _mapping.SecurityType;
    public string? PrimaryExchange => _mapping.PrimaryExchange;
    public string? Figi => _mapping.Figi;
    public string? Isin => _mapping.Isin;
    public string? Cusip => _mapping.Cusip;
    public string? Notes => _mapping.Notes;
    public bool IsCustomMapping => _mapping.IsCustomMapping;
    public Dictionary<string, string>? ProviderSymbols => _mapping.ProviderSymbols;

    public string ProviderCountText
    {
        get
        {
            var count = _mapping.ProviderSymbols?.Count(kv => !string.IsNullOrWhiteSpace(kv.Value)) ?? 0;
            return count > 0 ? $"{count} provider(s) configured" : "Using defaults";
        }
    }
}

/// <summary>
/// Entry for a provider-specific symbol in the details panel.
/// </summary>
public sealed class ProviderSymbolEntry
{
    public string ProviderId { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string DefaultSymbol { get; set; } = string.Empty;
}
