using DataIngestion.Contracts.Messages;
using DataIngestion.Gateway.Services;
using Microsoft.AspNetCore.Mvc;

namespace DataIngestion.Gateway.Controllers;

/// <summary>
/// Controller for managing market data subscriptions.
/// </summary>
[ApiController]
[Route("api/v1/subscriptions")]
public class SubscriptionController : ControllerBase
{
    private readonly IProviderManager _providerManager;
    private readonly ILogger<SubscriptionController> _logger;

    public SubscriptionController(IProviderManager providerManager, ILogger<SubscriptionController> logger)
    {
        _providerManager = providerManager;
        _logger = logger;
    }

    /// <summary>
    /// Subscribe to market data for a symbol.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Subscribe([FromBody] SubscriptionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.Symbol))
            return BadRequest(new ErrorResponse("Symbol is required"));

        if (string.IsNullOrEmpty(request.Provider))
            return BadRequest(new ErrorResponse("Provider is required"));

        try
        {
            var subscriptionTypes = request.Types?.Length > 0
                ? request.Types
                : [SubscriptionType.All];

            var subscriptionId = await _providerManager.SubscribeAsync(
                request.Provider,
                request.Symbol,
                subscriptionTypes,
                ct);

            return CreatedAtAction(
                nameof(GetSubscription),
                new { id = subscriptionId },
                new SubscriptionResponse(
                    subscriptionId,
                    request.Symbol,
                    request.Provider,
                    subscriptionTypes.Select(t => t.ToString()).ToArray(),
                    true,
                    null
                ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create subscription for {Symbol} on {Provider}",
                request.Symbol, request.Provider);
            return BadRequest(new ErrorResponse(ex.Message));
        }
    }

    /// <summary>
    /// Get subscription details.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetSubscription(int id)
    {
        // In a real implementation, we'd look up the subscription
        return NotFound(new ErrorResponse($"Subscription {id} not found"));
    }

    /// <summary>
    /// Unsubscribe from market data.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unsubscribe(int id, [FromQuery] string provider, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(provider))
            return BadRequest(new ErrorResponse("Provider query parameter is required"));

        await _providerManager.UnsubscribeAsync(provider, id, ct);
        return NoContent();
    }

    /// <summary>
    /// Get all provider connection statuses.
    /// </summary>
    [HttpGet("providers")]
    [ProducesResponseType(typeof(Dictionary<string, ProviderStatusDto>), StatusCodes.Status200OK)]
    public IActionResult GetProviderStatuses()
    {
        var statuses = _providerManager.GetProviderStatuses();

        var result = statuses.ToDictionary(
            kvp => kvp.Key,
            kvp => new ProviderStatusDto(
                kvp.Value.ProviderName,
                kvp.Value.IsConnected,
                kvp.Value.LastConnectedAt,
                kvp.Value.LastDisconnectedAt,
                kvp.Value.ReconnectAttempts,
                kvp.Value.LastError
            ));

        return Ok(result);
    }

    /// <summary>
    /// Connect to a provider.
    /// </summary>
    [HttpPost("providers/{name}/connect")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ConnectProvider(string name, CancellationToken ct)
    {
        var success = await _providerManager.ConnectProviderAsync(name, ct);

        if (success)
        {
            return Ok(new { message = $"Connected to {name}" });
        }

        return StatusCode(503, new ErrorResponse($"Failed to connect to {name}"));
    }

    /// <summary>
    /// Disconnect from a provider.
    /// </summary>
    [HttpPost("providers/{name}/disconnect")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DisconnectProvider(string name, CancellationToken ct)
    {
        await _providerManager.DisconnectProviderAsync(name, ct);
        return Ok(new { message = $"Disconnected from {name}" });
    }
}

#region DTOs

public record SubscriptionRequest(
    string Symbol,
    string Provider,
    SubscriptionType[]? Types
);

public record SubscriptionResponse(
    int SubscriptionId,
    string Symbol,
    string Provider,
    string[] ActiveTypes,
    bool Success,
    string? ErrorMessage
);

public record ProviderStatusDto(
    string Name,
    bool IsConnected,
    DateTimeOffset LastConnectedAt,
    DateTimeOffset? LastDisconnectedAt,
    int ReconnectAttempts,
    string? LastError
);

#endregion
