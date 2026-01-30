#if STOCKSHARP
using StockSharp.Messages;
using System.ComponentModel;
using System.Globalization;
#endif

namespace MarketDataCollector.Infrastructure.Providers.StockSharp.ConnectorHub.AdapterConfigs;

/// <summary>
/// Configuration for custom/dynamic StockSharp adapters.
/// Allows loading any StockSharp adapter by type name with configurable properties.
/// Supports 80+ additional exchanges not covered by built-in configs.
/// </summary>
public sealed class CustomAdapterConfig : StockSharpAdapterConfigBase
{
    private readonly string _adapterId;
    private readonly string _displayName;
    private readonly string _description;

    /// <summary>
    /// Creates a new custom adapter configuration.
    /// </summary>
    /// <param name="adapterId">Unique adapter identifier.</param>
    /// <param name="adapterTypeName">Fully qualified or simple adapter type name.</param>
    /// <param name="displayName">Optional display name (defaults to adapter ID).</param>
    /// <param name="description">Optional description.</param>
    public CustomAdapterConfig(
        string adapterId,
        string adapterTypeName,
        string? displayName = null,
        string? description = null)
    {
        _adapterId = adapterId ?? throw new ArgumentNullException(nameof(adapterId));
        AdapterTypeName = adapterTypeName ?? throw new ArgumentNullException(nameof(adapterTypeName));
        _displayName = displayName ?? adapterId;
        _description = description ?? $"Custom StockSharp adapter: {adapterTypeName}";
    }

    /// <inheritdoc/>
    public override string AdapterId => _adapterId;

    /// <inheritdoc/>
    public override string DisplayName => _displayName;

    /// <inheritdoc/>
    public override string Description => _description;

    /// <summary>
    /// Fully qualified adapter type name (e.g., "StockSharp.LMAX.LmaxMessageAdapter").
    /// </summary>
    public string AdapterTypeName { get; }

    /// <summary>
    /// Optional assembly name if the type is not assembly-qualified.
    /// </summary>
    public string? AdapterAssembly { get; init; }

    /// <summary>
    /// Dictionary of adapter property settings to apply after creation.
    /// Keys are property names, values are string representations to be converted.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Properties { get; init; }

#if STOCKSHARP
    /// <inheritdoc/>
    public override IMessageAdapter CreateAdapter(IdGenerator transactionIdGenerator)
    {
        var resolvedTypeName = ResolveTypeName();
        var adapterType = Type.GetType(resolvedTypeName, throwOnError: false);

        if (adapterType == null)
        {
            throw new NotSupportedException(
                $"Unable to load StockSharp adapter '{resolvedTypeName}'. " +
                "Ensure the connector package is installed and the type name is correct.");
        }

        object? adapterInstance = null;

        // Try to create with IdGenerator constructor first
        try
        {
            adapterInstance = Activator.CreateInstance(adapterType, transactionIdGenerator);
        }
        catch (MissingMethodException)
        {
            // Fall back to parameterless constructor
            adapterInstance = Activator.CreateInstance(adapterType);
        }

        if (adapterInstance is not IMessageAdapter adapter)
        {
            throw new NotSupportedException(
                $"Adapter type '{resolvedTypeName}' does not implement IMessageAdapter.");
        }

        // Apply property settings
        ApplySettings(adapter);

        return adapter;
    }

    private string ResolveTypeName()
    {
        if (AdapterTypeName.Contains(','))
            return AdapterTypeName;

        if (string.IsNullOrWhiteSpace(AdapterAssembly))
            return AdapterTypeName;

        return $"{AdapterTypeName}, {AdapterAssembly}";
    }

    private void ApplySettings(IMessageAdapter adapter)
    {
        if (Properties == null) return;

        foreach (var (key, value) in Properties)
        {
            var property = adapter.GetType().GetProperty(key);
            if (property == null || !property.CanWrite)
                continue;

            try
            {
                var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                object? convertedValue;

                if (targetType.IsEnum)
                {
                    convertedValue = Enum.Parse(targetType, value, ignoreCase: true);
                }
                else if (targetType == typeof(TimeSpan))
                {
                    convertedValue = TimeSpan.Parse(value, CultureInfo.InvariantCulture);
                }
                else if (targetType == typeof(System.Security.SecureString))
                {
                    convertedValue = value.ToSecureString();
                }
                else
                {
                    var converter = TypeDescriptor.GetConverter(targetType);
                    convertedValue = converter.ConvertFromInvariantString(value);
                }

                property.SetValue(adapter, convertedValue);
            }
            catch
            {
                // Log warning but continue - some properties may fail to set
            }
        }
    }
#endif

    /// <inheritdoc/>
    public override AdapterValidationResult Validate()
    {
        var errors = new List<string>();

        if (IsNullOrEmpty(AdapterTypeName))
            errors.Add("Adapter type name is required");

        return errors.Count > 0
            ? AdapterValidationResult.WithErrors(errors.ToArray())
            : AdapterValidationResult.Success();
    }

    /// <summary>
    /// Creates a custom config for a well-known StockSharp adapter.
    /// </summary>
    public static CustomAdapterConfig ForStockSharpAdapter(
        string adapterId,
        string adapterClassName,
        string packageName,
        string? displayName = null,
        string? description = null,
        IReadOnlyDictionary<string, string>? properties = null)
    {
        return new CustomAdapterConfig(
            adapterId,
            $"StockSharp.{packageName}.{adapterClassName}MessageAdapter",
            displayName ?? adapterId,
            description ?? $"{displayName ?? adapterId} market data via StockSharp")
        {
            AdapterAssembly = $"StockSharp.{packageName}",
            Properties = properties
        };
    }
}

/// <summary>
/// Factory methods for creating common custom adapter configurations.
/// </summary>
public static class CustomAdapterConfigs
{
    /// <summary>LMAX Exchange adapter for FX.</summary>
    public static CustomAdapterConfig LMAX(string apiKey, string apiSecret) =>
        CustomAdapterConfig.ForStockSharpAdapter(
            "lmax", "Lmax", "LMAX",
            "LMAX Exchange",
            "FX and CFD data via LMAX Exchange - institutional-grade liquidity",
            new Dictionary<string, string>
            {
                ["Login"] = apiKey,
                ["Password"] = apiSecret
            });

    /// <summary>Coinbase adapter for crypto.</summary>
    public static CustomAdapterConfig Coinbase(string apiKey, string apiSecret) =>
        CustomAdapterConfig.ForStockSharpAdapter(
            "coinbase", "Coinbase", "Coinbase",
            "Coinbase",
            "Cryptocurrency data via Coinbase - US-regulated crypto exchange",
            new Dictionary<string, string>
            {
                ["Key"] = apiKey,
                ["Secret"] = apiSecret
            });

    /// <summary>Kraken adapter for crypto.</summary>
    public static CustomAdapterConfig Kraken(string apiKey, string apiSecret) =>
        CustomAdapterConfig.ForStockSharpAdapter(
            "kraken", "Kraken", "Kraken",
            "Kraken",
            "Cryptocurrency data via Kraken - established crypto exchange",
            new Dictionary<string, string>
            {
                ["Key"] = apiKey,
                ["Secret"] = apiSecret
            });

    /// <summary>Bitfinex adapter for crypto.</summary>
    public static CustomAdapterConfig Bitfinex(string apiKey, string apiSecret) =>
        CustomAdapterConfig.ForStockSharpAdapter(
            "bitfinex", "Bitfinex", "Bitfinex",
            "Bitfinex",
            "Cryptocurrency data via Bitfinex - advanced crypto exchange",
            new Dictionary<string, string>
            {
                ["Key"] = apiKey,
                ["Secret"] = apiSecret
            });

    /// <summary>FTX adapter for crypto (if still operational).</summary>
    public static CustomAdapterConfig FTX(string apiKey, string apiSecret) =>
        CustomAdapterConfig.ForStockSharpAdapter(
            "ftx", "Ftx", "FTX",
            "FTX",
            "Cryptocurrency data via FTX exchange",
            new Dictionary<string, string>
            {
                ["Key"] = apiKey,
                ["Secret"] = apiSecret
            });

    /// <summary>CQG adapter for futures.</summary>
    public static CustomAdapterConfig CQG(string userName, string password) =>
        CustomAdapterConfig.ForStockSharpAdapter(
            "cqg", "CqgCom", "Cqg.Com",
            "CQG",
            "Futures and options data via CQG - professional futures platform",
            new Dictionary<string, string>
            {
                ["UserName"] = userName,
                ["Password"] = password
            });

    /// <summary>Sterling adapter for US equities.</summary>
    public static CustomAdapterConfig Sterling(string userName, string password) =>
        CustomAdapterConfig.ForStockSharpAdapter(
            "sterling", "Sterling", "Sterling",
            "Sterling Trader",
            "US equity data via Sterling Trader - professional trading platform",
            new Dictionary<string, string>
            {
                ["Login"] = userName,
                ["Password"] = password
            });
}
