using System.Text.Json;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Infrastructure.Providers.MultiProvider;

namespace MarketDataCollector.Application.Monitoring;

/// <summary>
/// Periodically writes provider metrics to a JSON file for external dashboards.
/// </summary>
public sealed class ProviderMetricsStatusWriter : IAsyncDisposable
{
    private readonly string _path;
    private readonly IMultiProviderService _multiProviderService;
    private readonly CancellationTokenSource _cts = new();
    private readonly Serilog.ILogger _log = LoggingSetup.ForContext<ProviderMetricsStatusWriter>();
    private Task? _loop;

    public ProviderMetricsStatusWriter(string dataRoot, IMultiProviderService multiProviderService)
    {
        _path = Path.Combine(dataRoot, "_status", "providers.json");
        _multiProviderService = multiProviderService;
    }

    /// <summary>
    /// Starts periodic writing of provider metrics.
    /// </summary>
    public void Start(TimeSpan interval)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _loop = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                await WriteOnceAsync();
                try { await Task.Delay(interval, _cts.Token); }
                catch (TaskCanceledException) { }
            }
        });

        _log.Information("Provider metrics status writer started, writing to {Path}", _path);
    }

    /// <summary>
    /// Writes provider metrics once.
    /// </summary>
    public async Task WriteOnceAsync()
    {
        try
        {
            var comparison = _multiProviderService.GetComparisonMetrics();
            var connectionStatus = _multiProviderService.GetConnectionStatus();
            var healthStates = _multiProviderService.GetHealthStates();

            var providers = comparison.Providers.Select(p => new ProviderMetricsStatusEntry(
                ProviderId: p.ProviderId,
                ProviderType: p.ProviderType.ToString(),
                TradesReceived: p.TradesReceived,
                DepthUpdatesReceived: p.DepthUpdatesReceived,
                QuotesReceived: p.QuotesReceived,
                ConnectionAttempts: p.ConnectionAttempts,
                ConnectionFailures: p.ConnectionFailures,
                MessagesDropped: p.MessagesDropped,
                ActiveSubscriptions: p.ActiveSubscriptions,
                AverageLatencyMs: p.AverageLatencyMs,
                MinLatencyMs: p.MinLatencyMs,
                MaxLatencyMs: p.MaxLatencyMs,
                DataQualityScore: p.DataQualityScore,
                ConnectionSuccessRate: p.ConnectionSuccessRate,
                IsConnected: connectionStatus.TryGetValue(p.ProviderId, out var status) && status.IsConnected,
                IsHealthy: !healthStates.TryGetValue(p.ProviderId, out var health) || health.ConsecutiveFailures == 0,
                Timestamp: p.Timestamp
            )).ToArray();

            var payload = new ProviderMetricsStatus(
                Timestamp: comparison.Timestamp,
                Providers: providers,
                TotalProviders: comparison.TotalProviders,
                HealthyProviders: comparison.HealthyProviders
            );

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await File.WriteAllTextAsync(_path, json);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to write provider metrics status");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Error during ProviderMetricsStatusWriter disposal");
            }
        }
        _cts.Dispose();
    }
}

/// <summary>
/// Status record for provider metrics that can be serialized to JSON.
/// </summary>
public sealed record ProviderMetricsStatus(
    DateTimeOffset Timestamp,
    ProviderMetricsStatusEntry[] Providers,
    int TotalProviders,
    int HealthyProviders
);

/// <summary>
/// Individual provider metrics entry for status file.
/// </summary>
public sealed record ProviderMetricsStatusEntry(
    string ProviderId,
    string ProviderType,
    long TradesReceived,
    long DepthUpdatesReceived,
    long QuotesReceived,
    long ConnectionAttempts,
    long ConnectionFailures,
    long MessagesDropped,
    long ActiveSubscriptions,
    double AverageLatencyMs,
    double MinLatencyMs,
    double MaxLatencyMs,
    double DataQualityScore,
    double ConnectionSuccessRate,
    bool IsConnected,
    bool IsHealthy,
    DateTimeOffset Timestamp
);
