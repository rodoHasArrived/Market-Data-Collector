using MarketDataCollector.Ui.Services.Services;
using Microsoft.UI.Xaml.Controls;

namespace MarketDataCollector.Uwp.Contracts;

/// <summary>
/// Interface for form validation with inline error display.
/// </summary>
public interface IFormValidationService
{
    void ShowValidationResult(InfoBar infoBar, ValidationResult result);
    ValidationResult ValidateTextBox(TextBox textBox, Func<string?, ValidationResult> validator, TextBlock? errorTextBlock = null);
    ValidationResult ValidateAutoSuggestBox(AutoSuggestBox suggestBox, Func<string?, ValidationResult> validator, TextBlock? errorTextBlock = null);
    void ClearValidation(TextBox textBox, TextBlock? errorTextBlock = null);
}
