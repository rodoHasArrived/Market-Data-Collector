using System.Text.Json;
using System.Text.Json.Serialization;
using MarketDataCollector.Contracts.Archive;
using MarketDataCollector.Ui.Services;
using MarketDataCollector.Wpf.Services;

namespace MarketDataCollector.Wpf.Serialization;

/// <summary>
/// Source-generated JSON context for WPF service types.
/// Eliminates reflection-based serialization in storage and retention services (ADR-014).
///
/// USAGE: Pass WpfJsonContext.PrettyPrintOptions to JsonSerializer.Serialize / Deserialize
/// for any type listed in the [JsonSerializable] attributes below.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    UseStringEnumConverter = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true,
    GenerationMode = JsonSourceGenerationMode.Default)]
// Archive health types (ArchiveHealthService)
[JsonSerializable(typeof(ArchiveHealthStatus))]
[JsonSerializable(typeof(ArchiveIssue))]
[JsonSerializable(typeof(ArchiveIssue[]))]
[JsonSerializable(typeof(StorageHealthInfo))]
// Retention settings types (RetentionAssuranceService)
[JsonSerializable(typeof(RetentionSettingsData))]
[JsonSerializable(typeof(RetentionConfiguration))]
[JsonSerializable(typeof(RetentionGuardrails))]
[JsonSerializable(typeof(LegalHold))]
[JsonSerializable(typeof(List<LegalHold>))]
// Retention audit report types (RetentionAssuranceService)
[JsonSerializable(typeof(RetentionAuditReport))]
[JsonSerializable(typeof(RetentionPolicy))]
[JsonSerializable(typeof(DeletedFileInfo))]
[JsonSerializable(typeof(List<DeletedFileInfo>))]
[JsonSerializable(typeof(ChecksumVerificationResult))]
[JsonSerializable(typeof(VerifiedFile))]
[JsonSerializable(typeof(List<VerifiedFile>))]
[JsonSerializable(typeof(CleanupStatus))]
public partial class WpfJsonContext : JsonSerializerContext
{
    /// <summary>
    /// Pre-configured options for pretty-printed output.
    /// Use for config files, audit reports, and health status persistence.
    /// - Indented output for readability
    /// - Enums serialized as strings
    /// - Null values omitted
    /// - Source-generated serializers (no reflection)
    /// </summary>
    public static readonly JsonSerializerOptions PrettyPrintOptions = new()
    {
        TypeInfoResolver = Default,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };
}
