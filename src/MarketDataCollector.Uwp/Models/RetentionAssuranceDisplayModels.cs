using System;
using System.Collections.Generic;

namespace MarketDataCollector.Uwp.Models;

/// <summary>
/// Compliance attestation report model.
/// </summary>
public sealed class ComplianceAttestation
{
    public DateTime GeneratedAt { get; set; }
    public string GeneratedBy { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public RetentionConfigurationSummary RetentionConfiguration { get; set; } = new();
    public GuardrailsSummary Guardrails { get; set; } = new();
    public List<LegalHoldSummary> ActiveLegalHolds { get; set; } = new();
    public List<AuditSummary> RecentAudits { get; set; } = new();
    public SystemStatusSummary SystemStatus { get; set; } = new();
    public string AttestationStatement { get; set; } = string.Empty;
}

/// <summary>
/// Retention configuration summary for attestation.
/// </summary>
public sealed class RetentionConfigurationSummary
{
    public int TickDataRetentionDays { get; set; }
    public int BarDataRetentionDays { get; set; }
    public int QuoteDataRetentionDays { get; set; }
    public bool CompressBeforeDelete { get; set; }
    public bool ArchiveToCloud { get; set; }
}

/// <summary>
/// Guardrails summary for attestation.
/// </summary>
public sealed class GuardrailsSummary
{
    public int MinTickDataDays { get; set; }
    public int MinBarDataDays { get; set; }
    public int MinQuoteDataDays { get; set; }
    public int MaxDailyDeletedFiles { get; set; }
    public bool RequireChecksumVerification { get; set; }
    public bool RequireDryRunPreview { get; set; }
}

/// <summary>
/// Legal hold summary for attestation.
/// </summary>
public sealed class LegalHoldSummary
{
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int SymbolCount { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// Audit summary for attestation.
/// </summary>
public sealed class AuditSummary
{
    public DateTime ExecutedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int FilesDeleted { get; set; }
    public long BytesFreed { get; set; }
    public int ErrorCount { get; set; }
}

/// <summary>
/// System status summary for attestation.
/// </summary>
public sealed class SystemStatusSummary
{
    public string DataRootPath { get; set; } = string.Empty;
    public int TotalSymbolsConfigured { get; set; }
    public string ApplicationVersion { get; set; } = string.Empty;
    public DateTime LastConfigurationChange { get; set; }
}
