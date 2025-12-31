namespace IBDataCollector.Application.Config;

// TODO: SECURITY - API credentials should NOT be stored in config files
// Recommendations:
// 1. Use environment variables: Environment.GetEnvironmentVariable("ALPACA_KEY_ID")
// 2. Use a secure vault service (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault)
// 3. Use .NET User Secrets for local development (dotnet user-secrets)
// 4. Ensure appsettings.json with real credentials is in .gitignore
// 5. Never commit __SET_ME__ placeholders with actual values

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
