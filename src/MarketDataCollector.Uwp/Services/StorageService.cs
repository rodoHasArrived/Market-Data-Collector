using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Contracts.Api;
using MarketDataCollector.Ui.Services;
using MarketDataCollector.Uwp.Views;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for managing data storage, file operations, and storage statistics.
/// Provides access to storage paths, file listings, and space usage.
/// Inherits platform-agnostic API methods from <see cref="StorageServiceBase"/>.
/// </summary>
public sealed class StorageService : StorageServiceBase, IStorageService
{
    private static StorageService? _instance;
    private static readonly object _lock = new();

    public static StorageService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new StorageService();
                }
            }
            return _instance;
        }
    }

    private StorageService() { }

    /// <summary>
    /// Gets list of data files for a symbol.
    /// </summary>
    public async Task<List<DataFileInfo>> GetSymbolFilesAsync(string symbol, CancellationToken ct = default)
    {
        var response = await ApiClientService.Instance.GetAsync<List<SymbolFileDto>>(
            UiApiRoutes.WithParam(UiApiRoutes.StorageSymbolFiles, "symbol", symbol), ct);

        if (response == null) return new List<DataFileInfo>();

        var files = new List<DataFileInfo>();
        foreach (var dto in response)
        {
            files.Add(new DataFileInfo
            {
                FileName = dto.FileName,
                FileType = dto.DataType,
                FileSize = FormatBytes(dto.SizeBytes),
                ModifiedDate = dto.ModifiedAt.ToString("yyyy-MM-dd HH:mm"),
                EventCount = dto.RecordCount.ToString("N0"),
                FileIcon = GetFileIcon(dto.DataType),
                TypeBackground = GetTypeBackground(dto.DataType)
            });
        }

        return files;
    }

    private static SolidColorBrush GetTypeBackground(string dataType)
    {
        return dataType.ToLowerInvariant() switch
        {
            "trades" => new SolidColorBrush(Windows.UI.Color.FromArgb(30, 63, 185, 80)),    // Green
            "quotes" => new SolidColorBrush(Windows.UI.Color.FromArgb(30, 88, 166, 255)),   // Blue
            "depth" => new SolidColorBrush(Windows.UI.Color.FromArgb(30, 210, 153, 34)),    // Orange
            "bars" => new SolidColorBrush(Windows.UI.Color.FromArgb(30, 163, 113, 247)),    // Purple
            _ => new SolidColorBrush(Windows.UI.Color.FromArgb(30, 139, 148, 158))          // Gray
        };
    }
}
