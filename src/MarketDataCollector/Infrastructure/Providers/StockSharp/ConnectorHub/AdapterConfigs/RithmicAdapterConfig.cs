#if STOCKSHARP
using StockSharp.Messages;
using System.Security;
#endif

namespace MarketDataCollector.Infrastructure.Providers.StockSharp.ConnectorHub.AdapterConfigs;

/// <summary>
/// Configuration for Rithmic adapter via StockSharp.
/// Provides low-latency futures market data for CME, NYMEX, COMEX, CBOT, ICE.
/// </summary>
public sealed class RithmicAdapterConfig : StockSharpAdapterConfigBase
{
    /// <inheritdoc/>
    public override string AdapterId => "rithmic";

    /// <inheritdoc/>
    public override string DisplayName => "Rithmic";

    /// <inheritdoc/>
    public override string Description =>
        "Low-latency futures market data via Rithmic. " +
        "Direct market access for CME, NYMEX, COMEX, CBOT, ICE exchanges.";

    /// <summary>Rithmic server environment.</summary>
    public string Server { get; init; } = "Rithmic Test";

    /// <summary>Rithmic account username.</summary>
    public string UserName { get; init; } = "";

    /// <summary>Rithmic account password.</summary>
    public string Password { get; init; } = "";

    /// <summary>SSL certificate file path.</summary>
    public string CertFile { get; init; } = "";

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedMarkets { get; init; } = ["US"];

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedAssetClasses { get; init; } =
        ["futures", "options"];

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedExchanges { get; init; } =
        ["CME", "CBOT", "NYMEX", "COMEX", "ICE", "GLOBEX"];

    /// <inheritdoc/>
    public override IReadOnlyList<string> MappedProviderIds { get; init; } =
        ["rithmic", "r-trader"];

    /// <inheritdoc/>
    public override bool SupportsStreaming { get; init; } = true;

    /// <inheritdoc/>
    public override bool SupportsBackfill { get; init; } = true;

    /// <inheritdoc/>
    public override bool SupportsMarketDepth { get; init; } = true;

    /// <inheritdoc/>
    public override int? MaxDepthLevels { get; init; } = 10;

#if STOCKSHARP && STOCKSHARP_RITHMIC
    /// <inheritdoc/>
    public override IMessageAdapter CreateAdapter(IdGenerator transactionIdGenerator)
    {
        return new StockSharp.Rithmic.RithmicMessageAdapter(transactionIdGenerator)
        {
            Server = Server,
            UserName = UserName,
            Password = ToSecureString(Password),
            CertFile = CertFile
        };
    }

    private static SecureString ToSecureString(string value)
    {
        var secure = new SecureString();
        foreach (var c in value)
            secure.AppendChar(c);
        secure.MakeReadOnly();
        return secure;
    }
#elif STOCKSHARP
    /// <inheritdoc/>
    public override IMessageAdapter CreateAdapter(IdGenerator transactionIdGenerator)
    {
        throw new NotSupportedException(
            "Rithmic adapter requires StockSharp.Rithmic package. " +
            "Install with: dotnet add package StockSharp.Rithmic");
    }
#endif

    /// <inheritdoc/>
    public override AdapterValidationResult Validate()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (IsNullOrEmpty(UserName))
            errors.Add("Rithmic username is required");

        if (IsNullOrEmpty(Password))
            errors.Add("Rithmic password is required");

        if (Server.Contains("Test", StringComparison.OrdinalIgnoreCase))
            warnings.Add("Using Rithmic test server - data may differ from production");

        return errors.Count > 0
            ? AdapterValidationResult.WithErrors(errors.ToArray())
            : warnings.Count > 0
                ? AdapterValidationResult.WithWarnings(warnings.ToArray())
                : AdapterValidationResult.Success();
    }
}
