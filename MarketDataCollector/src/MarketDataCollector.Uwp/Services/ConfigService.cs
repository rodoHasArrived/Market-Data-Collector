using System.Text.Json;
using MarketDataCollector.Uwp.Models;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for managing application configuration.
/// </summary>
public class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string ConfigPath { get; }

    public ConfigService()
    {
        // Look for config in app directory or parent directories
        var baseDir = AppContext.BaseDirectory;
        var configName = "appsettings.json";

        // Try multiple locations
        var paths = new[]
        {
            Path.Combine(baseDir, configName),
            Path.Combine(baseDir, "..", configName),
            Path.Combine(baseDir, "..", "..", configName),
            Path.Combine(baseDir, "..", "..", "..", "..", configName)
        };

        ConfigPath = paths.FirstOrDefault(File.Exists) ?? paths[0];
    }

    public async Task<AppConfig?> LoadConfigAsync()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                return new AppConfig();
            }

            var json = await File.ReadAllTextAsync(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public async Task SaveConfigAsync(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        var directory = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        await File.WriteAllTextAsync(ConfigPath, json);
    }

    public async Task SaveDataSourceAsync(string dataSource)
    {
        var config = await LoadConfigAsync() ?? new AppConfig();
        config.DataSource = dataSource;
        await SaveConfigAsync(config);
    }

    public async Task SaveAlpacaOptionsAsync(AlpacaOptions options)
    {
        var config = await LoadConfigAsync() ?? new AppConfig();
        config.Alpaca = options;
        await SaveConfigAsync(config);
    }

    public async Task SaveStorageConfigAsync(string dataRoot, bool compress, StorageConfig storage)
    {
        var config = await LoadConfigAsync() ?? new AppConfig();
        config.DataRoot = dataRoot;
        config.Compress = compress;
        config.Storage = storage;
        await SaveConfigAsync(config);
    }

    public async Task AddOrUpdateSymbolAsync(SymbolConfig symbol)
    {
        var config = await LoadConfigAsync() ?? new AppConfig();
        var symbols = config.Symbols?.ToList() ?? new List<SymbolConfig>();

        var existingIndex = symbols.FindIndex(s =>
            string.Equals(s.Symbol, symbol.Symbol, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
        {
            symbols[existingIndex] = symbol;
        }
        else
        {
            symbols.Add(symbol);
        }

        config.Symbols = symbols.ToArray();
        await SaveConfigAsync(config);
    }

    public async Task DeleteSymbolAsync(string symbol)
    {
        var config = await LoadConfigAsync() ?? new AppConfig();
        var symbols = config.Symbols?.ToList() ?? new List<SymbolConfig>();

        symbols.RemoveAll(s =>
            string.Equals(s.Symbol, symbol, StringComparison.OrdinalIgnoreCase));

        config.Symbols = symbols.ToArray();
        await SaveConfigAsync(config);
    }
}
