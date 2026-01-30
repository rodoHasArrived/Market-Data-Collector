#if STOCKSHARP
using StockSharp.Messages;
#endif

namespace MarketDataCollector.Infrastructure.Providers.StockSharp.ConnectorHub.AdapterConfigs;

/// <summary>
/// Configuration for Polygon.io adapter via StockSharp.
/// Provides comprehensive US equity, options, forex, and crypto data.
/// </summary>
public sealed class PolygonAdapterConfig : StockSharpAdapterConfigBase
{
    /// <inheritdoc/>
    public override string AdapterId => "polygon";

    /// <inheritdoc/>
    public override string DisplayName => "Polygon.io";

    /// <inheritdoc/>
    public override string Description =>
        "US equity, options, forex, and crypto market data via Polygon.io. " +
        "Full SIP tape coverage with aggregate bars and reference data.";

    /// <summary>Polygon API key.</summary>
    public string ApiKey { get; init; } = "";

    /// <summary>Use delayed data feed (free tier).</summary>
    public bool UseDelayed { get; init; } = false;

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedMarkets { get; init; } = ["US"];

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedAssetClasses { get; init; } =
        ["equity", "etf", "options", "forex", "crypto", "index"];

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedExchanges { get; init; } =
        ["NYSE", "NASDAQ", "AMEX", "ARCA", "BATS", "IEX", "OTC"];

    /// <inheritdoc/>
    public override IReadOnlyList<string> MappedProviderIds { get; init; } =
        ["polygon", "polygon.io", "polygon-streaming"];

    /// <inheritdoc/>
    public override bool SupportsStreaming { get; init; } = true;

    /// <inheritdoc/>
    public override bool SupportsBackfill { get; init; } = true;

    /// <inheritdoc/>
    public override bool SupportsMarketDepth { get; init; } = false;

#if STOCKSHARP && STOCKSHARP_POLYGON
    /// <inheritdoc/>
    public override IMessageAdapter CreateAdapter(IdGenerator transactionIdGenerator)
    {
        return new StockSharp.Polygon.PolygonMessageAdapter(transactionIdGenerator)
        {
            Key = ApiKey
        };
    }
#elif STOCKSHARP
    /// <inheritdoc/>
    public override IMessageAdapter CreateAdapter(IdGenerator transactionIdGenerator)
    {
        throw new NotSupportedException(
            "Polygon adapter requires StockSharp.Polygon package. " +
            "Install with: dotnet add package StockSharp.Polygon");
    }
#endif

    /// <inheritdoc/>
    public override AdapterValidationResult Validate()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (IsNullOrEmpty(ApiKey))
            errors.Add("Polygon API key is required");

        if (UseDelayed)
            warnings.Add("Using delayed data feed - real-time data requires paid subscription");

        return errors.Count > 0
            ? AdapterValidationResult.WithErrors(errors.ToArray())
            : warnings.Count > 0
                ? AdapterValidationResult.WithWarnings(warnings.ToArray())
                : AdapterValidationResult.Success();
    }
}
