using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Contracts.Export;

namespace MarketDataCollector.Uwp.Contracts;

/// <summary>
/// Interface for managing export presets.
/// </summary>
public interface IExportPresetService
{
    IReadOnlyList<ExportPreset> Presets { get; }
    event EventHandler? PresetsChanged;

    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task LoadPresetsAsync(CancellationToken cancellationToken = default);
    Task SavePresetsAsync(CancellationToken cancellationToken = default);
    Task<ExportPreset> CreatePresetAsync(string name, string? description = null, ExportPresetFormat format = ExportPresetFormat.Parquet, string? destination = null, CancellationToken cancellationToken = default);
    Task<bool> UpdatePresetAsync(ExportPreset preset, CancellationToken cancellationToken = default);
    Task<bool> DeletePresetAsync(string presetId, CancellationToken cancellationToken = default);
    ExportPreset? GetPreset(string presetId);
    ExportPreset? GetPresetByName(string name);
    Task<ExportPreset> DuplicatePresetAsync(string presetId, string newName, CancellationToken cancellationToken = default);
    Task RecordPresetUsageAsync(string presetId, CancellationToken cancellationToken = default);
    Task<string> ExportPresetsAsync(string[] presetIds, string destinationPath, CancellationToken cancellationToken = default);
    Task<int> ImportPresetsAsync(string filePath, CancellationToken cancellationToken = default);
}
