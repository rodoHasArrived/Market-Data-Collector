using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;

namespace MarketDataCollector.Uwp.Contracts;

/// <summary>
/// Interface for managing InfoBar notifications with auto-dismiss behavior.
/// </summary>
public interface IInfoBarService
{
    Task ShowAsync(
        InfoBar infoBar,
        InfoBarSeverity severity,
        string title,
        string message,
        CancellationToken cancellationToken = default);

    Task ShowAsync(
        InfoBar infoBar,
        InfoBarSeverity severity,
        string title,
        string message,
        int durationMs,
        CancellationToken cancellationToken = default);

    Task ShowErrorAsync(
        InfoBar infoBar,
        string title,
        string message,
        string? context = null,
        string? remedy = null,
        CancellationToken cancellationToken = default);
}
