using System;
using System.Collections.Generic;

namespace MarketDataCollector.Ui.Services;

// =====================================================================================
// Admin Maintenance DTOs — shared between WPF and UWP desktop applications.
// Extracted from duplicate definitions to provide a single source of truth.
// =====================================================================================

#region Schedule Models

/// <summary>
/// Result of getting the maintenance schedule.
/// </summary>
public class MaintenanceScheduleResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public MaintenanceScheduleResponse? Schedule { get; set; }
}

/// <summary>
/// Response payload describing the current maintenance schedule.
/// </summary>
public class MaintenanceScheduleResponse
{
    public bool Enabled { get; set; }
    public string? CronExpression { get; set; }
    public string? HumanReadable { get; set; }
    public DateTime? NextRunTime { get; set; }
    public DateTime? LastRunTime { get; set; }
    public List<string> EnabledOperations { get; set; } = new();
}

/// <summary>
/// Configuration for updating the maintenance schedule.
/// </summary>
public class MaintenanceScheduleConfig
{
    public bool Enabled { get; set; }
    public string? CronExpression { get; set; }
    public bool RunCompression { get; set; }
    public bool RunCleanup { get; set; }
    public bool RunIntegrityCheck { get; set; }
    public bool RunTierMigration { get; set; }
}

#endregion

#region Maintenance Run Models

/// <summary>
/// Options for triggering a maintenance run.
/// </summary>
public class MaintenanceRunOptions
{
    public bool RunCompression { get; set; } = true;
    public bool RunCleanup { get; set; } = true;
    public bool RunIntegrityCheck { get; set; } = true;
    public bool RunTierMigration { get; set; }
    public bool DryRun { get; set; }
}

/// <summary>
/// Result of a maintenance run operation.
/// </summary>
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

/// <summary>
/// HTTP response payload for a maintenance run.
/// </summary>
public class MaintenanceRunResponse
{
    public string? RunId { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? Status { get; set; }
    public List<MaintenanceOperation>? Operations { get; set; }
}

/// <summary>
/// Individual maintenance operation within a run.
/// </summary>
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

#endregion

#region Maintenance History Models

/// <summary>
/// Result of getting maintenance history.
/// </summary>
public class MaintenanceHistoryResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<MaintenanceRunSummary> Runs { get; set; } = new();
}

/// <summary>
/// HTTP response payload for maintenance history.
/// </summary>
public class MaintenanceHistoryResponse
{
    public List<MaintenanceRunSummary>? Runs { get; set; }
}

/// <summary>
/// Summary of a completed maintenance run.
/// </summary>
public class MaintenanceRunSummary
{
    public string RunId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public int OperationsCompleted { get; set; }
    public int OperationsFailed { get; set; }
}

#endregion

#region Tier Models

/// <summary>
/// Result of getting tier configuration.
/// </summary>
public class TierConfigResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<StorageTierConfig> Tiers { get; set; } = new();
    public bool AutoMigrationEnabled { get; set; }
    public string? MigrationSchedule { get; set; }
}

/// <summary>
/// HTTP response payload for tier configuration.
/// </summary>
public class TierConfigResponse
{
    public List<StorageTierConfig>? Tiers { get; set; }
    public bool AutoMigrationEnabled { get; set; }
    public string? MigrationSchedule { get; set; }
}

/// <summary>
/// Configuration for a single storage tier.
/// </summary>
public class StorageTierConfig
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int Priority { get; set; }
    public int RetentionDays { get; set; }
    public string CompressionLevel { get; set; } = "Standard";
    public bool Enabled { get; set; }
}

/// <summary>
/// Options for tier migration.
/// </summary>
public class TierMigrationOptions
{
    public DateOnly? OlderThan { get; set; }
    public List<string>? Symbols { get; set; }
    public List<string>? EventTypes { get; set; }
    public bool DryRun { get; set; }
}

/// <summary>
/// Result of a tier migration operation.
/// </summary>
public class TierMigrationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int FilesProcessed { get; set; }
    public long BytesMigrated { get; set; }
    public long SpaceSavedBytes { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// HTTP response payload for tier migration.
/// </summary>
public class TierMigrationResponse
{
    public int FilesProcessed { get; set; }
    public long BytesMigrated { get; set; }
    public long SpaceSavedBytes { get; set; }
    public string[]? Errors { get; set; }
}

/// <summary>
/// Result of getting tier usage statistics.
/// </summary>
public class TierUsageResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<TierUsage> TierUsage { get; set; } = new();
    public long TotalSizeBytes { get; set; }
    public long TotalFiles { get; set; }
}

/// <summary>
/// HTTP response payload for tier usage.
/// </summary>
public class TierUsageResponse
{
    public List<TierUsage>? TierUsage { get; set; }
    public long TotalSizeBytes { get; set; }
    public long TotalFiles { get; set; }
}

/// <summary>
/// Usage statistics for a single storage tier.
/// </summary>
public class TierUsage
{
    public string TierName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public long FileCount { get; set; }
    public double PercentOfTotal { get; set; }
    public DateOnly? OldestData { get; set; }
    public DateOnly? NewestData { get; set; }
}

#endregion

#region Retention Policy Models

/// <summary>
/// Result of getting retention policies.
/// </summary>
public class RetentionPoliciesResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<RetentionPolicy> Policies { get; set; } = new();
    public RetentionPolicy? DefaultPolicy { get; set; }
}

/// <summary>
/// HTTP response payload for retention policies.
/// </summary>
public class RetentionPoliciesResponse
{
    public List<RetentionPolicy>? Policies { get; set; }
    public RetentionPolicy? DefaultPolicy { get; set; }
}

/// <summary>
/// A retention policy definition.
/// </summary>
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

/// <summary>
/// Result of applying retention policies.
/// </summary>
public class RetentionApplyResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int FilesDeleted { get; set; }
    public long BytesFreed { get; set; }
    public List<string> AffectedSymbols { get; set; } = new();
    public bool WasDryRun { get; set; }
}

/// <summary>
/// HTTP response payload for retention apply.
/// </summary>
public class RetentionApplyResponse
{
    public int FilesDeleted { get; set; }
    public long BytesFreed { get; set; }
    public string[]? AffectedSymbols { get; set; }
}

#endregion

#region Cleanup Models

/// <summary>
/// Options for file cleanup operations.
/// </summary>
public class CleanupOptions
{
    public bool DeleteEmptyDirectories { get; set; } = true;
    public bool DeleteTempFiles { get; set; } = true;
    public bool DeleteOrphanedFiles { get; set; }
    public bool DeleteCorruptFiles { get; set; }
    public int OlderThanDays { get; set; }
}

/// <summary>
/// Result of previewing cleanup candidates.
/// </summary>
public class CleanupPreviewResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<CleanupFileInfo> FilesToDelete { get; set; } = new();
    public long TotalBytes { get; set; }
    public int TotalFiles { get; set; }
}

/// <summary>
/// HTTP response payload for cleanup preview.
/// </summary>
public class CleanupPreviewResponse
{
    public List<CleanupFileInfo>? FilesToDelete { get; set; }
    public long TotalBytes { get; set; }
    public int TotalFiles { get; set; }
}

/// <summary>
/// Information about a file eligible for cleanup.
/// </summary>
public class CleanupFileInfo
{
    public string Path { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime LastModified { get; set; }
}

/// <summary>
/// Result of executing cleanup.
/// </summary>
public class MaintenanceCleanupResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int FilesDeleted { get; set; }
    public long BytesFreed { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// HTTP response payload for cleanup execution.
/// </summary>
public class CleanupResultResponse
{
    public int FilesDeleted { get; set; }
    public long BytesFreed { get; set; }
    public string[]? Errors { get; set; }
}

#endregion

#region Permission & Self-Test Models

/// <summary>
/// Result of permission validation.
/// </summary>
public class PermissionValidationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public bool CanDelete { get; set; }
    public List<string> Issues { get; set; } = new();
}

/// <summary>
/// HTTP response payload for permission validation.
/// </summary>
public class PermissionValidationResponse
{
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public bool CanDelete { get; set; }
    public string[]? Issues { get; set; }
}

/// <summary>
/// Options for self-test operations.
/// </summary>
public class SelfTestOptions
{
    public bool TestStorage { get; set; } = true;
    public bool TestProviders { get; set; } = true;
    public bool TestConfiguration { get; set; } = true;
    public bool TestNetwork { get; set; } = true;
}

/// <summary>
/// Result of self-test operations.
/// </summary>
public class SelfTestResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<SelfTestItem> Tests { get; set; } = new();
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
}

/// <summary>
/// HTTP response payload for self-test.
/// </summary>
public class SelfTestResponse
{
    public bool AllPassed { get; set; }
    public List<SelfTestItem>? Tests { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
}

/// <summary>
/// Individual self-test item.
/// </summary>
public class SelfTestItem
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public double DurationMs { get; set; }
}

#endregion

#region Error Codes & Config Models

/// <summary>
/// Result of getting error codes.
/// </summary>
public class ErrorCodesResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<ErrorCodeInfo> ErrorCodes { get; set; } = new();
}

/// <summary>
/// HTTP response payload for error codes.
/// </summary>
public class ErrorCodesResponse
{
    public List<ErrorCodeInfo>? ErrorCodes { get; set; }
}

/// <summary>
/// Error code with category, description, and resolution.
/// </summary>
public class ErrorCodeInfo
{
    public string Code { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Resolution { get; set; }
    public string Severity { get; set; } = string.Empty;
}

/// <summary>
/// Result of showing configuration.
/// </summary>
public class ShowConfigResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<ConfigSection> Sections { get; set; } = new();
}

/// <summary>
/// HTTP response payload for show config.
/// </summary>
public class ShowConfigResponse
{
    public List<ConfigSection>? Sections { get; set; }
}

/// <summary>
/// A named configuration section.
/// </summary>
public class ConfigSection
{
    public string Name { get; set; } = string.Empty;
    public List<ConfigItem> Items { get; set; } = new();
}

/// <summary>
/// Individual configuration key-value entry.
/// </summary>
public class ConfigItem
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? Source { get; set; }
    public bool IsSensitive { get; set; }
}

#endregion

#region Quick Check Models

/// <summary>
/// Result of a quick health check.
/// </summary>
public class QuickCheckResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string Overall { get; set; } = string.Empty;
    public List<QuickCheckItem> Checks { get; set; } = new();
}

/// <summary>
/// HTTP response payload for quick check.
/// </summary>
public class QuickCheckResponse
{
    public string Overall { get; set; } = string.Empty;
    public List<QuickCheckItem>? Checks { get; set; }
}

/// <summary>
/// Individual quick check item.
/// </summary>
public class QuickCheckItem
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Details { get; set; }
}

/// <summary>
/// HTTP operation response used for POST mutations.
/// </summary>
public class OperationResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}

#endregion
