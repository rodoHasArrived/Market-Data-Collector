using QuantConnect;
using QuantConnect.Data;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Domain.Events;
using System.Text.Json;

namespace MarketDataCollector.Integrations.Lean;

/// <summary>
/// Custom Lean BaseData implementation for MarketDataCollector BBO quote events.
/// Allows Lean algorithms to consume best bid/offer data collected by MarketDataCollector.
/// </summary>
public class MarketDataCollectorQuoteData : BaseData
{
    /// <summary>Best bid price</summary>
    public decimal BidPrice { get; set; }

    /// <summary>Best bid size</summary>
    public decimal BidSize { get; set; }

    /// <summary>Best ask price</summary>
    public decimal AskPrice { get; set; }

    /// <summary>Best ask size</summary>
    public decimal AskSize { get; set; }

    /// <summary>Mid price (average of bid and ask)</summary>
    public decimal MidPrice { get; set; }

    /// <summary>Bid-ask spread</summary>
    public decimal Spread { get; set; }

    /// <summary>Sequence number for ordering</summary>
    public long SequenceNumber { get; set; }

    /// <summary>Bid exchange</summary>
    public string BidExchange { get; set; } = string.Empty;

    /// <summary>Ask exchange</summary>
    public string AskExchange { get; set; } = string.Empty;

    /// <summary>
    /// Return the URL string source of the file.
    /// </summary>
    public override SubscriptionDataSource GetSource(SubscriptionDataConfig config, DateTime date, bool isLiveMode)
    {
        if (isLiveMode)
        {
            return new SubscriptionDataSource(string.Empty, SubscriptionTransportMedium.LocalFile);
        }

        // For backtesting, construct the path to the JSONL file
        // Assuming data is organized as: {DataRoot}/{Symbol}/bboquote/{date}.jsonl
        var dataRoot = Globals.DataFolder;
        var symbol = config.Symbol.Value.ToUpperInvariant();
        var dateStr = date.ToString("yyyy-MM-dd");
        var filePath = Path.Combine(dataRoot, "marketdatacollector", symbol, "bboquote", $"{dateStr}.jsonl");

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

            if (marketEvent == null || marketEvent.Type != MarketEventType.BboQuote)
                return null!;

            // Extract BBO quote payload
            var quotePayload = marketEvent.Payload as JsonElement?;
            if (quotePayload == null)
                return null!;

            var quote = JsonSerializer.Deserialize<BboQuotePayload>(quotePayload.Value.GetRawText());
            if (quote == null)
                return null!;

            return new MarketDataCollectorQuoteData
            {
                Symbol = Symbol.Create(marketEvent.Symbol, SecurityType.Equity, Market.USA),
                Time = marketEvent.Timestamp.UtcDateTime,
                Value = quote.MidPrice,  // Use mid price as the primary value
                BidPrice = quote.BidPrice,
                BidSize = quote.BidSize,
                AskPrice = quote.AskPrice,
                AskSize = quote.AskSize,
                MidPrice = quote.MidPrice,
                Spread = quote.Spread,
                SequenceNumber = quote.SequenceNumber,
                BidExchange = quote.BidExchange ?? string.Empty,
                AskExchange = quote.AskExchange ?? string.Empty
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
        return new MarketDataCollectorQuoteData
        {
            Symbol = Symbol,
            Time = Time,
            Value = Value,
            BidPrice = BidPrice,
            BidSize = BidSize,
            AskPrice = AskPrice,
            AskSize = AskSize,
            MidPrice = MidPrice,
            Spread = Spread,
            SequenceNumber = SequenceNumber,
            BidExchange = BidExchange,
            AskExchange = AskExchange
        };
    }
}
