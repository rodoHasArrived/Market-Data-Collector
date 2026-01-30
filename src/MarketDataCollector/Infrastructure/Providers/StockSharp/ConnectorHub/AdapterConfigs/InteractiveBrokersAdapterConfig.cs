#if STOCKSHARP
using StockSharp.Messages;
using System.Net;
#endif

namespace MarketDataCollector.Infrastructure.Providers.StockSharp.ConnectorHub.AdapterConfigs;

/// <summary>
/// Configuration for Interactive Brokers adapter via StockSharp.
/// Provides global multi-asset coverage through TWS/Gateway.
/// </summary>
public sealed class InteractiveBrokersAdapterConfig : StockSharpAdapterConfigBase
{
    /// <inheritdoc/>
    public override string AdapterId => "ib";

    /// <inheritdoc/>
    public override string DisplayName => "Interactive Brokers";

    /// <inheritdoc/>
    public override string Description =>
        "Global multi-asset data via Interactive Brokers TWS/Gateway. " +
        "Covers equities, futures, options, forex, and bonds worldwide.";

    /// <summary>TWS/Gateway host address.</summary>
    public string Host { get; init; } = "127.0.0.1";

    /// <summary>TWS/Gateway port (7496 for TWS, 4001 for Gateway).</summary>
    public int Port { get; init; } = 7496;

    /// <summary>Client ID for IB connection.</summary>
    public int ClientId { get; init; } = 1;

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedMarkets { get; init; } =
        ["US", "EU", "UK", "APAC", "CA", "AU"];

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedAssetClasses { get; init; } =
        ["equity", "futures", "options", "forex", "bonds", "etf", "index"];

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedExchanges { get; init; } =
        ["NYSE", "NASDAQ", "AMEX", "ARCA", "BATS", "IEX", "CME", "CBOT", "NYMEX", "COMEX",
         "ICE", "GLOBEX", "LSE", "IBIS", "TSE", "ASX", "SGX", "HKEX"];

    /// <inheritdoc/>
    public override IReadOnlyList<string> MappedProviderIds { get; init; } =
        ["ib", "interactivebrokers", "tws", "ibkr"];

    /// <inheritdoc/>
    public override bool SupportsStreaming { get; init; } = true;

    /// <inheritdoc/>
    public override bool SupportsBackfill { get; init; } = true;

    /// <inheritdoc/>
    public override bool SupportsMarketDepth { get; init; } = true;

    /// <inheritdoc/>
    public override int? MaxDepthLevels { get; init; } = 10;

#if STOCKSHARP && STOCKSHARP_INTERACTIVEBROKERS
    /// <inheritdoc/>
    public override IMessageAdapter CreateAdapter(IdGenerator transactionIdGenerator)
    {
        return new StockSharp.InteractiveBrokers.InteractiveBrokersMessageAdapter(transactionIdGenerator)
        {
            Address = new IPEndPoint(IPAddress.Parse(Host), Port),
            ClientId = ClientId
        };
    }
#elif STOCKSHARP
    /// <inheritdoc/>
    public override IMessageAdapter CreateAdapter(IdGenerator transactionIdGenerator)
    {
        throw new NotSupportedException(
            "Interactive Brokers adapter requires StockSharp.InteractiveBrokers package. " +
            "Install with: dotnet add package StockSharp.InteractiveBrokers");
    }
#endif

    /// <inheritdoc/>
    public override AdapterValidationResult Validate()
    {
        var errors = new List<string>();

        if (Port <= 0 || Port > 65535)
            errors.Add($"Invalid port: {Port}");

        if (!IPAddress.TryParse(Host, out _))
            errors.Add($"Invalid host address: {Host}");

        return errors.Count > 0
            ? AdapterValidationResult.WithErrors(errors.ToArray())
            : AdapterValidationResult.Success();
    }
}
