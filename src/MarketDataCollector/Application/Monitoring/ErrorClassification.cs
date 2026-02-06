using System.Collections.Frozen;
using System.Text.RegularExpressions;
using MarketDataCollector.Application.Exceptions;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Application.Monitoring;

/// <summary>
/// Categorizes errors by their root cause domain.
/// </summary>
public enum ErrorCategory
{
    /// <summary>Connection failures, timeouts, DNS resolution errors.</summary>
    Network,

    /// <summary>Credential failures, token expiry, unauthorized access.</summary>
    Authentication,

    /// <summary>HTTP 429 responses, quota exceeded, throttling.</summary>
    RateLimit,

    /// <summary>Validation failures, corrupt data, anomalies.</summary>
    DataQuality,

    /// <summary>Disk full, permission denied, IO errors.</summary>
    Storage,

    /// <summary>Invalid configuration, missing settings.</summary>
    Configuration,

    /// <summary>Provider-specific errors, API errors.</summary>
    Provider,

    /// <summary>Unexpected exceptions, null references, argument errors.</summary>
    Internal,

    /// <summary>Temporary failures that typically auto-recover.</summary>
    Transient
}

/// <summary>
/// Granular severity levels extending beyond the existing <see cref="ErrorLevel"/> enum.
/// </summary>
public enum ErrorSeverity
{
    /// <summary>Logged for diagnostic purposes but not actively tracked.</summary>
    Debug = 0,

    /// <summary>Informational event, no action needed.</summary>
    Info = 1,

    /// <summary>Potential issue that should be monitored.</summary>
    Warning = 2,

    /// <summary>Action needed, but the system continues operating.</summary>
    Error = 3,

    /// <summary>Immediate action required, possible data loss.</summary>
    Critical = 4,

    /// <summary>System cannot continue operating.</summary>
    Fatal = 5
}

/// <summary>
/// An error entry enriched with classification metadata including category,
/// severity, retryability, and suggested remediation actions.
/// </summary>
/// <param name="Entry">The original error entry from the ring buffer.</param>
/// <param name="Category">The classified error category.</param>
/// <param name="Severity">The classified severity level.</param>
/// <param name="IsRetryable">Whether the operation that caused this error can be retried.</param>
/// <param name="SuggestedAction">Human-readable remediation guidance.</param>
/// <param name="Tags">Descriptive tags for filtering and grouping (e.g., "backfill", "alpaca", "rate-limit").</param>
public sealed record ClassifiedError(
    ErrorEntry Entry,
    ErrorCategory Category,
    ErrorSeverity Severity,
    bool IsRetryable,
    string SuggestedAction,
    string[] Tags);

/// <summary>
/// Aggregate summary of a collection of classified errors, providing
/// breakdowns by category, severity, and retryability.
/// </summary>
/// <param name="TotalErrors">Total number of classified errors.</param>
/// <param name="ByCategoryCount">Error counts keyed by <see cref="ErrorCategory"/>.</param>
/// <param name="BySeverityCount">Error counts keyed by <see cref="ErrorSeverity"/>.</param>
/// <param name="MostCommonCategory">The category with the highest error count, or null if empty.</param>
/// <param name="HighestSeverity">The highest severity observed, or null if empty.</param>
/// <param name="RetryableCount">Number of errors that are retryable.</param>
/// <param name="NonRetryableCount">Number of errors that are not retryable.</param>
/// <param name="TopSuggestedActions">Deduplicated list of suggested remediation actions.</param>
public sealed record ErrorClassificationSummary(
    int TotalErrors,
    IReadOnlyDictionary<ErrorCategory, int> ByCategoryCount,
    IReadOnlyDictionary<ErrorSeverity, int> BySeverityCount,
    ErrorCategory? MostCommonCategory,
    ErrorSeverity? HighestSeverity,
    int RetryableCount,
    int NonRetryableCount,
    IReadOnlyList<string> TopSuggestedActions);

/// <summary>
/// Classifies errors into structured categories with severity levels, retryability,
/// and suggested remediation actions based on exception types, message patterns,
/// and contextual information.
/// </summary>
/// <remarks>
/// Classification is performed using a layered strategy:
/// <list type="number">
///   <item>Exception type mapping (strongly-typed custom exceptions)</item>
///   <item>Exception type name matching (for entries without a live exception)</item>
///   <item>Message pattern matching (regex-based heuristics)</item>
///   <item>Context-based severity adjustment</item>
/// </list>
/// </remarks>
public sealed class ErrorClassifier
{
    private static readonly ILogger _log = LoggingSetup.ForContext<ErrorClassifier>();

    /// <summary>
    /// Message patterns mapped to their classification outcomes.
    /// Evaluated in order; first match wins.
    /// </summary>
    private static readonly FrozenDictionary<string, (ErrorCategory Category, ErrorSeverity Severity, bool IsRetryable, string Action)> s_exceptionTypeMap =
        new Dictionary<string, (ErrorCategory, ErrorSeverity, bool, string)>
        {
            [nameof(RateLimitException)] = (ErrorCategory.RateLimit, ErrorSeverity.Warning, true, "Wait for rate limit window to reset before retrying. Consider reducing request frequency."),
            [nameof(ConnectionException)] = (ErrorCategory.Network, ErrorSeverity.Error, true, "Check network connectivity and provider endpoint availability. Verify firewall rules."),
            [nameof(OperationTimeoutException)] = (ErrorCategory.Network, ErrorSeverity.Warning, true, "Operation timed out. Check network latency and consider increasing timeout thresholds."),
            [nameof(ValidationException)] = (ErrorCategory.DataQuality, ErrorSeverity.Warning, false, "Review data validation rules. Inspect the incoming data for format or constraint violations."),
            [nameof(SequenceValidationException)] = (ErrorCategory.DataQuality, ErrorSeverity.Warning, false, "Sequence anomaly detected. Check provider data stream for gaps or resets."),
            [nameof(ConfigurationException)] = (ErrorCategory.Configuration, ErrorSeverity.Error, false, "Review application configuration. Run --validate-config or --quick-check to identify issues."),
            [nameof(StorageException)] = (ErrorCategory.Storage, ErrorSeverity.Error, false, "Check disk space, file permissions, and storage path configuration."),
            [nameof(DataProviderException)] = (ErrorCategory.Provider, ErrorSeverity.Error, true, "Check provider status and API documentation. Verify API credentials are valid."),
        }.ToFrozenDictionary();

    /// <summary>
    /// Regex patterns evaluated against error messages for classification when
    /// exception type mapping does not produce a match.
    /// </summary>
    private static readonly (Regex Pattern, ErrorCategory Category, ErrorSeverity Severity, bool IsRetryable, string Action)[] s_messagePatterns =
    [
        // Rate limiting patterns
        (new Regex(@"\b429\b|rate.?limit|too many requests|quota exceeded|throttl", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            ErrorCategory.RateLimit, ErrorSeverity.Warning, true,
            "Rate limit exceeded. Back off and retry after the indicated delay."),

        // Authentication patterns
        (new Regex(@"\b401\b|\b403\b|unauthori[sz]ed|forbidden|credential|token.?expir|authentication.?fail|invalid.?api.?key", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            ErrorCategory.Authentication, ErrorSeverity.Error, false,
            "Authentication failure. Verify API credentials in environment variables and check for token expiry."),

        // Network / connectivity patterns
        (new Regex(@"connect(ion)?.?(refused|reset|closed|fail|timeout|abort)|dns.?(resolv|lookup|fail)|socket|network.?(unreachable|error)|no.?route|ECONNREFUSED|ETIMEDOUT|EHOSTUNREACH", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            ErrorCategory.Network, ErrorSeverity.Error, true,
            "Network connectivity issue. Verify the provider endpoint is reachable and check DNS resolution."),

        // Timeout patterns (beyond OperationTimeoutException)
        (new Regex(@"timed?\s*out|timeout|deadline.?exceeded|request.?expired", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            ErrorCategory.Network, ErrorSeverity.Warning, true,
            "Operation timed out. Consider increasing timeout values or checking provider latency."),

        // Storage / IO patterns
        (new Regex(@"disk.?(full|space)|no.?space|permission.?denied|access.?denied|IOException|DirectoryNotFoundException|could not (write|create|open) file|read.?only file.?system", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            ErrorCategory.Storage, ErrorSeverity.Error, false,
            "Storage error. Check available disk space, file permissions, and storage path configuration."),

        // Data quality patterns
        (new Regex(@"invalid.?data|corrupt|malformed|parse.?error|deserialization|unexpected.?format|anomal|data.?quality|out.?of.?range|negative.?price|zero.?volume", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            ErrorCategory.DataQuality, ErrorSeverity.Warning, false,
            "Data quality issue detected. Review the incoming data stream and validation rules."),

        // Configuration patterns
        (new Regex(@"config(uration)?.?(missing|invalid|error|not found)|missing.?setting|required.?field|appsettings", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            ErrorCategory.Configuration, ErrorSeverity.Error, false,
            "Configuration error. Check appsettings.json and environment variables. Run --quick-check."),

        // Transient / temporary patterns
        (new Regex(@"transient|temporary|service.?unavailable|\b503\b|\b502\b|bad.?gateway|try.?again|retry", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            ErrorCategory.Transient, ErrorSeverity.Warning, true,
            "Transient failure detected. The operation should auto-recover on retry."),

        // Provider API patterns
        (new Regex(@"api.?error|\b500\b|internal.?server.?error|provider.?(error|fail|unavailable)|upstream", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            ErrorCategory.Provider, ErrorSeverity.Error, true,
            "Provider API error. Check provider status page and API documentation."),
    ];

    /// <summary>
    /// Classifies a live exception into a <see cref="ClassifiedError"/> with category,
    /// severity, retryability, and suggested remediation.
    /// </summary>
    /// <param name="ex">The exception to classify.</param>
    /// <param name="context">Optional context string (e.g., "backfill", "streaming").</param>
    /// <param name="symbol">Optional trading symbol associated with the error.</param>
    /// <param name="provider">Optional data provider name.</param>
    /// <returns>A fully classified error record.</returns>
    public ClassifiedError Classify(Exception ex, string? context = null, string? symbol = null, string? provider = null)
    {
        ArgumentNullException.ThrowIfNull(ex);

        var entry = new ErrorEntry(
            Id: GenerateId(),
            Timestamp: DateTimeOffset.UtcNow,
            Level: MapExceptionToLevel(ex),
            Source: ex.Source ?? "Unknown",
            Message: ex.Message,
            ExceptionType: ex.GetType().Name,
            StackTrace: ex.StackTrace,
            Context: context,
            Symbol: symbol ?? ExtractSymbolFromException(ex),
            Provider: provider ?? ExtractProviderFromException(ex));

        var (category, severity, isRetryable, suggestedAction) = ClassifyByExceptionType(ex);

        // If exception type didn't yield a specific classification, try message patterns
        if (category == ErrorCategory.Internal)
        {
            var messageResult = ClassifyByMessagePattern(ex.Message);
            if (messageResult.HasValue)
            {
                (category, severity, isRetryable, suggestedAction) = messageResult.Value;
            }
        }

        // Adjust severity based on context
        severity = AdjustSeverityForContext(severity, context, category);

        var tags = BuildTags(category, context, symbol, provider, ex);

        _log.Debug(
            "Classified error {ErrorId} as {Category}/{Severity} (retryable={IsRetryable}) for {Symbol} via {Provider}",
            entry.Id, category, severity, isRetryable, symbol, provider);

        return new ClassifiedError(entry, category, severity, isRetryable, suggestedAction, tags);
    }

    /// <summary>
    /// Classifies an existing <see cref="ErrorEntry"/> from the ring buffer into
    /// a <see cref="ClassifiedError"/>.
    /// </summary>
    /// <param name="entry">The error entry to classify.</param>
    /// <returns>A fully classified error record.</returns>
    public ClassifiedError Classify(ErrorEntry entry)
    {
        var (category, severity, isRetryable, suggestedAction) = ClassifyByExceptionTypeName(entry.ExceptionType);

        // If type name didn't yield a specific classification, try message patterns
        if (category == ErrorCategory.Internal)
        {
            var messageResult = ClassifyByMessagePattern(entry.Message);
            if (messageResult.HasValue)
            {
                (category, severity, isRetryable, suggestedAction) = messageResult.Value;
            }
        }

        // Map existing ErrorLevel to ErrorSeverity as a baseline if we landed on Internal
        if (category == ErrorCategory.Internal)
        {
            severity = MapErrorLevelToSeverity(entry.Level);
        }

        // Adjust severity based on context
        severity = AdjustSeverityForContext(severity, entry.Context, category);

        var tags = BuildTags(category, entry.Context, entry.Symbol, entry.Provider, exceptionTypeName: entry.ExceptionType);

        _log.Debug(
            "Classified entry {ErrorId} as {Category}/{Severity} (retryable={IsRetryable})",
            entry.Id, category, severity, isRetryable);

        return new ClassifiedError(entry, category, severity, isRetryable, suggestedAction, tags);
    }

    /// <summary>
    /// Produces an aggregate summary of a collection of classified errors.
    /// </summary>
    /// <param name="errors">The classified errors to summarize.</param>
    /// <returns>An <see cref="ErrorClassificationSummary"/> with breakdowns by category and severity.</returns>
    public ErrorClassificationSummary GetSummary(IEnumerable<ClassifiedError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var errorList = errors as IReadOnlyList<ClassifiedError> ?? errors.ToList();

        if (errorList.Count == 0)
        {
            return new ErrorClassificationSummary(
                TotalErrors: 0,
                ByCategoryCount: new Dictionary<ErrorCategory, int>(),
                BySeverityCount: new Dictionary<ErrorSeverity, int>(),
                MostCommonCategory: null,
                HighestSeverity: null,
                RetryableCount: 0,
                NonRetryableCount: 0,
                TopSuggestedActions: []);
        }

        var byCategoryCount = errorList
            .GroupBy(e => e.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        var bySeverityCount = errorList
            .GroupBy(e => e.Severity)
            .ToDictionary(g => g.Key, g => g.Count());

        var mostCommonCategory = byCategoryCount
            .OrderByDescending(kvp => kvp.Value)
            .First().Key;

        var highestSeverity = bySeverityCount
            .OrderByDescending(kvp => (int)kvp.Key)
            .First().Key;

        var retryableCount = errorList.Count(e => e.IsRetryable);

        var topSuggestedActions = errorList
            .Select(e => e.SuggestedAction)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        _log.Debug(
            "Generated error classification summary: {TotalErrors} errors, most common category {MostCommonCategory}, highest severity {HighestSeverity}",
            errorList.Count, mostCommonCategory, highestSeverity);

        return new ErrorClassificationSummary(
            TotalErrors: errorList.Count,
            ByCategoryCount: byCategoryCount,
            BySeverityCount: bySeverityCount,
            MostCommonCategory: mostCommonCategory,
            HighestSeverity: highestSeverity,
            RetryableCount: retryableCount,
            NonRetryableCount: errorList.Count - retryableCount,
            TopSuggestedActions: topSuggestedActions);
    }

    /// <summary>
    /// Classifies based on the live exception's runtime type.
    /// </summary>
    private static (ErrorCategory Category, ErrorSeverity Severity, bool IsRetryable, string Action) ClassifyByExceptionType(Exception ex)
    {
        return ex switch
        {
            RateLimitException rle => (
                ErrorCategory.RateLimit,
                ErrorSeverity.Warning,
                true,
                rle.RetryAfter.HasValue
                    ? $"Rate limit exceeded. Retry after {rle.RetryAfter.Value.TotalSeconds:F0}s."
                    : "Rate limit exceeded. Wait for the rate limit window to reset before retrying."),

            ConnectionException => (
                ErrorCategory.Network,
                ErrorSeverity.Error,
                true,
                "Connection failure. Check network connectivity and provider endpoint availability."),

            OperationTimeoutException ote => (
                ErrorCategory.Network,
                ErrorSeverity.Warning,
                true,
                ote.Timeout.HasValue
                    ? $"Operation '{ote.OperationName ?? "unknown"}' timed out after {ote.Timeout.Value.TotalSeconds:F0}s. Consider increasing the timeout."
                    : "Operation timed out. Check network latency and consider increasing timeout thresholds."),

            SequenceValidationException sve => (
                ErrorCategory.DataQuality,
                ErrorSeverity.Warning,
                false,
                $"Sequence validation failure ({sve.ValidationType}). Check provider data stream for gaps or resets."),

            ValidationException => (
                ErrorCategory.DataQuality,
                ErrorSeverity.Warning,
                false,
                "Data validation failed. Review the incoming data for format or constraint violations."),

            ConfigurationException ce => (
                ErrorCategory.Configuration,
                ErrorSeverity.Error,
                false,
                ce.FieldName is not null
                    ? $"Configuration error in field '{ce.FieldName}'. Run --quick-check to identify issues."
                    : "Configuration error. Review appsettings.json and environment variables."),

            StorageException se => (
                ErrorCategory.Storage,
                ErrorSeverity.Error,
                false,
                se.Path is not null
                    ? $"Storage error at path '{se.Path}'. Check disk space and file permissions."
                    : "Storage error. Check disk space, file permissions, and storage path configuration."),

            DataProviderException => (
                ErrorCategory.Provider,
                ErrorSeverity.Error,
                true,
                "Data provider error. Check provider status and API documentation."),

            // Framework / BCL exceptions that indicate transient issues
            HttpRequestException => (
                ErrorCategory.Network,
                ErrorSeverity.Warning,
                true,
                "HTTP request failed. Check network connectivity and provider availability."),

            TaskCanceledException or OperationCanceledException => (
                ErrorCategory.Transient,
                ErrorSeverity.Info,
                false,
                "Operation was cancelled. This may be expected during shutdown or timeout."),

            IOException => (
                ErrorCategory.Storage,
                ErrorSeverity.Error,
                false,
                "I/O error. Check disk space, file permissions, and storage availability."),

            UnauthorizedAccessException => (
                ErrorCategory.Storage,
                ErrorSeverity.Error,
                false,
                "Access denied. Check file system permissions for the storage directory."),

            ArgumentException or ArgumentNullException or ArgumentOutOfRangeException => (
                ErrorCategory.Internal,
                ErrorSeverity.Error,
                false,
                "Invalid argument detected. This indicates a programming error that should be reported."),

            NullReferenceException => (
                ErrorCategory.Internal,
                ErrorSeverity.Critical,
                false,
                "Null reference encountered. This indicates a programming error that should be reported."),

            OutOfMemoryException => (
                ErrorCategory.Internal,
                ErrorSeverity.Fatal,
                false,
                "System is out of memory. Reduce channel capacity or monitored symbols. Restart the application."),

            _ => (
                ErrorCategory.Internal,
                ErrorSeverity.Error,
                false,
                "Unexpected error. Check logs for details and report if recurring.")
        };
    }

    /// <summary>
    /// Classifies based on the exception type name stored in an <see cref="ErrorEntry"/>.
    /// </summary>
    private static (ErrorCategory Category, ErrorSeverity Severity, bool IsRetryable, string Action) ClassifyByExceptionTypeName(string? exceptionTypeName)
    {
        if (string.IsNullOrEmpty(exceptionTypeName))
        {
            return (ErrorCategory.Internal, ErrorSeverity.Warning, false, "Unknown error type. Check logs for details.");
        }

        if (s_exceptionTypeMap.TryGetValue(exceptionTypeName, out var mapped))
        {
            return mapped;
        }

        // Check for common BCL exception type names
        return exceptionTypeName switch
        {
            nameof(HttpRequestException) => (ErrorCategory.Network, ErrorSeverity.Warning, true,
                "HTTP request failed. Check network connectivity and provider availability."),
            nameof(TaskCanceledException) or nameof(OperationCanceledException) => (ErrorCategory.Transient, ErrorSeverity.Info, false,
                "Operation was cancelled. This may be expected during shutdown or timeout."),
            nameof(IOException) => (ErrorCategory.Storage, ErrorSeverity.Error, false,
                "I/O error. Check disk space, file permissions, and storage availability."),
            nameof(UnauthorizedAccessException) => (ErrorCategory.Storage, ErrorSeverity.Error, false,
                "Access denied. Check file system permissions for the storage directory."),
            nameof(ArgumentException) or nameof(ArgumentNullException) or nameof(ArgumentOutOfRangeException) => (ErrorCategory.Internal, ErrorSeverity.Error, false,
                "Invalid argument detected. This indicates a programming error that should be reported."),
            nameof(NullReferenceException) => (ErrorCategory.Internal, ErrorSeverity.Critical, false,
                "Null reference encountered. This indicates a programming error that should be reported."),
            nameof(OutOfMemoryException) => (ErrorCategory.Internal, ErrorSeverity.Fatal, false,
                "System is out of memory. Reduce channel capacity or monitored symbols. Restart the application."),
            _ => (ErrorCategory.Internal, ErrorSeverity.Error, false,
                "Unexpected error. Check logs for details and report if recurring.")
        };
    }

    /// <summary>
    /// Attempts classification by matching the error message against known patterns.
    /// </summary>
    private static (ErrorCategory Category, ErrorSeverity Severity, bool IsRetryable, string Action)? ClassifyByMessagePattern(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return null;
        }

        foreach (var (pattern, category, severity, isRetryable, action) in s_messagePatterns)
        {
            if (pattern.IsMatch(message))
            {
                return (category, severity, isRetryable, action);
            }
        }

        return null;
    }

    /// <summary>
    /// Adjusts severity based on the operational context. For example, errors during
    /// backfill are typically less severe than during live streaming since backfill
    /// can be retried.
    /// </summary>
    private static ErrorSeverity AdjustSeverityForContext(ErrorSeverity baseSeverity, string? context, ErrorCategory category)
    {
        if (string.IsNullOrEmpty(context))
        {
            return baseSeverity;
        }

        var contextLower = context.ToLowerInvariant();

        // Backfill errors are typically less severe since the operation can be retried
        if (contextLower.Contains("backfill") && baseSeverity > ErrorSeverity.Warning && category != ErrorCategory.Configuration)
        {
            return baseSeverity - 1;
        }

        // Streaming/live errors are more critical since they affect real-time data capture
        if ((contextLower.Contains("streaming") || contextLower.Contains("live")) && baseSeverity < ErrorSeverity.Fatal)
        {
            if (category is ErrorCategory.Network or ErrorCategory.Provider)
            {
                return baseSeverity + 1;
            }
        }

        // Storage errors during archival may be elevated since they risk data loss
        if (contextLower.Contains("archival") && category == ErrorCategory.Storage && baseSeverity < ErrorSeverity.Fatal)
        {
            return baseSeverity + 1;
        }

        return baseSeverity;
    }

    /// <summary>
    /// Maps the existing <see cref="ErrorLevel"/> to <see cref="ErrorSeverity"/>.
    /// </summary>
    private static ErrorSeverity MapErrorLevelToSeverity(ErrorLevel level) => level switch
    {
        ErrorLevel.Warning => ErrorSeverity.Warning,
        ErrorLevel.Error => ErrorSeverity.Error,
        ErrorLevel.Critical => ErrorSeverity.Critical,
        _ => ErrorSeverity.Error
    };

    /// <summary>
    /// Maps an exception to the coarse <see cref="ErrorLevel"/> for the <see cref="ErrorEntry"/>.
    /// </summary>
    private static ErrorLevel MapExceptionToLevel(Exception ex) => ex switch
    {
        RateLimitException => ErrorLevel.Warning,
        OperationTimeoutException => ErrorLevel.Warning,
        ValidationException => ErrorLevel.Warning,
        SequenceValidationException => ErrorLevel.Warning,
        OutOfMemoryException => ErrorLevel.Critical,
        NullReferenceException => ErrorLevel.Critical,
        _ => ErrorLevel.Error
    };

    /// <summary>
    /// Attempts to extract a symbol from known exception types that carry symbol metadata.
    /// </summary>
    private static string? ExtractSymbolFromException(Exception ex) => ex switch
    {
        DataProviderException dpe => dpe.Symbol,
        SequenceValidationException sve => sve.Symbol,
        _ => null
    };

    /// <summary>
    /// Attempts to extract a provider name from known exception types that carry provider metadata.
    /// </summary>
    private static string? ExtractProviderFromException(Exception ex) => ex switch
    {
        DataProviderException dpe => dpe.Provider,
        ConnectionException ce => ce.Provider,
        OperationTimeoutException ote => ote.Provider,
        _ => null
    };

    /// <summary>
    /// Builds descriptive tags for a classified error based on its category,
    /// context, symbol, provider, and exception metadata.
    /// </summary>
    private static string[] BuildTags(
        ErrorCategory category,
        string? context,
        string? symbol,
        string? provider,
        Exception? ex = null,
        string? exceptionTypeName = null)
    {
        var tags = new List<string>(6)
        {
            category.ToString().ToLowerInvariant()
        };

        if (!string.IsNullOrEmpty(context))
        {
            tags.Add(context.ToLowerInvariant());
        }

        if (!string.IsNullOrEmpty(symbol))
        {
            tags.Add(symbol.ToUpperInvariant());
        }

        if (!string.IsNullOrEmpty(provider))
        {
            tags.Add(provider.ToLowerInvariant());
        }

        // Add specific tags based on exception characteristics
        var typeName = ex?.GetType().Name ?? exceptionTypeName;
        if (typeName is not null)
        {
            if (typeName.Contains("RateLimit", StringComparison.OrdinalIgnoreCase))
            {
                tags.Add("rate-limit");
            }
            else if (typeName.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
            {
                tags.Add("timeout");
            }
            else if (typeName.Contains("Connection", StringComparison.OrdinalIgnoreCase))
            {
                tags.Add("connection");
            }
        }

        return tags.ToArray();
    }

    private static string GenerateId()
    {
        return $"ERR-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString()[..8]}";
    }
}
