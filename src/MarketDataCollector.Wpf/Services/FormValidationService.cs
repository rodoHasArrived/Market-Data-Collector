using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Service for form validation with inline error display.
/// Provides consistent validation across all forms in the WPF application.
/// </summary>
public sealed class FormValidationService
{
    private static FormValidationService? _instance;
    private static readonly object _lock = new();

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

    public static ValidationResult ValidateRequired(string? value, string fieldName = "This field")
    {
        if (string.IsNullOrWhiteSpace(value))
            return ValidationResult.Error($"{fieldName} is required.");
        return ValidationResult.Success();
    }

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

    public static ValidationResult ValidateFilePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ValidationResult.Error("File path is required.");
        try
        {
            _ = System.IO.Path.GetFullPath(value);
            return ValidationResult.Success();
        }
        catch
        {
            return ValidationResult.Error("Invalid file path format.");
        }
    }

    public static ValidationResult ValidateNumericRange(double? value, double min, double max, string fieldName = "Value")
    {
        if (!value.HasValue)
            return ValidationResult.Error($"{fieldName} is required.");
        if (value < min || value > max)
            return ValidationResult.Error($"{fieldName} must be between {min} and {max}.");
        return ValidationResult.Success();
    }

    #endregion

    #region UI Helpers

    /// <summary>
    /// Shows validation result in a TextBlock status area.
    /// </summary>
    public void ShowValidationResult(TextBlock statusBlock, ValidationResult result)
    {
        if (result.IsValid && !result.IsWarning)
        {
            statusBlock.Visibility = Visibility.Collapsed;
            return;
        }

        statusBlock.Text = result.IsWarning ? $"Warning: {result.Message}" : $"Error: {result.Message}";
        statusBlock.Foreground = result.IsWarning
            ? new SolidColorBrush(Color.FromArgb(255, 210, 153, 34))
            : new SolidColorBrush(Color.FromArgb(255, 248, 81, 73));
        statusBlock.Visibility = Visibility.Visible;
    }

    public ValidationResult ValidateTextBox(TextBox textBox, Func<string?, ValidationResult> validator, TextBlock? errorTextBlock = null)
    {
        var result = validator(textBox.Text);
        ApplyValidationStyle(textBox, result, errorTextBlock);
        return result;
    }

    private void ApplyValidationStyle(TextBox textBox, ValidationResult result, TextBlock? errorTextBlock)
    {
        if (!result.IsValid && !result.IsWarning)
            textBox.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 248, 81, 73));
        else if (result.IsWarning)
            textBox.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 210, 153, 34));
        else
            textBox.ClearValue(TextBox.BorderBrushProperty);

        if (errorTextBlock != null)
            ShowErrorText(errorTextBlock, result);
    }

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

    public void ClearValidation(TextBox textBox, TextBlock? errorTextBlock = null)
    {
        textBox.ClearValue(TextBox.BorderBrushProperty);
        if (errorTextBlock != null)
            errorTextBlock.Visibility = Visibility.Collapsed;
    }

    #endregion
}

public class ValidationResult
{
    public bool IsValid { get; private init; }
    public bool IsWarning { get; private init; }
    public string Message { get; private init; } = string.Empty;

    private ValidationResult() { }

    public static ValidationResult Success() => new() { IsValid = true };
    public static ValidationResult Error(string message) => new() { IsValid = false, Message = message };
    public static ValidationResult Warning(string message) => new() { IsValid = true, IsWarning = true, Message = message };

    public static implicit operator bool(ValidationResult result) => result.IsValid;
}

public static class ValidationExtensions
{
    public static ValidationResult Combine(params ValidationResult[] results)
    {
        foreach (var result in results)
            if (!result.IsValid && !result.IsWarning)
                return result;

        foreach (var result in results)
            if (result.IsWarning)
                return result;

        return ValidationResult.Success();
    }
}
