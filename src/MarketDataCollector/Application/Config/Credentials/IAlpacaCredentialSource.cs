using System.Text.Json;
using MarketDataCollector.Application.Config;

namespace MarketDataCollector.Application.Config.Credentials;

public sealed record AlpacaCredentials(string KeyId, string SecretKey);

public interface IAlpacaCredentialSource
{
    string Name { get; }

    bool TryResolve(AlpacaOptions baseOptions, out AlpacaCredentials credentials);
}

public sealed class EnvironmentAlpacaCredentialSource : IAlpacaCredentialSource
{
    public string Name => "Environment";

    public bool TryResolve(AlpacaOptions baseOptions, out AlpacaCredentials credentials)
    {
        var key = Environment.GetEnvironmentVariable("ALPACA_KEY_ID");
        var secret = Environment.GetEnvironmentVariable("ALPACA_SECRET_KEY");

        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret))
        {
            credentials = default!;
            return false;
        }

        credentials = new AlpacaCredentials(key.Trim(), secret.Trim());
        return true;
    }
}

public sealed class FileAlpacaCredentialSource : IAlpacaCredentialSource
{
    private readonly string _path;

    public FileAlpacaCredentialSource(string? path = null)
    {
        _path = path ?? Environment.GetEnvironmentVariable("MDC_ALPACA_CREDENTIALS_PATH") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".mdc", "alpaca.json");
    }

    public string Name => "SecretsFile";

    public bool TryResolve(AlpacaOptions baseOptions, out AlpacaCredentials credentials)
    {
        credentials = default!;

        if (!File.Exists(_path))
            return false;

        try
        {
            using var stream = File.OpenRead(_path);
            var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;

            if (!root.TryGetProperty("keyId", out var keyProp) || !root.TryGetProperty("secretKey", out var secretProp))
                return false;

            var key = keyProp.GetString();
            var secret = secretProp.GetString();

            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret))
                return false;

            credentials = new AlpacaCredentials(key.Trim(), secret.Trim());
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class ConfigAlpacaCredentialSource : IAlpacaCredentialSource
{
    public string Name => "Config";

    public bool TryResolve(AlpacaOptions baseOptions, out AlpacaCredentials credentials)
    {
        if (!string.IsNullOrWhiteSpace(baseOptions.KeyId) && !string.IsNullOrWhiteSpace(baseOptions.SecretKey))
        {
            credentials = new AlpacaCredentials(baseOptions.KeyId.Trim(), baseOptions.SecretKey.Trim());
            return true;
        }

        credentials = default!;
        return false;
    }
}
