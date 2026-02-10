using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for administrative and maintenance operations including
/// archive scheduling, tier migration, retention policies, and file cleanup.
/// </summary>
public sealed class AdminMaintenanceService : IAdminMaintenanceService
{
    private static AdminMaintenanceService? _instance;
    private static readonly object _lock = new();
    private readonly ApiClientService _apiClient;

    public static AdminMaintenanceService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new AdminMaintenanceService();
                }
            }
            return _instance;
        }
    }

    private AdminMaintenanceService()
    {
        _apiClient = ApiClientService.Instance;
    }

    #region Archive Maintenance

    /// <summary>
    /// Gets the current archive maintenance schedule.
    /// </summary>
    public async Task<MaintenanceScheduleResult> GetMaintenanceScheduleAsync(CancellationToken ct = default)
    {
        var response = await _apiClient.GetWithResponseAsync<MaintenanceScheduleResponse>(
            "/api/admin/maintenance/schedule",
            ct);

        if (response.Success && response.Data != null)
        {
            return new MaintenanceScheduleResult
            {
                Success = true,
                Schedule = response.Data
            };
        }

        return new MaintenanceScheduleResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Failed to get maintenance schedule"
        };
    }

    /// <summary>
    /// Updates the archive maintenance schedule.
    /// </summary>
    public async Task<OperationResult> UpdateMaintenanceScheduleAsync(
        MaintenanceScheduleConfig schedule,
        CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<OperationResponse>(
            "/api/admin/maintenance/schedule",
            schedule,
            ct);

        return new OperationResult
        {
            Success = response.Success,
            Message = response.Data?.Message,
            Error = response.ErrorMessage
        };
    }

    /// <summary>
    /// Triggers an immediate maintenance run.
    /// </summary>
    public async Task<MaintenanceRunResult> RunMaintenanceNowAsync(
        MaintenanceRunOptions? options = null,
        CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<MaintenanceRunResponse>(
            "/api/admin/maintenance/run",
            options ?? new MaintenanceRunOptions(),
            ct);

        if (response.Success && response.Data != null)
        {
            return new MaintenanceRunResult
            {
                Success = true,
                RunId = response.Data.RunId,
                StartTime = response.Data.StartTime,
                Status = response.Data.Status,
                Operations = response.Data.Operations?.ToList() ?? new List<MaintenanceOperation>()
            };
        }

        return new MaintenanceRunResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Failed to run maintenance"
        };
    }

    /// <summary>
    /// Gets the status of a maintenance run.
    /// </summary>
    public async Task<MaintenanceRunResult> GetMaintenanceRunStatusAsync(
        string runId,
        CancellationToken ct = default)
    {
        var response = await _apiClient.GetWithResponseAsync<MaintenanceRunResponse>(
            $"/api/admin/maintenance/run/{runId}",
            ct);

        if (response.Success && response.Data != null)
        {
            return new MaintenanceRunResult
            {
                Success = true,
                RunId = response.Data.RunId,
                StartTime = response.Data.StartTime,
                EndTime = response.Data.EndTime,
                Status = response.Data.Status,
                Operations = response.Data.Operations?.ToList() ?? new List<MaintenanceOperation>()
            };
        }

        return new MaintenanceRunResult
        {
            Success = false,
            Error = response.ErrorMessage
        };
    }

    /// <summary>
    /// Gets maintenance history.
    /// </summary>
    public async Task<MaintenanceHistoryResult> GetMaintenanceHistoryAsync(
        int limit = 20,
        CancellationToken ct = default)
    {
        var response = await _apiClient.GetWithResponseAsync<MaintenanceHistoryResponse>(
            $"/api/admin/maintenance/history?limit={limit}",
            ct);

        if (response.Success && response.Data != null)
        {
            return new MaintenanceHistoryResult
            {
                Success = true,
                Runs = response.Data.Runs?.ToList() ?? new List<MaintenanceRunSummary>()
            };
        }

        return new MaintenanceHistoryResult
        {
            Success = false,
            Error = response.ErrorMessage
        };
    }

    #endregion

    #region Tier Migration

    /// <summary>
    /// Gets the current tier configuration.
    /// </summary>
    public async Task<TierConfigResult> GetTierConfigurationAsync(CancellationToken ct = default)
    {
        var response = await _apiClient.GetWithResponseAsync<TierConfigResponse>(
            "/api/admin/storage/tiers",
            ct);

        if (response.Success && response.Data != null)
        {
            return new TierConfigResult
            {
                Success = true,
                Tiers = response.Data.Tiers?.ToList() ?? new List<StorageTierConfig>(),
                AutoMigrationEnabled = response.Data.AutoMigrationEnabled,
                MigrationSchedule = response.Data.MigrationSchedule
            };
        }

        return new TierConfigResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Failed to get tier configuration"
        };
    }

    /// <summary>
    /// Updates tier configuration.
    /// </summary>
    public async Task<OperationResult> UpdateTierConfigurationAsync(
        List<StorageTierConfig> tiers,
        bool autoMigrationEnabled,
        string? migrationSchedule = null,
        CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<OperationResponse>(
            "/api/admin/storage/tiers",
            new
            {
                tiers,
                autoMigrationEnabled,
                migrationSchedule
            },
            ct);

        return new OperationResult
        {
            Success = response.Success,
            Message = response.Data?.Message,
            Error = response.ErrorMessage
        };
    }

    /// <summary>
    /// Triggers migration of data from hot to cold tier.
    /// </summary>
    public async Task<TierMigrationResult> MigrateToTierAsync(
        string targetTier,
        TierMigrationOptions? options = null,
        CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<TierMigrationResponse>(
            $"/api/admin/storage/migrate/{targetTier}",
            options ?? new TierMigrationOptions(),
            ct);

        if (response.Success && response.Data != null)
        {
            return new TierMigrationResult
            {
                Success = true,
                FilesProcessed = response.Data.FilesProcessed,
                BytesMigrated = response.Data.BytesMigrated,
                SpaceSavedBytes = response.Data.SpaceSavedBytes,
                Errors = response.Data.Errors?.ToList() ?? new List<string>()
            };
        }

        return new TierMigrationResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Migration failed"
        };
    }

    /// <summary>
    /// Gets current storage tier usage statistics.
    /// </summary>
    public async Task<TierUsageResult> GetTierUsageAsync(CancellationToken ct = default)
    {
        var response = await _apiClient.GetWithResponseAsync<TierUsageResponse>(
            "/api/admin/storage/usage",
            ct);

        if (response.Success && response.Data != null)
        {
            return new TierUsageResult
            {
                Success = true,
                TierUsage = response.Data.TierUsage?.ToList() ?? new List<TierUsage>(),
                TotalSizeBytes = response.Data.TotalSizeBytes,
                TotalFiles = response.Data.TotalFiles
            };
        }

        return new TierUsageResult
        {
            Success = false,
            Error = response.ErrorMessage
        };
    }

    #endregion

    #region Retention Policies

    /// <summary>
    /// Gets all retention policies.
    /// </summary>
    public async Task<RetentionPoliciesResult> GetRetentionPoliciesAsync(CancellationToken ct = default)
    {
        var response = await _apiClient.GetWithResponseAsync<RetentionPoliciesResponse>(
            "/api/admin/retention",
            ct);

        if (response.Success && response.Data != null)
        {
            return new RetentionPoliciesResult
            {
                Success = true,
                Policies = response.Data.Policies?.ToList() ?? new List<RetentionPolicy>(),
                DefaultPolicy = response.Data.DefaultPolicy
            };
        }

        return new RetentionPoliciesResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Failed to get retention policies"
        };
    }

    /// <summary>
    /// Creates or updates a retention policy.
    /// </summary>
    public async Task<OperationResult> SaveRetentionPolicyAsync(
        RetentionPolicy policy,
        CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<OperationResponse>(
            "/api/admin/retention",
            policy,
            ct);

        return new OperationResult
        {
            Success = response.Success,
            Message = response.Data?.Message,
            Error = response.ErrorMessage
        };
    }

    /// <summary>
    /// Deletes a retention policy.
    /// </summary>
    public async Task<OperationResult> DeleteRetentionPolicyAsync(
        string policyId,
        CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<OperationResponse>(
            $"/api/admin/retention/{policyId}/delete",
            null,
            ct);

        return new OperationResult
        {
            Success = response.Success,
            Message = response.Data?.Message,
            Error = response.ErrorMessage
        };
    }

    /// <summary>
    /// Applies retention policies and deletes expired data.
    /// </summary>
    public async Task<RetentionApplyResult> ApplyRetentionPoliciesAsync(
        bool dryRun = false,
        CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<RetentionApplyResponse>(
            "/api/admin/retention/apply",
            new { dryRun },
            ct);

        if (response.Success && response.Data != null)
        {
            return new RetentionApplyResult
            {
                Success = true,
                FilesDeleted = response.Data.FilesDeleted,
                BytesFreed = response.Data.BytesFreed,
                AffectedSymbols = response.Data.AffectedSymbols?.ToList() ?? new List<string>(),
                WasDryRun = dryRun
            };
        }

        return new RetentionApplyResult
        {
            Success = false,
            Error = response.ErrorMessage
        };
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
        var response = await _apiClient.PostWithResponseAsync<CleanupPreviewResponse>(
            "/api/admin/cleanup/preview",
            options,
            ct);

        if (response.Success && response.Data != null)
        {
            return new CleanupPreviewResult
            {
                Success = true,
                FilesToDelete = response.Data.FilesToDelete?.ToList() ?? new List<CleanupFileInfo>(),
                TotalBytes = response.Data.TotalBytes,
                TotalFiles = response.Data.TotalFiles
            };
        }

        return new CleanupPreviewResult
        {
            Success = false,
            Error = response.ErrorMessage
        };
    }

    /// <summary>
    /// Executes file cleanup.
    /// </summary>
    public async Task<CleanupResult> ExecuteCleanupAsync(
        CleanupOptions options,
        CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<CleanupResultResponse>(
            "/api/admin/cleanup/execute",
            options,
            ct);

        if (response.Success && response.Data != null)
        {
            return new CleanupResult
            {
                Success = true,
                FilesDeleted = response.Data.FilesDeleted,
                BytesFreed = response.Data.BytesFreed,
                Errors = response.Data.Errors?.ToList() ?? new List<string>()
            };
        }

        return new CleanupResult
        {
            Success = false,
            Error = response.ErrorMessage
        };
    }

    /// <summary>
    /// Validates storage permissions.
    /// </summary>
    public async Task<PermissionValidationResult> ValidatePermissionsAsync(CancellationToken ct = default)
    {
        var response = await _apiClient.GetWithResponseAsync<PermissionValidationResponse>(
            "/api/admin/storage/permissions",
            ct);

        if (response.Success && response.Data != null)
        {
            return new PermissionValidationResult
            {
                Success = true,
                CanRead = response.Data.CanRead,
                CanWrite = response.Data.CanWrite,
                CanDelete = response.Data.CanDelete,
                Issues = response.Data.Issues?.ToList() ?? new List<string>()
            };
        }

        return new PermissionValidationResult
        {
            Success = false,
            Error = response.ErrorMessage
        };
    }

    #endregion

    #region Self-Test

    /// <summary>
    /// Runs system self-tests.
    /// </summary>
    public async Task<SelfTestResult> RunSelfTestAsync(
        SelfTestOptions? options = null,
        CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<SelfTestResponse>(
            "/api/admin/selftest",
            options ?? new SelfTestOptions(),
            ct);

        if (response.Success && response.Data != null)
        {
            return new SelfTestResult
            {
                Success = response.Data.AllPassed,
                Tests = response.Data.Tests?.ToList() ?? new List<SelfTestItem>(),
                PassedCount = response.Data.PassedCount,
                FailedCount = response.Data.FailedCount,
                SkippedCount = response.Data.SkippedCount
            };
        }

        return new SelfTestResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Self-test failed"
        };
    }

    /// <summary>
    /// Gets list of available error codes and their descriptions.
    /// </summary>
    public async Task<ErrorCodesResult> GetErrorCodesAsync(CancellationToken ct = default)
    {
        var response = await _apiClient.GetWithResponseAsync<ErrorCodesResponse>(
            "/api/admin/error-codes",
            ct);

        if (response.Success && response.Data != null)
        {
            return new ErrorCodesResult
            {
                Success = true,
                ErrorCodes = response.Data.ErrorCodes?.ToList() ?? new List<ErrorCodeInfo>()
            };
        }

        return new ErrorCodesResult
        {
            Success = false,
            Error = response.ErrorMessage
        };
    }

    /// <summary>
    /// Gets current configuration in human-readable format.
    /// </summary>
    public async Task<ShowConfigResult> ShowConfigAsync(CancellationToken ct = default)
    {
        var response = await _apiClient.GetWithResponseAsync<ShowConfigResponse>(
            "/api/admin/show-config",
            ct);

        if (response.Success && response.Data != null)
        {
            return new ShowConfigResult
            {
                Success = true,
                Sections = response.Data.Sections?.ToList() ?? new List<ConfigSection>()
            };
        }

        return new ShowConfigResult
        {
            Success = false,
            Error = response.ErrorMessage
        };
    }

    /// <summary>
    /// Runs quick health check.
    /// </summary>
    public async Task<QuickCheckResult> RunQuickCheckAsync(CancellationToken ct = default)
    {
        var response = await _apiClient.GetWithResponseAsync<QuickCheckResponse>(
            "/api/admin/quick-check",
            ct);

        if (response.Success && response.Data != null)
        {
            return new QuickCheckResult
            {
                Success = true,
                Overall = response.Data.Overall,
                Checks = response.Data.Checks?.ToList() ?? new List<QuickCheckItem>()
            };
        }

        return new QuickCheckResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Quick check failed"
        };
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

public class OperationResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
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

public class PermissionValidationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public bool CanDelete { get; set; }
    public List<string> Issues { get; set; } = new();
}

public class PermissionValidationResponse
{
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public bool CanDelete { get; set; }
    public string[]? Issues { get; set; }
}

public class SelfTestOptions
{
    public bool TestStorage { get; set; } = true;
    public bool TestProviders { get; set; } = true;
    public bool TestConfiguration { get; set; } = true;
    public bool TestNetwork { get; set; } = true;
}

public class SelfTestResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<SelfTestItem> Tests { get; set; } = new();
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
}

public class SelfTestResponse
{
    public bool AllPassed { get; set; }
    public List<SelfTestItem>? Tests { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
}

public class SelfTestItem
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public double DurationMs { get; set; }
}

public class ErrorCodesResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<ErrorCodeInfo> ErrorCodes { get; set; } = new();
}

public class ErrorCodesResponse
{
    public List<ErrorCodeInfo>? ErrorCodes { get; set; }
}

public class ErrorCodeInfo
{
    public string Code { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Resolution { get; set; }
    public string Severity { get; set; } = string.Empty;
}

public class ShowConfigResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<ConfigSection> Sections { get; set; } = new();
}

public class ShowConfigResponse
{
    public List<ConfigSection>? Sections { get; set; }
}

public class ConfigSection
{
    public string Name { get; set; } = string.Empty;
    public List<ConfigItem> Items { get; set; } = new();
}

public class ConfigItem
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? Source { get; set; }
    public bool IsSensitive { get; set; }
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
