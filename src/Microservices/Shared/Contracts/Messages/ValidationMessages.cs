namespace DataIngestion.Contracts.Messages;

/// <summary>
/// Command to validate ingested data.
/// </summary>
public interface IValidateIngestionData : IIngestionMessage
{
    string Symbol { get; }
    DataValidationType ValidationType { get; }
    object Data { get; }
    ValidationRuleSet RuleSet { get; }
}

/// <summary>
/// Types of data validation.
/// </summary>
public enum DataValidationType
{
    Trade,
    Quote,
    OrderBook,
    OhlcvBar,
    TimeSeries
}

/// <summary>
/// Validation rule sets.
/// </summary>
public enum ValidationRuleSet
{
    /// <summary>Basic structural validation only.</summary>
    Basic,

    /// <summary>Standard validation including range checks.</summary>
    Standard,

    /// <summary>Strict validation with cross-reference checks.</summary>
    Strict,

    /// <summary>Custom rules defined per symbol/exchange.</summary>
    Custom
}

/// <summary>
/// Result of data validation.
/// </summary>
public interface IDataValidationResult : IIngestionMessage
{
    string Symbol { get; }
    DataValidationType ValidationType { get; }
    bool IsValid { get; }
    IReadOnlyList<ValidationIssue> Issues { get; }
    ValidationSeverity HighestSeverity { get; }
    TimeSpan ValidationDuration { get; }
}

/// <summary>
/// Individual validation issue.
/// </summary>
public record ValidationIssue(
    string Code,
    string Message,
    ValidationSeverity Severity,
    string? Field = null,
    object? ExpectedValue = null,
    object? ActualValue = null
);

/// <summary>
/// Severity levels for validation issues.
/// </summary>
public enum ValidationSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Data quality metrics for a symbol.
/// </summary>
public interface IDataQualityMetrics : IIngestionMessage
{
    string Symbol { get; }
    DateTimeOffset PeriodStart { get; }
    DateTimeOffset PeriodEnd { get; }
    long TotalRecords { get; }
    long ValidRecords { get; }
    long InvalidRecords { get; }
    double ValidityRate { get; }
    long GapCount { get; }
    long DuplicateCount { get; }
    long OutOfOrderCount { get; }
    IReadOnlyDictionary<string, int> IssuesByCode { get; }
}

/// <summary>
/// Alert when data quality drops below threshold.
/// </summary>
public interface IDataQualityAlert : IIngestionMessage
{
    string Symbol { get; }
    string AlertType { get; }
    string Message { get; }
    ValidationSeverity Severity { get; }
    double CurrentValue { get; }
    double ThresholdValue { get; }
    DateTimeOffset DetectedAt { get; }
}
