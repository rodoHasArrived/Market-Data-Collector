using System;
using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Ui.Services.Services;
using Windows.Storage;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// UWP implementation of export preset service.
/// Uses ApplicationData.Current.LocalFolder for storage.
/// All business logic is in <see cref="ExportPresetServiceBase"/>.
/// </summary>
public sealed class ExportPresetService : ExportPresetServiceBase, IExportPresetService
{
    private static ExportPresetService? _instance;
    private static readonly object _lock = new();

    public static ExportPresetService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new ExportPresetService();
                }
            }
            return _instance;
        }
    }

    private ExportPresetService() { }

    protected override async Task<string?> ReadPresetsJsonAsync(CancellationToken cancellationToken)
    {
        var localFolder = ApplicationData.Current.LocalFolder;
        var file = await localFolder.TryGetItemAsync(PresetsFileName) as StorageFile;

        if (file != null)
            return await FileIO.ReadTextAsync(file);

        return null;
    }

    protected override async Task WritePresetsJsonAsync(string json, CancellationToken cancellationToken)
    {
        var localFolder = ApplicationData.Current.LocalFolder;
        var file = await localFolder.CreateFileAsync(PresetsFileName, CreationCollisionOption.ReplaceExisting);
        await FileIO.WriteTextAsync(file, json);
    }

    protected override void LogError(string message, Exception ex)
    {
        LoggingService.Instance.LogError(message, ex);
    }
}
