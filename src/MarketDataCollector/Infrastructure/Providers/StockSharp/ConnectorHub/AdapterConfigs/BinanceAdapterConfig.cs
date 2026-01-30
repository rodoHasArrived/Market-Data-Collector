#if STOCKSHARP
using StockSharp.Messages;
#endif

namespace MarketDataCollector.Infrastructure.Providers.StockSharp.ConnectorHub.AdapterConfigs;

/// <summary>
/// Configuration for Binance adapter via StockSharp.
/// Provides cryptocurrency market data from the world's largest crypto exchange.
/// </summary>
public sealed class BinanceAdapterConfig : StockSharpAdapterConfigBase
{
    /// <inheritdoc/>
    public override string AdapterId => "binance";

    /// <inheritdoc/>
    public override string DisplayName => "Binance";

    /// <inheritdoc/>
    public override string Description =>
        "Cryptocurrency market data via Binance. " +
        "Spot, futures, and options data for 500+ crypto pairs.";

    /// <summary>Binance API key (optional for market data).</summary>
    public string ApiKey { get; init; } = "";

    /// <summary>Binance API secret (optional for market data).</summary>
    public string ApiSecret { get; init; } = "";

    /// <summary>Use testnet environment.</summary>
    public bool UseTestnet { get; init; } = false;

    /// <summary>Use futures API (USDM perpetuals).</summary>
    public bool UseFutures { get; init; } = false;

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedMarkets { get; init; } = ["GLOBAL"];

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedAssetClasses { get; init; } =
        ["crypto", "crypto-futures", "crypto-options"];

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedExchanges { get; init; } =
        ["BINANCE", "BINANCE-FUTURES", "BINANCE-US"];

    /// <inheritdoc/>
    public override IReadOnlyList<string> MappedProviderIds { get; init; } =
        ["binance", "binance-spot", "binance-futures", "binance-us"];

    /// <inheritdoc/>
    public override bool SupportsStreaming { get; init; } = true;

    /// <inheritdoc/>
    public override bool SupportsBackfill { get; init; } = true;

    /// <inheritdoc/>
    public override bool SupportsMarketDepth { get; init; } = true;

    /// <inheritdoc/>
    public override int? MaxDepthLevels { get; init; } = 20;

#if STOCKSHARP && STOCKSHARP_BINANCE
    /// <inheritdoc/>
    public override IMessageAdapter CreateAdapter(IdGenerator transactionIdGenerator)
    {
        var adapter = new StockSharp.Binance.BinanceMessageAdapter(transactionIdGenerator);

        if (!string.IsNullOrEmpty(ApiKey))
        {
            adapter.Key = ApiKey;
            adapter.Secret = ApiSecret.ToSecureString();
        }

        return adapter;
    }
#elif STOCKSHARP
    /// <inheritdoc/>
    public override IMessageAdapter CreateAdapter(IdGenerator transactionIdGenerator)
    {
        throw new NotSupportedException(
            "Binance adapter requires StockSharp.Binance package. " +
            "Install with: dotnet add package StockSharp.Binance");
    }
#endif

    /// <inheritdoc/>
    public override AdapterValidationResult Validate()
    {
        var warnings = new List<string>();

        if (UseTestnet)
            warnings.Add("Using Binance testnet - data may differ from production");

        if (string.IsNullOrEmpty(ApiKey))
            warnings.Add("No API key provided - some endpoints may be rate limited");

        return warnings.Count > 0
            ? AdapterValidationResult.WithWarnings(warnings.ToArray())
            : AdapterValidationResult.Success();
    }
}
