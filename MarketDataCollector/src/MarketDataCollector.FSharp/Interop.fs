/// C# interoperability helpers for the F# domain library.
/// Provides extension methods and adapters for seamless C# consumption.
module MarketDataCollector.FSharp.Interop

open System
open System.Runtime.CompilerServices
open MarketDataCollector.FSharp.Domain.MarketEvents
open MarketDataCollector.FSharp.Domain.Sides
open MarketDataCollector.FSharp.Domain.Integrity
open MarketDataCollector.FSharp.Validation.ValidationTypes
open MarketDataCollector.FSharp.Calculations

/// Extension methods for Option types to work with C# nullable types.
[<Extension>]
type OptionExtensions =

    /// Convert Option<T> to Nullable<T> for value types.
    [<Extension>]
    static member ToNullable(opt: 'T option) : Nullable<'T> =
        match opt with
        | Some v -> Nullable v
        | None -> Nullable()

    /// Convert Option<T> to T or null for reference types.
    [<Extension>]
    static member ToNullableRef(opt: 'T option) : 'T =
        match opt with
        | Some v -> v
        | None -> Unchecked.defaultof<'T>

    /// Convert Option<T> to T or a default value.
    [<Extension>]
    static member GetValueOrDefault(opt: 'T option, defaultValue: 'T) : 'T =
        Option.defaultValue defaultValue opt

    /// Check if Option has a value.
    [<Extension>]
    static member HasValue(opt: 'T option) : bool =
        Option.isSome opt

/// C#-friendly wrapper for trade events.
[<Sealed>]
type TradeEventWrapper(trade: TradeEvent) =

    member _.Symbol = trade.Symbol
    member _.Price = trade.Price
    member _.Quantity = trade.Quantity
    member _.Side = trade.Side.ToInt()
    member _.SequenceNumber = trade.SequenceNumber
    member _.Timestamp = trade.Timestamp
    member _.ExchangeTimestamp = trade.ExchangeTimestamp.ToNullable()
    member _.StreamId = trade.StreamId.ToNullableRef()
    member _.Venue = trade.Venue.ToNullableRef()

    member _.ToFSharpEvent() = trade

    static member FromFSharp(trade: TradeEvent) = TradeEventWrapper(trade)

    static member Create(symbol, price, quantity, side, sequenceNumber, timestamp) =
        TradeEventWrapper({
            Symbol = symbol
            Price = price
            Quantity = quantity
            Side = AggressorSide.FromInt(side)
            SequenceNumber = sequenceNumber
            Timestamp = timestamp
            ExchangeTimestamp = None
            StreamId = None
            Venue = None
        })

/// C#-friendly wrapper for quote events.
[<Sealed>]
type QuoteEventWrapper(quote: QuoteEvent) =

    member _.Symbol = quote.Symbol
    member _.BidPrice = quote.BidPrice
    member _.BidSize = quote.BidSize
    member _.AskPrice = quote.AskPrice
    member _.AskSize = quote.AskSize
    member _.SequenceNumber = quote.SequenceNumber
    member _.Timestamp = quote.Timestamp
    member _.ExchangeTimestamp = quote.ExchangeTimestamp.ToNullable()

    member _.ToFSharpEvent() = quote

    static member FromFSharp(quote: QuoteEvent) = QuoteEventWrapper(quote)

    static member Create(symbol, bidPrice, bidSize, askPrice, askSize, sequenceNumber, timestamp) =
        QuoteEventWrapper({
            Symbol = symbol
            BidPrice = bidPrice
            BidSize = bidSize
            AskPrice = askPrice
            AskSize = askSize
            SequenceNumber = sequenceNumber
            Timestamp = timestamp
            ExchangeTimestamp = None
            StreamId = None
        })

/// C#-friendly validation result.
[<Sealed>]
type ValidationResultWrapper<'T>(result: ValidationResult<'T>) =

    member _.IsSuccess =
        match result with
        | Ok _ -> true
        | Error _ -> false

    member _.Value =
        match result with
        | Ok v -> v
        | Error _ -> Unchecked.defaultof<'T>

    member _.Errors =
        match result with
        | Ok _ -> [||]
        | Error errors -> errors |> List.map (fun e -> e.Description) |> List.toArray

    member _.ErrorDetails =
        match result with
        | Ok _ -> [||]
        | Error errors -> errors |> List.toArray

/// C#-friendly spread calculator.
[<Sealed>]
type SpreadCalculator private () =

    static member Calculate(bidPrice: decimal, askPrice: decimal) : Nullable<decimal> =
        (Spread.calculate bidPrice askPrice).ToNullable()

    static member MidPrice(bidPrice: decimal, askPrice: decimal) : Nullable<decimal> =
        (Spread.midPrice bidPrice askPrice).ToNullable()

    static member SpreadBps(bidPrice: decimal, askPrice: decimal) : Nullable<decimal> =
        (Spread.spreadBps bidPrice askPrice).ToNullable()

    static member FromQuote(quote: QuoteEvent) : Nullable<decimal> =
        (Spread.fromQuote quote).ToNullable()

    static member MidPriceFromQuote(quote: QuoteEvent) : Nullable<decimal> =
        (Spread.midPriceFromQuote quote).ToNullable()

/// C#-friendly imbalance calculator.
[<Sealed>]
type ImbalanceCalculator private () =

    static member Calculate(bidQuantity: int64, askQuantity: int64) : Nullable<decimal> =
        (Imbalance.calculate bidQuantity askQuantity).ToNullable()

    static member FromQuote(quote: QuoteEvent) : Nullable<decimal> =
        (Imbalance.fromQuote quote).ToNullable()

    static member Microprice(book: OrderBookSnapshot) : Nullable<decimal> =
        (Imbalance.microprice book).ToNullable()

/// C#-friendly aggregation functions.
[<Sealed>]
type AggregationFunctions private () =

    static member Vwap(trades: TradeEvent seq) : Nullable<decimal> =
        (Aggregations.vwap trades).ToNullable()

    static member Twap(trades: TradeEvent seq) : Nullable<decimal> =
        (Aggregations.twap trades).ToNullable()

    static member TotalVolume(trades: TradeEvent seq) : int64 =
        Aggregations.totalVolume trades

    static member OrderFlowImbalance(trades: TradeEvent seq) : int64 =
        Aggregations.orderFlowImbalance trades

    static member VolumeBreakdown(trades: TradeEvent seq) : Aggregations.VolumeBreakdown =
        Aggregations.volumeBreakdown trades

/// C#-friendly trade validator.
[<Sealed>]
type TradeValidator private () =

    static member Validate(trade: TradeEvent) : ValidationResultWrapper<TradeEvent> =
        ValidationResultWrapper(Validation.TradeValidator.validateTradeDefault trade)

    static member IsValid(trade: TradeEvent) : bool =
        Validation.TradeValidator.isValidTrade trade

    static member ValidateWithConfig(trade: TradeEvent, config: Validation.TradeValidator.TradeValidationConfig) =
        ValidationResultWrapper(Validation.TradeValidator.validateTrade config trade)

/// C#-friendly quote validator.
[<Sealed>]
type QuoteValidator private () =

    static member Validate(quote: QuoteEvent) : ValidationResultWrapper<QuoteEvent> =
        ValidationResultWrapper(Validation.QuoteValidator.validateQuoteDefault quote)

    static member IsValid(quote: QuoteEvent) : bool =
        Validation.QuoteValidator.isValidQuote quote

    static member HasValidSpread(quote: QuoteEvent) : bool =
        Validation.QuoteValidator.hasValidSpread quote

/// C#-friendly aggressor inference.
[<Sealed>]
type AggressorInference private () =

    static member Infer(tradePrice: decimal, bidPrice: Nullable<decimal>, askPrice: Nullable<decimal>) : int =
        let bidOpt = if bidPrice.HasValue then Some bidPrice.Value else None
        let askOpt = if askPrice.HasValue then Some askPrice.Value else None
        (Sides.inferAggressor tradePrice bidOpt askOpt).ToInt()

    static member InferFromQuote(tradePrice: decimal, quote: QuoteEvent) : int =
        (Sides.inferAggressor tradePrice (Some quote.BidPrice) (Some quote.AskPrice)).ToInt()
