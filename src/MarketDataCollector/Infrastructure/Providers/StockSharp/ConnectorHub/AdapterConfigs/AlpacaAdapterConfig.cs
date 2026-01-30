#if STOCKSHARP
using StockSharp.Messages;
#endif

namespace MarketDataCollector.Infrastructure.Providers.StockSharp.ConnectorHub.AdapterConfigs;

/// <summary>
/// Configuration for Alpaca Markets adapter via StockSharp.
/// Provides US equity and crypto market data with commission-free trading.
/// </summary>
public sealed class AlpacaAdapterConfig : StockSharpAdapterConfigBase
{
    /// <inheritdoc/>
    public override string AdapterId => "alpaca";

    /// <inheritdoc/>
    public override string DisplayName => "Alpaca Markets";

    /// <inheritdoc/>
    public override string Description =>
        "US equity and crypto market data via Alpaca Markets. " +
        "Offers real-time and historical data with free tier available.";

    /// <summary>Alpaca API key ID.</summary>
    public string KeyId { get; init; } = "";

    /// <summary>Alpaca API secret key.</summary>
    public string SecretKey { get; init; } = "";

    /// <summary>Use paper trading environment.</summary>
    public bool UsePaper { get; init; } = true;

    /// <summary>Data feed type (iex or sip).</summary>
    public string Feed { get; init; } = "iex";

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedMarkets { get; init; } = ["US"];

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedAssetClasses { get; init; } =
        ["equity", "etf", "crypto"];

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedExchanges { get; init; } =
        ["NYSE", "NASDAQ", "AMEX", "ARCA", "BATS", "IEX"];

    /// <inheritdoc/>
    public override IReadOnlyList<string> MappedProviderIds { get; init; } =
        ["alpaca", "alpaca-markets", "alpaca-streaming"];

    /// <inheritdoc/>
    public override bool SupportsStreaming { get; init; } = true;

    /// <inheritdoc/>
    public override bool SupportsBackfill { get; init; } = true;

    /// <inheritdoc/>
    public override bool SupportsMarketDepth { get; init; } = false;

#if STOCKSHARP && STOCKSHARP_ALPACA
    /// <inheritdoc/>
    public override IMessageAdapter CreateAdapter(IdGenerator transactionIdGenerator)
    {
        return new StockSharp.Alpaca.AlpacaMessageAdapter(transactionIdGenerator)
        {
            Key = KeyId,
            Secret = SecretKey.ToSecureString(),
            IsDemo = UsePaper
        };
    }
#elif STOCKSHARP
    /// <inheritdoc/>
    public override IMessageAdapter CreateAdapter(IdGenerator transactionIdGenerator)
    {
        throw new NotSupportedException(
            "Alpaca adapter requires StockSharp.Alpaca package. " +
            "Install with: dotnet add package StockSharp.Alpaca");
    }
#endif

    /// <inheritdoc/>
    public override AdapterValidationResult Validate()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (IsNullOrEmpty(KeyId))
            errors.Add("Alpaca Key ID is required");

        if (IsNullOrEmpty(SecretKey))
            errors.Add("Alpaca Secret Key is required");

        if (UsePaper)
            warnings.Add("Using paper trading environment - data may be delayed");

        if (Feed == "iex")
            warnings.Add("Using IEX feed - only covers ~50% of US volume. Consider SIP for full coverage.");

        return errors.Count > 0
            ? AdapterValidationResult.WithErrors(errors.ToArray())
            : warnings.Count > 0
                ? AdapterValidationResult.WithWarnings(warnings.ToArray())
                : AdapterValidationResult.Success();
    }
}

#if STOCKSHARP
internal static class SecureStringExtensions
{
    public static System.Security.SecureString ToSecureString(this string value)
    {
        var secure = new System.Security.SecureString();
        foreach (var c in value)
            secure.AppendChar(c);
        secure.MakeReadOnly();
        return secure;
    }
}
#endif
