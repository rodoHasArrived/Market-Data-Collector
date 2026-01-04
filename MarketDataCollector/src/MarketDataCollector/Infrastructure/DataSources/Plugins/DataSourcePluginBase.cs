using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Infrastructure.DataSources.Plugins;

/// <summary>
/// Base class for data source plugins providing common functionality
/// and simplified implementation patterns.
/// </summary>
public abstract class DataSourcePluginBase : DataSourceBase, IDataSourcePlugin
{
    private PluginContext? _context;
    private PluginConfiguration _configuration = new();

    /// <summary>
    /// Gets the plugin context after loading.
    /// </summary>
    protected PluginContext Context => _context
        ?? throw new InvalidOperationException("Plugin has not been loaded. Call OnLoadAsync first.");

    /// <summary>
    /// Gets the current plugin configuration.
    /// </summary>
    protected PluginConfiguration Configuration => _configuration;

    /// <inheritdoc />
    public abstract PluginMetadata PluginInfo { get; }

    /// <summary>
    /// Creates a new DataSourcePluginBase.
    /// </summary>
    protected DataSourcePluginBase(
        DataSourceOptions? options = null,
        ILogger? logger = null)
        : base(options ?? DataSourceOptions.Default, logger)
    {
    }

    /// <inheritdoc />
    public virtual async Task OnLoadAsync(PluginContext context, CancellationToken ct = default)
    {
        _context = context;
        _configuration = context.Configuration;

        Log.Information("Plugin {PluginId} v{Version} loading...",
            PluginInfo.PluginId, PluginInfo.Version);

        await OnPluginLoadAsync(context, ct).ConfigureAwait(false);

        Log.Information("Plugin {PluginId} loaded successfully", PluginInfo.PluginId);
    }

    /// <summary>
    /// Override to perform custom load logic.
    /// </summary>
    protected virtual Task OnPluginLoadAsync(PluginContext context, CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc />
    public virtual async Task OnUnloadAsync(CancellationToken ct = default)
    {
        Log.Information("Plugin {PluginId} unloading...", PluginInfo.PluginId);

        await OnPluginUnloadAsync(ct).ConfigureAwait(false);

        Log.Information("Plugin {PluginId} unloaded", PluginInfo.PluginId);
    }

    /// <summary>
    /// Override to perform custom unload logic.
    /// </summary>
    protected virtual Task OnPluginUnloadAsync(CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc />
    public virtual async Task OnConfigurationChangedAsync(PluginConfiguration newConfig, CancellationToken ct = default)
    {
        var previousConfig = _configuration;
        _configuration = newConfig;

        Log.Information("Plugin {PluginId} configuration changed", PluginInfo.PluginId);

        await OnPluginConfigurationChangedAsync(previousConfig, newConfig, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Override to handle configuration changes.
    /// </summary>
    protected virtual Task OnPluginConfigurationChangedAsync(
        PluginConfiguration previousConfig,
        PluginConfiguration newConfig,
        CancellationToken ct) => Task.CompletedTask;

    /// <summary>
    /// Gets a configuration setting with a default value.
    /// </summary>
    protected T GetSetting<T>(string key, T defaultValue)
    {
        return Configuration.GetSetting(key, defaultValue) ?? defaultValue;
    }

    /// <summary>
    /// Gets the plugin's data directory for storing state.
    /// </summary>
    protected string DataDirectory => _context?.DataDirectory
        ?? Path.Combine(AppContext.BaseDirectory, "plugin-data", PluginInfo.PluginId);

    /// <summary>
    /// Ensures the data directory exists and returns its path.
    /// </summary>
    protected string EnsureDataDirectory()
    {
        var dir = DataDirectory;
        Directory.CreateDirectory(dir);
        return dir;
    }
}

/// <summary>
/// Base class for historical data source plugins.
/// </summary>
public abstract class HistoricalDataSourcePluginBase : DataSourcePluginBase, IHistoricalDataSource
{
    /// <inheritdoc />
    public abstract bool SupportsIntraday { get; }

    /// <inheritdoc />
    public abstract IReadOnlyList<string> SupportedBarIntervals { get; }

    /// <inheritdoc />
    public abstract bool SupportsDividends { get; }

    /// <inheritdoc />
    public abstract bool SupportsSplits { get; }

    /// <summary>
    /// Creates a new HistoricalDataSourcePluginBase.
    /// </summary>
    protected HistoricalDataSourcePluginBase(
        DataSourceOptions? options = null,
        ILogger? logger = null)
        : base(options, logger)
    {
    }

    /// <inheritdoc />
    public abstract Task<IReadOnlyList<Domain.Models.HistoricalBar>> GetDailyBarsAsync(
        string symbol,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default);

    /// <inheritdoc />
    public abstract Task<IReadOnlyList<Infrastructure.Providers.Backfill.AdjustedHistoricalBar>> GetAdjustedDailyBarsAsync(
        string symbol,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default);

    /// <inheritdoc />
    public abstract Task<IReadOnlyList<IntradayBar>> GetIntradayBarsAsync(
        string symbol,
        string interval,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken ct = default);

    /// <inheritdoc />
    public abstract Task<IReadOnlyList<DividendInfo>> GetDividendsAsync(
        string symbol,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default);

    /// <inheritdoc />
    public abstract Task<IReadOnlyList<SplitInfo>> GetSplitsAsync(
        string symbol,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default);
}

/// <summary>
/// Base class for real-time data source plugins.
/// </summary>
public abstract class RealtimeDataSourcePluginBase : DataSourcePluginBase, IRealtimeDataSource
{
    /// <inheritdoc />
    public abstract bool IsConnected { get; }

    /// <inheritdoc />
    public abstract IObservable<RealtimeTrade> Trades { get; }

    /// <inheritdoc />
    public abstract IObservable<RealtimeQuote> Quotes { get; }

    /// <inheritdoc />
    public abstract IObservable<RealtimeDepthUpdate> DepthUpdates { get; }

    /// <inheritdoc />
    public abstract IReadOnlySet<int> ActiveSubscriptions { get; }

    /// <inheritdoc />
    public abstract IReadOnlySet<string> SubscribedSymbols { get; }

    /// <summary>
    /// Creates a new RealtimeDataSourcePluginBase.
    /// </summary>
    protected RealtimeDataSourcePluginBase(
        DataSourceOptions? options = null,
        ILogger? logger = null)
        : base(options, logger)
    {
    }

    /// <inheritdoc />
    public abstract Task ConnectAsync(CancellationToken ct = default);

    /// <inheritdoc />
    public abstract Task DisconnectAsync(CancellationToken ct = default);

    /// <inheritdoc />
    public abstract int SubscribeTrades(Application.Config.SymbolConfig config);

    /// <inheritdoc />
    public abstract void UnsubscribeTrades(int subscriptionId);

    /// <inheritdoc />
    public abstract int SubscribeQuotes(Application.Config.SymbolConfig config);

    /// <inheritdoc />
    public abstract void UnsubscribeQuotes(int subscriptionId);

    /// <inheritdoc />
    public abstract int SubscribeMarketDepth(Application.Config.SymbolConfig config);

    /// <inheritdoc />
    public abstract void UnsubscribeMarketDepth(int subscriptionId);

    /// <inheritdoc />
    public abstract void UnsubscribeAll();
}

/// <summary>
/// Base class for hybrid (real-time and historical) data source plugins.
/// </summary>
public abstract class HybridDataSourcePluginBase : DataSourcePluginBase, IRealtimeDataSource, IHistoricalDataSource
{
    #region IRealtimeDataSource

    /// <inheritdoc />
    public abstract bool IsConnected { get; }

    /// <inheritdoc />
    public abstract IObservable<RealtimeTrade> Trades { get; }

    /// <inheritdoc />
    public abstract IObservable<RealtimeQuote> Quotes { get; }

    /// <inheritdoc />
    public abstract IObservable<RealtimeDepthUpdate> DepthUpdates { get; }

    /// <inheritdoc />
    public abstract IReadOnlySet<int> ActiveSubscriptions { get; }

    /// <inheritdoc />
    public abstract IReadOnlySet<string> SubscribedSymbols { get; }

    /// <inheritdoc />
    public abstract Task ConnectAsync(CancellationToken ct = default);

    /// <inheritdoc />
    public abstract Task DisconnectAsync(CancellationToken ct = default);

    /// <inheritdoc />
    public abstract int SubscribeTrades(Application.Config.SymbolConfig config);

    /// <inheritdoc />
    public abstract void UnsubscribeTrades(int subscriptionId);

    /// <inheritdoc />
    public abstract int SubscribeQuotes(Application.Config.SymbolConfig config);

    /// <inheritdoc />
    public abstract void UnsubscribeQuotes(int subscriptionId);

    /// <inheritdoc />
    public abstract int SubscribeMarketDepth(Application.Config.SymbolConfig config);

    /// <inheritdoc />
    public abstract void UnsubscribeMarketDepth(int subscriptionId);

    /// <inheritdoc />
    public abstract void UnsubscribeAll();

    #endregion

    #region IHistoricalDataSource

    /// <inheritdoc />
    public abstract bool SupportsIntraday { get; }

    /// <inheritdoc />
    public abstract IReadOnlyList<string> SupportedBarIntervals { get; }

    /// <inheritdoc />
    public abstract bool SupportsDividends { get; }

    /// <inheritdoc />
    public abstract bool SupportsSplits { get; }

    /// <inheritdoc />
    public abstract Task<IReadOnlyList<Domain.Models.HistoricalBar>> GetDailyBarsAsync(
        string symbol,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default);

    /// <inheritdoc />
    public abstract Task<IReadOnlyList<Infrastructure.Providers.Backfill.AdjustedHistoricalBar>> GetAdjustedDailyBarsAsync(
        string symbol,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default);

    /// <inheritdoc />
    public abstract Task<IReadOnlyList<IntradayBar>> GetIntradayBarsAsync(
        string symbol,
        string interval,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken ct = default);

    /// <inheritdoc />
    public abstract Task<IReadOnlyList<DividendInfo>> GetDividendsAsync(
        string symbol,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default);

    /// <inheritdoc />
    public abstract Task<IReadOnlyList<SplitInfo>> GetSplitsAsync(
        string symbol,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default);

    #endregion

    /// <summary>
    /// Creates a new HybridDataSourcePluginBase.
    /// </summary>
    protected HybridDataSourcePluginBase(
        DataSourceOptions? options = null,
        ILogger? logger = null)
        : base(options, logger)
    {
    }
}
