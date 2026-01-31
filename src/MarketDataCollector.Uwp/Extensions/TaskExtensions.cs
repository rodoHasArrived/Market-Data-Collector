using System.Diagnostics;

namespace MarketDataCollector.Uwp.Extensions;

/// <summary>
/// Extension methods for <see cref="Task"/> to handle async void scenarios safely.
///
/// The `async void` pattern is unavoidable in event handlers (Button_Click, Page_Loaded, etc.)
/// but has dangerous semantics - unhandled exceptions crash the app.
///
/// This extension provides a safe wrapper that:
/// 1. Captures exceptions instead of letting them crash the app
/// 2. Logs errors for debugging
/// 3. Optionally invokes a callback for custom error handling
///
/// Example usage:
/// <code>
/// // Before (dangerous - unhandled exceptions crash app)
/// private async void Button_Click(object sender, RoutedEventArgs e)
/// {
///     await DoSomethingAsync(); // Exception here crashes app
/// }
///
/// // After (safe - exceptions are caught and logged)
/// private void Button_Click(object sender, RoutedEventArgs e)
/// {
///     DoSomethingAsync().SafeFireAndForget(
///         onException: ex => LoggingService.Instance.LogError("Button click failed", ex));
/// }
/// </code>
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Safely executes a task without awaiting, catching and logging any exceptions.
    /// Use this instead of `async void` event handlers to prevent app crashes.
    /// </summary>
    /// <param name="task">The task to execute.</param>
    /// <param name="onException">Optional callback when an exception occurs.</param>
    /// <param name="continueOnCapturedContext">Whether to continue on the captured context. Default false.</param>
    public static async void SafeFireAndForget(
        this Task task,
        Action<Exception>? onException = null,
        bool continueOnCapturedContext = false)
    {
        try
        {
            await task.ConfigureAwait(continueOnCapturedContext);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected behavior, not an error
            Debug.WriteLine("Task was cancelled (expected)");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SafeFireAndForget caught exception: {ex}");

            onException?.Invoke(ex);

            // Also log to our logging service if available
            try
            {
                Services.LoggingService.Instance.LogError("Unhandled async exception", ex);
            }
            catch
            {
                // Ignore logging failures to prevent infinite loops
            }
        }
    }

    /// <summary>
    /// Safely executes a task without awaiting, catching and logging any exceptions.
    /// Generic version for Task{T}.
    /// </summary>
    /// <typeparam name="T">The result type of the task.</typeparam>
    /// <param name="task">The task to execute.</param>
    /// <param name="onException">Optional callback when an exception occurs.</param>
    /// <param name="continueOnCapturedContext">Whether to continue on the captured context. Default false.</param>
    public static async void SafeFireAndForget<T>(
        this Task<T> task,
        Action<Exception>? onException = null,
        bool continueOnCapturedContext = false)
    {
        try
        {
            await task.ConfigureAwait(continueOnCapturedContext);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected behavior, not an error
            Debug.WriteLine("Task was cancelled (expected)");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SafeFireAndForget caught exception: {ex}");

            onException?.Invoke(ex);

            try
            {
                Services.LoggingService.Instance.LogError("Unhandled async exception", ex);
            }
            catch
            {
                // Ignore logging failures
            }
        }
    }

    /// <summary>
    /// Safely executes a ValueTask without awaiting, catching and logging any exceptions.
    /// </summary>
    /// <param name="task">The ValueTask to execute.</param>
    /// <param name="onException">Optional callback when an exception occurs.</param>
    /// <param name="continueOnCapturedContext">Whether to continue on the captured context. Default false.</param>
    public static async void SafeFireAndForget(
        this ValueTask task,
        Action<Exception>? onException = null,
        bool continueOnCapturedContext = false)
    {
        try
        {
            await task.ConfigureAwait(continueOnCapturedContext);
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("ValueTask was cancelled (expected)");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SafeFireAndForget caught exception: {ex}");

            onException?.Invoke(ex);

            try
            {
                Services.LoggingService.Instance.LogError("Unhandled async exception", ex);
            }
            catch
            {
                // Ignore logging failures
            }
        }
    }

    /// <summary>
    /// Safely executes a ValueTask{T} without awaiting, catching and logging any exceptions.
    /// </summary>
    /// <typeparam name="T">The result type of the task.</typeparam>
    /// <param name="task">The ValueTask to execute.</param>
    /// <param name="onException">Optional callback when an exception occurs.</param>
    /// <param name="continueOnCapturedContext">Whether to continue on the captured context. Default false.</param>
    public static async void SafeFireAndForget<T>(
        this ValueTask<T> task,
        Action<Exception>? onException = null,
        bool continueOnCapturedContext = false)
    {
        try
        {
            await task.ConfigureAwait(continueOnCapturedContext);
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("ValueTask was cancelled (expected)");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SafeFireAndForget caught exception: {ex}");

            onException?.Invoke(ex);

            try
            {
                Services.LoggingService.Instance.LogError("Unhandled async exception", ex);
            }
            catch
            {
                // Ignore logging failures
            }
        }
    }
}
