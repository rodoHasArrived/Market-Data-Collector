namespace MarketDataCollector.Infrastructure.Plugins.Core;

/// <summary>
/// Configuration interface for plugins.
/// Provides type-safe access to settings from environment variables or configuration files.
/// </summary>
/// <remarks>
/// Design note: This replaces the complex nested appsettings.json configuration
/// with a simpler, environment-variable-first approach. Plugins declare what
/// configuration they need, and the host provides it.
///
/// Environment variable naming convention:
/// - PLUGIN_ID__SETTING_NAME (double underscore for nesting)
/// - Example: ALPACA__KEY_ID, ALPACA__SECRET_KEY
/// </remarks>
public interface IPluginConfig
{
    /// <summary>
    /// Gets a required configuration value.
    /// Throws if the value is not found.
    /// </summary>
    /// <param name="key">Configuration key.</param>
    /// <returns>Configuration value.</returns>
    /// <exception cref="KeyNotFoundException">If the key is not found.</exception>
    string GetRequired(string key);

    /// <summary>
    /// Gets an optional configuration value.
    /// </summary>
    /// <param name="key">Configuration key.</param>
    /// <param name="defaultValue">Default value if not found.</param>
    /// <returns>Configuration value or default.</returns>
    string? Get(string key, string? defaultValue = null);

    /// <summary>
    /// Gets a typed configuration value.
    /// </summary>
    /// <typeparam name="T">Target type.</typeparam>
    /// <param name="key">Configuration key.</param>
    /// <param name="defaultValue">Default value if not found or conversion fails.</param>
    /// <returns>Typed configuration value.</returns>
    T Get<T>(string key, T defaultValue = default!) where T : IParsable<T>;

    /// <summary>
    /// Checks if a configuration key exists.
    /// </summary>
    /// <param name="key">Configuration key.</param>
    /// <returns>True if the key exists.</returns>
    bool HasKey(string key);

    /// <summary>
    /// Gets all configuration keys for this plugin.
    /// </summary>
    IEnumerable<string> GetKeys();

    /// <summary>
    /// Binds configuration to a strongly-typed options object.
    /// </summary>
    /// <typeparam name="TOptions">Options type with settable properties.</typeparam>
    /// <returns>Populated options object.</returns>
    TOptions Bind<TOptions>() where TOptions : new();
}

/// <summary>
/// Simple implementation of IPluginConfig backed by a dictionary.
/// Typically populated from environment variables.
/// </summary>
public sealed class PluginConfig : IPluginConfig
{
    private readonly IReadOnlyDictionary<string, string> _values;
    private readonly string _prefix;

    /// <summary>
    /// Creates a plugin config from a dictionary of values.
    /// </summary>
    public PluginConfig(IReadOnlyDictionary<string, string> values, string prefix = "")
    {
        _values = values;
        _prefix = prefix;
    }

    /// <summary>
    /// Creates a plugin config from environment variables with a given prefix.
    /// </summary>
    /// <param name="prefix">Environment variable prefix (e.g., "ALPACA").</param>
    public static PluginConfig FromEnvironment(string prefix)
    {
        var envPrefix = prefix.ToUpperInvariant().Replace("-", "_") + "__";
        var values = Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .Where(e => e.Key.ToString()?.StartsWith(envPrefix, StringComparison.OrdinalIgnoreCase) == true)
            .ToDictionary(
                e => e.Key.ToString()![envPrefix.Length..].ToLowerInvariant(),
                e => e.Value?.ToString() ?? "",
                StringComparer.OrdinalIgnoreCase);

        return new PluginConfig(values, prefix);
    }

    /// <summary>
    /// Creates an empty config.
    /// </summary>
    public static PluginConfig Empty => new(new Dictionary<string, string>());

    public string GetRequired(string key)
    {
        if (_values.TryGetValue(NormalizeKey(key), out var value))
            return value;

        throw new KeyNotFoundException(
            $"Required configuration '{_prefix}__{key}' not found. " +
            $"Set environment variable {_prefix.ToUpperInvariant()}__{key.ToUpperInvariant()} or add to configuration.");
    }

    public string? Get(string key, string? defaultValue = null) =>
        _values.TryGetValue(NormalizeKey(key), out var value) ? value : defaultValue;

    public T Get<T>(string key, T defaultValue = default!) where T : IParsable<T>
    {
        var stringValue = Get(key);
        if (stringValue is null)
            return defaultValue;

        if (T.TryParse(stringValue, null, out var result))
            return result;

        return defaultValue;
    }

    public bool HasKey(string key) => _values.ContainsKey(NormalizeKey(key));

    public IEnumerable<string> GetKeys() => _values.Keys;

    public TOptions Bind<TOptions>() where TOptions : new()
    {
        var options = new TOptions();
        var properties = typeof(TOptions).GetProperties()
            .Where(p => p.CanWrite);

        foreach (var prop in properties)
        {
            var key = ToSnakeCase(prop.Name);
            if (_values.TryGetValue(key, out var value))
            {
                try
                {
                    var convertedValue = ConvertValue(value, prop.PropertyType);
                    prop.SetValue(options, convertedValue);
                }
                catch
                {
                    // Skip properties that can't be converted
                }
            }
        }

        return options;
    }

    private static string NormalizeKey(string key) =>
        key.ToLowerInvariant().Replace("-", "_");

    private static string ToSnakeCase(string name)
    {
        var result = new System.Text.StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0)
                result.Append('_');
            result.Append(char.ToLowerInvariant(c));
        }
        return result.ToString();
    }

    private static object? ConvertValue(string value, Type targetType)
    {
        if (targetType == typeof(string))
            return value;

        if (targetType == typeof(bool))
            return bool.Parse(value);

        if (targetType == typeof(int))
            return int.Parse(value);

        if (targetType == typeof(long))
            return long.Parse(value);

        if (targetType == typeof(double))
            return double.Parse(value);

        if (targetType == typeof(decimal))
            return decimal.Parse(value);

        if (targetType == typeof(TimeSpan))
            return TimeSpan.Parse(value);

        if (targetType.IsEnum)
            return Enum.Parse(targetType, value, ignoreCase: true);

        return Convert.ChangeType(value, targetType);
    }
}

/// <summary>
/// Builder for creating plugin configurations.
/// </summary>
public sealed class PluginConfigBuilder
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _prefix;

    public PluginConfigBuilder(string prefix = "")
    {
        _prefix = prefix;
    }

    /// <summary>
    /// Adds a configuration value.
    /// </summary>
    public PluginConfigBuilder Add(string key, string value)
    {
        _values[key.ToLowerInvariant()] = value;
        return this;
    }

    /// <summary>
    /// Adds configuration from environment variables.
    /// </summary>
    public PluginConfigBuilder AddEnvironment()
    {
        var envPrefix = _prefix.ToUpperInvariant().Replace("-", "_") + "__";
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var key = entry.Key.ToString();
            if (key?.StartsWith(envPrefix, StringComparison.OrdinalIgnoreCase) == true)
            {
                var normalizedKey = key[envPrefix.Length..].ToLowerInvariant();
                _values[normalizedKey] = entry.Value?.ToString() ?? "";
            }
        }
        return this;
    }

    /// <summary>
    /// Adds configuration from a dictionary.
    /// </summary>
    public PluginConfigBuilder AddDictionary(IReadOnlyDictionary<string, string> values)
    {
        foreach (var (key, value) in values)
        {
            _values[key.ToLowerInvariant()] = value;
        }
        return this;
    }

    /// <summary>
    /// Builds the configuration.
    /// </summary>
    public IPluginConfig Build() => new PluginConfig(_values, _prefix);
}
