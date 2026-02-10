using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Ui.Services;

namespace MarketDataCollector.Uwp.Contracts;

/// <summary>
/// Interface for retention policy assurance with guardrails, legal holds, and verification.
/// </summary>
public interface IRetentionAssuranceService
{
    RetentionConfiguration Configuration { get; }
    IReadOnlyList<LegalHold> LegalHolds { get; }
    IReadOnlyList<RetentionAuditReport> AuditReports { get; }

    Task LoadConfigurationAsync();
    Task SaveConfigurationAsync();
    RetentionValidationResult ValidateRetentionPolicy(RetentionPolicy policy);
    Task<RetentionDryRunResult> PerformDryRunAsync(RetentionPolicy policy, string dataRoot, CancellationToken ct = default);
    Task<StorageHealthCheckResult?> RunHealthCheckAsync(bool validateChecksums = true, CancellationToken ct = default);
    Task<OrphanFilesResult?> FindOrphanedFilesAsync(CancellationToken ct = default);
    Task<ChecksumVerificationResult> VerifyChecksumsAsync(IEnumerable<FileToDelete> files, string archiveManifestPath, CancellationToken ct = default);
    Task<RetentionAuditReport> ExecuteRetentionCleanupAsync(RetentionDryRunResult dryRun, bool verifyChecksums, CancellationToken ct = default);
    Task CreateLegalHoldAsync(string name, string reason, IEnumerable<string> symbols, DateTime? expiresAt = null);
    Task ReleaseLegalHoldAsync(string holdId);
    HashSet<string> GetSymbolsUnderLegalHold();
    Task<string> ExportAuditReportAsync(RetentionAuditReport report, string format = "json");

    event EventHandler<LegalHoldEventArgs>? LegalHoldCreated;
    event EventHandler<LegalHoldEventArgs>? LegalHoldReleased;
}
