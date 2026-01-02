using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using DataIngestion.Contracts.Messages;
using DataIngestion.Gateway.Configuration;
using MassTransit;
using Serilog;

namespace DataIngestion.Gateway.Services;

/// <summary>
/// Routes ingestion data to appropriate downstream services via message bus or HTTP.
/// </summary>
public sealed class DataRouter : IDataRouter
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GatewayConfig _config;
    private readonly ILogger _log = Log.ForContext<DataRouter>();
    private readonly MetricsCollector _metrics;

    private readonly ConcurrentDictionary<string, long> _routedByService = new();
    private readonly ConcurrentDictionary<string, long> _routedByDataType = new();
    private long _totalRouted;
    private long _totalFailed;
    private long _totalRoutingTimeMs;

    public DataRouter(
        IPublishEndpoint publishEndpoint,
        IHttpClientFactory httpClientFactory,
        GatewayConfig config,
        MetricsCollector metrics)
    {
        _publishEndpoint = publishEndpoint;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _metrics = metrics;
    }

    public async Task<RoutingResult> RouteAsync(
        IngestionDataType dataType,
        string symbol,
        object data,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var targetService = ResolveTargetService(dataType, symbol);

        try
        {
            _log.Debug("Routing {DataType} for {Symbol} to {Service}", dataType, symbol, targetService);

            // Create message wrapper
            var message = CreateRoutedMessage(dataType, symbol, data);

            // Publish to message bus
            await _publishEndpoint.Publish(message, ct);

            sw.Stop();
            RecordSuccess(targetService, dataType.ToString(), sw.ElapsedMilliseconds);

            return new RoutingResult(
                Success: true,
                TargetService: targetService,
                MessageId: message.MessageId.ToString(),
                RoutingTime: sw.Elapsed
            );
        }
        catch (Exception ex)
        {
            sw.Stop();
            RecordFailure(targetService, dataType.ToString());

            _log.Error(ex, "Failed to route {DataType} for {Symbol} to {Service}",
                dataType, symbol, targetService);

            return new RoutingResult(
                Success: false,
                TargetService: targetService,
                RoutingTime: sw.Elapsed,
                ErrorMessage: ex.Message
            );
        }
    }

    public RoutingStatistics GetStatistics()
    {
        var avgRoutingTime = _totalRouted > 0
            ? (double)_totalRoutingTimeMs / _totalRouted
            : 0;

        return new RoutingStatistics(
            TotalRouted: Interlocked.Read(ref _totalRouted),
            TotalFailed: Interlocked.Read(ref _totalFailed),
            RoutedByService: new Dictionary<string, long>(_routedByService),
            RoutedByDataType: new Dictionary<string, long>(_routedByDataType),
            AverageRoutingTimeMs: avgRoutingTime
        );
    }

    private string ResolveTargetService(IngestionDataType dataType, string symbol)
    {
        // Check custom routing rules first
        if (_config.RoutingRules?.Count > 0)
        {
            var matchingRule = _config.RoutingRules
                .Where(r => r.Enabled)
                .Where(r => r.DataType == dataType.ToString() || r.DataType == "*")
                .Where(r => string.IsNullOrEmpty(r.SymbolPattern) ||
                           symbol.StartsWith(r.SymbolPattern, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.Priority)
                .FirstOrDefault();

            if (matchingRule != null)
            {
                return matchingRule.TargetService;
            }
        }

        // Default routing by data type
        return dataType switch
        {
            IngestionDataType.Trade or IngestionDataType.HistoricalTrade => "TradeIngestion",
            IngestionDataType.OrderBookSnapshot or IngestionDataType.OrderBookUpdate => "OrderBookIngestion",
            IngestionDataType.Quote => "QuoteIngestion",
            IngestionDataType.HistoricalQuote or IngestionDataType.HistoricalBar => "HistoricalIngestion",
            _ => "TradeIngestion"
        };
    }

    private RoutedIngestionMessage CreateRoutedMessage(
        IngestionDataType dataType,
        string symbol,
        object data)
    {
        return new RoutedIngestionMessage
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Source = "Gateway",
            SchemaVersion = 1,
            Symbol = symbol,
            DataType = dataType,
            Payload = JsonSerializer.Serialize(data),
            RoutedAt = DateTimeOffset.UtcNow
        };
    }

    private void RecordSuccess(string service, string dataType, long routingTimeMs)
    {
        Interlocked.Increment(ref _totalRouted);
        Interlocked.Add(ref _totalRoutingTimeMs, routingTimeMs);
        _routedByService.AddOrUpdate(service, 1, (_, v) => v + 1);
        _routedByDataType.AddOrUpdate(dataType, 1, (_, v) => v + 1);
        _metrics.RecordRouting(service, dataType, true, routingTimeMs);
    }

    private void RecordFailure(string service, string dataType)
    {
        Interlocked.Increment(ref _totalFailed);
        _metrics.RecordRouting(service, dataType, false, 0);
    }
}

/// <summary>
/// Message wrapper for routed ingestion data.
/// </summary>
public class RoutedIngestionMessage : IRouteIngestionData
{
    public Guid MessageId { get; init; }
    public Guid CorrelationId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string Source { get; init; } = string.Empty;
    public int SchemaVersion { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public IngestionDataType DataType { get; init; }
    public string RawPayload => Payload;
    public string Payload { get; init; } = string.Empty;
    public string ContentType => "application/json";
    public string Provider => Source;
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
    public DateTimeOffset RoutedAt { get; init; }
}
