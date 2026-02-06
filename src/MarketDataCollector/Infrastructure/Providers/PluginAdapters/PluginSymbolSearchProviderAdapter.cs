using MarketDataCollector.Infrastructure.Contracts;
using MarketDataCollector.Infrastructure.Providers.Core;
using SdkSearch = MarketDataCollector.ProviderSdk.Providers;

namespace MarketDataCollector.Infrastructure.Providers.PluginAdapters;

/// <summary>
/// Adapts an SDK <see cref="SdkSearch.ISymbolSearchProvider"/> to the internal
/// <see cref="SymbolSearch.ISymbolSearchProvider"/> interface.
/// </summary>
[ImplementsAdr("ADR-001", "Adapter bridging plugin symbol search providers to core registry")]
public sealed class PluginSymbolSearchProviderAdapter : SymbolSearch.ISymbolSearchProvider
{
    private readonly SdkSearch.ISymbolSearchProvider _inner;

    public PluginSymbolSearchProviderAdapter(SdkSearch.ISymbolSearchProvider inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public string Name => _inner.ProviderId;
    public string DisplayName => _inner.DisplayName;
    public int Priority => _inner.Priority;

    // IProviderMetadata
    public string ProviderId => _inner.ProviderId;
    public string ProviderDisplayName => _inner.DisplayName;
    public string ProviderDescription => _inner.Description;
    public int ProviderPriority => _inner.Priority;
    public ProviderCapabilities ProviderCapabilities => ProviderCapabilities.SymbolSearch;

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => _inner.IsAvailableAsync(ct);

    public async Task<IReadOnlyList<Application.Subscriptions.Models.SymbolSearchResult>> SearchAsync(
        string query, int limit = 10, CancellationToken ct = default)
    {
        var sdkResults = await _inner.SearchAsync(query, limit, ct).ConfigureAwait(false);
        return sdkResults.Select(r => new Application.Subscriptions.Models.SymbolSearchResult(
            Symbol: r.Symbol,
            Name: r.Name,
            Exchange: r.Exchange,
            AssetType: r.AssetType,
            Currency: r.Currency,
            Country: r.Region
        )).ToList();
    }

    public async Task<Application.Subscriptions.Models.SymbolDetails?> GetDetailsAsync(
        string symbol, CancellationToken ct = default)
    {
        var sdkDetails = await _inner.GetDetailsAsync(symbol, ct).ConfigureAwait(false);
        if (sdkDetails is null)
            return null;

        return new Application.Subscriptions.Models.SymbolDetails(
            Symbol: sdkDetails.Symbol,
            Name: sdkDetails.Name,
            Exchange: sdkDetails.Exchange,
            AssetType: sdkDetails.AssetType,
            Currency: sdkDetails.Currency,
            Description: sdkDetails.Description,
            Sector: sdkDetails.Sector,
            Industry: sdkDetails.Industry,
            Country: sdkDetails.Country
        );
    }
}
