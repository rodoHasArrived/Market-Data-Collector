using DataIngestion.Contracts.Messages;
using System.Threading;
using DataIngestion.Gateway.Services;
using Microsoft.AspNetCore.Mvc;

namespace DataIngestion.Gateway.Controllers;

/// <summary>
/// Controller for data ingestion endpoints.
/// </summary>
[ApiController]
[Route("api/v1/ingest")]
public class IngestionController : ControllerBase
{
    private readonly IDataRouter _dataRouter;
    private readonly ILogger<IngestionController> _logger;

    public IngestionController(IDataRouter dataRouter, ILogger<IngestionController> logger)
    {
        _dataRouter = dataRouter;
        _logger = logger;
    }

    /// <summary>
    /// Ingest a single trade.
    /// </summary>
    [HttpPost("trades")]
    [ProducesResponseType(typeof(IngestionResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> IngestTrade([FromBody] TradeIngestionDto trade, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(trade.Symbol))
            return BadRequest(new ErrorResponse("Symbol is required"));

        var result = await _dataRouter.RouteAsync(
            IngestionDataType.Trade,
            trade.Symbol,
            trade,
            ct);

        if (!result.Success)
        {
            return StatusCode(503, new ErrorResponse(result.ErrorMessage ?? "Routing failed"));
        }

        return Accepted(new IngestionResponse(result.MessageId!, "Trade queued for processing"));
    }

    /// <summary>
    /// Ingest a batch of trades.
    /// </summary>
    [HttpPost("trades/batch")]
    [ProducesResponseType(typeof(BatchIngestionResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> IngestTradesBatch([FromBody] TradesBatchDto batch, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(batch.Symbol))
            return BadRequest(new ErrorResponse("Symbol is required"));

        if (batch.Trades == null || batch.Trades.Count == 0)
            return BadRequest(new ErrorResponse("At least one trade is required"));

        var result = await _dataRouter.RouteAsync(
            IngestionDataType.Trade,
            batch.Symbol,
            batch,
            ct);

        return Accepted(new BatchIngestionResponse(
            result.MessageId!,
            batch.Trades.Count,
            result.Success ? batch.Trades.Count : 0,
            result.Success ? 0 : batch.Trades.Count
        ));
    }

    /// <summary>
    /// Ingest a quote/BBO update.
    /// </summary>
    [HttpPost("quotes")]
    [ProducesResponseType(typeof(IngestionResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> IngestQuote([FromBody] QuoteIngestionDto quote, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(quote.Symbol))
            return BadRequest(new ErrorResponse("Symbol is required"));

        var result = await _dataRouter.RouteAsync(
            IngestionDataType.Quote,
            quote.Symbol,
            quote,
            ct);

        if (!result.Success)
        {
            return StatusCode(503, new ErrorResponse(result.ErrorMessage ?? "Routing failed"));
        }

        return Accepted(new IngestionResponse(result.MessageId!, "Quote queued for processing"));
    }

    /// <summary>
    /// Ingest an order book snapshot.
    /// </summary>
    [HttpPost("orderbook")]
    [ProducesResponseType(typeof(IngestionResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> IngestOrderBook([FromBody] OrderBookSnapshotDto snapshot, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(snapshot.Symbol))
            return BadRequest(new ErrorResponse("Symbol is required"));

        var result = await _dataRouter.RouteAsync(
            IngestionDataType.OrderBookSnapshot,
            snapshot.Symbol,
            snapshot,
            ct);

        if (!result.Success)
        {
            return StatusCode(503, new ErrorResponse(result.ErrorMessage ?? "Routing failed"));
        }

        return Accepted(new IngestionResponse(result.MessageId!, "Order book snapshot queued for processing"));
    }

    /// <summary>
    /// Ingest an order book update.
    /// </summary>
    [HttpPost("orderbook/update")]
    [ProducesResponseType(typeof(IngestionResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> IngestOrderBookUpdate([FromBody] OrderBookUpdateDto update, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(update.Symbol))
            return BadRequest(new ErrorResponse("Symbol is required"));

        var result = await _dataRouter.RouteAsync(
            IngestionDataType.OrderBookUpdate,
            update.Symbol,
            update,
            ct);

        if (!result.Success)
        {
            return StatusCode(503, new ErrorResponse(result.ErrorMessage ?? "Routing failed"));
        }

        return Accepted(new IngestionResponse(result.MessageId!, "Order book update queued for processing"));
    }

    /// <summary>
    /// Get routing statistics.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(RoutingStatistics), StatusCodes.Status200OK)]
    public IActionResult GetStatistics()
    {
        var stats = _dataRouter.GetStatistics();
        return Ok(stats);
    }
}

#region DTOs

public record TradeIngestionDto(
    string Symbol,
    DateTimeOffset? Timestamp,
    decimal Price,
    long Size,
    string? AggressorSide,
    string? TradeId,
    string? Exchange,
    string? Source
);

public record TradesBatchDto(
    string Symbol,
    string? Source,
    List<TradeIngestionDto> Trades
);

public record QuoteIngestionDto(
    string Symbol,
    DateTimeOffset? Timestamp,
    decimal BidPrice,
    long BidSize,
    decimal AskPrice,
    long AskSize,
    string? BidExchange,
    string? AskExchange,
    string? Source
);

public record OrderBookSnapshotDto(
    string Symbol,
    DateTimeOffset? Timestamp,
    List<OrderBookLevelDto> Bids,
    List<OrderBookLevelDto> Asks,
    string? Exchange,
    string? Source
);

public record OrderBookLevelDto(
    decimal Price,
    long Size,
    string? MarketMaker
);

public record OrderBookUpdateDto(
    string Symbol,
    DateTimeOffset? Timestamp,
    string UpdateType,
    string Side,
    int Position,
    decimal? Price,
    long? Size,
    string? MarketMaker,
    string? Exchange,
    string? Source
);

public record IngestionResponse(
    string MessageId,
    string Message
);

public record BatchIngestionResponse(
    string MessageId,
    int TotalCount,
    int QueuedCount,
    int FailedCount
);

public record ErrorResponse(string Error);

#endregion
