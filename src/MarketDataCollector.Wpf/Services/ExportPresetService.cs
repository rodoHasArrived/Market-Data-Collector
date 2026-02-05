using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MarketDataCollector.Contracts.Export;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Service for managing export presets.
/// Implements Feature Refinement #69 - Archive Export Presets.
/// </summary>
public sealed class ExportPresetService
{
    private static ExportPresetService? _instance;
    private static readonly object _lock = new();

    private const string PresetsKey = "ExportPresets";
    private const string PresetsFileName = "export_presets.json";

    private readonly List<ExportPreset> _presets = new();
    private bool _initialized;

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

    private ExportPresetService()
    {
    }

    /// <summary>
    /// Event raised when presets are modified.
    /// </summary>
    public event EventHandler? PresetsChanged;

    /// <summary>
    /// Gets all available export presets.
    /// </summary>
    public IReadOnlyList<ExportPreset> Presets => _presets.AsReadOnly();

    /// <summary>
    /// Initializes the service and loads presets.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;

        await LoadPresetsAsync();
        _initialized = true;
    }

    /// <summary>
    /// Loads presets from storage.
    /// </summary>
    public async Task LoadPresetsAsync()
    {
        try
        {
            var localFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MarketDataCollector");
            Directory.CreateDirectory(localFolderPath);
            var filePath = Path.Combine(localFolderPath, PresetsFileName);

            if (File.Exists(filePath))
            {
                var json = await File.ReadAllTextAsync(filePath);
                var presets = JsonSerializer.Deserialize<List<ExportPreset>>(json);
                if (presets != null)
                {
                    _presets.Clear();
                    _presets.AddRange(presets);
                }
            }

            // Add built-in presets if none exist
            if (_presets.Count == 0)
            {
                _presets.AddRange(GetBuiltInPresets());
                await SavePresetsAsync();
            }

            // Ensure built-in presets are always present
            EnsureBuiltInPresets();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExportPresetService] Error loading presets: {ex.Message}");

            // Fallback to built-in presets
            if (_presets.Count == 0)
            {
                _presets.AddRange(GetBuiltInPresets());
            }
        }
    }

    /// <summary>
    /// Saves presets to storage.
    /// </summary>
    public async Task SavePresetsAsync()
    {
        try
        {
            var localFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MarketDataCollector");
            Directory.CreateDirectory(localFolderPath);
            var filePath = Path.Combine(localFolderPath, PresetsFileName);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(_presets, options);
            await File.WriteAllTextAsync(filePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExportPresetService] Error saving presets: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a new export preset.
    /// </summary>
    public async Task<ExportPreset> CreatePresetAsync(
        string name,
        string? description = null,
        ExportPresetFormat format = ExportPresetFormat.Parquet,
        string? destination = null)
    {
        var preset = new ExportPreset
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Description = description,
            Format = format,
            Destination = destination ?? GetDefaultDestination(format),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsBuiltIn = false
        };

        _presets.Add(preset);
        await SavePresetsAsync();
        PresetsChanged?.Invoke(this, EventArgs.Empty);

        return preset;
    }

    /// <summary>
    /// Updates an existing preset.
    /// </summary>
    public async Task<bool> UpdatePresetAsync(ExportPreset preset)
    {
        var index = _presets.FindIndex(p => p.Id == preset.Id);
        if (index == -1 || _presets[index].IsBuiltIn)
        {
            return false;
        }

        preset.UpdatedAt = DateTime.UtcNow;
        _presets[index] = preset;
        await SavePresetsAsync();
        PresetsChanged?.Invoke(this, EventArgs.Empty);

        return true;
    }

    /// <summary>
    /// Deletes a preset by ID.
    /// </summary>
    public async Task<bool> DeletePresetAsync(string presetId)
    {
        var preset = _presets.FirstOrDefault(p => p.Id == presetId);
        if (preset == null || preset.IsBuiltIn)
        {
            return false;
        }

        _presets.Remove(preset);
        await SavePresetsAsync();
        PresetsChanged?.Invoke(this, EventArgs.Empty);

        return true;
    }

    /// <summary>
    /// Gets a preset by ID.
    /// </summary>
    public ExportPreset? GetPreset(string presetId)
    {
        return _presets.FirstOrDefault(p => p.Id == presetId);
    }

    /// <summary>
    /// Gets a preset by name.
    /// </summary>
    public ExportPreset? GetPresetByName(string name)
    {
        return _presets.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Duplicates an existing preset with a new name.
    /// </summary>
    public async Task<ExportPreset> DuplicatePresetAsync(string presetId, string newName)
    {
        var source = _presets.FirstOrDefault(p => p.Id == presetId);
        if (source == null)
        {
            throw new ArgumentException($"Preset not found: {presetId}");
        }

        var duplicate = new ExportPreset
        {
            Id = Guid.NewGuid().ToString(),
            Name = newName,
            Description = source.Description,
            Format = source.Format,
            Compression = source.Compression,
            Destination = source.Destination,
            FilenamePattern = source.FilenamePattern,
            Filters = new ExportPresetFilters
            {
                EventTypes = source.Filters.EventTypes.ToArray(),
                Symbols = source.Filters.Symbols.ToArray(),
                DateRangeType = source.Filters.DateRangeType,
                CustomStartDate = source.Filters.CustomStartDate,
                CustomEndDate = source.Filters.CustomEndDate,
                SessionFilter = source.Filters.SessionFilter,
                MinQualityScore = source.Filters.MinQualityScore
            },
            Schedule = source.Schedule,
            ScheduleEnabled = false,
            PostExportHook = source.PostExportHook,
            NotifyOnComplete = source.NotifyOnComplete,
            IncludeDataDictionary = source.IncludeDataDictionary,
            IncludeLoaderScript = source.IncludeLoaderScript,
            OverwriteExisting = source.OverwriteExisting,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsBuiltIn = false
        };

        _presets.Add(duplicate);
        await SavePresetsAsync();
        PresetsChanged?.Invoke(this, EventArgs.Empty);

        return duplicate;
    }

    /// <summary>
    /// Records that a preset was used for an export.
    /// </summary>
    public async Task RecordPresetUsageAsync(string presetId)
    {
        var preset = _presets.FirstOrDefault(p => p.Id == presetId);
        if (preset != null)
        {
            preset.LastUsedAt = DateTime.UtcNow;
            preset.UseCount++;
            await SavePresetsAsync();
        }
    }

    /// <summary>
    /// Exports presets to a JSON file for sharing.
    /// </summary>
    public async Task<string> ExportPresetsAsync(string[] presetIds, string destinationPath)
    {
        var presetsToExport = _presets.Where(p => presetIds.Contains(p.Id)).ToList();

        // Remove IDs and reset usage stats for export
        foreach (var preset in presetsToExport)
        {
            preset.Id = Guid.NewGuid().ToString();
            preset.IsBuiltIn = false;
            preset.UseCount = 0;
            preset.LastUsedAt = null;
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(presetsToExport, options);
        var filePath = Path.Combine(destinationPath, $"export_presets_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        await File.WriteAllTextAsync(filePath, json);

        return filePath;
    }

    /// <summary>
    /// Imports presets from a JSON file.
    /// </summary>
    public async Task<int> ImportPresetsAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var importedPresets = JsonSerializer.Deserialize<List<ExportPreset>>(json);

        if (importedPresets == null || importedPresets.Count == 0)
        {
            return 0;
        }

        var importedCount = 0;
        foreach (var preset in importedPresets)
        {
            // Generate new ID to avoid conflicts
            preset.Id = Guid.NewGuid().ToString();
            preset.IsBuiltIn = false;
            preset.CreatedAt = DateTime.UtcNow;
            preset.UpdatedAt = DateTime.UtcNow;

            // Check for name conflicts
            var existingName = _presets.FirstOrDefault(p => p.Name == preset.Name);
            if (existingName != null)
            {
                preset.Name = $"{preset.Name} (Imported)";
            }

            _presets.Add(preset);
            importedCount++;
        }

        await SavePresetsAsync();
        PresetsChanged?.Invoke(this, EventArgs.Empty);

        return importedCount;
    }

    /// <summary>
    /// Expands path template variables.
    /// </summary>
    public static string ExpandPathTemplate(string template, string? symbol = null, DateTime? date = null)
    {
        var now = date ?? DateTime.UtcNow;
        return template
            .Replace("{year}", now.Year.ToString())
            .Replace("{month}", now.Month.ToString("D2"))
            .Replace("{day}", now.Day.ToString("D2"))
            .Replace("{date}", now.ToString("yyyy-MM-dd"))
            .Replace("{symbol}", symbol ?? "")
            .Replace("{format}", "")
            .TrimEnd('.');
    }

    /// <summary>
    /// Expands filename pattern variables.
    /// </summary>
    public static string ExpandFilenamePattern(
        string pattern,
        string symbol,
        string eventType,
        DateTime date,
        ExportPresetFormat format)
    {
        var formatExt = format switch
        {
            ExportPresetFormat.Parquet => "parquet",
            ExportPresetFormat.Csv => "csv",
            ExportPresetFormat.Jsonl => "jsonl",
            ExportPresetFormat.Lean => "zip",
            ExportPresetFormat.Xlsx => "xlsx",
            ExportPresetFormat.Sql => "sql",
            ExportPresetFormat.Raw => "jsonl.gz",
            _ => "data"
        };

        return pattern
            .Replace("{symbol}", symbol)
            .Replace("{type}", eventType)
            .Replace("{date}", date.ToString("yyyy-MM-dd"))
            .Replace("{format}", formatExt);
    }

    /// <summary>
    /// Gets the date range for a preset's filter settings.
    /// </summary>
    public static (DateTime Start, DateTime End) GetDateRange(ExportPresetFilters filters)
    {
        var now = DateTime.UtcNow.Date;

        return filters.DateRangeType switch
        {
            DateRangeType.Today => (now, now),
            DateRangeType.Yesterday => (now.AddDays(-1), now.AddDays(-1)),
            DateRangeType.LastWeek => (now.AddDays(-7), now),
            DateRangeType.LastMonth => (now.AddMonths(-1), now),
            DateRangeType.LastQuarter => (now.AddDays(-90), now),
            DateRangeType.LastYear => (now.AddYears(-1), now),
            DateRangeType.All => (DateTime.MinValue, now),
            DateRangeType.Custom => (
                filters.CustomStartDate ?? now.AddDays(-7),
                filters.CustomEndDate ?? now
            ),
            _ => (now.AddDays(-7), now)
        };
    }

    private void EnsureBuiltInPresets()
    {
        var builtInIds = new[] { "python-pandas", "r-stats", "quantconnect-lean", "excel", "postgresql" };

        foreach (var builtIn in GetBuiltInPresets())
        {
            if (!_presets.Any(p => p.Id == builtIn.Id))
            {
                _presets.Insert(0, builtIn);
            }
        }
    }

    private static string GetDefaultDestination(ExportPresetFormat format)
    {
        var basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "MarketDataCollector",
            "Exports");

        return format switch
        {
            ExportPresetFormat.Lean => Path.Combine(basePath, "Lean", "{symbol}"),
            ExportPresetFormat.Xlsx => Path.Combine(basePath, "Excel"),
            ExportPresetFormat.Sql => Path.Combine(basePath, "SQL"),
            _ => Path.Combine(basePath, "{year}", "{month}")
        };
    }

    private static List<ExportPreset> GetBuiltInPresets()
    {
        return new List<ExportPreset>
        {
            new ExportPreset
            {
                Id = "python-pandas",
                Name = "Python/Pandas",
                Description = "Parquet format optimized for pandas.read_parquet(). Includes loader script and data dictionary.",
                Format = ExportPresetFormat.Parquet,
                Compression = ExportPresetCompression.Snappy,
                Destination = Path.Combine("{year}", "{month}"),
                FilenamePattern = "{symbol}_{date}.parquet",
                Filters = new ExportPresetFilters
                {
                    EventTypes = new[] { "Trade", "BboQuote" },
                    DateRangeType = DateRangeType.LastWeek
                },
                IncludeLoaderScript = true,
                IncludeDataDictionary = true,
                IsBuiltIn = true,
                CreatedAt = DateTime.UtcNow
            },
            new ExportPreset
            {
                Id = "r-stats",
                Name = "R Statistics",
                Description = "CSV format with proper NA handling and ISO date formats for R data.table or tidyverse.",
                Format = ExportPresetFormat.Csv,
                Compression = ExportPresetCompression.None,
                Destination = Path.Combine("{year}", "{month}"),
                FilenamePattern = "{symbol}_{type}_{date}.csv",
                Filters = new ExportPresetFilters
                {
                    EventTypes = new[] { "Trade", "BboQuote" },
                    DateRangeType = DateRangeType.LastWeek
                },
                IncludeLoaderScript = true,
                IncludeDataDictionary = true,
                IsBuiltIn = true,
                CreatedAt = DateTime.UtcNow
            },
            new ExportPreset
            {
                Id = "quantconnect-lean",
                Name = "QuantConnect Lean",
                Description = "Native Lean data format with zip packaging for backtesting.",
                Format = ExportPresetFormat.Lean,
                Compression = ExportPresetCompression.Zip,
                Destination = Path.Combine("equity", "usa", "daily"),
                FilenamePattern = "{symbol}.zip",
                Filters = new ExportPresetFilters
                {
                    EventTypes = new[] { "HistoricalBar" },
                    DateRangeType = DateRangeType.All
                },
                IncludeLoaderScript = false,
                IncludeDataDictionary = false,
                IsBuiltIn = true,
                CreatedAt = DateTime.UtcNow
            },
            new ExportPreset
            {
                Id = "excel",
                Name = "Microsoft Excel",
                Description = "XLSX format with multiple sheets, optimized for Excel analysis.",
                Format = ExportPresetFormat.Xlsx,
                Compression = ExportPresetCompression.None,
                Destination = "{year}",
                FilenamePattern = "MarketData_{date}.xlsx",
                Filters = new ExportPresetFilters
                {
                    EventTypes = new[] { "Trade", "BboQuote" },
                    DateRangeType = DateRangeType.Yesterday
                },
                IncludeLoaderScript = false,
                IncludeDataDictionary = true,
                IsBuiltIn = true,
                CreatedAt = DateTime.UtcNow
            },
            new ExportPreset
            {
                Id = "postgresql",
                Name = "PostgreSQL/TimescaleDB",
                Description = "CSV with COPY command for fast database import. Includes DDL scripts.",
                Format = ExportPresetFormat.Csv,
                Compression = ExportPresetCompression.None,
                Destination = "database_import",
                FilenamePattern = "{symbol}_{type}_{date}.csv",
                Filters = new ExportPresetFilters
                {
                    EventTypes = new[] { "Trade", "BboQuote" },
                    DateRangeType = DateRangeType.LastWeek
                },
                IncludeLoaderScript = true,
                IncludeDataDictionary = true,
                IsBuiltIn = true,
                CreatedAt = DateTime.UtcNow
            }
        };
    }
}
