using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Contracts;

/// <summary>
/// Interface for administrative and maintenance operations including
/// archive scheduling, tier migration, retention policies, and file cleanup.
/// </summary>
public interface IAdminMaintenanceService
{
    Task<MaintenanceScheduleResult> GetMaintenanceScheduleAsync(CancellationToken ct = default);
    Task<OperationResult> UpdateMaintenanceScheduleAsync(MaintenanceScheduleConfig schedule, CancellationToken ct = default);
    Task<MaintenanceRunResult> RunMaintenanceNowAsync(MaintenanceRunOptions? options = null, CancellationToken ct = default);
    Task<MaintenanceRunResult> GetMaintenanceRunStatusAsync(string runId, CancellationToken ct = default);
    Task<MaintenanceHistoryResult> GetMaintenanceHistoryAsync(int limit = 20, CancellationToken ct = default);
    Task<TierConfigResult> GetTierConfigurationAsync(CancellationToken ct = default);
    Task<OperationResult> UpdateTierConfigurationAsync(List<StorageTierConfig> tiers, bool autoMigrationEnabled, string? migrationSchedule = null, CancellationToken ct = default);
    Task<TierMigrationResult> MigrateToTierAsync(string targetTier, TierMigrationOptions? options = null, CancellationToken ct = default);
    Task<TierUsageResult> GetTierUsageAsync(CancellationToken ct = default);
    Task<RetentionPoliciesResult> GetRetentionPoliciesAsync(CancellationToken ct = default);
    Task<OperationResult> SaveRetentionPolicyAsync(RetentionPolicy policy, CancellationToken ct = default);
    Task<OperationResult> DeleteRetentionPolicyAsync(string policyId, CancellationToken ct = default);
    Task<RetentionApplyResult> ApplyRetentionPoliciesAsync(bool dryRun = false, CancellationToken ct = default);
    Task<CleanupPreviewResult> PreviewCleanupAsync(CleanupOptions options, CancellationToken ct = default);
    Task<CleanupResult> ExecuteCleanupAsync(CleanupOptions options, CancellationToken ct = default);
    Task<PermissionValidationResult> ValidatePermissionsAsync(CancellationToken ct = default);
    Task<SelfTestResult> RunSelfTestAsync(SelfTestOptions? options = null, CancellationToken ct = default);
    Task<ErrorCodesResult> GetErrorCodesAsync(CancellationToken ct = default);
    Task<ShowConfigResult> ShowConfigAsync(CancellationToken ct = default);
    Task<QuickCheckResult> RunQuickCheckAsync(CancellationToken ct = default);
}
