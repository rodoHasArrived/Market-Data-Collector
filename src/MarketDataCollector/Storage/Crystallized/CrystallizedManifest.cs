using System.Text.Json;
using System.Text.Json.Serialization;

namespace MarketDataCollector.Storage.Crystallized;

/// <summary>
/// Manifest file for a symbol directory, describing available data.
/// Stored as _manifest.json in each symbol folder.
/// </summary>
public sealed record SymbolManifest
{
    /// <summary>
    /// Schema version for manifest file format.
    /// </summary>
    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// Trading symbol (e.g., "AAPL", "SPY").
    /// </summary>
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = "";

    /// <summary>
    /// Data provider/source (e.g., "alpaca", "polygon").
    /// </summary>
    [JsonPropertyName("provider")]
    public string Provider { get; init; } = "";

    /// <summary>
    /// Human-readable description of the symbol.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Asset class (equity, crypto, futures, options, forex).
    /// </summary>
    [JsonPropertyName("asset_class")]
    public string AssetClass { get; init; } = "equity";

    /// <summary>
    /// Primary exchange for the symbol.
    /// </summary>
    [JsonPropertyName("exchange")]
    public string? Exchange { get; init; }

    /// <summary>
    /// Currency for price data.
    /// </summary>
    [JsonPropertyName("currency")]
    public string Currency { get; init; } = "USD";

    /// <summary>
    /// Available data categories with their metadata.
    /// </summary>
    [JsonPropertyName("categories")]
    public Dictionary<string, CategoryMetadata> Categories { get; init; } = new();

    /// <summary>
    /// Earliest date with data across all categories.
    /// </summary>
    [JsonPropertyName("earliest_date")]
    public DateOnly? EarliestDate { get; init; }

    /// <summary>
    /// Latest date with data across all categories.
    /// </summary>
    [JsonPropertyName("latest_date")]
    public DateOnly? LatestDate { get; init; }

    /// <summary>
    /// Total number of data files.
    /// </summary>
    [JsonPropertyName("total_files")]
    public int TotalFiles { get; init; }

    /// <summary>
    /// Total storage size in bytes.
    /// </summary>
    [JsonPropertyName("total_bytes")]
    public long TotalBytes { get; init; }

    /// <summary>
    /// When this manifest was last updated.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Additional metadata (provider-specific or user-defined).
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Metadata for a data category within a symbol.
/// </summary>
public sealed class CategoryMetadata
{
    /// <summary>
    /// Display name for the category.
    /// </summary>
    [JsonPropertyName("display_name")]
    public string DisplayName { get; init; } = "";

    /// <summary>
    /// Description of what this data contains.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Available time granularities (for bars and orderflow).
    /// </summary>
    [JsonPropertyName("granularities")]
    public List<string>? Granularities { get; init; }

    /// <summary>
    /// Earliest date with data in this category.
    /// </summary>
    [JsonPropertyName("earliest_date")]
    public DateOnly? EarliestDate { get; init; }

    /// <summary>
    /// Latest date with data in this category.
    /// </summary>
    [JsonPropertyName("latest_date")]
    public DateOnly? LatestDate { get; init; }

    /// <summary>
    /// Number of files in this category.
    /// </summary>
    [JsonPropertyName("file_count")]
    public int FileCount { get; init; }

    /// <summary>
    /// Total size in bytes for this category.
    /// </summary>
    [JsonPropertyName("total_bytes")]
    public long TotalBytes { get; init; }

    /// <summary>
    /// Approximate row/record count.
    /// </summary>
    [JsonPropertyName("row_count")]
    public long? RowCount { get; init; }

    /// <summary>
    /// CSV column headers for this category.
    /// </summary>
    [JsonPropertyName("columns")]
    public string[]? Columns { get; init; }
}

/// <summary>
/// Root catalog file listing all available symbols and providers.
/// Stored as _catalog.json in the root data directory.
/// </summary>
public sealed class DataCatalog
{
    /// <summary>
    /// Schema version for catalog file format.
    /// </summary>
    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// Human-readable title for this data collection.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; init; } = "Market Data Collection";

    /// <summary>
    /// Description of this data collection.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// List of data providers in this collection.
    /// </summary>
    [JsonPropertyName("providers")]
    public List<ProviderSummary> Providers { get; init; } = new();

    /// <summary>
    /// Index of all symbols across all providers.
    /// </summary>
    [JsonPropertyName("symbols")]
    public List<SymbolSummary> Symbols { get; init; } = new();

    /// <summary>
    /// Overall date range for the entire collection.
    /// </summary>
    [JsonPropertyName("date_range")]
    public DateRangeSummary? DateRange { get; init; }

    /// <summary>
    /// Total storage statistics.
    /// </summary>
    [JsonPropertyName("storage")]
    public StorageSummary? Storage { get; init; }

    /// <summary>
    /// When this catalog was last updated.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Data format information.
    /// </summary>
    [JsonPropertyName("format")]
    public FormatInfo Format { get; init; } = new();
}

/// <summary>
/// Summary information about a data provider.
/// </summary>
public sealed class ProviderSummary
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("symbol_count")]
    public int SymbolCount { get; init; }

    [JsonPropertyName("categories")]
    public List<string> Categories { get; init; } = new();
}

/// <summary>
/// Summary information about a symbol.
/// </summary>
public sealed class SymbolSummary
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = "";

    [JsonPropertyName("provider")]
    public string Provider { get; init; } = "";

    [JsonPropertyName("asset_class")]
    public string AssetClass { get; init; } = "equity";

    [JsonPropertyName("categories")]
    public List<string> Categories { get; init; } = new();

    [JsonPropertyName("earliest_date")]
    public DateOnly? EarliestDate { get; init; }

    [JsonPropertyName("latest_date")]
    public DateOnly? LatestDate { get; init; }

    [JsonPropertyName("manifest_path")]
    public string? ManifestPath { get; init; }
}

/// <summary>
/// Date range summary.
/// </summary>
public sealed class DateRangeSummary
{
    [JsonPropertyName("earliest")]
    public DateOnly Earliest { get; init; }

    [JsonPropertyName("latest")]
    public DateOnly Latest { get; init; }

    [JsonPropertyName("trading_days")]
    public int? TradingDays { get; init; }
}

/// <summary>
/// Storage statistics summary.
/// </summary>
public sealed class StorageSummary
{
    [JsonPropertyName("total_files")]
    public int TotalFiles { get; init; }

    [JsonPropertyName("total_bytes")]
    public long TotalBytes { get; init; }

    [JsonPropertyName("total_bytes_human")]
    public string? TotalBytesHuman { get; init; }
}

/// <summary>
/// Information about the storage format.
/// </summary>
public sealed class FormatInfo
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0";

    [JsonPropertyName("file_format")]
    public string FileFormat { get; init; } = "jsonl";

    [JsonPropertyName("compression")]
    public string? Compression { get; init; }

    [JsonPropertyName("self_documenting_names")]
    public bool SelfDocumentingNames { get; init; } = true;

    [JsonPropertyName("documentation_url")]
    public string? DocumentationUrl { get; init; }
}

/// <summary>
/// Service for reading and writing manifest files.
/// </summary>
public sealed class ManifestService
{
    private readonly CrystallizedStorageFormat _format;
    private readonly JsonSerializerOptions _jsonOptions;

    public ManifestService(CrystallizedStorageFormat format)
    {
        _format = format ?? throw new ArgumentNullException(nameof(format));
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Reads a symbol manifest from disk.
    /// </summary>
    public async Task<SymbolManifest?> ReadSymbolManifestAsync(
        string provider,
        string symbol,
        CancellationToken ct = default)
    {
        var path = _format.GetSymbolManifestPath(provider, symbol);

        if (!File.Exists(path))
            return null;

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<SymbolManifest>(stream, _jsonOptions, ct);
    }

    /// <summary>
    /// Writes a symbol manifest to disk.
    /// </summary>
    public async Task WriteSymbolManifestAsync(
        SymbolManifest manifest,
        CancellationToken ct = default)
    {
        var path = _format.GetSymbolManifestPath(manifest.Provider, manifest.Symbol);
        var dir = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // Write atomically
        var tempPath = path + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, manifest, _jsonOptions, ct);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    /// <summary>
    /// Reads the root catalog from disk.
    /// </summary>
    public async Task<DataCatalog?> ReadCatalogAsync(CancellationToken ct = default)
    {
        var path = _format.GetCatalogPath();

        if (!File.Exists(path))
            return null;

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<DataCatalog>(stream, _jsonOptions, ct);
    }

    /// <summary>
    /// Writes the root catalog to disk.
    /// </summary>
    public async Task WriteCatalogAsync(DataCatalog catalog, CancellationToken ct = default)
    {
        var path = _format.GetCatalogPath();
        var dir = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // Write atomically
        var tempPath = path + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, catalog, _jsonOptions, ct);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    /// <summary>
    /// Scans a symbol directory and generates/updates its manifest.
    /// </summary>
    public async Task<SymbolManifest> ScanAndUpdateManifestAsync(
        string provider,
        string symbol,
        CancellationToken ct = default)
    {
        var existing = await ReadSymbolManifestAsync(provider, symbol, ct);

        var manifest = new SymbolManifest
        {
            Symbol = symbol,
            Provider = provider,
            Description = existing?.Description,
            AssetClass = existing?.AssetClass ?? "equity",
            Exchange = existing?.Exchange,
            Currency = existing?.Currency ?? "USD",
            Categories = await ScanCategoriesAsync(provider, symbol, ct),
            UpdatedAt = DateTimeOffset.UtcNow,
            Metadata = existing?.Metadata
        };

        // Compute aggregate statistics
        var allDates = manifest.Categories.Values
            .Where(c => c.EarliestDate.HasValue)
            .Select(c => c.EarliestDate!.Value)
            .ToList();

        var latestDates = manifest.Categories.Values
            .Where(c => c.LatestDate.HasValue)
            .Select(c => c.LatestDate!.Value)
            .ToList();

        var updatedManifest = manifest with
        {
            EarliestDate = allDates.Count > 0 ? allDates.Min() : null,
            LatestDate = latestDates.Count > 0 ? latestDates.Max() : null,
            TotalFiles = manifest.Categories.Values.Sum(c => c.FileCount),
            TotalBytes = manifest.Categories.Values.Sum(c => c.TotalBytes)
        };

        await WriteSymbolManifestAsync(updatedManifest, ct);
        return updatedManifest;
    }

    private async Task<Dictionary<string, CategoryMetadata>> ScanCategoriesAsync(
        string provider,
        string symbol,
        CancellationToken ct)
    {
        var result = new Dictionary<string, CategoryMetadata>();

        foreach (var category in Enum.GetValues<CrystallizedDataCategory>())
        {
            if (category == CrystallizedDataCategory.System)
                continue;

            var metadata = await ScanCategoryAsync(provider, symbol, category, ct);
            if (metadata != null && metadata.FileCount > 0)
            {
                result[category.ToFolderName()] = metadata;
            }
        }

        return result;
    }

    private Task<CategoryMetadata?> ScanCategoryAsync(
        string provider,
        string symbol,
        CrystallizedDataCategory category,
        CancellationToken ct)
    {
        // Get the symbol directory from the manifest path
        var manifestPath = _format.GetSymbolManifestPath(provider, symbol);
        var symbolDir = Path.GetDirectoryName(manifestPath);

        if (string.IsNullOrEmpty(symbolDir))
            return Task.FromResult<CategoryMetadata?>(null);

        var categoryDir = Path.Combine(symbolDir, category.ToFolderName());

        if (!Directory.Exists(categoryDir))
            return Task.FromResult<CategoryMetadata?>(null);

        // Supported file extensions for market data files
        var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jsonl", ".csv",
            ".gz", ".gzip",
            ".zst", ".zstd",
            ".lz4", ".br"
        };

        var files = new List<FileInfo>();
        var granularities = new List<string>();

        try
        {
            // Enumerate all files recursively
            foreach (var filePath in Directory.EnumerateFiles(categoryDir, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();

                var ext = Path.GetExtension(filePath);
                if (!supportedExtensions.Contains(ext))
                    continue;

                // Check for compound extensions like .jsonl.gz
                var fileName = Path.GetFileName(filePath);
                if (!fileName.Contains(".jsonl") && !fileName.Contains(".csv"))
                    continue;

                files.Add(new FileInfo(filePath));
            }

            // For bars and orderflow categories, detect available granularities from subdirectories
            if (category == CrystallizedDataCategory.Bars || category == CrystallizedDataCategory.OrderFlow)
            {
                foreach (var subDir in Directory.EnumerateDirectories(categoryDir))
                {
                    var dirName = Path.GetFileName(subDir);
                    if (!string.IsNullOrEmpty(dirName) && !dirName.StartsWith("_"))
                    {
                        granularities.Add(dirName);
                    }
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directories we can't access
            return Task.FromResult<CategoryMetadata?>(null);
        }
        catch (DirectoryNotFoundException)
        {
            return Task.FromResult<CategoryMetadata?>(null);
        }

        if (files.Count == 0)
            return Task.FromResult<CategoryMetadata?>(null);

        // Compute file statistics
        var totalBytes = files.Sum(f => f.Length);
        var fileCount = files.Count;

        // Extract dates from file names to determine date range
        var dates = new List<DateOnly>();
        foreach (var file in files)
        {
            var parsed = CrystallizedStorageFormat.ParseFileName(file.Name);
            if (parsed != null && TryParseDatePartition(parsed.DatePartition, out var date))
            {
                dates.Add(date);
            }
        }

        DateOnly? earliestDate = dates.Count > 0 ? dates.Min() : null;
        DateOnly? latestDate = dates.Count > 0 ? dates.Max() : null;

        var metadata = new CategoryMetadata
        {
            DisplayName = category.ToDisplayName(),
            Description = category.GetDescription(),
            Granularities = granularities.Count > 0 ? granularities.OrderBy(g => g).ToList() : null,
            EarliestDate = earliestDate,
            LatestDate = latestDate,
            FileCount = fileCount,
            TotalBytes = totalBytes,
            Columns = category.GetCsvHeaders()
        };

        return Task.FromResult<CategoryMetadata?>(metadata);
    }

    /// <summary>
    /// Attempts to parse a date partition string into a DateOnly.
    /// Supports formats: yyyy-MM-dd, yyyy-MM, yyyy
    /// </summary>
    private static bool TryParseDatePartition(string partition, out DateOnly date)
    {
        date = default;

        if (string.IsNullOrEmpty(partition))
            return false;

        // Try full date format: yyyy-MM-dd
        if (DateOnly.TryParseExact(partition, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out date))
            return true;

        // Try month format: yyyy-MM (use first day of month)
        if (partition.Length == 7 && partition[4] == '-')
        {
            if (int.TryParse(partition.AsSpan(0, 4), out var year) &&
                int.TryParse(partition.AsSpan(5, 2), out var month) &&
                year >= 1 && year <= 9999 && month >= 1 && month <= 12)
            {
                date = new DateOnly(year, month, 1);
                return true;
            }
        }

        // Try year format: yyyy (use first day of year)
        if (partition.Length == 4 && int.TryParse(partition, out var yearOnly) && yearOnly >= 1 && yearOnly <= 9999)
        {
            date = new DateOnly(yearOnly, 1, 1);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Formats a byte count as a human-readable string.
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < suffixes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {suffixes[order]}";
    }
}
