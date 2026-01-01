using QuantConnect;
using QuantConnect.Data;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Domain.Events;
using System.Text.Json;

namespace MarketDataCollector.Integrations.Lean;

/// <summary>
/// Custom Lean BaseData implementation for MarketDataCollector trade events.
/// Allows Lean algorithms to consume tick-by-tick trade data collected by MarketDataCollector.
/// </summary>
public class MarketDataCollectorTradeData : BaseData
{
    /// <summary>Trade price</summary>
    public decimal TradePrice { get; set; }

    /// <summary>Trade volume/size</summary>
    public decimal TradeSize { get; set; }

    /// <summary>Exchange where trade occurred</summary>
    public string Exchange { get; set; } = string.Empty;

    /// <summary>Trade conditions/flags</summary>
    public List<string> Conditions { get; set; } = new();

    /// <summary>Sequence number for ordering</summary>
    public long SequenceNumber { get; set; }

    /// <summary>Aggressor side (buy/sell)</summary>
    public string AggressorSide { get; set; } = string.Empty;

    /// <summary>
    /// Return the URL string source of the file. This will be converted to a stream.
    /// </summary>
    public override SubscriptionDataSource GetSource(SubscriptionDataConfig config, DateTime date, bool isLiveMode)
    {
        // In live mode, data would come from the real-time collector
        if (isLiveMode)
        {
            return new SubscriptionDataSource(string.Empty, SubscriptionTransportMedium.LocalFile);
        }

        // For backtesting, construct the path to the JSONL file in the MarketDataCollector data directory
        // Assuming data is organized as: {DataRoot}/{Symbol}/trade/{date}.jsonl
        var dataRoot = Globals.DataFolder; // Lean's data folder
        var symbol = config.Symbol.Value.ToUpperInvariant();
        var dateStr = date.ToString("yyyy-MM-dd");
        var filePath = Path.Combine(dataRoot, "marketdatacollector", symbol, "trade", $"{dateStr}.jsonl");

        return new SubscriptionDataSource(filePath, SubscriptionTransportMedium.LocalFile);
    }

    /// <summary>
    /// Reader converts each line of the data source into a BaseData object.
    /// </summary>
    public override BaseData Reader(SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
    {
        try
        {
            // Parse the JSONL line as a MarketEvent
            var marketEvent = JsonSerializer.Deserialize<MarketEvent>(line);

            if (marketEvent == null || marketEvent.Type != MarketEventType.Trade)
                return null!;

            // Extract trade payload
            var tradePayload = marketEvent.Payload as JsonElement?;
            if (tradePayload == null)
                return null!;

            var trade = JsonSerializer.Deserialize<Trade>(tradePayload.Value.GetRawText());
            if (trade == null)
                return null!;

            return new MarketDataCollectorTradeData
            {
                Symbol = Symbol.Create(marketEvent.Symbol, SecurityType.Equity, Market.USA),
                Time = marketEvent.Timestamp.UtcDateTime,
                Value = trade.Price,
                TradePrice = trade.Price,
                TradeSize = trade.Size,
                Exchange = trade.Exchange ?? string.Empty,
                Conditions = trade.Conditions?.ToList() ?? new List<string>(),
                SequenceNumber = trade.SequenceNumber,
                AggressorSide = trade.AggressorSide?.ToString() ?? "Unknown"
            };
        }
        catch (Exception)
        {
            // Log parsing errors in production
            return null!;
        }
    }

    /// <summary>
    /// Clone implementation required by Lean
    /// </summary>
    public override BaseData Clone()
    {
        return new MarketDataCollectorTradeData
        {
            Symbol = Symbol,
            Time = Time,
            Value = Value,
            TradePrice = TradePrice,
            TradeSize = TradeSize,
            Exchange = Exchange,
            Conditions = new List<string>(Conditions),
            SequenceNumber = SequenceNumber,
            AggressorSide = AggressorSide
        };
    }
}
