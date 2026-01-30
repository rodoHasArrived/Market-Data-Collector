#if STOCKSHARP
using StockSharp.Messages;
using System.Net;
#endif

namespace MarketDataCollector.Infrastructure.Providers.StockSharp.ConnectorHub.AdapterConfigs;

/// <summary>
/// Configuration for IQFeed adapter via StockSharp.
/// Provides tick-level US equity data with comprehensive historical lookups.
/// </summary>
public sealed class IQFeedAdapterConfig : StockSharpAdapterConfigBase
{
    /// <inheritdoc/>
    public override string AdapterId => "iqfeed";

    /// <inheritdoc/>
    public override string DisplayName => "IQFeed";

    /// <inheritdoc/>
    public override string Description =>
        "Tick-level US equity and futures data via IQFeed (DTN). " +
        "Comprehensive historical data with 25+ years of tick data.";

    /// <summary>IQFeed server host address.</summary>
    public string Host { get; init; } = "127.0.0.1";

    /// <summary>Port for Level 1 (quotes) data.</summary>
    public int Level1Port { get; init; } = 9100;

    /// <summary>Port for Level 2 (market depth) data.</summary>
    public int Level2Port { get; init; } = 9200;

    /// <summary>Port for historical data lookup.</summary>
    public int LookupPort { get; init; } = 9300;

    /// <summary>DTN product ID.</summary>
    public string ProductId { get; init; } = "";

    /// <summary>DTN product version.</summary>
    public string ProductVersion { get; init; } = "1.0";

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedMarkets { get; init; } = ["US"];

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedAssetClasses { get; init; } =
        ["equity", "etf", "futures", "options", "index"];

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedExchanges { get; init; } =
        ["NYSE", "NASDAQ", "AMEX", "ARCA", "BATS", "CME", "CBOT", "NYMEX", "COMEX"];

    /// <inheritdoc/>
    public override IReadOnlyList<string> MappedProviderIds { get; init; } =
        ["iqfeed", "dtn", "iqfeed-dtn"];

    /// <inheritdoc/>
    public override bool SupportsStreaming { get; init; } = true;

    /// <inheritdoc/>
    public override bool SupportsBackfill { get; init; } = true;

    /// <inheritdoc/>
    public override bool SupportsMarketDepth { get; init; } = true;

    /// <inheritdoc/>
    public override int? MaxDepthLevels { get; init; } = 10;

#if STOCKSHARP && STOCKSHARP_IQFEED
    /// <inheritdoc/>
    public override IMessageAdapter CreateAdapter(IdGenerator transactionIdGenerator)
    {
        return new StockSharp.IQFeed.IQFeedMessageAdapter(transactionIdGenerator)
        {
            Level1Address = new IPEndPoint(IPAddress.Parse(Host), Level1Port),
            Level2Address = new IPEndPoint(IPAddress.Parse(Host), Level2Port),
            LookupAddress = new IPEndPoint(IPAddress.Parse(Host), LookupPort)
        };
    }
#elif STOCKSHARP
    /// <inheritdoc/>
    public override IMessageAdapter CreateAdapter(IdGenerator transactionIdGenerator)
    {
        throw new NotSupportedException(
            "IQFeed adapter requires StockSharp.IQFeed package. " +
            "Install with: dotnet add package StockSharp.IQFeed");
    }
#endif

    /// <inheritdoc/>
    public override AdapterValidationResult Validate()
    {
        var errors = new List<string>();

        if (!IPAddress.TryParse(Host, out _))
            errors.Add($"Invalid host address: {Host}");

        if (Level1Port <= 0 || Level1Port > 65535)
            errors.Add($"Invalid Level1 port: {Level1Port}");

        if (Level2Port <= 0 || Level2Port > 65535)
            errors.Add($"Invalid Level2 port: {Level2Port}");

        if (LookupPort <= 0 || LookupPort > 65535)
            errors.Add($"Invalid Lookup port: {LookupPort}");

        return errors.Count > 0
            ? AdapterValidationResult.WithErrors(errors.ToArray())
            : AdapterValidationResult.Success();
    }
}
