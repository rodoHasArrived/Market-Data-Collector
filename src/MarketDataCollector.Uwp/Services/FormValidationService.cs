using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for form validation with inline error display.
/// Provides consistent validation across all forms in the application.
/// </summary>
public sealed class FormValidationService
{
    private static FormValidationService? _instance;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static FormValidationService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new FormValidationService();
                }
            }
            return _instance;
        }
    }

    private FormValidationService() { }

    #region Validation Rules

    /// <summary>
    /// Validates that a field is not empty.
    /// </summary>
    public static ValidationResult ValidateRequired(string? value, string fieldName = "This field")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Error($"{fieldName} is required.");
        }
        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates a stock symbol format.
    /// </summary>
    public static ValidationResult ValidateSymbol(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Error("Symbol is required.");
        }

        var trimmed = value.Trim();

        if (trimmed.Length < 1 || trimmed.Length > 10)
        {
            return ValidationResult.Error("Symbol must be between 1 and 10 characters.");
        }

        if (!Regex.IsMatch(trimmed, @"^[A-Za-z0-9./-]+$"))
        {
            return ValidationResult.Error("Symbol can only contain letters, numbers, dots, dashes, and slashes.");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates a comma-separated list of symbols.
    /// </summary>
    public static ValidationResult ValidateSymbolList(string? value, int maxSymbols = 100)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Error("At least one symbol is required.");
        }

        var symbols = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (symbols.Length == 0)
        {
            return ValidationResult.Error("At least one symbol is required.");
        }

        if (symbols.Length > maxSymbols)
        {
            return ValidationResult.Error($"Maximum {maxSymbols} symbols allowed.");
        }

        var invalidSymbols = new List<string>();
        foreach (var symbol in symbols)
        {
            var result = ValidateSymbol(symbol);
            if (!result.IsValid)
            {
                invalidSymbols.Add(symbol);
            }
        }

        if (invalidSymbols.Count > 0)
        {
            return ValidationResult.Error($"Invalid symbol(s): {string.Join(", ", invalidSymbols.Take(5))}{(invalidSymbols.Count > 5 ? "..." : "")}");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates a date range.
    /// </summary>
    public static ValidationResult ValidateDateRange(DateTimeOffset? fromDate, DateTimeOffset? toDate)
    {
        if (!fromDate.HasValue || !toDate.HasValue)
        {
            return ValidationResult.Error("Both start and end dates are required.");
        }

        if (fromDate.Value > toDate.Value)
        {
            return ValidationResult.Error("Start date must be before or equal to end date.");
        }

        if (fromDate.Value > DateTimeOffset.Now)
        {
            return ValidationResult.Error("Start date cannot be in the future.");
        }

        var daysDiff = (toDate.Value - fromDate.Value).TotalDays;
        if (daysDiff > 365 * 10)
        {
            return ValidationResult.Warning("Date range spans more than 10 years. This may take a long time.");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates an API key format.
    /// </summary>
    public static ValidationResult ValidateApiKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Error("API key is required.");
        }

        if (value.Length < 8)
        {
            return ValidationResult.Error("API key seems too short. Please check the key.");
        }

        if (value.Contains(' '))
        {
            return ValidationResult.Error("API key should not contain spaces.");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates a URL format.
    /// </summary>
    public static ValidationResult ValidateUrl(string? value, string fieldName = "URL")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Error($"{fieldName} is required.");
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return ValidationResult.Error($"Invalid {fieldName} format. Example: http://localhost:8080");
        }

        if (uri.Scheme != "http" && uri.Scheme != "https")
        {
            return ValidationResult.Error($"{fieldName} must start with http:// or https://");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates a port number.
    /// </summary>
    public static ValidationResult ValidatePort(int? value)
    {
        if (!value.HasValue)
        {
            return ValidationResult.Error("Port is required.");
        }

        if (value < 1 || value > 65535)
        {
            return ValidationResult.Error("Port must be between 1 and 65535.");
        }

        if (value < 1024)
        {
            return ValidationResult.Warning("Port below 1024 may require administrator privileges.");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates a file path.
    /// </summary>
    public static ValidationResult ValidateFilePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Error("File path is required.");
        }

        try
        {
            var fullPath = System.IO.Path.GetFullPath(value);
            return ValidationResult.Success();
        }
        catch (Exception)
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
        {
            return ValidationResult.Error($"{fieldName} is required.");
        }

        if (value < min || value > max)
        {
            return ValidationResult.Error($"{fieldName} must be between {min} and {max}.");
        }

        return ValidationResult.Success();
    }

    #endregion

    #region UI Helpers

    /// <summary>
    /// Shows validation result in an InfoBar.
    /// </summary>
    public void ShowValidationResult(InfoBar infoBar, ValidationResult result)
    {
        if (result.IsValid)
        {
            infoBar.IsOpen = false;
            return;
        }

        infoBar.Severity = result.IsWarning ? InfoBarSeverity.Warning : InfoBarSeverity.Error;
        infoBar.Title = result.IsWarning ? "Warning" : "Validation Error";
        infoBar.Message = result.Message;
        infoBar.IsOpen = true;
    }

    /// <summary>
    /// Validates a TextBox and shows inline error styling.
    /// </summary>
    public ValidationResult ValidateTextBox(TextBox textBox, Func<string?, ValidationResult> validator, TextBlock? errorTextBlock = null)
    {
        var result = validator(textBox.Text);
        ApplyValidationStyle(textBox, result, errorTextBlock);
        return result;
    }

    /// <summary>
    /// Validates an AutoSuggestBox and shows inline error styling.
    /// </summary>
    public ValidationResult ValidateAutoSuggestBox(AutoSuggestBox suggestBox, Func<string?, ValidationResult> validator, TextBlock? errorTextBlock = null)
    {
        var result = validator(suggestBox.Text);

        // AutoSuggestBox doesn't have direct BorderBrush, but we can use a containing Border
        if (suggestBox.Parent is Border border)
        {
            ApplyBorderValidationStyle(border, result);
        }

        if (errorTextBlock != null)
        {
            ShowErrorText(errorTextBlock, result);
        }

        return result;
    }

    /// <summary>
    /// Applies validation styling to a TextBox.
    /// </summary>
    private void ApplyValidationStyle(TextBox textBox, ValidationResult result, TextBlock? errorTextBlock)
    {
        if (!result.IsValid && !result.IsWarning)
        {
            textBox.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 248, 81, 73)); // Error red
        }
        else if (result.IsWarning)
        {
            textBox.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 210, 153, 34)); // Warning orange
        }
        else
        {
            textBox.ClearValue(TextBox.BorderBrushProperty);
        }

        if (errorTextBlock != null)
        {
            ShowErrorText(errorTextBlock, result);
        }
    }

    /// <summary>
    /// Applies validation styling to a Border.
    /// </summary>
    private void ApplyBorderValidationStyle(Border border, ValidationResult result)
    {
        if (!result.IsValid && !result.IsWarning)
        {
            border.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 248, 81, 73));
            border.BorderThickness = new Thickness(2);
        }
        else if (result.IsWarning)
        {
            border.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 210, 153, 34));
            border.BorderThickness = new Thickness(2);
        }
        else
        {
            border.ClearValue(Border.BorderBrushProperty);
            border.BorderThickness = new Thickness(0);
        }
    }

    /// <summary>
    /// Shows error message in a TextBlock.
    /// </summary>
    private void ShowErrorText(TextBlock errorTextBlock, ValidationResult result)
    {
        if (!result.IsValid)
        {
            errorTextBlock.Text = result.Message;
            errorTextBlock.Foreground = result.IsWarning
                ? new SolidColorBrush(Color.FromArgb(255, 210, 153, 34))
                : new SolidColorBrush(Color.FromArgb(255, 248, 81, 73));
            errorTextBlock.Visibility = Visibility.Visible;
        }
        else
        {
            errorTextBlock.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Clears validation styling from a TextBox.
    /// </summary>
    public void ClearValidation(TextBox textBox, TextBlock? errorTextBlock = null)
    {
        textBox.ClearValue(TextBox.BorderBrushProperty);
        if (errorTextBlock != null)
        {
            errorTextBlock.Visibility = Visibility.Collapsed;
        }
    }

    #endregion
}

/// <summary>
/// Represents the result of a validation operation.
/// </summary>
public class ValidationResult
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
/// Extension methods for easier validation.
/// </summary>
public static class ValidationExtensions
{
    /// <summary>
    /// Validates multiple results and returns the first error or success.
    /// </summary>
    public static ValidationResult Combine(params ValidationResult[] results)
    {
        foreach (var result in results)
        {
            if (!result.IsValid && !result.IsWarning)
            {
                return result;
            }
        }

        // Return first warning if no errors
        foreach (var result in results)
        {
            if (result.IsWarning)
            {
                return result;
            }
        }

        return ValidationResult.Success();
    }
}
