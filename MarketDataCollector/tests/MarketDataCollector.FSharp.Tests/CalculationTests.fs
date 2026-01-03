/// Unit tests for F# calculation functions.
module MarketDataCollector.FSharp.Tests.CalculationTests

open System
open Xunit
open FsUnit.Xunit
open MarketDataCollector.FSharp.Domain.MarketEvents
open MarketDataCollector.FSharp.Domain.Sides
open MarketDataCollector.FSharp.Calculations.Spread
open MarketDataCollector.FSharp.Calculations.Imbalance
open MarketDataCollector.FSharp.Calculations.Aggregations

let createTestQuote bidPrice bidSize askPrice askSize : QuoteEvent = {
    Symbol = "TEST"
    BidPrice = bidPrice
    BidSize = bidSize
    AskPrice = askPrice
    AskSize = askSize
    SequenceNumber = 1L
    Timestamp = DateTimeOffset.UtcNow
    ExchangeTimestamp = None
    StreamId = None
}

let createTestTrade price quantity side seqNum : TradeEvent = {
    Symbol = "TEST"
    Price = price
    Quantity = quantity
    Side = side
    SequenceNumber = seqNum
    Timestamp = DateTimeOffset.UtcNow
    ExchangeTimestamp = None
    StreamId = None
    Venue = None
}

// Spread Tests

[<Fact>]
let ``calculate returns correct spread`` () =
    let spread = calculate 100.00m 100.10m
    spread |> should equal (Some 0.10m)

[<Fact>]
let ``calculate returns None for invalid prices`` () =
    calculate 0m 100.00m |> should equal None
    calculate 100.00m 0m |> should equal None
    calculate 100.10m 100.00m |> should equal None // crossed

[<Fact>]
let ``midPrice calculates correct value`` () =
    let mid = midPrice 100.00m 100.10m
    mid |> should equal (Some 100.05m)

[<Fact>]
let ``spreadBps calculates correct basis points`` () =
    let bps = spreadBps 100.00m 100.10m
    match bps with
    | Some value ->
        // 0.10 / 100.05 * 10000 ≈ 9.995
        value |> should be (greaterThan 9.9m)
        value |> should be (lessThan 10.1m)
    | None -> failwith "Expected Some value"

[<Fact>]
let ``fromQuote calculates spread from quote`` () =
    let quote = createTestQuote 99.90m 1000L 100.10m 500L
    let spread = fromQuote quote
    spread |> should equal (Some 0.20m)

[<Fact>]
let ``effectiveSpread calculates correct value`` () =
    // Trade at mid should have 0 effective spread
    let effSpread = effectiveSpread 100.05m 100.00m 100.10m
    effSpread |> should equal (Some 0.00m)

    // Trade at ask should have effective spread = quoted spread
    let effSpread2 = effectiveSpread 100.10m 100.00m 100.10m
    effSpread2 |> should equal (Some 0.10m)

[<Fact>]
let ``relativeSpread calculates percentage`` () =
    let relSpread = relativeSpread 100.00m 100.10m
    match relSpread with
    | Some value ->
        // 0.10 / 100.05 * 100 ≈ 0.0999%
        value |> should be (greaterThan 0.09m)
        value |> should be (lessThan 0.11m)
    | None -> failwith "Expected Some value"

// Imbalance Tests

[<Fact>]
let ``Imbalance.calculate returns correct value`` () =
    // Equal sizes = 0 imbalance
    let balanced = Imbalance.calculate 1000L 1000L
    balanced |> should equal (Some 0m)

    // All bid = +1 imbalance
    let allBid = Imbalance.calculate 1000L 0L
    allBid |> should equal (Some 1m)

    // All ask = -1 imbalance
    let allAsk = Imbalance.calculate 0L 1000L
    allAsk |> should equal (Some -1m)

[<Fact>]
let ``Imbalance.calculate with unequal sizes`` () =
    // 75% bid, 25% ask => (75-25)/(75+25) = 0.5
    let result = Imbalance.calculate 750L 250L
    result |> should equal (Some 0.5m)

[<Fact>]
let ``Imbalance.calculate returns None for zero total`` () =
    let result = Imbalance.calculate 0L 0L
    result |> should equal None

[<Fact>]
let ``Imbalance.fromQuote calculates from quote`` () =
    let quote = createTestQuote 100.00m 1000L 100.10m 500L
    let imbalance = Imbalance.fromQuote quote
    // (1000 - 500) / (1000 + 500) = 500/1500 ≈ 0.333
    match imbalance with
    | Some value ->
        value |> should be (greaterThan 0.3m)
        value |> should be (lessThan 0.4m)
    | None -> failwith "Expected Some value"

[<Fact>]
let ``getImbalanceDirection returns Buy for positive imbalance`` () =
    let direction = getImbalanceDirection 0.5m
    direction |> should equal (Some Side.Buy)

[<Fact>]
let ``getImbalanceDirection returns Sell for negative imbalance`` () =
    let direction = getImbalanceDirection -0.5m
    direction |> should equal (Some Side.Sell)

[<Fact>]
let ``getImbalanceDirection returns None for balanced`` () =
    let direction = getImbalanceDirection 0.05m
    direction |> should equal None

[<Fact>]
let ``isSignificantImbalance checks threshold`` () =
    isSignificantImbalance 0.3m 0.5m |> should equal true
    isSignificantImbalance 0.3m 0.2m |> should equal false

// Aggregation Tests

[<Fact>]
let ``vwap calculates correct value`` () =
    let trades = [
        createTestTrade 100.00m 100L AggressorSide.Buyer 1L
        createTestTrade 101.00m 200L AggressorSide.Buyer 2L
    ]
    // VWAP = (100*100 + 101*200) / (100 + 200) = 30200/300 = 100.666...
    let result = vwap trades
    match result with
    | Some v ->
        v |> should be (greaterThan 100.6m)
        v |> should be (lessThan 100.7m)
    | None -> failwith "Expected Some value"

[<Fact>]
let ``vwap returns None for empty list`` () =
    let result = vwap Seq.empty
    result |> should equal None

[<Fact>]
let ``totalVolume sums quantities`` () =
    let trades = [
        createTestTrade 100.00m 100L AggressorSide.Buyer 1L
        createTestTrade 101.00m 200L AggressorSide.Buyer 2L
        createTestTrade 102.00m 50L AggressorSide.Seller 3L
    ]
    let total = totalVolume trades
    total |> should equal 350L

[<Fact>]
let ``volumeBreakdown calculates correct volumes`` () =
    let trades = [
        createTestTrade 100.00m 100L AggressorSide.Buyer 1L
        createTestTrade 101.00m 200L AggressorSide.Seller 2L
        createTestTrade 102.00m 50L AggressorSide.Unknown 3L
    ]
    let breakdown = volumeBreakdown trades
    breakdown.BuyVolume |> should equal 100L
    breakdown.SellVolume |> should equal 200L
    breakdown.UnknownVolume |> should equal 50L
    breakdown.TotalVolume |> should equal 350L

[<Fact>]
let ``orderFlowImbalance calculates signed sum`` () =
    let trades = [
        createTestTrade 100.00m 100L AggressorSide.Buyer 1L
        createTestTrade 101.00m 200L AggressorSide.Seller 2L
    ]
    let ofi = orderFlowImbalance trades
    // 100 - 200 = -100
    ofi |> should equal -100L

[<Fact>]
let ``priceRange calculates high minus low`` () =
    let trades = [
        createTestTrade 100.00m 100L AggressorSide.Buyer 1L
        createTestTrade 105.00m 100L AggressorSide.Buyer 2L
        createTestTrade 102.00m 100L AggressorSide.Buyer 3L
    ]
    let range = priceRange trades
    range |> should equal (Some 5.00m)

[<Fact>]
let ``priceReturn calculates percentage change`` () =
    let now = DateTimeOffset.UtcNow
    let trades = [
        { createTestTrade 100.00m 100L AggressorSide.Buyer 1L with Timestamp = now }
        { createTestTrade 105.00m 100L AggressorSide.Buyer 2L with Timestamp = now.AddSeconds(1.0) }
    ]
    let pctReturn = priceReturn trades
    // (105 - 100) / 100 * 100 = 5%
    pctReturn |> should equal (Some 5.00m)

[<Fact>]
let ``tradeStatistics calculates correct stats`` () =
    let trades = [
        createTestTrade 100.00m 100L AggressorSide.Buyer 1L
        createTestTrade 101.00m 200L AggressorSide.Buyer 2L
        createTestTrade 102.00m 50L AggressorSide.Seller 3L
    ]
    let stats = tradeStatistics trades
    match stats with
    | Some s ->
        s.TradeCount |> should equal 3
        s.TotalVolume |> should equal 350L
        s.MinSize |> should equal 50L
        s.MaxSize |> should equal 200L
    | None -> failwith "Expected Some stats"

[<Fact>]
let ``createOhlcvBar creates correct bar`` () =
    let now = DateTimeOffset.UtcNow
    let trades = [
        { createTestTrade 100.00m 100L AggressorSide.Buyer 1L with Timestamp = now }
        { createTestTrade 105.00m 100L AggressorSide.Buyer 2L with Timestamp = now.AddSeconds(1.0) }
        { createTestTrade 98.00m 100L AggressorSide.Seller 3L with Timestamp = now.AddSeconds(2.0) }
        { createTestTrade 102.00m 100L AggressorSide.Buyer 4L with Timestamp = now.AddSeconds(3.0) }
    ]
    let bar = createOhlcvBar trades
    match bar with
    | Some b ->
        b.Open |> should equal 100.00m
        b.High |> should equal 105.00m
        b.Low |> should equal 98.00m
        b.Close |> should equal 102.00m
        b.Volume |> should equal 400L
        b.TradeCount |> should equal 4
    | None -> failwith "Expected Some bar"
