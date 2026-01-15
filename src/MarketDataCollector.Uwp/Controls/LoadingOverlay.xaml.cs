using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MarketDataCollector.Uwp.Controls;

/// <summary>
/// A reusable loading overlay control that provides consistent loading state UI.
/// Supports indeterminate and determinate progress, cancel functionality, and accessibility.
/// </summary>
public sealed partial class LoadingOverlay : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(
            nameof(IsLoading),
            typeof(bool),
            typeof(LoadingOverlay),
            new PropertyMetadata(false, OnIsLoadingChanged));

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(
            nameof(Message),
            typeof(string),
            typeof(LoadingOverlay),
            new PropertyMetadata("Loading..."));

    public static readonly DependencyProperty SubMessageProperty =
        DependencyProperty.Register(
            nameof(SubMessage),
            typeof(string),
            typeof(LoadingOverlay),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(
            nameof(Progress),
            typeof(double),
            typeof(LoadingOverlay),
            new PropertyMetadata(0.0, OnProgressChanged));

    public static readonly DependencyProperty ShowProgressProperty =
        DependencyProperty.Register(
            nameof(ShowProgress),
            typeof(bool),
            typeof(LoadingOverlay),
            new PropertyMetadata(false));

    public static readonly DependencyProperty CanCancelProperty =
        DependencyProperty.Register(
            nameof(CanCancel),
            typeof(bool),
            typeof(LoadingOverlay),
            new PropertyMetadata(false));

    public static readonly DependencyProperty TimeRemainingProperty =
        DependencyProperty.Register(
            nameof(TimeRemaining),
            typeof(TimeSpan?),
            typeof(LoadingOverlay),
            new PropertyMetadata(null));

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets whether the loading overlay is visible.
    /// </summary>
    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    /// <summary>
    /// Gets or sets the main loading message.
    /// </summary>
    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>
    /// Gets or sets the sub-message (additional details).
    /// </summary>
    public string SubMessage
    {
        get => (string)GetValue(SubMessageProperty);
        set => SetValue(SubMessageProperty, value);
    }

    /// <summary>
    /// Gets or sets the progress value (0-100).
    /// </summary>
    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show the progress bar.
    /// </summary>
    public bool ShowProgress
    {
        get => (bool)GetValue(ShowProgressProperty);
        set => SetValue(ShowProgressProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the operation can be cancelled.
    /// </summary>
    public bool CanCancel
    {
        get => (bool)GetValue(CanCancelProperty);
        set => SetValue(CanCancelProperty, value);
    }

    /// <summary>
    /// Gets or sets the estimated time remaining.
    /// </summary>
    public TimeSpan? TimeRemaining
    {
        get => (TimeSpan?)GetValue(TimeRemainingProperty);
        set => SetValue(TimeRemainingProperty, value);
    }

    /// <summary>
    /// Gets whether there is a sub-message to display.
    /// </summary>
    public bool HasSubMessage => !string.IsNullOrEmpty(SubMessage);

    /// <summary>
    /// Gets the formatted progress text.
    /// </summary>
    public string ProgressText => $"{Progress:F0}%";

    /// <summary>
    /// Gets whether there is time remaining to display.
    /// </summary>
    public bool HasTimeRemaining => TimeRemaining.HasValue;

    /// <summary>
    /// Gets the formatted time remaining text.
    /// </summary>
    public string TimeRemainingText
    {
        get
        {
            if (!TimeRemaining.HasValue) return string.Empty;

            var time = TimeRemaining.Value;
            if (time.TotalHours >= 1)
                return $"~{time.Hours}h {time.Minutes}m remaining";
            if (time.TotalMinutes >= 1)
                return $"~{time.Minutes}m {time.Seconds}s remaining";
            return $"~{time.Seconds}s remaining";
        }
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when the cancel button is clicked.
    /// </summary>
    public event EventHandler? CancelRequested;

    #endregion

    public LoadingOverlay()
    {
        this.InitializeComponent();
    }

    private static void OnIsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoadingOverlay overlay && e.NewValue is bool isLoading)
        {
            // Announce state change for screen readers
            if (isLoading)
            {
                Helpers.AccessibilityHelper.Announce(
                    overlay,
                    overlay.Message,
                    Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationProcessing.ImportantMostRecent);
            }
        }
    }

    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoadingOverlay overlay)
        {
            // Notify property changes for bindings
            overlay.Bindings.Update();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    #region Helper Methods

    /// <summary>
    /// Shows the loading overlay with a message.
    /// </summary>
    public void Show(string message, bool canCancel = false)
    {
        Message = message;
        CanCancel = canCancel;
        ShowProgress = false;
        IsLoading = true;
    }

    /// <summary>
    /// Shows the loading overlay with progress bar.
    /// </summary>
    public void ShowWithProgress(string message, double progress = 0, bool canCancel = true)
    {
        Message = message;
        Progress = progress;
        CanCancel = canCancel;
        ShowProgress = true;
        IsLoading = true;
    }

    /// <summary>
    /// Updates the progress.
    /// </summary>
    public void UpdateProgress(double progress, string? subMessage = null, TimeSpan? timeRemaining = null)
    {
        Progress = progress;
        if (subMessage != null)
            SubMessage = subMessage;
        if (timeRemaining.HasValue)
            TimeRemaining = timeRemaining;

        Bindings.Update();
    }

    /// <summary>
    /// Hides the loading overlay.
    /// </summary>
    public void Hide()
    {
        IsLoading = false;
        Progress = 0;
        SubMessage = string.Empty;
        TimeRemaining = null;
    }

    #endregion
}
