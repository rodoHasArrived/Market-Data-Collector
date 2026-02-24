using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using MarketDataCollector.Application.Logging;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using Serilog;

namespace MarketDataCollector.Storage.Export;

/// <summary>
/// Service for exporting collected market data in analysis-ready formats.
/// Split into partial classes: Formats (CSV/Parquet/JSONL/Lean/SQL/XLSX), IO (documentation/loaders/utilities).
/// </summary>
public sealed partial class AnalysisExportService
{
    private readonly ILogger _log = LoggingSetup.ForContext<AnalysisExportService>();
    private readonly string _dataRoot;
    private readonly Dictionary<string, ExportProfile> _profiles;

    public AnalysisExportService(string dataRoot)
    {
        _dataRoot = dataRoot;
        _profiles = ExportProfile.GetBuiltInProfiles()
            .ToDictionary(p => p.Id, p => p);
    }

    /// <summary>
    /// Get all available export profiles.
    /// </summary>
    public IReadOnlyList<ExportProfile> GetProfiles() => _profiles.Values.ToList();

    /// <summary>
    /// Get a specific profile by ID.
    /// </summary>
    public ExportProfile? GetProfile(string profileId) =>
        _profiles.TryGetValue(profileId, out var profile) ? profile : null;

    /// <summary>
    /// Register a custom export profile.
    /// </summary>
    public void RegisterProfile(ExportProfile profile)
    {
        _profiles[profile.Id] = profile;
        _log.Information("Registered export profile: {ProfileId} ({Name})", profile.Id, profile.Name);
    }

    /// <summary>
    /// Export data according to the request.
    /// </summary>
    public async Task<ExportResult> ExportAsync(ExportRequest request, CancellationToken ct = default)
    {
        var profile = request.CustomProfile ?? GetProfile(request.ProfileId);
        if (profile is null)
            return ExportResult.CreateFailure(request.ProfileId, $"Unknown profile: {request.ProfileId}");

        var result = ExportResult.CreateSuccess(profile.Id, request.OutputDirectory);

        try
        {
            _log.Information("Starting export with profile {ProfileId} to {OutputDir}",
                profile.Id, request.OutputDirectory);

            // Ensure output directory exists
            Directory.CreateDirectory(request.OutputDirectory);

            // Find source files to export
            var sourceFiles = FindSourceFiles(request);
            if (sourceFiles.Count is 0)
            {
                result.Warnings = [.. result.Warnings, "No source data found for the specified criteria"];
                result.CompletedAt = DateTime.UtcNow;
                return result;
            }

            _log.Information("Found {FileCount} source files to export", sourceFiles.Count);

            // Export based on format
            var exportedFiles = new List<ExportedFile>();

            switch (profile.Format)
            {
                case ExportFormat.Csv:
                    exportedFiles = await ExportToCsvAsync(sourceFiles, request, profile, ct);
                    break;
                case ExportFormat.Parquet:
                    exportedFiles = await ExportToParquetAsync(sourceFiles, request, profile, ct);
                    break;
                case ExportFormat.Jsonl:
                    exportedFiles = await ExportToJsonlAsync(sourceFiles, request, profile, ct);
                    break;
                case ExportFormat.Lean:
                    exportedFiles = await ExportToLeanAsync(sourceFiles, request, profile, ct);
                    break;
                case ExportFormat.Sql:
                    exportedFiles = await ExportToSqlAsync(sourceFiles, request, profile, ct);
                    break;
                case ExportFormat.Xlsx:
                    exportedFiles = await ExportToXlsxAsync(sourceFiles, request, profile, ct);
                    break;
                case ExportFormat.Arrow:
                    exportedFiles = await ExportToArrowAsync(sourceFiles, request, profile, ct);
                    break;
                default:
                    throw new NotSupportedException($"Format {profile.Format} is not supported");
            }

            result.Files = exportedFiles.ToArray();
            result.FilesGenerated = exportedFiles.Count;
            result.TotalRecords = exportedFiles.Sum(f => f.RecordCount);
            result.TotalBytes = exportedFiles.Sum(f => f.SizeBytes);
            result.Symbols = exportedFiles
                .Where(f => f.Symbol != null)
                .Select(f => f.Symbol!)
                .Distinct()
                .ToArray();

            result.DateRange = new ExportDateRange
            {
                Start = request.StartDate,
                End = request.EndDate,
                TradingDays = CountTradingDays(request.StartDate, request.EndDate)
            };

            // Generate supporting files
            if (profile.IncludeDataDictionary)
            {
                var dictPath = await GenerateDataDictionaryAsync(
                    request.OutputDirectory, request.EventTypes, profile, ct);
                result.DataDictionaryPath = dictPath;
            }

            if (profile.IncludeLoaderScript)
            {
                var scriptPath = await GenerateLoaderScriptAsync(
                    request.OutputDirectory, profile, exportedFiles, ct);
                result.LoaderScriptPath = scriptPath;
            }

            result.CompletedAt = DateTime.UtcNow;
            result.Success = true;

            // Generate lineage manifest for data provenance and reproducibility
            try
            {
                var manifestPath = await GenerateLineageManifestAsync(
                    request.OutputDirectory, request, result, ct);
                result.LineageManifestPath = manifestPath;
            }
            catch (Exception manifestEx)
            {
                _log.Warning(manifestEx, "Failed to generate lineage manifest (non-fatal)");
                result.Warnings = [.. result.Warnings, $"Lineage manifest generation failed: {manifestEx.Message}"];
            }

            _log.Information("Export completed: {FileCount} files, {RecordCount:N0} records, {Bytes:N0} bytes",
                result.FilesGenerated, result.TotalRecords, result.TotalBytes);

            return result;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Export failed for profile {ProfileId}", profile.Id);
            result.Success = false;
            result.Error = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
            return result;
        }
    }

    private List<SourceFile> FindSourceFiles(ExportRequest request)
    {
        if (!Directory.Exists(_dataRoot)) return new List<SourceFile>();

        return new[] { "*.jsonl", "*.jsonl.gz" }
            .SelectMany(pattern => Directory.GetFiles(_dataRoot, pattern, SearchOption.AllDirectories))
            .Select(ParseFileName)
            .Where(f => f is not null)
            .Select(f => f!)
            .Where(f => request.Symbols is not { Length: > 0 } ||
                        request.Symbols.Contains(f.Symbol, StringComparer.OrdinalIgnoreCase))
            .Where(f => request.EventTypes is not { Length: > 0 } ||
                        request.EventTypes.Contains(f.EventType, StringComparer.OrdinalIgnoreCase))
            .Where(f => !f.Date.HasValue ||
                        (f.Date.Value >= request.StartDate.Date && f.Date.Value <= request.EndDate.Date))
            .OrderBy(f => f.Symbol)
            .ThenBy(f => f.Date)
            .ToList();
    }

    private SourceFile? ParseFileName(string path)
    {
        var fileName = Path.GetFileName(path);
        var parts = fileName.Split('.');

        if (parts.Length < 2) return null;

        // Handle patterns like: AAPL.Trade.jsonl, SPY.BboQuote.2026-01-03.jsonl.gz
        var result = new SourceFile
        {
            Path = path,
            IsCompressed = fileName.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
        };

        // Try to extract symbol and event type
        result.Symbol = parts[0];

        if (parts.Length >= 3)
        {
            result.EventType = parts[1];

            // Check if there's a date component
            if (parts.Length >= 4 && DateTime.TryParse(parts[2], out var date))
            {
                result.Date = date;
            }
        }

        return result;
    }

    private static int CountTradingDays(DateTime start, DateTime end)
    {
        var count = 0;
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
        {
            if (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                count++;
        }
        return count;
    }

    /// <summary>
    /// Groups source files by symbol if requested, otherwise returns a single group.
    /// </summary>
    private static IEnumerable<IGrouping<string?, SourceFile>> GroupBySymbolIfRequired(
        List<SourceFile> files, bool splitBySymbol) =>
        splitBySymbol
            ? files.GroupBy(f => f.Symbol)
            : files.GroupBy(_ => (string?)"combined");

    private class SourceFile
    {
        public string Path { get; set; } = string.Empty;
        public string? Symbol { get; set; }
        public string? EventType { get; set; }
        public DateTime? Date { get; set; }
        public bool IsCompressed { get; set; }
    }
}
