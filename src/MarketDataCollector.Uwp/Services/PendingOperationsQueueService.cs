using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for managing a queue of pending operations that can be executed when connectivity is restored.
/// Supports offline queuing with automatic retry and dependency management.
/// </summary>
public sealed class PendingOperationsQueueService : IPendingOperationsQueueService
{
    private static PendingOperationsQueueService? _instance;
    private static readonly object _lock = new();

    private readonly string _dataDirectory;
    private readonly string _queueFilePath;
    private readonly ConnectionService _connectionService;
    private readonly NotificationService _notificationService;

    private OfflineQueueConfig _config;
    private readonly ConcurrentDictionary<string, PendingOperation> _operations;
    private readonly SemaphoreSlim _processLock = new(1, 1);
    private Timer? _processTimer;
    private bool _isProcessing;
    private CancellationTokenSource? _processCts;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static PendingOperationsQueueService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new PendingOperationsQueueService();
                }
            }
            return _instance;
        }
    }

    private PendingOperationsQueueService()
    {
        _dataDirectory = Path.Combine(AppContext.BaseDirectory, "data", "_persistence");
        _queueFilePath = Path.Combine(_dataDirectory, "pending_operations.json");

        _connectionService = ConnectionService.Instance;
        _notificationService = NotificationService.Instance;

        _config = new OfflineQueueConfig();
        _operations = new ConcurrentDictionary<string, PendingOperation>();
    }

    /// <summary>
    /// Gets the current queue configuration.
    /// </summary>
    public OfflineQueueConfig Config => _config;

    /// <summary>
    /// Gets the number of pending operations.
    /// </summary>
    public int PendingCount => _operations.Count(o => o.Value.Status == PendingOperationStatus.Queued ||
                                                       o.Value.Status == PendingOperationStatus.WaitingForConnection);

    /// <summary>
    /// Gets all pending operations.
    /// </summary>
    public IReadOnlyList<PendingOperation> PendingOperations => _operations.Values
        .Where(o => o.Status == PendingOperationStatus.Queued || o.Status == PendingOperationStatus.WaitingForConnection)
        .OrderBy(o => o.Priority)
        .ThenBy(o => o.CreatedAt)
        .ToList();

    /// <summary>
    /// Initializes the queue service.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(_dataDirectory);
            await LoadQueueAsync();

            // Subscribe to connection state changes
            _connectionService.StateChanged += OnConnectionStateChanged;

            // Start processing timer
            if (_config.IsEnabled)
            {
                StartProcessingTimer();
            }

            LoggingService.Instance.LogInfo("PendingOperationsQueueService initialized", ("operationCount", _operations.Count.ToString()));
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Failed to initialize PendingOperationsQueueService", ex);
            throw;
        }
    }

    /// <summary>
    /// Shuts down the queue service.
    /// </summary>
    public async Task ShutdownAsync()
    {
        try
        {
            _connectionService.StateChanged -= OnConnectionStateChanged;
            _processCts?.Cancel();
            _processTimer?.Dispose();

            await SaveQueueAsync();

            LoggingService.Instance.LogInfo("PendingOperationsQueueService shut down");
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Error during PendingOperationsQueueService shutdown", ex);
        }
    }

    #region Queue Operations

    /// <summary>
    /// Enqueues an operation for later processing.
    /// </summary>
    public async Task<PendingOperation> EnqueueAsync(
        PendingOperationType operationType,
        object payload,
        int priority = 100,
        bool requiresConnection = true,
        TimeSpan? expiration = null,
        string? dependsOnOperationId = null)
    {
        if (_operations.Count >= _config.MaxQueueSize)
        {
            throw new InvalidOperationException($"Queue is full (max: {_config.MaxQueueSize})");
        }

        var operation = new PendingOperation
        {
            OperationType = operationType,
            Payload = JsonSerializer.Serialize(payload, JsonOptions),
            Priority = priority,
            RequiresConnection = requiresConnection,
            ExpiresAt = expiration.HasValue ? DateTime.UtcNow.Add(expiration.Value) : DateTime.UtcNow.AddHours(_config.DefaultExpirationHours),
            DependsOnOperationId = dependsOnOperationId,
            Status = requiresConnection && !_connectionService.IsConnected
                ? PendingOperationStatus.WaitingForConnection
                : PendingOperationStatus.Queued
        };

        _operations[operation.Id] = operation;
        await SaveQueueAsync();

        OperationQueued?.Invoke(this, new OperationEventArgs { Operation = operation });

        LoggingService.Instance.LogInfo("Enqueued operation", ("operationType", operationType.ToString()), ("operationId", operation.Id));
        return operation;
    }

    /// <summary>
    /// Enqueues a subscribe operation.
    /// </summary>
    public Task<PendingOperation> EnqueueSubscribeAsync(string symbol, string provider, string subscriptionType, SymbolConfig? config = null)
    {
        var payload = new PersistedSubscription
        {
            Symbol = symbol,
            Provider = provider,
            SubscriptionType = subscriptionType,
            Config = config
        };

        return EnqueueAsync(PendingOperationType.Subscribe, payload, priority: 50);
    }

    /// <summary>
    /// Enqueues a backfill operation.
    /// </summary>
    public Task<PendingOperation> EnqueueBackfillAsync(string[] symbols, string? provider = null, string? fromDate = null, string? toDate = null)
    {
        var payload = new BackfillTaskPayload
        {
            Symbols = symbols,
            Provider = provider,
            FromDate = fromDate,
            ToDate = toDate
        };

        return EnqueueAsync(PendingOperationType.StartBackfill, payload, priority: 100);
    }

    /// <summary>
    /// Cancels a pending operation.
    /// </summary>
    public async Task<bool> CancelOperationAsync(string operationId)
    {
        if (_operations.TryGetValue(operationId, out var operation))
        {
            if (operation.Status == PendingOperationStatus.Processing)
            {
                return false; // Cannot cancel while processing
            }

            operation.Status = PendingOperationStatus.Cancelled;
            await SaveQueueAsync();

            OperationCancelled?.Invoke(this, new OperationEventArgs { Operation = operation });
            return true;
        }
        return false;
    }

    /// <summary>
    /// Removes a completed or cancelled operation from the queue.
    /// </summary>
    public async Task RemoveOperationAsync(string operationId)
    {
        if (_operations.TryRemove(operationId, out _))
        {
            await SaveQueueAsync();
        }
    }

    /// <summary>
    /// Gets an operation by ID.
    /// </summary>
    public PendingOperation? GetOperation(string operationId)
    {
        _operations.TryGetValue(operationId, out var operation);
        return operation;
    }

    /// <summary>
    /// Clears all completed, failed, cancelled, and expired operations.
    /// </summary>
    public async Task ClearCompletedOperationsAsync()
    {
        var toRemove = _operations
            .Where(kvp => kvp.Value.Status == PendingOperationStatus.Completed ||
                         kvp.Value.Status == PendingOperationStatus.Failed ||
                         kvp.Value.Status == PendingOperationStatus.Cancelled ||
                         kvp.Value.Status == PendingOperationStatus.Expired)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var id in toRemove)
        {
            _operations.TryRemove(id, out _);
        }

        await SaveQueueAsync();
    }

    #endregion

    #region Processing

    /// <summary>
    /// Processes all pending operations immediately.
    /// </summary>
    public async Task ProcessQueueAsync(CancellationToken cancellationToken = default)
    {
        if (_isProcessing) return;

        await _processLock.WaitAsync(cancellationToken);
        try
        {
            _isProcessing = true;
            _processCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Check for expired operations
            await ExpireOperationsAsync();

            // Get operations ready for processing
            var readyOperations = _operations.Values
                .Where(o => IsOperationReady(o))
                .OrderBy(o => o.Priority)
                .ThenBy(o => o.CreatedAt)
                .Take(_config.BatchSize)
                .ToList();

            if (readyOperations.Count == 0) return;

            LoggingService.Instance.LogInfo("Processing pending operations", ("count", readyOperations.Count.ToString()));

            foreach (var operation in readyOperations)
            {
                if (_processCts.Token.IsCancellationRequested) break;

                await ProcessOperationAsync(operation, _processCts.Token);
            }

            _config.LastProcessedAt = DateTime.UtcNow;
            await SaveQueueAsync();
        }
        finally
        {
            _isProcessing = false;
            _processLock.Release();
        }
    }

    private bool IsOperationReady(PendingOperation operation)
    {
        // Check if operation is in a processable state
        if (operation.Status != PendingOperationStatus.Queued &&
            operation.Status != PendingOperationStatus.WaitingForConnection)
        {
            return false;
        }

        // Check connection requirement
        if (operation.RequiresConnection && !_connectionService.IsConnected)
        {
            return false;
        }

        // Check expiration
        if (operation.ExpiresAt.HasValue && operation.ExpiresAt.Value < DateTime.UtcNow)
        {
            return false;
        }

        // Check dependencies
        if (!string.IsNullOrEmpty(operation.DependsOnOperationId))
        {
            if (_operations.TryGetValue(operation.DependsOnOperationId, out var dependency))
            {
                if (dependency.Status != PendingOperationStatus.Completed)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private async Task ProcessOperationAsync(PendingOperation operation, CancellationToken cancellationToken)
    {
        operation.Status = PendingOperationStatus.Processing;

        try
        {
            LoggingService.Instance.LogDebug("Processing operation", ("operationType", operation.OperationType.ToString()), ("operationId", operation.Id));

            switch (operation.OperationType)
            {
                case PendingOperationType.Subscribe:
                    await ProcessSubscribeOperationAsync(operation, cancellationToken);
                    break;

                case PendingOperationType.Unsubscribe:
                    await ProcessUnsubscribeOperationAsync(operation, cancellationToken);
                    break;

                case PendingOperationType.StartBackfill:
                    await ProcessBackfillOperationAsync(operation, cancellationToken);
                    break;

                case PendingOperationType.StartSession:
                    await ProcessStartSessionOperationAsync(operation, cancellationToken);
                    break;

                case PendingOperationType.StopSession:
                    await ProcessStopSessionOperationAsync(operation, cancellationToken);
                    break;

                case PendingOperationType.VerifyArchive:
                    await ProcessVerifyArchiveOperationAsync(operation, cancellationToken);
                    break;

                case PendingOperationType.ExportData:
                    await ProcessExportOperationAsync(operation, cancellationToken);
                    break;

                default:
                    LoggingService.Instance.LogWarning("Unknown operation type", ("operationType", operation.OperationType.ToString()));
                    break;
            }

            operation.Status = PendingOperationStatus.Completed;
            operation.ProcessedAt = DateTime.UtcNow;

            OperationCompleted?.Invoke(this, new OperationEventArgs { Operation = operation });
        }
        catch (Exception ex)
        {
            operation.RetryCount++;
            operation.LastError = ex.Message;

            if (operation.RetryCount >= operation.MaxRetries)
            {
                operation.Status = PendingOperationStatus.Failed;
                OperationFailed?.Invoke(this, new OperationEventArgs { Operation = operation, Error = ex.Message });

                await _notificationService.NotifyErrorAsync(
                    "Operation Failed",
                    $"{operation.OperationType}: {ex.Message}");
            }
            else
            {
                operation.Status = PendingOperationStatus.Queued;
                LoggingService.Instance.LogWarning("Operation failed, will retry", ("operationId", operation.Id), ("retryCount", operation.RetryCount.ToString()), ("maxRetries", operation.MaxRetries.ToString()));
            }
        }

        await SaveQueueAsync();
    }

    private async Task ProcessSubscribeOperationAsync(PendingOperation operation, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<PersistedSubscription>(operation.Payload, JsonOptions);
        if (payload == null) return;

        // Persist the subscription for recovery
        var persistenceService = OfflineTrackingPersistenceService.Instance;
        await persistenceService.PersistSubscriptionAsync(payload);

        // The actual subscription would be handled by the connection manager
        LoggingService.Instance.LogDebug("Subscription queued", ("symbol", payload.Symbol), ("provider", payload.Provider));
    }

    private async Task ProcessUnsubscribeOperationAsync(PendingOperation operation, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<PersistedSubscription>(operation.Payload, JsonOptions);
        if (payload == null) return;

        var persistenceService = OfflineTrackingPersistenceService.Instance;
        await persistenceService.DeactivateSubscriptionAsync(payload.Symbol, payload.Provider, payload.SubscriptionType);
    }

    private async Task ProcessBackfillOperationAsync(PendingOperation operation, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<BackfillTaskPayload>(operation.Payload, JsonOptions);
        if (payload == null) return;

        var backfillService = BackfillService.Instance;
        var result = await backfillService.RunBackfillAsync(
            payload.Symbols,
            payload.Provider,
            payload.FromDate,
            payload.ToDate,
            cancellationToken);

        LoggingService.Instance.LogInfo("Backfill completed", ("barsWritten", (result?.BarsWritten ?? 0).ToString()));
    }

    private async Task ProcessStartSessionOperationAsync(PendingOperation operation, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<CollectionTaskPayload>(operation.Payload, JsonOptions);
        if (payload == null) return;

        var sessionService = CollectionSessionService.Instance;

        if (payload.AutoCreateSession)
        {
            var session = await sessionService.CreateDailySessionAsync(
                payload.Symbols ?? Array.Empty<string>(),
                payload.EventTypes ?? new[] { "Trade", "Quote" },
                payload.Provider);

            await sessionService.StartSessionAsync(session.Id);
        }
    }

    private async Task ProcessStopSessionOperationAsync(PendingOperation operation, CancellationToken cancellationToken)
    {
        var sessionService = CollectionSessionService.Instance;
        var activeSessions = await sessionService.GetActiveSessionsAsync();

        foreach (var session in activeSessions)
        {
            await sessionService.StopSessionAsync(session.Id);
        }
    }

    private async Task ProcessVerifyArchiveOperationAsync(PendingOperation operation, CancellationToken cancellationToken)
    {
        var archiveService = ArchiveHealthService.Instance;
        await archiveService.RunVerificationAsync(cancellationToken);
    }

    private async Task ProcessExportOperationAsync(PendingOperation operation, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<ExportTaskPayload>(operation.Payload, JsonOptions);
        if (payload == null) return;

        // Export implementation would go here
        await Task.CompletedTask;
    }

    private async Task ExpireOperationsAsync()
    {
        var now = DateTime.UtcNow;
        var expiredOperations = _operations.Values
            .Where(o => o.ExpiresAt.HasValue &&
                       o.ExpiresAt.Value < now &&
                       o.Status != PendingOperationStatus.Completed &&
                       o.Status != PendingOperationStatus.Expired)
            .ToList();

        foreach (var operation in expiredOperations)
        {
            operation.Status = PendingOperationStatus.Expired;
            OperationExpired?.Invoke(this, new OperationEventArgs { Operation = operation });
        }

        if (expiredOperations.Count > 0)
        {
            await SaveQueueAsync();
            LoggingService.Instance.LogInfo("Expired operations", ("count", expiredOperations.Count.ToString()));
        }
    }

    #endregion

    #region Connection Handling

    private void OnConnectionStateChanged(object? sender, ConnectionStateEventArgs e)
    {
        // Fire-and-forget the async work, with proper exception handling in the async method
        _ = SafeHandleConnectionStateChangedAsync(e);
    }

    private async Task SafeHandleConnectionStateChangedAsync(ConnectionStateEventArgs e)
    {
        try
        {
            await HandleConnectionStateChangedAsync(e);
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Error handling connection state change", ex);
        }
    }

    private async Task HandleConnectionStateChangedAsync(ConnectionStateEventArgs e)
    {
        if (e.NewState == ConnectionState.Connected && _config.ProcessOnReconnect)
        {
            // Update waiting operations to queued
            foreach (var operation in _operations.Values.Where(o => o.Status == PendingOperationStatus.WaitingForConnection))
            {
                operation.Status = PendingOperationStatus.Queued;
            }

            // Process the queue
            await ProcessQueueAsync();
        }
        else if (e.NewState == ConnectionState.Disconnected)
        {
            // Mark connection-requiring operations as waiting
            foreach (var operation in _operations.Values.Where(o =>
                o.RequiresConnection &&
                o.Status == PendingOperationStatus.Queued))
            {
                operation.Status = PendingOperationStatus.WaitingForConnection;
            }

            await SaveQueueAsync();
        }
    }

    #endregion

    #region Timer Management

    private void StartProcessingTimer()
    {
        var interval = TimeSpan.FromSeconds(_config.ProcessIntervalSeconds);
        _processTimer = new Timer(
            async _ =>
            {
                try
                {
                    await ProcessQueueAsync();
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError("Error in queue processing timer", ex);
                }
            },
            null,
            interval,
            interval);
    }

    #endregion

    #region Persistence

    private async Task LoadQueueAsync()
    {
        try
        {
            if (File.Exists(_queueFilePath))
            {
                var json = await File.ReadAllTextAsync(_queueFilePath);
                _config = JsonSerializer.Deserialize<OfflineQueueConfig>(json, JsonOptions) ?? new OfflineQueueConfig();

                _operations.Clear();
                foreach (var operation in _config.Operations)
                {
                    _operations[operation.Id] = operation;
                }
            }
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Failed to load queue", ex);
            _config = new OfflineQueueConfig();
        }
    }

    private async Task SaveQueueAsync()
    {
        try
        {
            _config.Operations = _operations.Values.ToArray();
            var json = JsonSerializer.Serialize(_config, JsonOptions);
            await File.WriteAllTextAsync(_queueFilePath, json);
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Failed to save queue", ex);
        }
    }

    #endregion

    #region Events

    public event EventHandler<OperationEventArgs>? OperationQueued;
    public event EventHandler<OperationEventArgs>? OperationCompleted;
    public event EventHandler<OperationEventArgs>? OperationFailed;
    public event EventHandler<OperationEventArgs>? OperationCancelled;
    public event EventHandler<OperationEventArgs>? OperationExpired;

    #endregion
}

public class OperationEventArgs : EventArgs
{
    public PendingOperation? Operation { get; set; }
    public string? Error { get; set; }
}
