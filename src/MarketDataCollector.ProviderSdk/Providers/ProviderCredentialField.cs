namespace MarketDataCollector.ProviderSdk.Providers;

/// <summary>
/// Describes a credential field required by a provider.
/// Used for UI form generation and environment variable documentation.
/// </summary>
/// <param name="Name">Internal field name (e.g., "ApiKey", "SecretKey").</param>
/// <param name="EnvironmentVariable">Environment variable name (e.g., "ALPACA__KEYID").</param>
/// <param name="DisplayName">Human-readable label for UI display.</param>
/// <param name="Required">Whether this field is required for the provider to function.</param>
/// <param name="IsSensitive">Whether this field contains sensitive data (masked in UI).</param>
public sealed record ProviderCredentialField(
    string Name,
    string? EnvironmentVariable,
    string DisplayName,
    bool Required,
    bool IsSensitive = true);
