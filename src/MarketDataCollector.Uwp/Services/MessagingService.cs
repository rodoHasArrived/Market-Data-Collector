using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for managing MassTransit message bus integration.
/// Provides visibility into message publishing, consumption, and bus health.
/// </summary>
public sealed class MessagingService : IMessagingService
{
    private static MessagingService? _instance;
    private static readonly object _lock = new();
    private readonly ApiClientService _apiClient;

    public static MessagingService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new MessagingService();
                }
            }
            return _instance;
        }
    }

    private MessagingService()
    {
        _apiClient = ApiClientService.Instance;
    }

    /// <summary>
    /// Raised when message bus status changes.
    /// </summary>
    public event EventHandler<BusStatusChangedEventArgs>? StatusChanged;

    /// <summary>
    /// Raised when a new message is published or consumed.
    /// </summary>
    public event EventHandler<MessageActivityEventArgs>? MessageActivity;

    /// <summary>
    /// Gets the current message bus configuration.
    /// </summary>
    public async Task<BusConfiguration> GetConfigurationAsync(CancellationToken ct = default)
    {
        var response = await _apiClient.GetWithResponseAsync<BusConfiguration>(
            "/api/messaging/config",
            ct);

        return response.Data ?? new BusConfiguration();
    }

    /// <summary>
    /// Gets the current message bus status.
    /// </summary>
    public async Task<BusStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var response = await _apiClient.GetWithResponseAsync<BusStatus>(
            "/api/messaging/status",
            ct);

        return response.Data ?? new BusStatus();
    }

    /// <summary>
    /// Gets message statistics.
    /// </summary>
    public async Task<MessageStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        var response = await _apiClient.GetWithResponseAsync<MessageStatistics>(
            "/api/messaging/stats",
            ct);

        return response.Data ?? new MessageStatistics();
    }

    /// <summary>
    /// Gets recent message activity.
    /// </summary>
    public async Task<MessageActivityResult> GetRecentActivityAsync(
        int limit = 100,
        string? messageType = null,
        CancellationToken ct = default)
    {
        var query = $"?limit={limit}";
        if (!string.IsNullOrEmpty(messageType))
            query += $"&type={Uri.EscapeDataString(messageType)}";

        var response = await _apiClient.GetWithResponseAsync<MessageActivityResponse>(
            $"/api/messaging/activity{query}",
            ct);

        if (response.Success && response.Data != null)
        {
            return new MessageActivityResult
            {
                Success = true,
                Messages = response.Data.Messages?.ToList() ?? new List<MessageInfo>()
            };
        }

        return new MessageActivityResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Failed to get message activity"
        };
    }

    /// <summary>
    /// Gets registered message consumers.
    /// </summary>
    public async Task<ConsumerListResult> GetConsumersAsync(CancellationToken ct = default)
    {
        var response = await _apiClient.GetWithResponseAsync<ConsumerListResponse>(
            "/api/messaging/consumers",
            ct);

        if (response.Success && response.Data != null)
        {
            return new ConsumerListResult
            {
                Success = true,
                Consumers = response.Data.Consumers?.ToList() ?? new List<ConsumerInfo>()
            };
        }

        return new ConsumerListResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Failed to get consumers"
        };
    }

    /// <summary>
    /// Gets endpoint information.
    /// </summary>
    public async Task<EndpointListResult> GetEndpointsAsync(CancellationToken ct = default)
    {
        var response = await _apiClient.GetWithResponseAsync<EndpointListResponse>(
            "/api/messaging/endpoints",
            ct);

        if (response.Success && response.Data != null)
        {
            return new EndpointListResult
            {
                Success = true,
                Endpoints = response.Data.Endpoints?.ToList() ?? new List<EndpointInfo>()
            };
        }

        return new EndpointListResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Failed to get endpoints"
        };
    }

    /// <summary>
    /// Tests the message bus connection.
    /// </summary>
    public async Task<BusTestResult> TestConnectionAsync(CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<BusTestResponse>(
            "/api/messaging/test",
            null,
            ct);

        if (response.Success && response.Data != null)
        {
            return new BusTestResult
            {
                Success = response.Data.Connected,
                LatencyMs = response.Data.LatencyMs,
                Error = response.Data.Error
            };
        }

        return new BusTestResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Connection test failed"
        };
    }

    /// <summary>
    /// Updates the message bus configuration.
    /// </summary>
    public async Task<bool> UpdateConfigurationAsync(
        BusConfigurationUpdate config,
        CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<object>(
            "/api/messaging/config",
            config,
            ct);

        return response.Success;
    }

    /// <summary>
    /// Enables or disables message publishing.
    /// </summary>
    public async Task<bool> SetPublishingEnabledAsync(bool enabled, CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<object>(
            "/api/messaging/publishing",
            new { enabled },
            ct);

        return response.Success;
    }

    /// <summary>
    /// Purges messages from a queue.
    /// </summary>
    public async Task<PurgeResult> PurgeQueueAsync(string queueName, CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<PurgeResponse>(
            $"/api/messaging/queues/{Uri.EscapeDataString(queueName)}/purge",
            null,
            ct);

        if (response.Success && response.Data != null)
        {
            return new PurgeResult
            {
                Success = true,
                PurgedCount = response.Data.PurgedCount
            };
        }

        return new PurgeResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Failed to purge queue"
        };
    }

    /// <summary>
    /// Gets error queue messages.
    /// </summary>
    public async Task<ErrorQueueResult> GetErrorQueueMessagesAsync(
        int limit = 50,
        CancellationToken ct = default)
    {
        var response = await _apiClient.GetWithResponseAsync<ErrorQueueResponse>(
            $"/api/messaging/errors?limit={limit}",
            ct);

        if (response.Success && response.Data != null)
        {
            return new ErrorQueueResult
            {
                Success = true,
                Messages = response.Data.Messages?.ToList() ?? new List<ErrorMessage>(),
                TotalCount = response.Data.TotalCount
            };
        }

        return new ErrorQueueResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Failed to get error messages"
        };
    }

    /// <summary>
    /// Retries a failed message.
    /// </summary>
    public async Task<bool> RetryMessageAsync(string messageId, CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<object>(
            $"/api/messaging/errors/{messageId}/retry",
            null,
            ct);

        return response.Success;
    }
}

#region Event Args

public class BusStatusChangedEventArgs : EventArgs
{
    public BusStatus? Status { get; set; }
}

public class MessageActivityEventArgs : EventArgs
{
    public MessageInfo? Message { get; set; }
}

#endregion

#region Configuration Classes

public class BusConfiguration
{
    public string TransportType { get; set; } = "InMemory";
    public string? ConnectionString { get; set; }
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? VirtualHost { get; set; }
    public string? Username { get; set; }
    public bool IsEnabled { get; set; }
    public bool PublishingEnabled { get; set; }
    public int RetryCount { get; set; }
    public int RetryIntervalSeconds { get; set; }
    public List<string>? EnabledMessageTypes { get; set; }
}

public class BusConfigurationUpdate
{
    public string? TransportType { get; set; }
    public string? ConnectionString { get; set; }
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? VirtualHost { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool? IsEnabled { get; set; }
    public bool? PublishingEnabled { get; set; }
    public int? RetryCount { get; set; }
    public int? RetryIntervalSeconds { get; set; }
    public List<string>? EnabledMessageTypes { get; set; }
}

#endregion

#region Status Classes

public class BusStatus
{
    public bool IsConnected { get; set; }
    public string State { get; set; } = "Unknown";
    public string? TransportType { get; set; }
    public DateTime? LastConnectedAt { get; set; }
    public DateTime? LastDisconnectedAt { get; set; }
    public string? LastError { get; set; }
    public int ReconnectAttempts { get; set; }
}

public class MessageStatistics
{
    public long TotalPublished { get; set; }
    public long TotalConsumed { get; set; }
    public long TotalFailed { get; set; }
    public long PublishedPerSecond { get; set; }
    public long ConsumedPerSecond { get; set; }
    public double AverageLatencyMs { get; set; }
    public Dictionary<string, long>? MessageTypeBreakdown { get; set; }
}

public class MessageInfo
{
    public string Id { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty; // Published/Consumed
    public DateTime Timestamp { get; set; }
    public string? Symbol { get; set; }
    public string? Endpoint { get; set; }
    public double? LatencyMs { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class ConsumerInfo
{
    public string Name { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public long MessagesConsumed { get; set; }
    public long MessagesFailed { get; set; }
    public double AverageProcessingMs { get; set; }
}

public class EndpointInfo
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Queue/Topic
    public bool IsHealthy { get; set; }
    public int PendingMessages { get; set; }
    public List<string>? Consumers { get; set; }
}

public class ErrorMessage
{
    public string Id { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Error { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public int RetryCount { get; set; }
    public string? OriginalMessage { get; set; }
}

#endregion

#region Result Classes

public class MessageActivityResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<MessageInfo> Messages { get; set; } = new();
}

public class ConsumerListResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<ConsumerInfo> Consumers { get; set; } = new();
}

public class EndpointListResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<EndpointInfo> Endpoints { get; set; } = new();
}

public class BusTestResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public double LatencyMs { get; set; }
}

public class PurgeResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int PurgedCount { get; set; }
}

public class ErrorQueueResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<ErrorMessage> Messages { get; set; } = new();
    public int TotalCount { get; set; }
}

#endregion

#region API Response Classes

public class MessageActivityResponse
{
    public List<MessageInfo>? Messages { get; set; }
}

public class ConsumerListResponse
{
    public List<ConsumerInfo>? Consumers { get; set; }
}

public class EndpointListResponse
{
    public List<EndpointInfo>? Endpoints { get; set; }
}

public class BusTestResponse
{
    public bool Connected { get; set; }
    public double LatencyMs { get; set; }
    public string? Error { get; set; }
}

public class PurgeResponse
{
    public int PurgedCount { get; set; }
}

public class ErrorQueueResponse
{
    public List<ErrorMessage>? Messages { get; set; }
    public int TotalCount { get; set; }
}

#endregion
