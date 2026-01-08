using System.Collections.Concurrent;
using DataIngestion.Contracts.Messages;
using DataIngestion.ValidationService.Configuration;
using MassTransit;
using Serilog;

namespace DataIngestion.ValidationService.Services;

public interface IQualityMetricsAggregator
{
    void RecordValidation(string symbol, string dataType, bool isValid, IEnumerable<ValidationIssue> issues);
    DataQualitySnapshot GetSnapshot(string symbol);
    IEnumerable<DataQualitySnapshot> GetAllSnapshots();
}

public record DataQualitySnapshot(
    string Symbol,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    long TotalRecords,
    long ValidRecords,
    long InvalidRecords,
    double ValidityRate,
    Dictionary<string, int> IssuesByCode
);

public sealed class QualityMetricsAggregator : IQualityMetricsAggregator
{
    private readonly ConcurrentDictionary<string, SymbolQualityState> _symbolStates = new();
    private readonly ValidationServiceConfig _config;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly Serilog.ILogger _log = Log.ForContext<QualityMetricsAggregator>();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastAlerts = new();

    public QualityMetricsAggregator(ValidationServiceConfig config, IPublishEndpoint publishEndpoint)
    {
        _config = config;
        _publishEndpoint = publishEndpoint;
    }

    public void RecordValidation(string symbol, string dataType, bool isValid, IEnumerable<ValidationIssue> issues)
    {
        var state = _symbolStates.GetOrAdd(symbol, _ => new SymbolQualityState(symbol));

        lock (state)
        {
            state.TotalRecords++;
            if (isValid) state.ValidRecords++;
            else state.InvalidRecords++;

            foreach (var issue in issues)
            {
                state.IssuesByCode.AddOrUpdate(issue.Code, 1, (_, v) => v + 1);
            }
        }

        // Check for quality alerts
        if (_config.Alerts.Enabled)
        {
            CheckAlerts(symbol, state);
        }
    }

    public DataQualitySnapshot GetSnapshot(string symbol)
    {
        if (!_symbolStates.TryGetValue(symbol, out var state))
            return new DataQualitySnapshot(symbol, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 0, 0, 0, 1.0, new());

        lock (state)
        {
            return new DataQualitySnapshot(
                symbol,
                state.PeriodStart,
                DateTimeOffset.UtcNow,
                state.TotalRecords,
                state.ValidRecords,
                state.InvalidRecords,
                state.TotalRecords > 0 ? (double)state.ValidRecords / state.TotalRecords : 1.0,
                new Dictionary<string, int>(state.IssuesByCode)
            );
        }
    }

    public IEnumerable<DataQualitySnapshot> GetAllSnapshots()
    {
        return _symbolStates.Keys.Select(GetSnapshot);
    }

    private void CheckAlerts(string symbol, SymbolQualityState state)
    {
        var validityRate = state.TotalRecords > 0 ? (double)state.ValidRecords / state.TotalRecords : 1.0;

        if (validityRate < _config.Alerts.ValidityRateThreshold)
        {
            var lastAlert = _lastAlerts.GetValueOrDefault(symbol, DateTimeOffset.MinValue);
            if ((DateTimeOffset.UtcNow - lastAlert).TotalSeconds >= _config.Alerts.AlertCooldownSeconds)
            {
                _lastAlerts[symbol] = DateTimeOffset.UtcNow;
                _log.Warning("Data quality alert for {Symbol}: validity rate {Rate:P2} below threshold {Threshold:P2}",
                    symbol, validityRate, _config.Alerts.ValidityRateThreshold);

                // Could publish alert message here
            }
        }
    }

    private class SymbolQualityState
    {
        public string Symbol { get; }
        public DateTimeOffset PeriodStart { get; } = DateTimeOffset.UtcNow;
        public long TotalRecords { get; set; }
        public long ValidRecords { get; set; }
        public long InvalidRecords { get; set; }
        public ConcurrentDictionary<string, int> IssuesByCode { get; } = new();

        public SymbolQualityState(string symbol) => Symbol = symbol;
    }
}
