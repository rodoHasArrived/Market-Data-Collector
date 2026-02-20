using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Ui.Services.Services;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// WPF implementation of export preset service.
/// Uses file-based storage in LocalApplicationData.
/// All business logic is in <see cref="ExportPresetServiceBase"/>.
/// </summary>
public sealed class ExportPresetService : ExportPresetServiceBase
{
    private static readonly Lazy<ExportPresetService> _instance = new(() => new ExportPresetService());
    public static ExportPresetService Instance => _instance.Value;

    private ExportPresetService() { }

    private static string GetPresetsFilePath()
    {
        var localFolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MarketDataCollector");
        Directory.CreateDirectory(localFolderPath);
        return Path.Combine(localFolderPath, PresetsFileName);
    }

    protected override async Task<string?> ReadPresetsJsonAsync(CancellationToken cancellationToken)
    {
        var filePath = GetPresetsFilePath();
        if (File.Exists(filePath))
            return await File.ReadAllTextAsync(filePath, cancellationToken);
        return null;
    }

    protected override async Task WritePresetsJsonAsync(string json, CancellationToken cancellationToken)
    {
        var filePath = GetPresetsFilePath();
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    protected override void LogError(string message, Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"[ExportPresetService] {message}: {ex.Message}");
    }
}
