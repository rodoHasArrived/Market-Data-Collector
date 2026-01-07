/// Pipeline transformation functions for market event streams.
/// Provides composable operators for filtering, mapping, and aggregating events.
module MarketDataCollector.FSharp.Pipeline.Transforms

open System
open MarketDataCollector.FSharp.Domain.MarketEvents
open MarketDataCollector.FSharp.Domain.Sides
open MarketDataCollector.FSharp.Domain.Integrity
open MarketDataCollector.FSharp.Validation.ValidationTypes
open MarketDataCollector.FSharp.Validation.ValidationPipeline
open MarketDataCollector.FSharp.Calculations

/// Filter events by symbol.
[<CompiledName("FilterBySymbol")>]
let filterBySymbol (symbol: string) (events: MarketEvent seq) : MarketEvent seq =
    events
    |> Seq.filter (fun event ->
        match MarketEvent.getSymbol event with
        | Some s -> s = symbol
        | None -> false)

/// Filter events by symbol list.
[<CompiledName("FilterBySymbols")>]
let filterBySymbols (symbols: string Set) (events: MarketEvent seq) : MarketEvent seq =
    events
    |> Seq.filter (fun event ->
        match MarketEvent.getSymbol event with
        | Some s -> Set.contains s symbols
        | None -> false)

/// Filter events by time range.
[<CompiledName("FilterByTimeRange")>]
let filterByTimeRange (startTime: DateTimeOffset) (endTime: DateTimeOffset) (events: MarketEvent seq) : MarketEvent seq =
    events
    |> Seq.filter (fun event ->
        let ts = MarketEvent.getTimestamp event
        ts >= startTime && ts <= endTime)

/// Filter to only trade events.
[<CompiledName("FilterTrades")>]
let filterTrades (events: MarketEvent seq) : TradeEvent seq =
    events
    |> Seq.choose (function
        | MarketEvent.Trade t -> Some t
        | _ -> None)

/// Filter to only quote events.
[<CompiledName("FilterQuotes")>]
let filterQuotes (events: MarketEvent seq) : QuoteEvent seq =
    events
    |> Seq.choose (function
        | MarketEvent.Quote q -> Some q
        | _ -> None)

/// Filter to only depth events.
[<CompiledName("FilterDepth")>]
let filterDepth (events: MarketEvent seq) : DepthEvent seq =
    events
    |> Seq.choose (function
        | MarketEvent.Depth d -> Some d
        | _ -> None)

/// Filter to only integrity events.
[<CompiledName("FilterIntegrity")>]
let filterIntegrity (events: MarketEvent seq) : IntegrityEvent seq =
    events
    |> Seq.choose (function
        | MarketEvent.Integrity i -> Some i
        | _ -> None)

/// Enrich trades with aggressor inference based on BBO.
[<CompiledName("EnrichWithAggressor")>]
let enrichWithAggressor (events: MarketEvent seq) : MarketEvent seq =
    let mutable lastQuote: QuoteEvent option = None

    events
    |> Seq.map (fun event ->
        match event with
        | MarketEvent.Quote q ->
            lastQuote <- Some q
            event
        | MarketEvent.Trade t ->
            match lastQuote with
            | Some q when t.Symbol = q.Symbol ->
                let inferredSide = inferAggressor t.Price (Some q.BidPrice) (Some q.AskPrice)
                MarketEvent.Trade { t with Side = inferredSide }
            | _ -> event
        | _ -> event)

/// Add spread calculations to quotes.
type EnrichedQuote = {
    Quote: QuoteEvent
    Spread: decimal option
    SpreadBps: decimal option
    MidPrice: decimal option
    Imbalance: decimal option
}

/// Enrich quotes with calculated fields.
[<CompiledName("EnrichQuotes")>]
let enrichQuotes (quotes: QuoteEvent seq) : EnrichedQuote seq =
    quotes
    |> Seq.map (fun q ->
        { Quote = q
          Spread = Spread.fromQuote q
          SpreadBps = Spread.spreadBpsFromQuote q
          MidPrice = Spread.midPriceFromQuote q
          Imbalance = Imbalance.fromQuote q })

/// Validate and filter events.
[<CompiledName("ValidateAndFilter")>]
let validateAndFilter (events: MarketEvent seq) : MarketEvent seq =
    events
    |> Seq.choose (fun event ->
        match MarketEventValidation.validateMarketEvent event with
        | Ok e -> Some e
        | Error _ -> None)

/// Partition events by type.
type PartitionedEvents = {
    Trades: TradeEvent list
    Quotes: QuoteEvent list
    Depth: DepthEvent list
    Integrity: IntegrityEvent list
    Other: MarketEvent list
}

/// Partition events by their type.
[<CompiledName("PartitionByType")>]
let partitionByType (events: MarketEvent seq) : PartitionedEvents =
    let mutable trades = []
    let mutable quotes = []
    let mutable depth = []
    let mutable integrity = []
    let mutable other = []

    for event in events do
        match event with
        | MarketEvent.Trade t -> trades <- t :: trades
        | MarketEvent.Quote q -> quotes <- q :: quotes
        | MarketEvent.Depth d -> depth <- d :: depth
        | MarketEvent.Integrity i -> integrity <- i :: integrity
        | _ -> other <- event :: other

    { Trades = List.rev trades
      Quotes = List.rev quotes
      Depth = List.rev depth
      Integrity = List.rev integrity
      Other = List.rev other }

/// Group events by symbol.
[<CompiledName("GroupBySymbol")>]
let groupBySymbol (events: MarketEvent seq) : Map<string, MarketEvent list> =
    events
    |> Seq.choose (fun event ->
        match MarketEvent.getSymbol event with
        | Some s -> Some (s, event)
        | None -> None)
    |> Seq.groupBy fst
    |> Seq.map (fun (symbol, events) -> symbol, events |> Seq.map snd |> Seq.toList)
    |> Map.ofSeq

/// Sample events at regular intervals.
[<CompiledName("SampleAtInterval")>]
let sampleAtInterval (intervalMs: int) (events: MarketEvent seq) : MarketEvent seq =
    let mutable lastSampleTime = DateTimeOffset.MinValue

    events
    |> Seq.filter (fun event ->
        let ts = MarketEvent.getTimestamp event
        let elapsed = (ts - lastSampleTime).TotalMilliseconds

        if elapsed >= float intervalMs then
            lastSampleTime <- ts
            true
        else
            false)

/// Deduplicate events by sequence number.
[<CompiledName("Deduplicate")>]
let deduplicate (events: MarketEvent seq) : MarketEvent seq =
    let seen = System.Collections.Generic.HashSet<int64>()

    events
    |> Seq.filter (fun event ->
        match MarketEvent.getSequenceNumber event with
        | Some seq -> seen.Add(seq)
        | None -> true)

/// Merge multiple event streams in timestamp order.
[<CompiledName("MergeStreams")>]
let mergeStreams (streams: MarketEvent seq list) : MarketEvent seq =
    streams
    |> Seq.concat
    |> Seq.sortBy MarketEvent.getTimestamp

/// Buffer events by count.
[<CompiledName("BufferByCount")>]
let bufferByCount (count: int) (events: MarketEvent seq) : MarketEvent list seq =
    events
    |> Seq.chunkBySize count
    |> Seq.map Array.toList

/// Buffer events by time window.
[<CompiledName("BufferByTime")>]
let bufferByTime (windowMs: int) (events: MarketEvent seq) : MarketEvent list seq =
    events
    |> Seq.groupBy (fun event ->
        let ts = MarketEvent.getTimestamp event
        ts.ToUnixTimeMilliseconds() / int64 windowMs)
    |> Seq.map (fun (_, group) -> Seq.toList group)

/// Pipeline composition operator.
let (|>>) (events: MarketEvent seq) (transform: MarketEvent seq -> MarketEvent seq) : MarketEvent seq =
    transform events

/// Create a transformation pipeline.
type TransformPipeline = {
    Transforms: (MarketEvent seq -> MarketEvent seq) list
}

module TransformPipeline =

    /// Create an empty pipeline.
    [<CompiledName("Create")>]
    let create () = { Transforms = [] }

    /// Add a transform to the pipeline.
    [<CompiledName("Add")>]
    let add (transform: MarketEvent seq -> MarketEvent seq) (pipeline: TransformPipeline) =
        { Transforms = pipeline.Transforms @ [transform] }

    /// Run the pipeline on events.
    [<CompiledName("Run")>]
    let run (pipeline: TransformPipeline) (events: MarketEvent seq) =
        pipeline.Transforms
        |> List.fold (fun acc transform -> transform acc) events

    /// Add symbol filter.
    [<CompiledName("FilterSymbol")>]
    let filterSymbol (symbol: string) (pipeline: TransformPipeline) =
        add (filterBySymbol symbol) pipeline

    /// Add time range filter.
    [<CompiledName("FilterTime")>]
    let filterTime (startTime: DateTimeOffset) (endTime: DateTimeOffset) (pipeline: TransformPipeline) =
        add (filterByTimeRange startTime endTime) pipeline

    /// Add validation filter.
    [<CompiledName("Validate")>]
    let validate (pipeline: TransformPipeline) =
        add validateAndFilter pipeline

    /// Add deduplication.
    [<CompiledName("Dedupe")>]
    let dedupe (pipeline: TransformPipeline) =
        add deduplicate pipeline
