using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Serialization;
using MarketDataCollector.Infrastructure.Contracts;
using MarketDataCollector.Storage.Archival;
using Serilog;

namespace MarketDataCollector.Storage.Packaging;

/// <summary>
/// Service for creating and importing portable data packages.
/// Packages include all data, metadata, quality reports, and scripts needed
/// for data portability and sharing.
/// </summary>
[ImplementsAdr("ADR-002", "Portable packaging for tiered storage export")]
public sealed class PortableDataPackager
{
    private readonly ILogger _log = LoggingSetup.ForContext<PortableDataPackager>();
    private readonly string _dataRoot;
    private readonly CompressionProfileManager? _compressionManager;

    private const string ManifestFileName = "manifest.json";
    private const string DataDirectory = "data";
    private const string MetadataDirectory = "metadata";
    private const string ScriptsDirectory = "scripts";
    private const string QualityReportFileName = "quality_report.json";
    private const string DataDictionaryFileName = "data_dictionary.md";
    private const string ReadmeFileName = "README.md";

    /// <summary>
    /// Event raised to report packaging progress.
    /// </summary>
    public event EventHandler<PackageProgress>? ProgressChanged;

    public PortableDataPackager(string dataRoot, CompressionProfileManager? compressionManager = null)
    {
        _dataRoot = dataRoot;
        _compressionManager = compressionManager;
    }

    /// <summary>
    /// Create a portable data package.
    /// </summary>
    public async Task<PackageResult> CreatePackageAsync(
        PackageOptions options,
        CancellationToken ct = default)
    {
        var result = new PackageResult { StartedAt = DateTime.UtcNow };
        var warnings = new List<string>();

        try
        {
            _log.Information("Starting package creation: {PackageName}", options.Name);
            ReportProgress(result.JobId, PackageStage.Initializing, 0, 0, 0, 0);

            // Ensure output directory exists
            Directory.CreateDirectory(options.OutputDirectory);

            // Scan for source files
            ReportProgress(result.JobId, PackageStage.Scanning, 0, 0, 0, 0);
            var sourceFiles = await ScanSourceFilesAsync(options, ct);

            if (sourceFiles.Count == 0)
            {
                return PackageResult.CreateFailure("No data files found matching the specified criteria");
            }

            _log.Information("Found {FileCount} files to package", sourceFiles.Count);

            // Build manifest
            ReportProgress(result.JobId, PackageStage.GeneratingManifest, 0, sourceFiles.Count, 0, 0);
            var manifest = await BuildManifestAsync(sourceFiles, options, ct);

            // Determine output file name
            var packageFileName = GetPackageFileName(options);
            var packagePath = Path.Combine(options.OutputDirectory, packageFileName);

            // Create the package
            ReportProgress(result.JobId, PackageStage.Writing, 0, sourceFiles.Count, 0, manifest.UncompressedSizeBytes);

            await CreatePackageFileAsync(packagePath, sourceFiles, manifest, options, ct);

            // Compute package checksum
            ReportProgress(result.JobId, PackageStage.ComputingChecksums, sourceFiles.Count, sourceFiles.Count, 0, 0);
            var packageChecksum = await ComputeFileChecksumAsync(packagePath, ct);
            var packageInfo = new FileInfo(packagePath);

            manifest.PackageChecksum = packageChecksum;
            manifest.PackageSizeBytes = packageInfo.Length;

            // Update the manifest inside the package with final checksum
            await UpdateManifestInPackageAsync(packagePath, manifest, options.Format, ct);

            // Build result
            result = PackageResult.CreateSuccess(packagePath, manifest);
            result.FilesIncluded = sourceFiles.Count;
            result.TotalEvents = manifest.TotalEvents;
            result.Symbols = manifest.Symbols;
            result.EventTypes = manifest.EventTypes;
            result.DateRange = manifest.DateRange;
            result.PackageSizeBytes = packageInfo.Length;
            result.UncompressedSizeBytes = manifest.UncompressedSizeBytes;
            result.PackageChecksum = packageChecksum;
            result.Warnings = warnings.ToArray();

            ReportProgress(result.JobId, PackageStage.Complete, sourceFiles.Count, sourceFiles.Count,
                manifest.PackageSizeBytes, manifest.UncompressedSizeBytes);

            _log.Information(
                "Package created successfully: {PackagePath} ({SizeBytes:N0} bytes, {CompressionRatio:F2}x compression)",
                packagePath, packageInfo.Length, result.CompressionRatio);

            return result;
        }
        catch (OperationCanceledException)
        {
            _log.Warning("Package creation cancelled");
            return PackageResult.CreateFailure("Operation cancelled");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Package creation failed");
            ReportProgress(result.JobId, PackageStage.Failed, 0, 0, 0, 0);
            return PackageResult.CreateFailure(ex.Message);
        }
    }

    /// <summary>
    /// Import/extract a portable data package.
    /// </summary>
    public async Task<ImportResult> ImportPackageAsync(
        string packagePath,
        string destinationDirectory,
        bool validateChecksums = true,
        bool mergeWithExisting = false,
        CancellationToken ct = default)
    {
        var result = new ImportResult
        {
            StartedAt = DateTime.UtcNow,
            SourcePath = packagePath,
            DestinationPath = destinationDirectory
        };

        var warnings = new List<string>();
        var validationErrors = new List<ValidationError>();

        try
        {
            _log.Information("Starting package import: {PackagePath}", packagePath);
            ReportProgress(result.JobId, PackageStage.Initializing, 0, 0, 0, 0);

            if (!File.Exists(packagePath))
            {
                return ImportResult.CreateFailure(packagePath, $"Package file not found: {packagePath}");
            }

            // Determine package format
            var format = DetectPackageFormat(packagePath);

            // Extract and read manifest first
            ReportProgress(result.JobId, PackageStage.Scanning, 0, 0, 0, 0);
            var manifest = await ReadManifestFromPackageAsync(packagePath, format, ct);

            if (manifest == null)
            {
                return ImportResult.CreateFailure(packagePath, "Package does not contain a valid manifest");
            }

            result.PackageId = manifest.PackageId;
            result.Manifest = manifest;

            // Create destination directory
            Directory.CreateDirectory(destinationDirectory);

            // Extract files
            ReportProgress(result.JobId, PackageStage.Processing, 0, manifest.TotalFiles, 0, manifest.UncompressedSizeBytes);

            var extractionResult = await ExtractPackageAsync(
                packagePath, destinationDirectory, manifest, format, validateChecksums, ct);

            result.FilesExtracted = extractionResult.FilesExtracted;
            result.BytesExtracted = extractionResult.BytesExtracted;
            result.FilesValidated = extractionResult.FilesValidated;
            result.ValidationFailures = extractionResult.ValidationFailures;
            validationErrors.AddRange(extractionResult.ValidationErrors);

            if (extractionResult.ValidationFailures > 0 && validateChecksums)
            {
                warnings.Add($"{extractionResult.ValidationFailures} files failed checksum validation");
            }

            result.Symbols = manifest.Symbols;
            result.EventTypes = manifest.EventTypes;
            result.DateRange = manifest.DateRange;
            result.Warnings = warnings.ToArray();
            result.ValidationErrors = validationErrors.ToArray();
            result.Success = extractionResult.ValidationFailures == 0 || !validateChecksums;
            result.CompletedAt = DateTime.UtcNow;

            if (!result.Success)
            {
                result.Error = "Some files failed checksum validation";
            }

            ReportProgress(result.JobId, PackageStage.Complete,
                result.FilesExtracted, manifest.TotalFiles, result.BytesExtracted, manifest.UncompressedSizeBytes);

            _log.Information(
                "Package imported: {FilesExtracted} files, {BytesExtracted:N0} bytes, {ValidationFailures} validation failures",
                result.FilesExtracted, result.BytesExtracted, result.ValidationFailures);

            return result;
        }
        catch (OperationCanceledException)
        {
            _log.Warning("Package import cancelled");
            return ImportResult.CreateFailure(packagePath, "Operation cancelled");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Package import failed");
            ReportProgress(result.JobId, PackageStage.Failed, 0, 0, 0, 0);
            return ImportResult.CreateFailure(packagePath, ex.Message);
        }
    }

    /// <summary>
    /// Validate a package without extracting.
    /// </summary>
    public async Task<PackageValidationResult> ValidatePackageAsync(
        string packagePath,
        CancellationToken ct = default)
    {
        try
        {
            _log.Information("Validating package: {PackagePath}", packagePath);

            if (!File.Exists(packagePath))
            {
                return new PackageValidationResult
                {
                    IsValid = false,
                    Error = $"Package file not found: {packagePath}"
                };
            }

            var format = DetectPackageFormat(packagePath);
            var manifest = await ReadManifestFromPackageAsync(packagePath, format, ct);

            if (manifest == null)
            {
                return new PackageValidationResult
                {
                    IsValid = false,
                    Error = "Package does not contain a valid manifest"
                };
            }

            var issues = new List<string>();

            // Validate manifest version
            if (string.IsNullOrEmpty(manifest.PackageVersion))
            {
                issues.Add("Missing package version in manifest");
            }

            // Validate required fields
            if (string.IsNullOrEmpty(manifest.PackageId))
            {
                issues.Add("Missing package ID in manifest");
            }

            if (manifest.Files == null || manifest.Files.Length == 0)
            {
                issues.Add("No files listed in manifest");
            }

            // Verify files exist in package
            var missingFiles = await VerifyFilesInPackageAsync(packagePath, manifest, format, ct);
            if (missingFiles.Count > 0)
            {
                issues.Add($"{missingFiles.Count} files listed in manifest are missing from package");
            }

            return new PackageValidationResult
            {
                IsValid = issues.Count == 0,
                Manifest = manifest,
                Issues = issues.ToArray(),
                MissingFiles = missingFiles.ToArray()
            };
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Package validation failed");
            return new PackageValidationResult
            {
                IsValid = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// List contents of a package without extracting.
    /// </summary>
    public async Task<PackageContents> ListPackageContentsAsync(
        string packagePath,
        CancellationToken ct = default)
    {
        var format = DetectPackageFormat(packagePath);
        var manifest = await ReadManifestFromPackageAsync(packagePath, format, ct);

        if (manifest == null)
        {
            throw new InvalidOperationException("Package does not contain a valid manifest");
        }

        return new PackageContents
        {
            PackageId = manifest.PackageId,
            Name = manifest.Name,
            Description = manifest.Description,
            CreatedAt = manifest.CreatedAt,
            TotalFiles = manifest.TotalFiles,
            TotalEvents = manifest.TotalEvents,
            PackageSizeBytes = manifest.PackageSizeBytes,
            UncompressedSizeBytes = manifest.UncompressedSizeBytes,
            Symbols = manifest.Symbols,
            EventTypes = manifest.EventTypes,
            DateRange = manifest.DateRange,
            Files = manifest.Files,
            Quality = manifest.Quality
        };
    }

    private Task<List<SourceFileInfo>> ScanSourceFilesAsync(PackageOptions options, CancellationToken ct)
    {
        var files = new List<SourceFileInfo>();

        if (!Directory.Exists(_dataRoot))
        {
            return Task.FromResult(files);
        }

        var patterns = new[] { "*.jsonl", "*.jsonl.gz", "*.jsonl.zst", "*.parquet", "*.csv" };

        foreach (var pattern in patterns)
        {
            foreach (var filePath in Directory.EnumerateFiles(_dataRoot, pattern, SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();

                var fileInfo = ParseSourceFile(filePath);
                if (fileInfo == null) continue;

                // Apply filters
                if (!MatchesFilters(fileInfo, options)) continue;

                files.Add(fileInfo);
            }
        }

        return Task.FromResult(files.OrderBy(f => f.Symbol).ThenBy(f => f.Date).ToList());
    }

    private SourceFileInfo? ParseSourceFile(string path)
    {
        var fileName = Path.GetFileName(path);
        var relativePath = Path.GetRelativePath(_dataRoot, path);

        var info = new SourceFileInfo
        {
            FullPath = path,
            RelativePath = relativePath,
            FileName = fileName,
            SizeBytes = new FileInfo(path).Length
        };

        // Determine compression
        if (fileName.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            info.IsCompressed = true;
            info.CompressionType = "gzip";
        }
        else if (fileName.EndsWith(".zst", StringComparison.OrdinalIgnoreCase))
        {
            info.IsCompressed = true;
            info.CompressionType = "zstd";
        }

        // Parse path components to extract metadata
        var pathParts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Try to extract symbol, event type, date from path/filename
        ExtractMetadataFromPath(info, pathParts, fileName);

        return info;
    }

    private void ExtractMetadataFromPath(SourceFileInfo info, string[] pathParts, string fileName)
    {
        // First, check path parts for symbol and event type (prioritize directory structure)
        // Common patterns: SYMBOL/EventType/date.jsonl or Provider/SYMBOL/EventType/date.jsonl
        for (var i = 0; i < pathParts.Length - 1; i++) // Exclude filename itself
        {
            var part = pathParts[i];

            // Skip common provider/root directories
            if (i == 0 && (part.Equals("live", StringComparison.OrdinalIgnoreCase) ||
                           part.Equals("historical", StringComparison.OrdinalIgnoreCase) ||
                           part.Equals("data", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            // Check for known event types in path
            if (IsKnownEventType(part))
            {
                info.EventType ??= part;
                continue;
            }

            // If we haven't found symbol yet and this isn't a date, it's likely the symbol
            if (info.Symbol == null && !DateTime.TryParse(part, out _))
            {
                info.Symbol = part.ToUpperInvariant();
            }
        }

        // Remove extensions to get base name
        var baseName = fileName;
        foreach (var ext in new[] { ".gz", ".zst", ".jsonl", ".parquet", ".csv" })
        {
            if (baseName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                baseName = baseName[..^ext.Length];
            }
        }

        // Try different naming conventions from filename if we still don't have metadata
        // Pattern: SYMBOL.EventType.Date or SYMBOL_EventType_Date
        var parts = baseName.Split('.', '_');

        if (parts.Length >= 1 && info.Symbol == null)
        {
            info.Symbol = parts[0].ToUpperInvariant();
        }

        if (parts.Length >= 2 && info.EventType == null)
        {
            info.EventType = parts[1];
        }

        // Try to parse date from filename parts
        foreach (var part in parts)
        {
            if (DateTime.TryParse(part, out var date))
            {
                info.Date = date;
                break;
            }

            // Try yyyy-MM-dd format
            if (part.Length == 10 && DateTime.TryParseExact(part, "yyyy-MM-dd", null,
                    System.Globalization.DateTimeStyles.None, out date))
            {
                info.Date = date;
                break;
            }
        }

        // Also check path parts for date if not found yet
        if (info.Date == null)
        {
            foreach (var pathPart in pathParts)
            {
                if (DateTime.TryParse(pathPart, out var date))
                {
                    info.Date = date;
                    break;
                }
            }
        }

        // Determine format
        if (fileName.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".jsonl.gz", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".jsonl.zst", StringComparison.OrdinalIgnoreCase))
        {
            info.Format = "jsonl";
        }
        else if (fileName.EndsWith(".parquet", StringComparison.OrdinalIgnoreCase))
        {
            info.Format = "parquet";
        }
        else if (fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            info.Format = "csv";
        }
    }

    private static bool IsKnownEventType(string value)
    {
        var knownTypes = new[] { "Trade", "BboQuote", "Quote", "L2Snapshot", "OrderBook", "Bar", "Depth" };
        return knownTypes.Any(t => t.Equals(value, StringComparison.OrdinalIgnoreCase));
    }

    private bool MatchesFilters(SourceFileInfo file, PackageOptions options)
    {
        // Symbol filter
        if (options.Symbols != null && options.Symbols.Length > 0)
        {
            if (file.Symbol == null || !options.Symbols.Contains(file.Symbol, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Event type filter
        if (options.EventTypes != null && options.EventTypes.Length > 0)
        {
            if (file.EventType == null || !options.EventTypes.Contains(file.EventType, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Date filter
        if (options.StartDate.HasValue && file.Date.HasValue)
        {
            if (file.Date.Value < options.StartDate.Value.Date)
            {
                return false;
            }
        }

        if (options.EndDate.HasValue && file.Date.HasValue)
        {
            if (file.Date.Value > options.EndDate.Value.Date)
            {
                return false;
            }
        }

        return true;
    }

    private async Task<PackageManifest> BuildManifestAsync(
        List<SourceFileInfo> files,
        PackageOptions options,
        CancellationToken ct)
    {
        var manifest = new PackageManifest
        {
            Name = options.Name,
            Description = options.Description,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Environment.MachineName,
            Format = options.Format.ToString(),
            Layout = options.InternalLayout.ToString(),
            Tags = options.Tags,
            CustomMetadata = options.CustomMetadata,
            Encrypted = !string.IsNullOrEmpty(options.Password)
        };

        var fileEntries = new List<PackageFileEntry>();
        var symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var eventTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        DateTime? minDate = null;
        DateTime? maxDate = null;
        long totalEvents = 0;
        long totalUncompressedSize = 0;

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            var checksum = options.VerifyChecksums
                ? await ComputeFileChecksumAsync(file.FullPath, ct)
                : string.Empty;

            var eventCount = await EstimateEventCountAsync(file.FullPath, ct);

            var entry = new PackageFileEntry
            {
                Path = GetPackageInternalPath(file, options.InternalLayout),
                Symbol = file.Symbol,
                EventType = file.EventType,
                Date = file.Date,
                Source = file.Source,
                Format = file.Format ?? "jsonl",
                Compressed = file.IsCompressed,
                CompressionType = file.CompressionType,
                SizeBytes = file.SizeBytes,
                UncompressedSizeBytes = file.IsCompressed ? file.SizeBytes * 5 : file.SizeBytes, // Estimate
                EventCount = eventCount,
                ChecksumSha256 = checksum
            };

            fileEntries.Add(entry);

            if (!string.IsNullOrEmpty(file.Symbol)) symbols.Add(file.Symbol);
            if (!string.IsNullOrEmpty(file.EventType)) eventTypes.Add(file.EventType);
            if (!string.IsNullOrEmpty(file.Source)) sources.Add(file.Source);

            if (file.Date.HasValue)
            {
                minDate = minDate == null ? file.Date : (file.Date < minDate ? file.Date : minDate);
                maxDate = maxDate == null ? file.Date : (file.Date > maxDate ? file.Date : maxDate);
            }

            totalEvents += eventCount;
            totalUncompressedSize += entry.UncompressedSizeBytes;
        }

        manifest.Files = fileEntries.ToArray();
        manifest.TotalFiles = fileEntries.Count;
        manifest.TotalEvents = totalEvents;
        manifest.UncompressedSizeBytes = totalUncompressedSize;
        manifest.Symbols = symbols.OrderBy(s => s).ToArray();
        manifest.EventTypes = eventTypes.OrderBy(t => t).ToArray();
        manifest.Sources = sources.OrderBy(s => s).ToArray();

        if (minDate.HasValue && maxDate.HasValue)
        {
            manifest.DateRange = new PackageDateRange
            {
                Start = minDate.Value,
                End = maxDate.Value,
                CalendarDays = (maxDate.Value - minDate.Value).Days + 1,
                TradingDays = CountTradingDays(minDate.Value, maxDate.Value)
            };
        }

        // Build schemas if requested
        if (options.IncludeSchemas)
        {
            manifest.Schemas = BuildSchemas(eventTypes);
        }

        return manifest;
    }

    private string GetPackageInternalPath(SourceFileInfo file, PackageLayout layout)
    {
        var fileName = file.FileName;
        var symbol = file.Symbol ?? "UNKNOWN";
        var eventType = file.EventType ?? "Unknown";
        var date = file.Date?.ToString("yyyy-MM-dd") ?? "undated";

        return layout switch
        {
            PackageLayout.ByDate => $"{DataDirectory}/{date}/{symbol}/{eventType}/{fileName}",
            PackageLayout.BySymbol => $"{DataDirectory}/{symbol}/{eventType}/{date}/{fileName}",
            PackageLayout.ByType => $"{DataDirectory}/{eventType}/{symbol}/{date}/{fileName}",
            PackageLayout.Flat => $"{DataDirectory}/{fileName}",
            _ => $"{DataDirectory}/{date}/{symbol}/{eventType}/{fileName}"
        };
    }

    private async Task<long> EstimateEventCountAsync(string path, CancellationToken ct)
    {
        try
        {
            // For JSONL files, count lines
            if (path.Contains(".jsonl", StringComparison.OrdinalIgnoreCase))
            {
                long count = 0;
                Stream stream = File.OpenRead(path);

                if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                {
                    stream = new GZipStream(stream, CompressionMode.Decompress);
                }

                await using (stream)
                using (var reader = new StreamReader(stream))
                {
                    while (await reader.ReadLineAsync(ct) != null)
                    {
                        count++;
                        if (count > 100000) // Sample first 100k lines for large files
                        {
                            // Estimate based on file size
                            var fileSize = new FileInfo(path).Length;
                            var bytesRead = stream.Position;
                            if (bytesRead > 0)
                            {
                                count = (long)(count * (double)fileSize / bytesRead);
                            }
                            break;
                        }
                    }
                }

                return count;
            }

            // For other formats, estimate based on file size
            var size = new FileInfo(path).Length;
            return size / 100; // Rough estimate: 100 bytes per record
        }
        catch
        {
            return 0;
        }
    }

    private async Task CreatePackageFileAsync(
        string packagePath,
        List<SourceFileInfo> sourceFiles,
        PackageManifest manifest,
        PackageOptions options,
        CancellationToken ct)
    {
        var compressionLevel = options.CompressionLevel switch
        {
            PackageCompressionLevel.None => CompressionLevel.NoCompression,
            PackageCompressionLevel.Fast => CompressionLevel.Fastest,
            PackageCompressionLevel.Balanced => CompressionLevel.Optimal,
            PackageCompressionLevel.Maximum => CompressionLevel.SmallestSize,
            _ => CompressionLevel.Optimal
        };

        await using var packageStream = File.Create(packagePath);

        if (options.Format == PackageFormat.Zip)
        {
            using var archive = new ZipArchive(packageStream, ZipArchiveMode.Create, leaveOpen: true);

            // Add manifest
            await AddFileToZipAsync(archive, ManifestFileName,
                JsonSerializer.Serialize(manifest, MarketDataJsonContext.PrettyPrintOptions),
                compressionLevel, ct);

            // Add data files
            var processedCount = 0;
            foreach (var file in sourceFiles)
            {
                ct.ThrowIfCancellationRequested();

                var internalPath = GetPackageInternalPath(file, options.InternalLayout);
                await AddFileToZipFromPathAsync(archive, internalPath, file.FullPath, compressionLevel, ct);

                processedCount++;
                ReportProgress(manifest.PackageId, PackageStage.Writing, processedCount, sourceFiles.Count,
                    0, manifest.UncompressedSizeBytes);
            }

            // Add supplementary files
            await AddSupplementaryFilesAsync(archive, manifest, options, compressionLevel, ct);
        }
        else if (options.Format == PackageFormat.TarGz)
        {
            // For tar.gz, we'll create a simple implementation
            // In production, you'd use SharpZipLib or similar
            await CreateTarGzPackageAsync(packageStream, sourceFiles, manifest, options, compressionLevel, ct);
        }
    }

    private async Task AddFileToZipAsync(
        ZipArchive archive,
        string entryName,
        string content,
        CompressionLevel level,
        CancellationToken ct)
    {
        var entry = archive.CreateEntry(entryName, level);
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream, Encoding.UTF8);
        await writer.WriteAsync(content);
    }

    private async Task AddFileToZipFromPathAsync(
        ZipArchive archive,
        string entryName,
        string sourcePath,
        CompressionLevel level,
        CancellationToken ct)
    {
        var entry = archive.CreateEntry(entryName, level);
        await using var entryStream = entry.Open();
        await using var sourceStream = File.OpenRead(sourcePath);
        await sourceStream.CopyToAsync(entryStream, ct);
    }

    private async Task AddSupplementaryFilesAsync(
        ZipArchive archive,
        PackageManifest manifest,
        PackageOptions options,
        CompressionLevel level,
        CancellationToken ct)
    {
        var supplementaryFiles = new List<SupplementaryFileInfo>();

        // Add README
        var readme = GenerateReadme(manifest, options);
        await AddFileToZipAsync(archive, ReadmeFileName, readme, level, ct);
        supplementaryFiles.Add(new SupplementaryFileInfo
        {
            Path = ReadmeFileName,
            Type = "readme",
            Description = "Package documentation"
        });

        // Add data dictionary if requested
        if (options.IncludeDataDictionary)
        {
            var dictionary = GenerateDataDictionary(manifest);
            var dictPath = $"{MetadataDirectory}/{DataDictionaryFileName}";
            await AddFileToZipAsync(archive, dictPath, dictionary, level, ct);
            supplementaryFiles.Add(new SupplementaryFileInfo
            {
                Path = dictPath,
                Type = "data_dictionary",
                Description = "Field definitions and data types"
            });
        }

        // Add loader scripts if requested
        if (options.IncludeLoaderScripts)
        {
            var pythonScript = GeneratePythonLoader(manifest);
            var pythonPath = $"{ScriptsDirectory}/load_data.py";
            await AddFileToZipAsync(archive, pythonPath, pythonScript, level, ct);
            supplementaryFiles.Add(new SupplementaryFileInfo
            {
                Path = pythonPath,
                Type = "loader_script",
                Description = "Python loader script"
            });

            var rScript = GenerateRLoader(manifest);
            var rPath = $"{ScriptsDirectory}/load_data.R";
            await AddFileToZipAsync(archive, rPath, rScript, level, ct);
            supplementaryFiles.Add(new SupplementaryFileInfo
            {
                Path = rPath,
                Type = "loader_script",
                Description = "R loader script"
            });
        }

        // Add import scripts if requested
        if (options.GenerateImportScripts && options.ImportScriptTargets != null)
        {
            foreach (var target in options.ImportScriptTargets)
            {
                var script = GenerateImportScript(manifest, target);
                var scriptPath = $"{ScriptsDirectory}/import_{target.ToString().ToLowerInvariant()}.sql";
                await AddFileToZipAsync(archive, scriptPath, script, level, ct);
                supplementaryFiles.Add(new SupplementaryFileInfo
                {
                    Path = scriptPath,
                    Type = "import_script",
                    Description = $"{target} import script"
                });
            }
        }

        manifest.SupplementaryFiles = supplementaryFiles.ToArray();
    }

    private async Task CreateTarGzPackageAsync(
        Stream outputStream,
        List<SourceFileInfo> sourceFiles,
        PackageManifest manifest,
        PackageOptions options,
        CompressionLevel level,
        CancellationToken ct)
    {
        // Simplified tar.gz implementation using GZipStream
        // For a complete implementation, use a proper tar library
        await using var gzipStream = new GZipStream(outputStream, level, leaveOpen: true);
        await using var writer = new StreamWriter(gzipStream);

        // Write manifest as first entry marker
        await writer.WriteLineAsync($"__MANIFEST__:{ManifestFileName}");
        await writer.WriteLineAsync(JsonSerializer.Serialize(manifest, MarketDataJsonContext.PrettyPrintOptions));
        await writer.WriteLineAsync("__END_MANIFEST__");

        // Write file entries
        foreach (var file in sourceFiles)
        {
            ct.ThrowIfCancellationRequested();

            var internalPath = GetPackageInternalPath(file, options.InternalLayout);
            await writer.WriteLineAsync($"__FILE__:{internalPath}:{file.SizeBytes}");

            await using var fileStream = File.OpenRead(file.FullPath);
            using var reader = new StreamReader(fileStream);
            var content = await reader.ReadToEndAsync(ct);
            await writer.WriteAsync(content);
            await writer.WriteLineAsync();
            await writer.WriteLineAsync("__END_FILE__");
        }
    }

    private async Task UpdateManifestInPackageAsync(
        string packagePath,
        PackageManifest manifest,
        PackageFormat format,
        CancellationToken ct)
    {
        if (format != PackageFormat.Zip) return;

        // Reopen and update manifest
        using var stream = new FileStream(packagePath, FileMode.Open, FileAccess.ReadWrite);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Update);

        var manifestEntry = archive.GetEntry(ManifestFileName);
        if (manifestEntry != null)
        {
            manifestEntry.Delete();
        }

        var newEntry = archive.CreateEntry(ManifestFileName, CompressionLevel.Optimal);
        await using var entryStream = newEntry.Open();
        await JsonSerializer.SerializeAsync(entryStream, manifest,
            MarketDataJsonContext.PrettyPrintOptions, ct);
    }

    private async Task<PackageManifest?> ReadManifestFromPackageAsync(
        string packagePath,
        PackageFormat format,
        CancellationToken ct)
    {
        if (format == PackageFormat.Zip)
        {
            using var stream = File.OpenRead(packagePath);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            var manifestEntry = archive.GetEntry(ManifestFileName);
            if (manifestEntry == null) return null;

            await using var entryStream = manifestEntry.Open();
            return await JsonSerializer.DeserializeAsync<PackageManifest>(entryStream, cancellationToken: ct);
        }

        // For tar.gz, simplified reading
        await using var fileStream = File.OpenRead(packagePath);
        await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzipStream);

        var line = await reader.ReadLineAsync(ct);
        if (line?.StartsWith("__MANIFEST__:") != true) return null;

        var jsonBuilder = new StringBuilder();
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            if (line == "__END_MANIFEST__") break;
            jsonBuilder.AppendLine(line);
        }

        return JsonSerializer.Deserialize<PackageManifest>(jsonBuilder.ToString());
    }

    private Task<List<string>> VerifyFilesInPackageAsync(
        string packagePath,
        PackageManifest manifest,
        PackageFormat format,
        CancellationToken ct)
    {
        var missingFiles = new List<string>();

        if (format == PackageFormat.Zip)
        {
            using var stream = File.OpenRead(packagePath);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            var entryNames = archive.Entries.Select(e => e.FullName).ToHashSet();

            foreach (var file in manifest.Files)
            {
                if (!entryNames.Contains(file.Path))
                {
                    missingFiles.Add(file.Path);
                }
            }
        }

        return Task.FromResult(missingFiles);
    }

    private async Task<ExtractionResult> ExtractPackageAsync(
        string packagePath,
        string destinationDirectory,
        PackageManifest manifest,
        PackageFormat format,
        bool validateChecksums,
        CancellationToken ct)
    {
        var result = new ExtractionResult();
        var validationErrors = new List<ValidationError>();

        if (format == PackageFormat.Zip)
        {
            using var stream = File.OpenRead(packagePath);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            foreach (var entry in archive.Entries)
            {
                ct.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(entry.Name)) continue; // Skip directories

                var destinationPath = Path.Combine(destinationDirectory, entry.FullName);
                var destinationDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }

                entry.ExtractToFile(destinationPath, overwrite: true);
                result.FilesExtracted++;
                result.BytesExtracted += entry.Length;

                // Validate checksum if requested and file is in manifest
                if (validateChecksums)
                {
                    var manifestEntry = manifest.Files.FirstOrDefault(f => f.Path == entry.FullName);
                    if (manifestEntry != null && !string.IsNullOrEmpty(manifestEntry.ChecksumSha256))
                    {
                        var actualChecksum = await ComputeFileChecksumAsync(destinationPath, ct);
                        if (actualChecksum == manifestEntry.ChecksumSha256)
                        {
                            result.FilesValidated++;
                        }
                        else
                        {
                            result.ValidationFailures++;
                            validationErrors.Add(new ValidationError
                            {
                                FilePath = entry.FullName,
                                ErrorType = "ChecksumMismatch",
                                ExpectedValue = manifestEntry.ChecksumSha256,
                                ActualValue = actualChecksum,
                                Message = "File checksum does not match manifest"
                            });
                        }
                    }
                }
            }
        }

        result.ValidationErrors = validationErrors;
        return result;
    }

    private PackageFormat DetectPackageFormat(string path)
    {
        if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return PackageFormat.Zip;
        if (path.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
            return PackageFormat.TarGz;
        if (path.EndsWith(".7z", StringComparison.OrdinalIgnoreCase))
            return PackageFormat.SevenZip;

        return PackageFormat.Zip; // Default
    }

    private string GetPackageFileName(PackageOptions options)
    {
        var baseName = SanitizeFileName(options.Name);
        var extension = options.Format switch
        {
            PackageFormat.Zip => ".zip",
            PackageFormat.TarGz => ".tar.gz",
            PackageFormat.SevenZip => ".7z",
            _ => ".zip"
        };

        return $"{baseName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}{extension}";
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }

    private async Task<string> ComputeFileChecksumAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static int CountTradingDays(DateTime start, DateTime end)
    {
        var count = 0;
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
        {
            if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                count++;
        }
        return count;
    }

    private Dictionary<string, PackageSchema> BuildSchemas(HashSet<string> eventTypes)
    {
        var schemas = new Dictionary<string, PackageSchema>();

        foreach (var eventType in eventTypes)
        {
            var schema = eventType.ToLowerInvariant() switch
            {
                "trade" => new PackageSchema
                {
                    EventType = "Trade",
                    Version = "1.0",
                    Fields = new[]
                    {
                        new PackageSchemaField { Name = "Timestamp", Type = "datetime", Description = "Event timestamp in UTC" },
                        new PackageSchemaField { Name = "Symbol", Type = "string", Description = "Ticker symbol" },
                        new PackageSchemaField { Name = "Price", Type = "decimal", Description = "Trade price" },
                        new PackageSchemaField { Name = "Size", Type = "long", Description = "Trade size in shares" },
                        new PackageSchemaField { Name = "Side", Type = "string", Nullable = true, Description = "Aggressor side (Buy/Sell)" },
                        new PackageSchemaField { Name = "Exchange", Type = "string", Nullable = true, Description = "Exchange code" },
                        new PackageSchemaField { Name = "SequenceNumber", Type = "long", Nullable = true, Description = "Sequence number for ordering" }
                    }
                },
                "bboquote" or "quote" => new PackageSchema
                {
                    EventType = "BboQuote",
                    Version = "1.0",
                    Fields = new[]
                    {
                        new PackageSchemaField { Name = "Timestamp", Type = "datetime", Description = "Event timestamp in UTC" },
                        new PackageSchemaField { Name = "Symbol", Type = "string", Description = "Ticker symbol" },
                        new PackageSchemaField { Name = "BidPrice", Type = "decimal", Description = "Best bid price" },
                        new PackageSchemaField { Name = "BidSize", Type = "long", Description = "Bid size in shares" },
                        new PackageSchemaField { Name = "AskPrice", Type = "decimal", Description = "Best ask price" },
                        new PackageSchemaField { Name = "AskSize", Type = "long", Description = "Ask size in shares" },
                        new PackageSchemaField { Name = "Spread", Type = "decimal", Nullable = true, Description = "Bid-ask spread" },
                        new PackageSchemaField { Name = "MidPrice", Type = "decimal", Nullable = true, Description = "Mid price" }
                    }
                },
                _ => new PackageSchema
                {
                    EventType = eventType,
                    Version = "1.0",
                    Fields = Array.Empty<PackageSchemaField>()
                }
            };

            schemas[eventType] = schema;
        }

        return schemas;
    }

    private static string GenerateReadme(PackageManifest manifest, PackageOptions options)
        => PackageScriptGenerator.GenerateReadme(manifest, options);

    private static string GenerateDataDictionary(PackageManifest manifest)
        => PackageScriptGenerator.GenerateDataDictionary(manifest);

    private static string GeneratePythonLoader(PackageManifest manifest)
        => PackageScriptGenerator.GeneratePythonLoader(manifest);

    private static string GenerateRLoader(PackageManifest manifest)
        => PackageScriptGenerator.GenerateRLoader(manifest);

    private static string GenerateImportScript(PackageManifest manifest, ImportScriptTarget target)
        => PackageScriptGenerator.GenerateImportScript(manifest, target);

    // NOTE: Script generation logic (README, data dictionary, Python/R/SQL/Spark loaders,
    // and database import scripts) has been extracted to PackageScriptGenerator.cs
    // for better navigability. The delegate methods above maintain the internal API.

    private void ReportProgress(string jobId, PackageStage stage, int filesProcessed, int totalFiles,
        long bytesProcessed, long totalBytes)
    {
        ProgressChanged?.Invoke(this, new PackageProgress
        {
            JobId = jobId,
            Stage = stage,
            FilesProcessed = filesProcessed,
            TotalFiles = totalFiles,
            BytesProcessed = bytesProcessed,
            TotalBytes = totalBytes
        });
    }

    private sealed class SourceFileInfo
    {
        public string FullPath { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string? Symbol { get; set; }
        public string? EventType { get; set; }
        public DateTime? Date { get; set; }
        public string? Source { get; set; }
        public string? Format { get; set; }
        public bool IsCompressed { get; set; }
        public string? CompressionType { get; set; }
        public long SizeBytes { get; set; }
    }

    private sealed class ExtractionResult
    {
        public int FilesExtracted { get; set; }
        public long BytesExtracted { get; set; }
        public int FilesValidated { get; set; }
        public int ValidationFailures { get; set; }
        public List<ValidationError> ValidationErrors { get; set; } = new();
    }
}

/// <summary>
/// Result of package validation.
/// </summary>
public sealed class PackageValidationResult
{
    public bool IsValid { get; set; }
    public PackageManifest? Manifest { get; set; }
    public string[]? Issues { get; set; }
    public string[]? MissingFiles { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Summary of package contents.
/// </summary>
public sealed class PackageContents
{
    public string PackageId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public int TotalFiles { get; set; }
    public long TotalEvents { get; set; }
    public long PackageSizeBytes { get; set; }
    public long UncompressedSizeBytes { get; set; }
    public string[] Symbols { get; set; } = Array.Empty<string>();
    public string[] EventTypes { get; set; } = Array.Empty<string>();
    public PackageDateRange? DateRange { get; set; }
    public PackageFileEntry[] Files { get; set; } = Array.Empty<PackageFileEntry>();
    public PackageQualityMetrics? Quality { get; set; }
}
