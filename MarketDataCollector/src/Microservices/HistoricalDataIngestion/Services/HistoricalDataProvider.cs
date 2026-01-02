using DataIngestion.Contracts.Messages;
using DataIngestion.HistoricalService.Configuration;
using Serilog;

namespace DataIngestion.HistoricalService.Services;

public interface IHistoricalDataProvider
{
    IAsyncEnumerable<HistoricalRecord> FetchDataAsync(
        string symbol, HistoricalDataType dataType,
        DateTimeOffset startDate, DateTimeOffset endDate,
        BarTimeframe? timeframe, CancellationToken ct);
}

public record HistoricalRecord(
    DateTimeOffset Timestamp,
    string Symbol,
    string RecordType,
    object Data
);

public sealed class CompositeHistoricalDataProvider : IHistoricalDataProvider
{
    private readonly HistoricalServiceConfig _config;
    private readonly ILogger _log = Log.ForContext<CompositeHistoricalDataProvider>();

    public CompositeHistoricalDataProvider(HistoricalServiceConfig config)
    {
        _config = config;
    }

    public async IAsyncEnumerable<HistoricalRecord> FetchDataAsync(
        string symbol, HistoricalDataType dataType,
        DateTimeOffset startDate, DateTimeOffset endDate,
        BarTimeframe? timeframe,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        _log.Information("Fetching historical {DataType} for {Symbol} from {Start} to {End}",
            dataType, symbol, startDate, endDate);

        // Simulate fetching historical data
        var current = startDate;
        var recordCount = 0;

        while (current < endDate && !ct.IsCancellationRequested)
        {
            // Generate sample data based on type
            object data = dataType switch
            {
                HistoricalDataType.Trades => new
                {
                    Price = 100m + (decimal)(Random.Shared.NextDouble() * 10),
                    Size = Random.Shared.Next(1, 1000),
                    Side = Random.Shared.Next(2) == 0 ? "buy" : "sell"
                },
                HistoricalDataType.OHLCV => new OhlcvBar(
                    current,
                    100m + (decimal)Random.Shared.NextDouble(),
                    105m + (decimal)Random.Shared.NextDouble(),
                    95m + (decimal)Random.Shared.NextDouble(),
                    102m + (decimal)Random.Shared.NextDouble(),
                    Random.Shared.Next(10000, 100000)
                ),
                _ => new { Value = Random.Shared.NextDouble() }
            };

            yield return new HistoricalRecord(current, symbol, dataType.ToString(), data);

            recordCount++;
            current = current.AddMinutes(timeframe switch
            {
                BarTimeframe.Minute => 1,
                BarTimeframe.FiveMinutes => 5,
                BarTimeframe.Hour => 60,
                BarTimeframe.Day => 1440,
                _ => 1
            });

            // Simulate rate limiting
            if (recordCount % 100 == 0)
                await Task.Delay(10, ct);
        }

        _log.Information("Fetched {Count} historical records for {Symbol}", recordCount, symbol);
    }
}
