using System;
using MarketDataCollector.Ui.Services;
using MarketDataCollector.Uwp.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// UWP/WinUI-specific form validation service with inline error display.
/// Validation rules are shared via <see cref="FormValidationRules"/>;
/// this class adds WinUI UI helpers only.
/// </summary>
public sealed class FormValidationService : IFormValidationService
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

    #region Validation Rules (delegates to shared FormValidationRules)

    public static ValidationResult ValidateRequired(string? value, string fieldName = "This field")
        => FormValidationRules.ValidateRequired(value, fieldName);

    public static ValidationResult ValidateSymbol(string? value)
        => FormValidationRules.ValidateSymbol(value);

    public static ValidationResult ValidateSymbolList(string? value, int maxSymbols = 100)
        => FormValidationRules.ValidateSymbolList(value, maxSymbols);

    public static ValidationResult ValidateDateRange(DateTimeOffset? fromDate, DateTimeOffset? toDate)
        => FormValidationRules.ValidateDateRange(fromDate, toDate);

    public static ValidationResult ValidateApiKey(string? value)
        => FormValidationRules.ValidateApiKey(value);

    public static ValidationResult ValidateUrl(string? value, string fieldName = "URL")
        => FormValidationRules.ValidateUrl(value, fieldName);

    public static ValidationResult ValidatePort(int? value)
        => FormValidationRules.ValidatePort(value);

    public static ValidationResult ValidateFilePath(string? value)
        => FormValidationRules.ValidateFilePath(value);

    public static ValidationResult ValidateNumericRange(double? value, double min, double max, string fieldName = "Value")
        => FormValidationRules.ValidateNumericRange(value, min, max, fieldName);

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

    private void ApplyValidationStyle(TextBox textBox, ValidationResult result, TextBlock? errorTextBlock)
    {
        if (!result.IsValid && !result.IsWarning)
        {
            textBox.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 248, 81, 73));
        }
        else if (result.IsWarning)
        {
            textBox.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 210, 153, 34));
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
