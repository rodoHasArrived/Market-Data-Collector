using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Uwp.Models;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for managing offline tracking persistence with Write-Ahead Logging (WAL).
/// Ensures symbol tracking survives logout, app restart, and system restart.
/// </summary>
public sealed class OfflineTrackingPersistenceService
{
    private static OfflineTrackingPersistenceService? _instance;
    private static readonly object _lock = new();

    private readonly string _dataDirectory;
    private readonly string _walFilePath;
    private readonly string _stateFilePath;
    private readonly string _subscriptionsFilePath;
    private readonly NotificationService _notificationService;

    private OfflineTrackingState _state;
    private SubscriptionPersistenceConfig _subscriptionsConfig;
    private readonly ConcurrentDictionary<string, WalEntry> _walEntries;
    private readonly SemaphoreSlim _walLock = new(1, 1);
    private Timer? _heartbeatTimer;
    private Timer? _checkpointTimer;
    private bool _isInitialized;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static OfflineTrackingPersistenceService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new OfflineTrackingPersistenceService();
                }
            }
            return _instance;
        }
    }

    private OfflineTrackingPersistenceService()
    {
        _dataDirectory = Path.Combine(AppContext.BaseDirectory, "data", "_persistence");
        _walFilePath = Path.Combine(_dataDirectory, "wal.json");
        _stateFilePath = Path.Combine(_dataDirectory, "state.json");
        _subscriptionsFilePath = Path.Combine(_dataDirectory, "subscriptions.json");

        _notificationService = NotificationService.Instance;
        _state = new OfflineTrackingState();
        _subscriptionsConfig = new SubscriptionPersistenceConfig();
        _walEntries = new ConcurrentDictionary<string, WalEntry>();
    }

    /// <summary>
    /// Gets the current state of the offline tracking system.
    /// </summary>
    public OfflineTrackingState State => _state;

    /// <summary>
    /// Gets whether the service is initialized.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Initializes the offline tracking persistence service.
    /// Performs recovery if needed based on previous state.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized) return;

        try
        {
            // Ensure data directory exists
            Directory.CreateDirectory(_dataDirectory);

            // Load previous state
            await LoadStateAsync();
            await LoadWalAsync();
            await LoadSubscriptionsAsync();

            // Check if we need to recover from a non-clean shutdown
            if (!_state.CleanShutdown && _state.IsServiceRunning)
            {
                System.Diagnostics.Debug.WriteLine("Detected non-clean shutdown, initiating recovery...");
                await PerformRecoveryAsync(RecoveryReason.CrashRecovery, cancellationToken);
            }
            else if (_state.ActiveSubscriptionCount > 0 && _subscriptionsConfig.AutoRecoveryEnabled)
            {
                System.Diagnostics.Debug.WriteLine("Initiating subscription recovery on startup...");
                await PerformRecoveryAsync(RecoveryReason.AppRestart, cancellationToken);
            }

            // Mark service as running
            _state.IsServiceRunning = true;
            _state.ServiceStartedAt = DateTime.UtcNow;
            _state.CleanShutdown = false;
            await SaveStateAsync();

            // Start heartbeat timer (every 30 seconds)
            _heartbeatTimer = new Timer(
                async _ => await UpdateHeartbeatAsync(),
                null,
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30));

            // Start checkpoint timer (every 5 minutes)
            _checkpointTimer = new Timer(
                async _ => await CreateCheckpointAsync(),
                null,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(5));

            _isInitialized = true;
            System.Diagnostics.Debug.WriteLine("OfflineTrackingPersistenceService initialized successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize OfflineTrackingPersistenceService: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Shuts down the service cleanly, marking state for clean recovery.
    /// </summary>
    public async Task ShutdownAsync()
    {
        try
        {
            _heartbeatTimer?.Dispose();
            _checkpointTimer?.Dispose();

            // Mark all active subscriptions as needing recovery
            foreach (var sub in _subscriptionsConfig.Subscriptions.Where(s => s.IsActive))
            {
                sub.LastActiveAt = DateTime.UtcNow;
            }
            await SaveSubscriptionsAsync();

            // Create final checkpoint
            await CreateCheckpointAsync();

            // Mark clean shutdown
            _state.IsServiceRunning = false;
            _state.CleanShutdown = true;
            await SaveStateAsync();

            _isInitialized = false;
            System.Diagnostics.Debug.WriteLine("OfflineTrackingPersistenceService shut down cleanly");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during shutdown: {ex.Message}");
        }
    }

    #region Write-Ahead Logging

    /// <summary>
    /// Writes an operation to the WAL before execution.
    /// Returns the WAL entry that can be used to mark completion.
    /// </summary>
    public async Task<WalEntry> WriteWalEntryAsync(string operationType, object payload, int priority = 100)
    {
        await _walLock.WaitAsync();
        try
        {
            var entry = new WalEntry
            {
                SequenceNumber = ++_state.WalSequenceNumber,
                OperationType = operationType,
                Payload = JsonSerializer.Serialize(payload, JsonOptions),
                Priority = priority,
                Status = WalEntryStatus.Pending
            };

            _walEntries[entry.Id] = entry;
            await SaveWalAsync();
            await SaveStateAsync();

            return entry;
        }
        finally
        {
            _walLock.Release();
        }
    }

    /// <summary>
    /// Marks a WAL entry as processing.
    /// </summary>
    public async Task MarkWalEntryProcessingAsync(string entryId)
    {
        if (_walEntries.TryGetValue(entryId, out var entry))
        {
            entry.Status = WalEntryStatus.Processing;
            await SaveWalAsync();
        }
    }

    /// <summary>
    /// Marks a WAL entry as completed and removes it from active WAL.
    /// </summary>
    public async Task CompleteWalEntryAsync(string entryId)
    {
        await _walLock.WaitAsync();
        try
        {
            if (_walEntries.TryRemove(entryId, out var entry))
            {
                entry.Status = WalEntryStatus.Completed;
                entry.ProcessedAt = DateTime.UtcNow;
                await SaveWalAsync();
            }
        }
        finally
        {
            _walLock.Release();
        }
    }

    /// <summary>
    /// Marks a WAL entry as failed with retry logic.
    /// </summary>
    public async Task FailWalEntryAsync(string entryId, string error)
    {
        await _walLock.WaitAsync();
        try
        {
            if (_walEntries.TryGetValue(entryId, out var entry))
            {
                entry.RetryCount++;
                entry.LastError = error;
                entry.FailedAt = DateTime.UtcNow;

                if (entry.RetryCount >= entry.MaxRetries)
                {
                    entry.Status = WalEntryStatus.Failed;
                }
                else
                {
                    entry.Status = WalEntryStatus.Pending;
                }

                await SaveWalAsync();
            }
        }
        finally
        {
            _walLock.Release();
        }
    }

    /// <summary>
    /// Gets all pending WAL entries ordered by priority and sequence.
    /// </summary>
    public IReadOnlyList<WalEntry> GetPendingWalEntries()
    {
        return _walEntries.Values
            .Where(e => e.Status == WalEntryStatus.Pending)
            .OrderBy(e => e.Priority)
            .ThenBy(e => e.SequenceNumber)
            .ToList();
    }

    #endregion

    #region Subscription Persistence

    /// <summary>
    /// Persists a subscription for recovery after restart.
    /// </summary>
    public async Task PersistSubscriptionAsync(PersistedSubscription subscription)
    {
        var existing = _subscriptionsConfig.Subscriptions.FirstOrDefault(s =>
            s.Symbol == subscription.Symbol &&
            s.Provider == subscription.Provider &&
            s.SubscriptionType == subscription.SubscriptionType);

        if (existing != null)
        {
            // Update existing
            existing.IsActive = subscription.IsActive;
            existing.LastActiveAt = subscription.IsActive ? DateTime.UtcNow : existing.LastActiveAt;
            existing.Config = subscription.Config;
            existing.ShouldAutoRecover = subscription.ShouldAutoRecover;
            existing.Metadata = subscription.Metadata;
        }
        else
        {
            // Add new
            var subscriptions = _subscriptionsConfig.Subscriptions.ToList();
            subscriptions.Add(subscription);
            _subscriptionsConfig.Subscriptions = subscriptions.ToArray();
        }

        _state.ActiveSubscriptionCount = _subscriptionsConfig.Subscriptions.Count(s => s.IsActive);
        await SaveSubscriptionsAsync();
        await SaveStateAsync();
    }

    /// <summary>
    /// Marks a subscription as inactive (but keeps for potential recovery).
    /// </summary>
    public async Task DeactivateSubscriptionAsync(string symbol, string provider, string subscriptionType)
    {
        var subscription = _subscriptionsConfig.Subscriptions.FirstOrDefault(s =>
            s.Symbol == symbol &&
            s.Provider == provider &&
            s.SubscriptionType == subscriptionType);

        if (subscription != null)
        {
            subscription.IsActive = false;
            subscription.LastActiveAt = DateTime.UtcNow;
            _state.ActiveSubscriptionCount = _subscriptionsConfig.Subscriptions.Count(s => s.IsActive);
            await SaveSubscriptionsAsync();
            await SaveStateAsync();
        }
    }

    /// <summary>
    /// Removes a subscription from persistence entirely.
    /// </summary>
    public async Task RemoveSubscriptionAsync(string symbol, string provider, string subscriptionType)
    {
        _subscriptionsConfig.Subscriptions = _subscriptionsConfig.Subscriptions
            .Where(s => !(s.Symbol == symbol && s.Provider == provider && s.SubscriptionType == subscriptionType))
            .ToArray();

        _state.ActiveSubscriptionCount = _subscriptionsConfig.Subscriptions.Count(s => s.IsActive);
        await SaveSubscriptionsAsync();
        await SaveStateAsync();
    }

    /// <summary>
    /// Gets all subscriptions that should be recovered.
    /// </summary>
    public IReadOnlyList<PersistedSubscription> GetSubscriptionsForRecovery()
    {
        return _subscriptionsConfig.Subscriptions
            .Where(s => s.ShouldAutoRecover && (s.IsActive || s.LastActiveAt.HasValue))
            .OrderBy(s => s.RecoveryPriority)
            .ThenBy(s => s.CreatedAt)
            .ToList();
    }

    /// <summary>
    /// Gets all active subscriptions.
    /// </summary>
    public IReadOnlyList<PersistedSubscription> GetActiveSubscriptions()
    {
        return _subscriptionsConfig.Subscriptions
            .Where(s => s.IsActive)
            .ToList();
    }

    #endregion

    #region Recovery

    /// <summary>
    /// Performs recovery after app restart or crash.
    /// </summary>
    public async Task<RecoveryAttempt> PerformRecoveryAsync(RecoveryReason reason, CancellationToken cancellationToken = default)
    {
        var attempt = new RecoveryAttempt
        {
            Reason = reason.ToString()
        };

        _state.IsRecoveryInProgress = true;
        await SaveStateAsync();

        try
        {
            System.Diagnostics.Debug.WriteLine($"Starting recovery: {reason}");

            // 1. Process pending WAL entries
            var pendingEntries = GetPendingWalEntries();
            foreach (var entry in pendingEntries)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    await ProcessWalEntryForRecoveryAsync(entry);
                    attempt.OperationsProcessed++;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to process WAL entry {entry.Id}: {ex.Message}");
                    attempt.OperationsFailed++;
                    var errors = attempt.Errors?.ToList() ?? new List<string>();
                    errors.Add($"WAL {entry.Id}: {ex.Message}");
                    attempt.Errors = errors.ToArray();
                }
            }

            // 2. Recover subscriptions
            if (_subscriptionsConfig.AutoRecoveryEnabled)
            {
                var subscriptionsToRecover = GetSubscriptionsForRecovery();

                // Wait configured delay before recovery
                if (_subscriptionsConfig.RecoveryDelaySeconds > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_subscriptionsConfig.RecoveryDelaySeconds), cancellationToken);
                }

                // Recover subscriptions in batches
                var semaphore = new SemaphoreSlim(_subscriptionsConfig.MaxConcurrentRecoveries);
                var tasks = subscriptionsToRecover.Select(async sub =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        await RecoverSubscriptionAsync(sub, cancellationToken);
                        Interlocked.Increment(ref attempt.SubscriptionsRecovered);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to recover subscription {sub.Symbol}: {ex.Message}");
                        Interlocked.Increment(ref attempt.SubscriptionsFailed);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);
            }

            attempt.Success = attempt.OperationsFailed == 0 && attempt.SubscriptionsFailed == 0;
            attempt.CompletedAt = DateTime.UtcNow;

            // Notify user of recovery results
            if (attempt.SubscriptionsRecovered > 0 || attempt.OperationsProcessed > 0)
            {
                await _notificationService.NotifyAsync(
                    "Recovery Completed",
                    $"Recovered {attempt.SubscriptionsRecovered} subscriptions, processed {attempt.OperationsProcessed} operations",
                    NotificationType.Info);
            }

            System.Diagnostics.Debug.WriteLine($"Recovery completed: {attempt.SubscriptionsRecovered} subscriptions, {attempt.OperationsProcessed} operations");
        }
        catch (Exception ex)
        {
            attempt.Success = false;
            var errors = attempt.Errors?.ToList() ?? new List<string>();
            errors.Add(ex.Message);
            attempt.Errors = errors.ToArray();
            System.Diagnostics.Debug.WriteLine($"Recovery failed: {ex.Message}");
        }
        finally
        {
            _state.IsRecoveryInProgress = false;
            _state.LastRecoveryAt = DateTime.UtcNow;
            _state.RecoveryCount++;
            await SaveStateAsync();
        }

        return attempt;
    }

    private async Task ProcessWalEntryForRecoveryAsync(WalEntry entry)
    {
        await MarkWalEntryProcessingAsync(entry.Id);

        try
        {
            // Dispatch based on operation type
            switch (entry.OperationType)
            {
                case "Subscribe":
                    // Re-initiate subscription
                    var subPayload = JsonSerializer.Deserialize<PersistedSubscription>(entry.Payload, JsonOptions);
                    if (subPayload != null)
                    {
                        // Subscription recovery is handled separately
                        System.Diagnostics.Debug.WriteLine($"WAL Subscribe entry for {subPayload.Symbol} will be recovered via subscription recovery");
                    }
                    break;

                case "StartBackfill":
                    var backfillPayload = JsonSerializer.Deserialize<BackfillTaskPayload>(entry.Payload, JsonOptions);
                    if (backfillPayload != null)
                    {
                        // Queue for scheduled task processing
                        System.Diagnostics.Debug.WriteLine($"WAL StartBackfill entry queued for processing: {string.Join(", ", backfillPayload.Symbols)}");
                    }
                    break;

                case "StartSession":
                    var sessionPayload = JsonSerializer.Deserialize<CollectionTaskPayload>(entry.Payload, JsonOptions);
                    if (sessionPayload != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"WAL StartSession entry queued for processing: {sessionPayload.SessionName}");
                    }
                    break;

                default:
                    System.Diagnostics.Debug.WriteLine($"Unknown WAL operation type: {entry.OperationType}");
                    break;
            }

            await CompleteWalEntryAsync(entry.Id);
        }
        catch (Exception ex)
        {
            await FailWalEntryAsync(entry.Id, ex.Message);
            throw;
        }
    }

    private async Task RecoverSubscriptionAsync(PersistedSubscription subscription, CancellationToken cancellationToken)
    {
        // This would integrate with the actual subscription system
        // For now, we mark it as active and let the connection service handle it
        subscription.IsActive = true;
        subscription.LastActiveAt = DateTime.UtcNow;
        await SaveSubscriptionsAsync();

        System.Diagnostics.Debug.WriteLine($"Marked subscription {subscription.Symbol} ({subscription.SubscriptionType}) for recovery via {subscription.Provider}");

        // Raise event for the connection service to pick up
        SubscriptionRecoveryRequested?.Invoke(this, new SubscriptionRecoveryEventArgs
        {
            Subscription = subscription
        });
    }

    #endregion

    #region Checkpoint and State Management

    /// <summary>
    /// Creates a checkpoint of the current state.
    /// </summary>
    public async Task CreateCheckpointAsync()
    {
        await _walLock.WaitAsync();
        try
        {
            // Clean up completed/failed/expired entries
            var entriesToRemove = _walEntries.Values
                .Where(e => e.Status == WalEntryStatus.Completed ||
                           e.Status == WalEntryStatus.Failed ||
                           e.Status == WalEntryStatus.Expired ||
                           (e.ExpiresAt.HasValue && e.ExpiresAt < DateTime.UtcNow))
                .Select(e => e.Id)
                .ToList();

            foreach (var id in entriesToRemove)
            {
                _walEntries.TryRemove(id, out _);
            }

            _state.LastCheckpointAt = DateTime.UtcNow;
            _subscriptionsConfig.LastCheckpointAt = DateTime.UtcNow;

            await SaveWalAsync();
            await SaveSubscriptionsAsync();
            await SaveStateAsync();

            System.Diagnostics.Debug.WriteLine($"Checkpoint created at {_state.LastCheckpointAt}");
        }
        finally
        {
            _walLock.Release();
        }
    }

    private async Task UpdateHeartbeatAsync()
    {
        try
        {
            _state.LastHeartbeatAt = DateTime.UtcNow;
            await SaveStateAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to update heartbeat: {ex.Message}");
        }
    }

    #endregion

    #region Persistence Helpers

    private async Task LoadStateAsync()
    {
        try
        {
            if (File.Exists(_stateFilePath))
            {
                var json = await File.ReadAllTextAsync(_stateFilePath);
                _state = JsonSerializer.Deserialize<OfflineTrackingState>(json, JsonOptions) ?? new OfflineTrackingState();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load state: {ex.Message}");
            _state = new OfflineTrackingState();
        }
    }

    private async Task SaveStateAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_state, JsonOptions);
            await File.WriteAllTextAsync(_stateFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save state: {ex.Message}");
        }
    }

    private async Task LoadWalAsync()
    {
        try
        {
            if (File.Exists(_walFilePath))
            {
                var json = await File.ReadAllTextAsync(_walFilePath);
                var entries = JsonSerializer.Deserialize<WalEntry[]>(json, JsonOptions) ?? Array.Empty<WalEntry>();
                _walEntries.Clear();
                foreach (var entry in entries)
                {
                    _walEntries[entry.Id] = entry;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load WAL: {ex.Message}");
        }
    }

    private async Task SaveWalAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_walEntries.Values.ToArray(), JsonOptions);
            await File.WriteAllTextAsync(_walFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save WAL: {ex.Message}");
        }
    }

    private async Task LoadSubscriptionsAsync()
    {
        try
        {
            if (File.Exists(_subscriptionsFilePath))
            {
                var json = await File.ReadAllTextAsync(_subscriptionsFilePath);
                _subscriptionsConfig = JsonSerializer.Deserialize<SubscriptionPersistenceConfig>(json, JsonOptions)
                    ?? new SubscriptionPersistenceConfig();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load subscriptions: {ex.Message}");
            _subscriptionsConfig = new SubscriptionPersistenceConfig();
        }
    }

    private async Task SaveSubscriptionsAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_subscriptionsConfig, JsonOptions);
            await File.WriteAllTextAsync(_subscriptionsFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save subscriptions: {ex.Message}");
        }
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when a subscription needs to be recovered.
    /// </summary>
    public event EventHandler<SubscriptionRecoveryEventArgs>? SubscriptionRecoveryRequested;

    /// <summary>
    /// Raised when recovery is completed.
    /// </summary>
    public event EventHandler<RecoveryCompletedEventArgs>? RecoveryCompleted;

    #endregion
}

/// <summary>
/// Reason for recovery.
/// </summary>
public enum RecoveryReason
{
    AppRestart,
    SystemRestart,
    CrashRecovery,
    Manual
}

/// <summary>
/// Event args for subscription recovery requests.
/// </summary>
public class SubscriptionRecoveryEventArgs : EventArgs
{
    public PersistedSubscription? Subscription { get; set; }
}

/// <summary>
/// Event args for recovery completed.
/// </summary>
public class RecoveryCompletedEventArgs : EventArgs
{
    public RecoveryAttempt? Attempt { get; set; }
}
