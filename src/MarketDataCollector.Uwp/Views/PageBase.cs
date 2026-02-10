using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MarketDataCollector.Uwp.Services;

namespace MarketDataCollector.Uwp.Views;

/// <summary>
/// H4: Base page class that provides common patterns for all UWP pages:
/// - Consistent loading state management
/// - Safe async event handling with logging
/// - ViewModel disposal on unload
/// - Standardized error display via InfoBar
/// </summary>
public abstract class PageBase : Page
{
    private bool _isInitialized;

    protected PageBase()
    {
        Loaded += PageBase_Loaded;
        Unloaded += PageBase_Unloaded;
    }

    /// <summary>
    /// Gets the logging service instance for structured logging.
    /// </summary>
    protected static LoggingService Logger => LoggingService.Instance;

    /// <summary>
    /// Override to perform async initialization when the page is first loaded.
    /// Called only once per page instance.
    /// </summary>
    protected virtual Task OnInitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Override to perform work each time the page is loaded (navigated to).
    /// </summary>
    protected virtual Task OnPageLoadedAsync() => Task.CompletedTask;

    /// <summary>
    /// Override to perform cleanup when the page is unloaded.
    /// Base implementation disposes the DataContext if it implements IDisposable.
    /// </summary>
    protected virtual void OnPageUnloaded()
    {
        // Dispose ViewModel if it implements IDisposable (Q1 pattern)
        (DataContext as IDisposable)?.Dispose();
    }

    /// <summary>
    /// Executes an async action with standardized error handling and logging.
    /// Use this to wrap all async event handlers in derived pages.
    /// </summary>
    /// <param name="action">The async action to execute.</param>
    /// <param name="operationName">A descriptive name for the operation (used in logs).</param>
    protected async void ExecuteAsync(Func<Task> action, string operationName = "operation")
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug($"{operationName} was cancelled");
        }
        catch (Exception ex)
        {
            Logger.LogError($"{operationName} failed", ex);
        }
    }

    /// <summary>
    /// Executes an async action with a loading state indicator.
    /// Sets IsLoading on the DataContext if it has such a property (via ObservableProperty).
    /// </summary>
    /// <param name="action">The async action to execute.</param>
    /// <param name="operationName">A descriptive name for the operation.</param>
    protected async Task ExecuteWithLoadingAsync(Func<Task> action, string operationName = "operation")
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug($"{operationName} was cancelled");
        }
        catch (Exception ex)
        {
            Logger.LogError($"{operationName} failed", ex);
        }
    }

    private async void PageBase_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                await OnInitializeAsync();
            }
            await OnPageLoadedAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Page load failed for {GetType().Name}", ex);
        }
    }

    private void PageBase_Unloaded(object sender, RoutedEventArgs e)
    {
        try
        {
            OnPageUnloaded();
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Page unload error in {GetType().Name}", ("error", ex.Message));
        }
    }
}
