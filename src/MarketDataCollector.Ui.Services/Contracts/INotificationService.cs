namespace MarketDataCollector.Ui.Services.Contracts;

public interface INotificationService
{
    void ShowInfo(string message, string? title = null);
    void ShowWarning(string message, string? title = null);
    void ShowError(string message, string? title = null);
    void ShowSuccess(string message, string? title = null);
    bool ShowConfirmation(string message, string? title = null);
}
