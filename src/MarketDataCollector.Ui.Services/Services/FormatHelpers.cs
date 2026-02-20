namespace MarketDataCollector.Ui.Services;

/// <summary>
/// Shared formatting utilities for UI services and views.
/// </summary>
public static class FormatHelpers
{
    private static readonly string[] ByteSizes = { "B", "KB", "MB", "GB", "TB" };

    /// <summary>
    /// Formats a byte count into a human-readable string (e.g., "1.5 GB").
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < ByteSizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:F1} {ByteSizes[order]}";
    }
}
