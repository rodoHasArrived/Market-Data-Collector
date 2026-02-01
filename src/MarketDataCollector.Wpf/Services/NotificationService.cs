using System.Windows;

namespace MarketDataCollector.Wpf.Services;

public sealed class NotificationService : INotificationService
{
    private readonly ILoggingService _logger;

    public NotificationService(ILoggingService logger)
    {
        _logger = logger;
    }

    public void ShowInfo(string message, string? title = null)
    {
        _logger.Log($"INFO: {message}");
        MessageBox.Show(message, title ?? "Information", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowWarning(string message, string? title = null)
    {
        _logger.Log($"WARNING: {message}");
        MessageBox.Show(message, title ?? "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public void ShowError(string message, string? title = null)
    {
        _logger.Log($"ERROR: {message}");
        MessageBox.Show(message, title ?? "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public void ShowSuccess(string message, string? title = null)
    {
        _logger.Log($"SUCCESS: {message}");
        MessageBox.Show(message, title ?? "Success", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public bool ShowConfirmation(string message, string? title = null)
    {
        _logger.Log($"CONFIRMATION: {message}");
        var result = MessageBox.Show(message, title ?? "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }
}
