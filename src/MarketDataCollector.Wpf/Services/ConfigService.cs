using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Wpf.Services;

public sealed class ConfigService : IConfigService
{
    private readonly ILoggingService _logger;
    private readonly string _configFilePath;
    private Dictionary<string, JsonElement> _config = new();

    public ConfigService(ILoggingService logger)
    {
        _logger = logger;
        _configFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MarketDataCollector",
            "appsettings.json");
        
        _logger.Log($"Config file path: {_configFilePath}");
    }

    public async Task LoadConfigurationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                _logger.Log("Loading configuration...");
                var json = await File.ReadAllTextAsync(_configFilePath, cancellationToken);
                var doc = JsonDocument.Parse(json);
                
                _config = doc.RootElement.EnumerateObject()
                    .ToDictionary(p => p.Name, p => p.Value.Clone());
                
                _logger.Log($"Configuration loaded: {_config.Count} keys");
            }
            else
            {
                _logger.Log("Configuration file not found, using defaults");
                _config = new Dictionary<string, JsonElement>();
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to load configuration: {ex.Message}");
            _config = new Dictionary<string, JsonElement>();
        }
    }

    public async Task SaveConfigurationAsync(string key, object value, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Log($"Saving configuration key: {key}");
            
            // Update in-memory config
            var json = JsonSerializer.Serialize(value);
            var element = JsonDocument.Parse(json).RootElement.Clone();
            _config[key] = element;
            
            // Ensure directory exists
            var directory = Path.GetDirectoryName(_configFilePath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // Save to file
            var configJson = JsonSerializer.Serialize(_config, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            await File.WriteAllTextAsync(_configFilePath, configJson, cancellationToken);
            
            _logger.Log("Configuration saved");
        }
        catch (Exception ex)
        {
            _logger.Log($"Failed to save configuration: {ex.Message}");
        }
    }

    public T? GetValue<T>(string key)
    {
        if (_config.TryGetValue(key, out var element))
        {
            try
            {
                return JsonSerializer.Deserialize<T>(element.GetRawText());
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to deserialize config key '{key}': {ex.Message}");
            }
        }
        return default;
    }

    public string? GetString(string key)
    {
        return GetValue<string>(key);
    }

    public int GetInt(string key, int defaultValue = 0)
    {
        return GetValue<int?>(key) ?? defaultValue;
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        return GetValue<bool?>(key) ?? defaultValue;
    }
}
