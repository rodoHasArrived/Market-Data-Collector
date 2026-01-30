using System.Text.Json.Serialization;

namespace MarketDataCollector.Contracts.Api;

// ============================================================
// Backfill Health and Provider Status DTOs
// ============================================================

/// <summary>
/// Response model for backfill provider health check.
/// </summary>
public sealed class BackfillHealthResponse
{
    [JsonPropertyName("isHealthy")]
    public bool IsHealthy { get; set; }

    [JsonPropertyName("providers")]
    public Dictionary<string, BackfillProviderHealth>? Providers { get; set; }
}

/// <summary>
/// Individual backfill provider health status.
/// </summary>
public sealed class BackfillProviderHealth
{
    [JsonPropertyName("isAvailable")]
    public bool IsAvailable { get; set; }

    [JsonPropertyName("latencyMs")]
    public double? LatencyMs { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("lastChecked")]
    public DateTime? LastChecked { get; set; }
}

// ============================================================
// Symbol Resolution DTOs
// ============================================================

/// <summary>
/// Symbol resolution result for backfill operations.
/// </summary>
public sealed class SymbolResolutionResponse
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("resolvedSymbol")]
    public string? ResolvedSymbol { get; set; }

    [JsonPropertyName("exchange")]
    public string? Exchange { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("securityType")]
    public string? SecurityType { get; set; }

    [JsonPropertyName("providerMappings")]
    public Dictionary<string, string>? ProviderMappings { get; set; }
}

// ============================================================
// Backfill Execution DTOs
// ============================================================

/// <summary>
/// Response for backfill execution initiation.
/// </summary>
public sealed class BackfillExecutionResponse
{
    [JsonPropertyName("executionId")]
    public string ExecutionId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Pending";

    [JsonPropertyName("startedAt")]
    public DateTime StartedAt { get; set; }

    [JsonPropertyName("symbols")]
    public string[] Symbols { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Backfill execution record for history tracking.
/// </summary>
public sealed class BackfillExecution
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("scheduleId")]
    public string ScheduleId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Pending";

    [JsonPropertyName("startedAt")]
    public DateTime StartedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("symbolsProcessed")]
    public int SymbolsProcessed { get; set; }

    [JsonPropertyName("barsDownloaded")]
    public int BarsDownloaded { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}

// ============================================================
// Backfill Preset DTOs
// ============================================================

/// <summary>
/// Backfill preset definition for scheduled operations.
/// </summary>
public sealed class BackfillPreset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("cronExpression")]
    public string CronExpression { get; set; } = string.Empty;

    [JsonPropertyName("lookbackDays")]
    public int LookbackDays { get; set; }
}

// ============================================================
// Backfill Statistics DTOs
// ============================================================

/// <summary>
/// Backfill statistics summary.
/// </summary>
public sealed class BackfillStatistics
{
    [JsonPropertyName("totalExecutions")]
    public int TotalExecutions { get; set; }

    [JsonPropertyName("successfulExecutions")]
    public int SuccessfulExecutions { get; set; }

    [JsonPropertyName("failedExecutions")]
    public int FailedExecutions { get; set; }

    [JsonPropertyName("totalBarsDownloaded")]
    public long TotalBarsDownloaded { get; set; }

    [JsonPropertyName("averageExecutionTimeSeconds")]
    public double AverageExecutionTimeSeconds { get; set; }

    [JsonPropertyName("lastSuccessfulExecution")]
    public DateTime? LastSuccessfulExecution { get; set; }
}

// ============================================================
// Gap-Fill Request DTO
// ============================================================

/// <summary>
/// Request for running a gap-fill operation.
/// </summary>
public sealed class GapFillRequest
{
    [JsonPropertyName("symbols")]
    public string[] Symbols { get; set; } = Array.Empty<string>();

    [JsonPropertyName("lookbackDays")]
    public int LookbackDays { get; set; } = 30;

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "High";
}
