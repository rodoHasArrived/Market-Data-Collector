namespace IBDataCollector.Application.Config;

/// <summary>
/// Alpaca Market Data configuration.
/// Docs: WebSocket stream + authentication. Uses Trading API keys with message auth.
/// </summary>
public sealed record AlpacaOptions(
    string KeyId,
    string SecretKey,
    string Feed = "iex",            // v2/{feed}: iex, sip, delayed_sip
    bool UseSandbox = false,        // stream.data.sandbox.alpaca.markets
    bool SubscribeQuotes = false    // if true, subscribes to quotes too (currently not wired to L2 collector)
);
