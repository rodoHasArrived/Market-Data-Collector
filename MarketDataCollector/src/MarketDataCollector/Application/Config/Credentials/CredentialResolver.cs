using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Application.Config.Credentials;

public sealed class CredentialResolver
{
    private readonly IReadOnlyList<IAlpacaCredentialSource> _alpacaSources;
    private readonly ILogger _log;

    public CredentialResolver(IEnumerable<IAlpacaCredentialSource>? alpacaSources = null, ILogger? log = null)
    {
        _alpacaSources = (alpacaSources ?? Array.Empty<IAlpacaCredentialSource>()).ToArray();
        _log = log ?? LoggingSetup.ForContext<CredentialResolver>();
    }

    public AlpacaOptions ResolveAlpaca(AppConfig cfg)
    {
        if (cfg.Alpaca is null)
            throw new InvalidOperationException("Alpaca configuration is required when DataSource=Alpaca.");

        foreach (var source in _alpacaSources)
        {
            if (source.TryResolve(cfg.Alpaca, out var credentials))
            {
                _log.Information("Resolved Alpaca credentials via {Source}", source.Name);
                return cfg.Alpaca with
                {
                    KeyId = credentials.KeyId,
                    SecretKey = credentials.SecretKey
                };
            }
        }

        if (!string.IsNullOrWhiteSpace(cfg.Alpaca.KeyId) && !string.IsNullOrWhiteSpace(cfg.Alpaca.SecretKey))
        {
            _log.Information("Using Alpaca credentials from configuration file");
            return cfg.Alpaca;
        }

        throw new InvalidOperationException(
            "Missing Alpaca credentials. Supply ALPACA_KEY_ID/ALPACA_SECRET_KEY, set MDC_ALPACA_CREDENTIALS_PATH, or add keys to appsettings.json.");
    }
}
