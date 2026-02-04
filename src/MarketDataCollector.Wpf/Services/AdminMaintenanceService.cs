using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Service for administrative and maintenance operations including
/// archive scheduling, tier migration, retention policies, and file cleanup.
/// </summary>
public sealed class AdminMaintenanceService
{
    private static readonly Lazy<AdminMaintenanceService> _instance = new(() => new AdminMaintenanceService());
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private string _baseUrl = "http://localhost:8080";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static AdminMaintenanceService Instance => _instance.Value;

    public string BaseUrl
    {
        get => _baseUrl;
        set => _baseUrl = value;
    }

    private AdminMaintenanceService() { }

    #region Archive Maintenance

    /// <summary>
    /// Gets the current archive maintenance schedule.
    /// </summary>
    public async Task<MaintenanceScheduleResult> GetMaintenanceScheduleAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/admin/maintenance/schedule", ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<MaintenanceScheduleResponse>(_jsonOptions, ct);
                return new MaintenanceScheduleResult
                {
                    Success = true,
                    Schedule = data
                };
            }
            return new MaintenanceScheduleResult
            {
                Success = false,
                Error = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
            };
        }
        catch (Exception ex)
        {
            return new MaintenanceScheduleResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Updates the archive maintenance schedule.
    /// </summary>
    public async Task<OperationResult> UpdateMaintenanceScheduleAsync(
        MaintenanceScheduleConfig schedule,
        CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_baseUrl}/api/admin/maintenance/schedule",
                schedule,
                _jsonOptions,
                ct);

            if (response.IsSuccessStatusCode)
            {
                return new OperationResult { Success = true, Message = "Schedule updated" };
            }
            return new OperationResult
            {
                Success = false,
                Error = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
            };
        }
        catch (Exception ex)
        {
            return new OperationResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Triggers an immediate maintenance run.
    /// </summary>
    public async Task<MaintenanceRunResult> RunMaintenanceNowAsync(
        MaintenanceRunOptions? options = null,
        CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_baseUrl}/api/admin/maintenance/run",
                options ?? new MaintenanceRunOptions(),
                _jsonOptions,
                ct);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<MaintenanceRunResponse>(_jsonOptions, ct);
                return new MaintenanceRunResult
                {
                    Success = true,
                    RunId = data?.RunId,
                    StartTime = data?.StartTime,
                    Status = data?.Status,
                    Operations = data?.Operations?.ToList() ?? new List<MaintenanceOperation>()
                };
            }
            return new MaintenanceRunResult
            {
                Success = false,
                Error = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
            };
        }
        catch (Exception ex)
        {
            return new MaintenanceRunResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Gets the status of a maintenance run.
    /// </summary>
    public async Task<MaintenanceRunResult> GetMaintenanceRunStatusAsync(
        string runId,
        CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/admin/maintenance/run/{runId}", ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<MaintenanceRunResponse>(_jsonOptions, ct);
                return new MaintenanceRunResult
                {
                    Success = true,
                    RunId = data?.RunId,
                    StartTime = data?.StartTime,
                    EndTime = data?.EndTime,
                    Status = data?.Status,
                    Operations = data?.Operations?.ToList() ?? new List<MaintenanceOperation>()
                };
            }
            return new MaintenanceRunResult
            {
                Success = false,
                Error = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
            };
        }
        catch (Exception ex)
        {
            return new MaintenanceRunResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Gets maintenance history.
    /// </summary>
    public async Task<MaintenanceHistoryResult> GetMaintenanceHistoryAsync(
        int limit = 20,
        CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/admin/maintenance/history?limit={limit}", ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<MaintenanceHistoryResponse>(_jsonOptions, ct);
                return new MaintenanceHistoryResult
                {
                    Success = true,
                    Runs = data?.Runs?.ToList() ?? new List<MaintenanceRunSummary>()
                };
            }
            return new MaintenanceHistoryResult
            {
                Success = false,
                Error = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
            };
        }
        catch (Exception ex)
        {
            return new MaintenanceHistoryResult { Success = false, Error = ex.Message };
        }
    }

    #endregion

    #region Tier Migration

    /// <summary>
    /// Gets the current tier configuration.
    /// </summary>
    public async Task<TierConfigResult> GetTierConfigurationAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/admin/storage/tiers", ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<TierConfigResponse>(_jsonOptions, ct);
                return new TierConfigResult
                {
                    Success = true,
                    Tiers = data?.Tiers?.ToList() ?? new List<StorageTierConfig>(),
                    AutoMigrationEnabled = data?.AutoMigrationEnabled ?? false,
                    MigrationSchedule = data?.MigrationSchedule
                };
            }
            return new TierConfigResult
            {
                Success = false,
                Error = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
            };
        }
        catch (Exception ex)
        {
            return new TierConfigResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Triggers migration of data from hot to cold tier.
    /// </summary>
    public async Task<TierMigrationResult> MigrateToTierAsync(
        string targetTier,
        TierMigrationOptions? options = null,
        CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_baseUrl}/api/admin/storage/migrate/{targetTier}",
                options ?? new TierMigrationOptions(),
                _jsonOptions,
                ct);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<TierMigrationResponse>(_jsonOptions, ct);
                return new TierMigrationResult
                {
                    Success = true,
                    FilesProcessed = data?.FilesProcessed ?? 0,
                    BytesMigrated = data?.BytesMigrated ?? 0,
                    SpaceSavedBytes = data?.SpaceSavedBytes ?? 0,
                    Errors = data?.Errors?.ToList() ?? new List<string>()
                };
            }
            return new TierMigrationResult
            {
                Success = false,
                Error = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
            };
        }
        catch (Exception ex)
        {
            return new TierMigrationResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Gets current storage tier usage statistics.
    /// </summary>
    public async Task<TierUsageResult> GetTierUsageAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/admin/storage/usage", ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<TierUsageResponse>(_jsonOptions, ct);
                return new TierUsageResult
                {
                    Success = true,
                    TierUsage = data?.TierUsage?.ToList() ?? new List<TierUsage>(),
                    TotalSizeBytes = data?.TotalSizeBytes ?? 0,
                    TotalFiles = data?.TotalFiles ?? 0
                };
            }
            return new TierUsageResult
            {
                Success = false,
                Error = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
            };
        }
        catch (Exception ex)
        {
            return new TierUsageResult { Success = false, Error = ex.Message };
        }
    }

    #endregion

    #region Retention Policies

    /// <summary>
    /// Gets all retention policies.
    /// </summary>
    public async Task<RetentionPoliciesResult> GetRetentionPoliciesAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/admin/retention", ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<RetentionPoliciesResponse>(_jsonOptions, ct);
                return new RetentionPoliciesResult
                {
                    Success = true,
                    Policies = data?.Policies?.ToList() ?? new List<RetentionPolicy>(),
                    DefaultPolicy = data?.DefaultPolicy
                };
            }
            return new RetentionPoliciesResult
            {
                Success = false,
                Error = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
            };
        }
        catch (Exception ex)
        {
            return new RetentionPoliciesResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Creates or updates a retention policy.
    /// </summary>
    public async Task<OperationResult> SaveRetentionPolicyAsync(
        RetentionPolicy policy,
        CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_baseUrl}/api/admin/retention",
                policy,
                _jsonOptions,
                ct);

            if (response.IsSuccessStatusCode)
            {
                return new OperationResult { Success = true, Message = "Policy saved" };
            }
            return new OperationResult
            {
                Success = false,
                Error = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
            };
        }
        catch (Exception ex)
        {
            return new OperationResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Deletes a retention policy.
    /// </summary>
    public async Task<OperationResult> DeleteRetentionPolicyAsync(
        string policyId,
        CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"{_baseUrl}/api/admin/retention/{policyId}", ct);
            if (response.IsSuccessStatusCode)
            {
                return new OperationResult { Success = true, Message = "Policy deleted" };
            }
            return new OperationResult
            {
                Success = false,
                Error = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
            };
        }
        catch (Exception ex)
        {
            return new OperationResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Applies retention policies and deletes expired data.
    /// </summary>
    public async Task<RetentionApplyResult> ApplyRetentionPoliciesAsync(
        bool dryRun = false,
        CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_baseUrl}/api/admin/retention/apply",
                new { dryRun },
                _jsonOptions,
                ct);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<RetentionApplyResponse>(_jsonOptions, ct);
                return new RetentionApplyResult
                {
                    Success = true,
                    FilesDeleted = data?.FilesDeleted ?? 0,
                    BytesFreed = data?.BytesFreed ?? 0,
                    AffectedSymbols = data?.AffectedSymbols?.ToList() ?? new List<string>(),
                    WasDryRun = dryRun
                };
            }
            return new RetentionApplyResult
            {
                Success = false,
                Error = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
            };
        }
        catch (Exception ex)
        {
            return new RetentionApplyResult { Success = false, Error = ex.Message };
        }
    }

    #endregion

    #region File Cleanup

    /// <summary>
    /// Gets files eligible for cleanup.
    /// </summary>
    public async Task<CleanupPreviewResult> PreviewCleanupAsync(
        CleanupOptions options,
        CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_baseUrl}/api/admin/cleanup/preview",
                options,
                _jsonOptions,
                ct);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<CleanupPreviewResponse>(_jsonOptions, ct);
                return new CleanupPreviewResult
                {
                    Success = true,
                    FilesToDelete = data?.FilesToDelete?.ToList() ?? new List<CleanupFileInfo>(),
                    TotalBytes = data?.TotalBytes ?? 0,
                    TotalFiles = data?.TotalFiles ?? 0
                };
            }
            return new CleanupPreviewResult
            {
                Success = false,
                Error = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
            };
        }
        catch (Exception ex)
        {
            return new CleanupPreviewResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Executes file cleanup.
    /// </summary>
    public async Task<CleanupResult> ExecuteCleanupAsync(
        CleanupOptions options,
        CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_baseUrl}/api/admin/cleanup/execute",
                options,
                _jsonOptions,
                ct);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<CleanupResultResponse>(_jsonOptions, ct);
                return new CleanupResult
                {
                    Success = true,
                    FilesDeleted = data?.FilesDeleted ?? 0,
                    BytesFreed = data?.BytesFreed ?? 0,
                    Errors = data?.Errors?.ToList() ?? new List<string>()
                };
            }
            return new CleanupResult
            {
                Success = false,
                Error = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
            };
        }
        catch (Exception ex)
        {
            return new CleanupResult { Success = false, Error = ex.Message };
        }
    }

    #endregion

    #region Quick Check

    /// <summary>
    /// Runs quick health check.
    /// </summary>
    public async Task<QuickCheckResult> RunQuickCheckAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/admin/quick-check", ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<QuickCheckResponse>(_jsonOptions, ct);
                return new QuickCheckResult
                {
                    Success = true,
                    Overall = data?.Overall ?? "Unknown",
                    Checks = data?.Checks?.ToList() ?? new List<QuickCheckItem>()
                };
            }
            return new QuickCheckResult
            {
                Success = false,
                Error = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
            };
        }
        catch (Exception ex)
        {
            return new QuickCheckResult { Success = false, Error = ex.Message };
        }
    }

    #endregion
}

#region Result Classes

public class OperationResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}

public class MaintenanceScheduleResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public MaintenanceScheduleResponse? Schedule { get; set; }
}

public class MaintenanceScheduleResponse
{
    public bool Enabled { get; set; }
    public string? CronExpression { get; set; }
    public string? HumanReadable { get; set; }
    public DateTime? NextRunTime { get; set; }
    public DateTime? LastRunTime { get; set; }
    public List<string> EnabledOperations { get; set; } = new();
}

public class MaintenanceScheduleConfig
{
    public bool Enabled { get; set; }
    public string? CronExpression { get; set; }
    public bool RunCompression { get; set; }
    public bool RunCleanup { get; set; }
    public bool RunIntegrityCheck { get; set; }
    public bool RunTierMigration { get; set; }
}

public class MaintenanceRunOptions
{
    public bool RunCompression { get; set; } = true;
    public bool RunCleanup { get; set; } = true;
    public bool RunIntegrityCheck { get; set; } = true;
    public bool RunTierMigration { get; set; } = false;
    public bool DryRun { get; set; } = false;
}

public class MaintenanceRunResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? RunId { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? Status { get; set; }
    public List<MaintenanceOperation> Operations { get; set; } = new();
}

public class MaintenanceRunResponse
{
    public string? RunId { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? Status { get; set; }
    public List<MaintenanceOperation>? Operations { get; set; }
}

public class MaintenanceOperation
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int ItemsProcessed { get; set; }
    public long BytesProcessed { get; set; }
    public string? Error { get; set; }
}

public class MaintenanceHistoryResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<MaintenanceRunSummary> Runs { get; set; } = new();
}

public class MaintenanceHistoryResponse
{
    public List<MaintenanceRunSummary>? Runs { get; set; }
}

public class MaintenanceRunSummary
{
    public string RunId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public int OperationsCompleted { get; set; }
    public int OperationsFailed { get; set; }
}

public class TierConfigResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<StorageTierConfig> Tiers { get; set; } = new();
    public bool AutoMigrationEnabled { get; set; }
    public string? MigrationSchedule { get; set; }
}

public class TierConfigResponse
{
    public List<StorageTierConfig>? Tiers { get; set; }
    public bool AutoMigrationEnabled { get; set; }
    public string? MigrationSchedule { get; set; }
}

public class StorageTierConfig
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int Priority { get; set; }
    public int RetentionDays { get; set; }
    public string CompressionLevel { get; set; } = "Standard";
    public bool Enabled { get; set; }
}

public class TierMigrationOptions
{
    public DateOnly? OlderThan { get; set; }
    public List<string>? Symbols { get; set; }
    public List<string>? EventTypes { get; set; }
    public bool DryRun { get; set; }
}

public class TierMigrationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int FilesProcessed { get; set; }
    public long BytesMigrated { get; set; }
    public long SpaceSavedBytes { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class TierMigrationResponse
{
    public int FilesProcessed { get; set; }
    public long BytesMigrated { get; set; }
    public long SpaceSavedBytes { get; set; }
    public string[]? Errors { get; set; }
}

public class TierUsageResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<TierUsage> TierUsage { get; set; } = new();
    public long TotalSizeBytes { get; set; }
    public long TotalFiles { get; set; }
}

public class TierUsageResponse
{
    public List<TierUsage>? TierUsage { get; set; }
    public long TotalSizeBytes { get; set; }
    public long TotalFiles { get; set; }
}

public class TierUsage
{
    public string TierName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public long FileCount { get; set; }
    public double PercentOfTotal { get; set; }
    public DateOnly? OldestData { get; set; }
    public DateOnly? NewestData { get; set; }
}

public class RetentionPoliciesResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<RetentionPolicy> Policies { get; set; } = new();
    public RetentionPolicy? DefaultPolicy { get; set; }
}

public class RetentionPoliciesResponse
{
    public List<RetentionPolicy>? Policies { get; set; }
    public RetentionPolicy? DefaultPolicy { get; set; }
}

public class RetentionPolicy
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int RetentionDays { get; set; }
    public string? SymbolPattern { get; set; }
    public List<string>? EventTypes { get; set; }
    public bool Enabled { get; set; }
    public bool IsDefault { get; set; }
}

public class RetentionApplyResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int FilesDeleted { get; set; }
    public long BytesFreed { get; set; }
    public List<string> AffectedSymbols { get; set; } = new();
    public bool WasDryRun { get; set; }
}

public class RetentionApplyResponse
{
    public int FilesDeleted { get; set; }
    public long BytesFreed { get; set; }
    public string[]? AffectedSymbols { get; set; }
}

public class CleanupOptions
{
    public bool DeleteEmptyDirectories { get; set; } = true;
    public bool DeleteTempFiles { get; set; } = true;
    public bool DeleteOrphanedFiles { get; set; } = false;
    public bool DeleteCorruptFiles { get; set; } = false;
    public int OlderThanDays { get; set; } = 0;
}

public class CleanupPreviewResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<CleanupFileInfo> FilesToDelete { get; set; } = new();
    public long TotalBytes { get; set; }
    public int TotalFiles { get; set; }
}

public class CleanupPreviewResponse
{
    public List<CleanupFileInfo>? FilesToDelete { get; set; }
    public long TotalBytes { get; set; }
    public int TotalFiles { get; set; }
}

public class CleanupFileInfo
{
    public string Path { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime LastModified { get; set; }
}

public class CleanupResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int FilesDeleted { get; set; }
    public long BytesFreed { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class CleanupResultResponse
{
    public int FilesDeleted { get; set; }
    public long BytesFreed { get; set; }
    public string[]? Errors { get; set; }
}

public class QuickCheckResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string Overall { get; set; } = string.Empty;
    public List<QuickCheckItem> Checks { get; set; } = new();
}

public class QuickCheckResponse
{
    public string Overall { get; set; } = string.Empty;
    public List<QuickCheckItem>? Checks { get; set; }
}

public class QuickCheckItem
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Details { get; set; }
}

#endregion
