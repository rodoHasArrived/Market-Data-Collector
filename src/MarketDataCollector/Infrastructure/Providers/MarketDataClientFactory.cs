using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Collectors;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Infrastructure.Contracts;
using MarketDataCollector.Infrastructure.Providers.Alpaca;
using MarketDataCollector.Infrastructure.Providers.InteractiveBrokers;
using MarketDataCollector.Infrastructure.Providers.Polygon;
using MarketDataCollector.Infrastructure.Providers.StockSharp;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers;

/// <summary>
/// Factory interface for creating market data clients by provider kind.
/// Enables runtime provider switching and simplifies testing.
/// </summary>
[ImplementsAdr("ADR-001", "Factory-based provider creation for runtime switching")]
public interface IMarketDataClientFactory
{
    /// <summary>
    /// Creates a market data client for the specified data source.
    /// </summary>
    IMarketDataClient Create(
        DataSourceKind dataSource,
        AppConfig config,
        IMarketEventPublisher publisher,
        TradeDataCollector tradeCollector,
        MarketDepthCollector depthCollector,
        QuoteCollector quoteCollector);

    /// <summary>
    /// Gets all supported data source kinds.
    /// </summary>
    IReadOnlyList<DataSourceKind> SupportedSources { get; }
}

/// <summary>
/// Default implementation that creates provider instances based on data source kind.
/// Replaces the switch statement in Program.cs with a testable, injectable factory.
/// </summary>
[ImplementsAdr("ADR-001", "Unified factory replacing scattered provider creation")]
public sealed class MarketDataClientFactory : IMarketDataClientFactory
{
    private readonly ILogger _log;
    private readonly Func<DataSourceKind, AppConfig, (string? KeyId, string? SecretKey)> _alpacaCredentialResolver;

    public MarketDataClientFactory(
        Func<DataSourceKind, AppConfig, (string? KeyId, string? SecretKey)>? alpacaCredentialResolver = null,
        ILogger? log = null)
    {
        _alpacaCredentialResolver = alpacaCredentialResolver ?? DefaultAlpacaCredentialResolver;
        _log = log ?? LoggingSetup.ForContext<MarketDataClientFactory>();
    }

    public IReadOnlyList<DataSourceKind> SupportedSources { get; } = new[]
    {
        DataSourceKind.IB,
        DataSourceKind.Alpaca,
        DataSourceKind.Polygon,
        DataSourceKind.StockSharp,
        DataSourceKind.NYSE
    };

    public IMarketDataClient Create(
        DataSourceKind dataSource,
        AppConfig config,
        IMarketEventPublisher publisher,
        TradeDataCollector tradeCollector,
        MarketDepthCollector depthCollector,
        QuoteCollector quoteCollector)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(tradeCollector);
        ArgumentNullException.ThrowIfNull(depthCollector);
        ArgumentNullException.ThrowIfNull(quoteCollector);

        _log.Information("Creating market data client for {DataSource}", dataSource);

        return dataSource switch
        {
            DataSourceKind.Alpaca => CreateAlpacaClient(config, tradeCollector, quoteCollector),
            DataSourceKind.Polygon => new PolygonMarketDataClient(publisher, tradeCollector, quoteCollector),
            DataSourceKind.StockSharp => new StockSharpMarketDataClient(
                tradeCollector,
                depthCollector,
                quoteCollector,
                config.StockSharp ?? new StockSharpConfig()),
            _ => new IBMarketDataClient(publisher, tradeCollector, depthCollector)
        };
    }

    private IMarketDataClient CreateAlpacaClient(AppConfig config, TradeDataCollector tradeCollector, QuoteCollector quoteCollector)
    {
        var (keyId, secretKey) = _alpacaCredentialResolver(DataSourceKind.Alpaca, config);
        return new AlpacaMarketDataClient(
            tradeCollector,
            quoteCollector,
            config.Alpaca! with { KeyId = keyId ?? "", SecretKey = secretKey ?? "" });
    }

    private static (string? KeyId, string? SecretKey) DefaultAlpacaCredentialResolver(DataSourceKind _, AppConfig config)
    {
        var keyId = config.Alpaca?.KeyId
                    ?? Environment.GetEnvironmentVariable("ALPACA__KEYID")
                    ?? Environment.GetEnvironmentVariable("ALPACA_KEY_ID");
        var secretKey = config.Alpaca?.SecretKey
                        ?? Environment.GetEnvironmentVariable("ALPACA__SECRETKEY")
                        ?? Environment.GetEnvironmentVariable("ALPACA_SECRET_KEY");
        return (keyId, secretKey);
    }
}
