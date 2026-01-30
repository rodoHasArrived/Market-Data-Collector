namespace MarketDataCollector.Contracts.Pipeline;

/// <summary>
/// Constants for pipeline policy configuration.
/// Shared between the main application and UWP to ensure consistent channel settings.
/// </summary>
/// <remarks>
/// These constants are used by <c>EventPipelinePolicy</c> in the main application
/// and directly by UWP services that cannot reference the main assembly due to
/// WinRT metadata constraints.
/// <para>
/// When modifying these values, ensure both usages remain synchronized:
/// <list type="bullet">
///   <item>Main: <c>src/MarketDataCollector/Application/Pipeline/EventPipelinePolicy.cs</c></item>
///   <item>UWP: <c>src/MarketDataCollector.Uwp/Services/LoggingService.cs</c></item>
/// </list>
/// </para>
/// </remarks>
public static class PipelinePolicyConstants
{
    /// <summary>
    /// Channel full mode: Wait (backpressure - blocks writers when full).
    /// Maps to <c>BoundedChannelFullMode.Wait</c>.
    /// </summary>
    public const int FullModeWait = 0;

    /// <summary>
    /// Channel full mode: DropNewest (drop incoming items when full).
    /// Maps to <c>BoundedChannelFullMode.DropNewest</c>.
    /// </summary>
    public const int FullModeDropNewest = 1;

    /// <summary>
    /// Channel full mode: DropOldest (drop oldest items when full).
    /// Maps to <c>BoundedChannelFullMode.DropOldest</c>.
    /// </summary>
    public const int FullModeDropOldest = 2;

    /// <summary>
    /// Channel full mode: DropWrite (drop write attempt when full).
    /// Maps to <c>BoundedChannelFullMode.DropWrite</c>.
    /// </summary>
    public const int FullModeDropWrite = 3;

    #region Preset: Default

    /// <summary>
    /// Default preset capacity for general-purpose event pipelines.
    /// </summary>
    public const int DefaultCapacity = 100_000;

    /// <summary>
    /// Default preset full mode (DropOldest).
    /// </summary>
    public const int DefaultFullMode = FullModeDropOldest;

    #endregion

    #region Preset: HighThroughput

    /// <summary>
    /// HighThroughput preset capacity for streaming data pipelines.
    /// </summary>
    public const int HighThroughputCapacity = 50_000;

    /// <summary>
    /// HighThroughput preset full mode (DropOldest).
    /// </summary>
    public const int HighThroughputFullMode = FullModeDropOldest;

    #endregion

    #region Preset: MessageBuffer

    /// <summary>
    /// MessageBuffer preset capacity for internal message buffering.
    /// </summary>
    public const int MessageBufferCapacity = 50_000;

    /// <summary>
    /// MessageBuffer preset full mode (DropOldest).
    /// </summary>
    public const int MessageBufferFullMode = FullModeDropOldest;

    #endregion

    #region Preset: MaintenanceQueue

    /// <summary>
    /// MaintenanceQueue preset capacity for background tasks.
    /// </summary>
    public const int MaintenanceQueueCapacity = 100;

    /// <summary>
    /// MaintenanceQueue preset full mode (Wait/backpressure).
    /// </summary>
    public const int MaintenanceQueueFullMode = FullModeWait;

    #endregion

    #region Preset: Logging

    /// <summary>
    /// Logging preset capacity for log channels.
    /// </summary>
    public const int LoggingCapacity = 1_000;

    /// <summary>
    /// Logging preset full mode (DropOldest).
    /// </summary>
    public const int LoggingFullMode = FullModeDropOldest;

    #endregion

    #region Preset: CompletionQueue

    /// <summary>
    /// CompletionQueue preset capacity for completion notifications.
    /// </summary>
    public const int CompletionQueueCapacity = 500;

    /// <summary>
    /// CompletionQueue preset full mode (Wait/backpressure - no drops).
    /// </summary>
    public const int CompletionQueueFullMode = FullModeWait;

    #endregion
}
