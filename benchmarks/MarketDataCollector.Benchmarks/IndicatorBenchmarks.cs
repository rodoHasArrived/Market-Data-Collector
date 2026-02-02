using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using MarketDataCollector.Application.Indicators;
using MarketDataCollector.Contracts.Domain.Enums;
using MarketDataCollector.Contracts.Domain.Models;
using MarketDataCollector.Domain.Models;

namespace MarketDataCollector.Benchmarks;

/// <summary>
/// Benchmarks for technical indicator calculations.
///
/// Reference: docs/open-source-references.md #25 (Skender.Stock.Indicators)
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class IndicatorBenchmarks
{
    private TechnicalIndicatorService _indicatorService = null!;
    private MarketTradeUpdate[] _trades = null!;
    private HistoricalBar[] _bars = null!;

    [Params(100, 500, 1000)]
    public int DataPoints;

    [GlobalSetup]
    public void Setup()
    {
        _indicatorService = new TechnicalIndicatorService(new IndicatorConfiguration
        {
            EnabledIndicators = new HashSet<IndicatorType>
            {
                IndicatorType.SMA,
                IndicatorType.EMA,
                IndicatorType.RSI,
                IndicatorType.MACD,
                IndicatorType.BollingerBands
            }
        });

        var random = new Random(42);
        var basePrice = 450m;

        // Generate trade updates
        _trades = new MarketTradeUpdate[DataPoints];
        for (int i = 0; i < DataPoints; i++)
        {
            basePrice += (decimal)(random.NextDouble() - 0.5) * 0.5m;
            _trades[i] = new MarketTradeUpdate(
                Timestamp: DateTimeOffset.UtcNow.AddSeconds(i),
                Symbol: "SPY",
                Price: basePrice,
                Size: random.Next(100, 10000),
                Aggressor: random.Next(2) == 0 ? AggressorSide.Buy : AggressorSide.Sell,
                SequenceNumber: i,
                StreamId: "BENCH",
                Venue: "TEST"
            );
        }

        // Generate historical bars
        basePrice = 450m;
        _bars = new HistoricalBar[DataPoints];
        for (int i = 0; i < DataPoints; i++)
        {
            var dayChange = (decimal)(random.NextDouble() - 0.5) * 5m;
            var high = basePrice + (decimal)random.NextDouble() * 2m;
            var low = basePrice - (decimal)random.NextDouble() * 2m;
            var close = basePrice + dayChange;

            _bars[i] = new HistoricalBar(
                Symbol: "SPY",
                SessionDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-DataPoints + i)),
                Open: basePrice,
                High: high,
                Low: low,
                Close: close,
                Volume: random.Next(1000000, 100000000),
                SequenceNumber: i
            );

            basePrice = close;
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _indicatorService = new TechnicalIndicatorService(new IndicatorConfiguration
        {
            EnabledIndicators = new HashSet<IndicatorType>
            {
                IndicatorType.SMA,
                IndicatorType.EMA,
                IndicatorType.RSI,
                IndicatorType.MACD,
                IndicatorType.BollingerBands
            }
        });
    }

    [Benchmark(Baseline = true)]
    public void ProcessTrades_Streaming()
    {
        foreach (var trade in _trades)
            _indicatorService.ProcessTrade(trade);
    }

    [Benchmark]
    public HistoricalIndicatorResult CalculateHistorical_AllIndicators()
    {
        return _indicatorService.CalculateHistorical("SPY", _bars);
    }

    [Benchmark]
    public IndicatorSnapshot? GetSnapshot_AfterProcessing()
    {
        foreach (var trade in _trades)
            _indicatorService.ProcessTrade(trade);

        return _indicatorService.GetSnapshot("SPY");
    }
}

/// <summary>
/// Benchmarks for individual indicator calculations.
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class SingleIndicatorBenchmarks
{
    private HistoricalBar[] _bars = null!;

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);
        var basePrice = 100m;

        _bars = new HistoricalBar[500];
        for (int i = 0; i < 500; i++)
        {
            var dayChange = (decimal)(random.NextDouble() - 0.5) * 2m;
            var high = basePrice + (decimal)random.NextDouble() * 1m;
            var low = basePrice - (decimal)random.NextDouble() * 1m;
            var close = basePrice + dayChange;

            _bars[i] = new HistoricalBar(
                Symbol: "SPY",
                SessionDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-500 + i)),
                Open: basePrice,
                High: high,
                Low: low,
                Close: close,
                Volume: random.Next(1000000, 100000000),
                SequenceNumber: i
            );

            basePrice = close;
        }
    }

    [Benchmark]
    public HistoricalIndicatorResult Calculate_SMA_Only()
    {
        var service = new TechnicalIndicatorService(new IndicatorConfiguration
        {
            EnabledIndicators = new HashSet<IndicatorType> { IndicatorType.SMA }
        });
        return service.CalculateHistorical("TEST", _bars);
    }

    [Benchmark]
    public HistoricalIndicatorResult Calculate_RSI_Only()
    {
        var service = new TechnicalIndicatorService(new IndicatorConfiguration
        {
            EnabledIndicators = new HashSet<IndicatorType> { IndicatorType.RSI }
        });
        return service.CalculateHistorical("TEST", _bars);
    }

    [Benchmark]
    public HistoricalIndicatorResult Calculate_MACD_Only()
    {
        var service = new TechnicalIndicatorService(new IndicatorConfiguration
        {
            EnabledIndicators = new HashSet<IndicatorType> { IndicatorType.MACD }
        });
        return service.CalculateHistorical("TEST", _bars);
    }

    [Benchmark]
    public HistoricalIndicatorResult Calculate_BollingerBands_Only()
    {
        var service = new TechnicalIndicatorService(new IndicatorConfiguration
        {
            EnabledIndicators = new HashSet<IndicatorType> { IndicatorType.BollingerBands }
        });
        return service.CalculateHistorical("TEST", _bars);
    }

    [Benchmark(Baseline = true)]
    public HistoricalIndicatorResult Calculate_AllIndicators()
    {
        var service = new TechnicalIndicatorService();
        return service.CalculateHistorical("TEST", _bars);
    }
}
