using System.Text.RegularExpressions;

namespace MarketDataCollector.Ui.Services;

/// <summary>
/// Platform-independent validation rules for forms.
/// Shared between UWP and WPF projects; UI-specific helpers remain in each platform.
/// </summary>
public static class FormValidationRules
{
    /// <summary>
    /// Validates that a field is not empty.
    /// </summary>
    public static ValidationResult ValidateRequired(string? value, string fieldName = "This field")
    {
        if (string.IsNullOrWhiteSpace(value))
            return ValidationResult.Error($"{fieldName} is required.");
        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates a stock symbol format.
    /// </summary>
    public static ValidationResult ValidateSymbol(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ValidationResult.Error("Symbol is required.");

        var trimmed = value.Trim();
        if (trimmed.Length < 1 || trimmed.Length > 10)
            return ValidationResult.Error("Symbol must be between 1 and 10 characters.");

        if (!Regex.IsMatch(trimmed, @"^[A-Za-z0-9./-]+$"))
            return ValidationResult.Error("Symbol can only contain letters, numbers, dots, dashes, and slashes.");

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates a comma-separated list of symbols.
    /// </summary>
    public static ValidationResult ValidateSymbolList(string? value, int maxSymbols = 100)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ValidationResult.Error("At least one symbol is required.");

        var symbols = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (symbols.Length == 0)
            return ValidationResult.Error("At least one symbol is required.");
        if (symbols.Length > maxSymbols)
            return ValidationResult.Error($"Maximum {maxSymbols} symbols allowed.");

        var invalidSymbols = new List<string>();
        foreach (var symbol in symbols)
        {
            var result = ValidateSymbol(symbol);
            if (!result.IsValid) invalidSymbols.Add(symbol);
        }

        if (invalidSymbols.Count > 0)
            return ValidationResult.Error($"Invalid symbol(s): {string.Join(", ", invalidSymbols.Take(5))}{(invalidSymbols.Count > 5 ? "..." : "")}");

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates a date range.
    /// </summary>
    public static ValidationResult ValidateDateRange(DateTimeOffset? fromDate, DateTimeOffset? toDate)
    {
        if (!fromDate.HasValue || !toDate.HasValue)
            return ValidationResult.Error("Both start and end dates are required.");
        if (fromDate.Value > toDate.Value)
            return ValidationResult.Error("Start date must be before or equal to end date.");
        if (fromDate.Value > DateTimeOffset.Now)
            return ValidationResult.Error("Start date cannot be in the future.");

        var daysDiff = (toDate.Value - fromDate.Value).TotalDays;
        if (daysDiff > 365 * 10)
            return ValidationResult.Warning("Date range spans more than 10 years. This may take a long time.");

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates an API key format.
    /// </summary>
    public static ValidationResult ValidateApiKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ValidationResult.Error("API key is required.");
        if (value.Length < 8)
            return ValidationResult.Error("API key seems too short. Please check the key.");
        if (value.Contains(' '))
            return ValidationResult.Error("API key should not contain spaces.");
        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates a URL format.
    /// </summary>
    public static ValidationResult ValidateUrl(string? value, string fieldName = "URL")
    {
        if (string.IsNullOrWhiteSpace(value))
            return ValidationResult.Error($"{fieldName} is required.");
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return ValidationResult.Error($"Invalid {fieldName} format. Example: http://localhost:8080");
        if (uri.Scheme != "http" && uri.Scheme != "https")
            return ValidationResult.Error($"{fieldName} must start with http:// or https://");
        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates a port number.
    /// </summary>
    public static ValidationResult ValidatePort(int? value)
    {
        if (!value.HasValue)
            return ValidationResult.Error("Port is required.");
        if (value < 1 || value > 65535)
            return ValidationResult.Error("Port must be between 1 and 65535.");
        if (value < 1024)
            return ValidationResult.Warning("Port below 1024 may require administrator privileges.");
        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates a file path.
    /// </summary>
    public static ValidationResult ValidateFilePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ValidationResult.Error("File path is required.");
        try
        {
            _ = Path.GetFullPath(value);
            return ValidationResult.Success();
        }
        catch
        {
            return ValidationResult.Error("Invalid file path format.");
        }
    }

    /// <summary>
    /// Validates a numeric range.
    /// </summary>
    public static ValidationResult ValidateNumericRange(double? value, double min, double max, string fieldName = "Value")
    {
        if (!value.HasValue)
            return ValidationResult.Error($"{fieldName} is required.");
        if (value < min || value > max)
            return ValidationResult.Error($"{fieldName} must be between {min} and {max}.");
        return ValidationResult.Success();
    }
}

/// <summary>
/// Represents the result of a validation operation.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>Gets whether validation passed.</summary>
    public bool IsValid { get; private init; }

    /// <summary>Gets whether this is a warning (valid but with concerns).</summary>
    public bool IsWarning { get; private init; }

    /// <summary>Gets the validation message.</summary>
    public string Message { get; private init; } = string.Empty;

    private ValidationResult() { }

    /// <summary>Creates a successful validation result.</summary>
    public static ValidationResult Success() => new() { IsValid = true };

    /// <summary>Creates a failed validation result with an error message.</summary>
    public static ValidationResult Error(string message) => new() { IsValid = false, Message = message };

    /// <summary>Creates a warning validation result (valid but with concerns).</summary>
    public static ValidationResult Warning(string message) => new() { IsValid = true, IsWarning = true, Message = message };

    /// <summary>Implicit conversion to bool for easy if checks.</summary>
    public static implicit operator bool(ValidationResult result) => result.IsValid;
}

/// <summary>
/// Extension methods for combining validation results.
/// </summary>
public static class ValidationExtensions
{
    /// <summary>
    /// Validates multiple results and returns the first error or warning, or success.
    /// </summary>
    public static ValidationResult Combine(params ValidationResult[] results)
    {
        foreach (var result in results.Where(r => !r.IsValid && !r.IsWarning))
            return result;

        foreach (var result in results.Where(r => r.IsWarning))
            return result;

        return ValidationResult.Success();
    }
}
