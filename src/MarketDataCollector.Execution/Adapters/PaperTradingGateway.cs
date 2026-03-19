using System.Runtime.CompilerServices;
using MarketDataCollector.Execution.Models;

namespace MarketDataCollector.Execution.Adapters;

/// <summary>
/// Simulated order gateway that routes no real orders to any exchange.
/// Fills are generated synthetically at a notional price (or at the limit price for
/// limit orders), making this the safe default for strategy validation before live promotion.
/// Implements ADR-015.
/// </summary>
[ImplementsAdr("ADR-015", "Simulated IOrderGateway over live MDC feed — no real orders")]
public sealed class PaperTradingGateway : IOrderGateway
{
    // Notional fill price used for market orders in this scaffold.
    // A production implementation would source the last-traded price from ILiveFeedAdapter.
    private const decimal ScaffoldMarketFillPrice = 1m;

    private readonly ILogger<PaperTradingGateway> _logger;
    private readonly System.Threading.Channels.Channel<OrderStatusUpdate> _updates;
    private readonly Dictionary<string, OrderRequest> _workingOrders = new();
    private readonly Lock _lock = new();
    private bool _disposed;

    /// <inheritdoc/>
    public string BrokerName => "Paper";

    /// <inheritdoc/>
    public ExecutionMode Mode => ExecutionMode.Paper;

    /// <summary>
    /// Creates a new paper trading gateway.
    /// </summary>
    public PaperTradingGateway(ILogger<PaperTradingGateway> logger)
    {
        _logger = logger;
        // Use EventPipelinePolicy for consistent backpressure settings across the platform (ADR-013).
        // CompletionQueue (Wait mode, 500 capacity) ensures no terminal order updates are dropped.
        _updates = EventPipelinePolicy.CompletionQueue.CreateChannel<OrderStatusUpdate>(
            singleReader: false, singleWriter: false);
    }

    /// <inheritdoc/>
    public Task<OrderAcknowledgement> SubmitAsync(OrderRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            _workingOrders[request.ClientOrderId] = request;
        }

        _logger.LogInformation(
            "Paper order accepted: {ClientOrderId} {Quantity} {Symbol} @ {Type}",
            request.ClientOrderId, request.Quantity, request.Symbol, request.Type);

        var ack = new OrderAcknowledgement(
            OrderId: request.ClientOrderId,
            ClientOrderId: request.ClientOrderId,
            Symbol: request.Symbol,
            Status: OrderStatus.Accepted,
            AcknowledgedAt: DateTimeOffset.UtcNow);

        // Use CancellationToken.None so the fill simulation always runs to completion
        // and emits a terminal update, even if the caller cancels after receiving the ack.
        _ = SimulateFillAsync(request, CancellationToken.None);

        return Task.FromResult(ack);
    }

    /// <inheritdoc/>
    public Task<bool> CancelAsync(string orderId, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        OrderRequest? cancelledRequest = null;
        lock (_lock)
        {
            if (_workingOrders.Remove(orderId, out var req))
            {
                cancelledRequest = req;
            }
        }

        if (cancelledRequest is not null)
        {
            _logger.LogInformation("Paper order cancelled: {OrderId} {Symbol}", orderId, cancelledRequest.Symbol);
            var update = new OrderStatusUpdate(
                OrderId: orderId,
                ClientOrderId: orderId,
                Symbol: cancelledRequest.Symbol,
                Status: OrderStatus.Cancelled,
                FilledQuantity: 0,
                AverageFillPrice: null,
                RejectReason: null,
                Timestamp: DateTimeOffset.UtcNow);

            _updates.Writer.TryWrite(update);
        }

        return Task.FromResult(cancelledRequest is not null);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<OrderStatusUpdate> StreamOrderUpdatesAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var update in _updates.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            yield return update;
        }
    }

    private async Task SimulateFillAsync(OrderRequest request, CancellationToken ct)
    {
        // Yield to allow the caller to receive the acknowledgement before the fill.
        await Task.Yield();

        lock (_lock)
        {
            _workingOrders.Remove(request.ClientOrderId);
        }

        // For limit orders use the limit price; for market orders use the scaffold notional price.
        // A real implementation would source the fill price from the live feed via ILiveFeedAdapter.
        var fillPrice = request.LimitPrice ?? ScaffoldMarketFillPrice;

        var fill = new OrderStatusUpdate(
            OrderId: request.ClientOrderId,
            ClientOrderId: request.ClientOrderId,
            Symbol: request.Symbol,
            Status: OrderStatus.Filled,
            FilledQuantity: Math.Abs(request.Quantity),
            AverageFillPrice: fillPrice,
            RejectReason: null,
            Timestamp: DateTimeOffset.UtcNow);

        _updates.Writer.TryWrite(fill);

        _logger.LogInformation(
            "Paper fill: {ClientOrderId} {Quantity} {Symbol} @ {FillPrice}",
            request.ClientOrderId, request.Quantity, request.Symbol, fillPrice);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _updates.Writer.TryComplete();

        await Task.CompletedTask.ConfigureAwait(false);
    }
}
