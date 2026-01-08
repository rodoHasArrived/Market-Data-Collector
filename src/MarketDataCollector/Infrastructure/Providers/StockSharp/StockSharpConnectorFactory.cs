#if STOCKSHARP
using System.Net;
using System.Security;
using StockSharp.Algo;
using StockSharp.BusinessEntities;
#endif
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Infrastructure.Providers.StockSharp;

/// <summary>
/// Factory for creating StockSharp connectors based on configuration.
/// Supports multiple connector types: Rithmic, IQFeed, CQG, Interactive Brokers, and more.
/// </summary>
public static class StockSharpConnectorFactory
{
    private static readonly ILogger Log = LoggingSetup.ForContext("StockSharpConnectorFactory");

#if STOCKSHARP
    /// <summary>
    /// Create a connector instance based on the configured type.
    /// </summary>
    /// <param name="config">StockSharp configuration with connector settings.</param>
    /// <returns>Configured StockSharp Connector ready for connection.</returns>
    /// <exception cref="NotSupportedException">Thrown when connector type is not supported.</exception>
    public static Connector Create(StockSharpConfig config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        var connector = new Connector();

        Log.Information("Creating StockSharp connector: {ConnectorType}", config.ConnectorType);

        // Add the appropriate message adapter based on connector type
        switch (config.ConnectorType.ToLowerInvariant())
        {
            case "rithmic":
                ConfigureRithmic(connector, config.Rithmic);
                break;

            case "iqfeed":
                ConfigureIQFeed(connector, config.IQFeed);
                break;

            case "cqg":
                ConfigureCQG(connector, config.CQG);
                break;

            case "interactivebrokers":
            case "ib":
                ConfigureInteractiveBrokers(connector, config.InteractiveBrokers);
                break;

            default:
                throw new NotSupportedException(
                    $"Connector type '{config.ConnectorType}' is not supported. " +
                    "Supported types: Rithmic, IQFeed, CQG, InteractiveBrokers");
        }

        return connector;
    }

    /// <summary>
    /// Configure Rithmic adapter for futures data (CME, NYMEX, etc.).
    /// Rithmic provides low-latency direct market access for futures trading.
    /// </summary>
    private static void ConfigureRithmic(Connector connector, RithmicConfig? cfg)
    {
#if STOCKSHARP_RITHMIC
        Log.Debug("Configuring Rithmic adapter: Server={Server}, User={User}",
            cfg?.Server ?? "Rithmic Test", cfg?.UserName ?? "(not set)");

        var adapter = new StockSharp.Rithmic.RithmicMessageAdapter(
            connector.TransactionIdGenerator)
        {
            Server = cfg?.Server ?? "Rithmic Test",
            UserName = cfg?.UserName ?? "",
            Password = ToSecureString(cfg?.Password ?? ""),
            CertFile = cfg?.CertFile ?? ""
        };

        connector.Adapter.InnerAdapters.Add(adapter);
        Log.Information("Rithmic adapter configured successfully");
#else
        throw new NotSupportedException(
            "Rithmic support requires StockSharp.Rithmic NuGet package. " +
            "Install with: dotnet add package StockSharp.Rithmic");
#endif
    }

    /// <summary>
    /// Configure IQFeed adapter for equities and options data.
    /// IQFeed provides comprehensive US equities data with historical lookups.
    /// </summary>
    private static void ConfigureIQFeed(Connector connector, IQFeedConfig? cfg)
    {
#if STOCKSHARP_IQFEED
        Log.Debug("Configuring IQFeed adapter: Host={Host}, L1Port={L1Port}",
            cfg?.Host ?? "127.0.0.1", cfg?.Level1Port ?? 9100);

        var adapter = new StockSharp.IQFeed.IQFeedMessageAdapter(
            connector.TransactionIdGenerator)
        {
            Level1Address = new IPEndPoint(
                IPAddress.Parse(cfg?.Host ?? "127.0.0.1"),
                cfg?.Level1Port ?? 9100),
            Level2Address = new IPEndPoint(
                IPAddress.Parse(cfg?.Host ?? "127.0.0.1"),
                cfg?.Level2Port ?? 9200),
            LookupAddress = new IPEndPoint(
                IPAddress.Parse(cfg?.Host ?? "127.0.0.1"),
                cfg?.LookupPort ?? 9300)
        };

        connector.Adapter.InnerAdapters.Add(adapter);
        Log.Information("IQFeed adapter configured successfully");
#else
        throw new NotSupportedException(
            "IQFeed support requires StockSharp.IQFeed NuGet package. " +
            "Install with: dotnet add package StockSharp.IQFeed");
#endif
    }

    /// <summary>
    /// Configure CQG adapter for futures and options data.
    /// CQG provides excellent historical data coverage for futures markets.
    /// </summary>
    private static void ConfigureCQG(Connector connector, CQGConfig? cfg)
    {
#if STOCKSHARP_CQG
        Log.Debug("Configuring CQG adapter: User={User}, DemoServer={Demo}",
            cfg?.UserName ?? "(not set)", cfg?.UseDemoServer ?? true);

        var adapter = new StockSharp.Cqg.Com.CqgComMessageAdapter(
            connector.TransactionIdGenerator)
        {
            UserName = cfg?.UserName ?? "",
            Password = ToSecureString(cfg?.Password ?? "")
        };

        connector.Adapter.InnerAdapters.Add(adapter);
        Log.Information("CQG adapter configured successfully");
#else
        throw new NotSupportedException(
            "CQG support requires StockSharp.Cqg.Com NuGet package. " +
            "Install with: dotnet add package StockSharp.Cqg.Com");
#endif
    }

    /// <summary>
    /// Configure Interactive Brokers adapter.
    /// IB provides global multi-asset coverage through TWS/Gateway.
    /// </summary>
    private static void ConfigureInteractiveBrokers(Connector connector, StockSharpIBConfig? cfg)
    {
#if STOCKSHARP_INTERACTIVEBROKERS
        Log.Debug("Configuring Interactive Brokers adapter: Host={Host}, Port={Port}, ClientId={ClientId}",
            cfg?.Host ?? "127.0.0.1", cfg?.Port ?? 7496, cfg?.ClientId ?? 1);

        var adapter = new StockSharp.InteractiveBrokers.InteractiveBrokersMessageAdapter(
            connector.TransactionIdGenerator)
        {
            Address = new IPEndPoint(
                IPAddress.Parse(cfg?.Host ?? "127.0.0.1"),
                cfg?.Port ?? 7496),
            ClientId = cfg?.ClientId ?? 1
        };

        connector.Adapter.InnerAdapters.Add(adapter);
        Log.Information("Interactive Brokers adapter configured successfully");
#else
        throw new NotSupportedException(
            "Interactive Brokers support requires StockSharp.InteractiveBrokers NuGet package. " +
            "Install with: dotnet add package StockSharp.InteractiveBrokers");
#endif
    }

    /// <summary>
    /// Convert plain text password to SecureString.
    /// </summary>
    private static SecureString ToSecureString(string value)
    {
        var secure = new SecureString();
        foreach (var c in value)
            secure.AppendChar(c);
        secure.MakeReadOnly();
        return secure;
    }

#else
    // Stub implementation when StockSharp is not available

    /// <summary>
    /// Stub: StockSharp packages not installed.
    /// </summary>
    public static object Create(StockSharpConfig config)
    {
        throw new NotSupportedException(
            "StockSharp integration requires StockSharp.Algo NuGet package. " +
            "Install with: dotnet add package StockSharp.Algo");
    }
#endif

    /// <summary>
    /// Get a list of all supported connector types.
    /// </summary>
    public static IReadOnlyList<string> SupportedConnectorTypes => new[]
    {
        "Rithmic",      // Futures (CME, NYMEX, COMEX, CBOT)
        "IQFeed",       // US Equities, Options
        "CQG",          // Futures, Options
        "InteractiveBrokers" // Global multi-asset
    };

    /// <summary>
    /// Check if a connector type is supported.
    /// </summary>
    public static bool IsSupported(string connectorType)
    {
        return SupportedConnectorTypes.Any(c =>
            c.Equals(connectorType, StringComparison.OrdinalIgnoreCase));
    }
}
