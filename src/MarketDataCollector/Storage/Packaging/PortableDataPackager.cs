using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MarketDataCollector.Application.Logging;
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

    private async Task<List<SourceFileInfo>> ScanSourceFilesAsync(PackageOptions options, CancellationToken ct)
    {
        var files = new List<SourceFileInfo>();

        if (!Directory.Exists(_dataRoot))
        {
            return files;
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

        return files.OrderBy(f => f.Symbol).ThenBy(f => f.Date).ToList();
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
        // Remove extensions to get base name
        var baseName = fileName;
        foreach (var ext in new[] { ".gz", ".zst", ".jsonl", ".parquet", ".csv" })
        {
            if (baseName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                baseName = baseName[..^ext.Length];
            }
        }

        // Try different naming conventions
        // Pattern: SYMBOL.EventType.Date or SYMBOL_EventType_Date
        var parts = baseName.Split('.', '_');

        if (parts.Length >= 1)
        {
            info.Symbol = parts[0].ToUpperInvariant();
        }

        if (parts.Length >= 2)
        {
            info.EventType = parts[1];
        }

        // Try to parse date from parts
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

        // Also check path parts for additional context
        foreach (var pathPart in pathParts)
        {
            if (DateTime.TryParse(pathPart, out var date))
            {
                info.Date ??= date;
            }

            // Check for known event types
            if (IsKnownEventType(pathPart))
            {
                info.EventType ??= pathPart;
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
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
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
        await writer.WriteLineAsync(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
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
            new JsonSerializerOptions { WriteIndented = true }, ct);
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

    private async Task<List<string>> VerifyFilesInPackageAsync(
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

        return missingFiles;
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

    private string GenerateReadme(PackageManifest manifest, PackageOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {manifest.Name}");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(manifest.Description))
        {
            sb.AppendLine(manifest.Description);
            sb.AppendLine();
        }
        sb.AppendLine("## Package Information");
        sb.AppendLine();
        sb.AppendLine($"- **Package ID:** {manifest.PackageId}");
        sb.AppendLine($"- **Created:** {manifest.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"- **Creator Version:** {manifest.CreatorVersion}");
        sb.AppendLine($"- **Format:** {manifest.Format}");
        sb.AppendLine();
        sb.AppendLine("## Data Summary");
        sb.AppendLine();
        sb.AppendLine($"- **Total Files:** {manifest.TotalFiles:N0}");
        sb.AppendLine($"- **Total Events:** {manifest.TotalEvents:N0}");
        sb.AppendLine($"- **Uncompressed Size:** {manifest.UncompressedSizeBytes:N0} bytes");
        sb.AppendLine($"- **Symbols:** {string.Join(", ", manifest.Symbols)}");
        sb.AppendLine($"- **Event Types:** {string.Join(", ", manifest.EventTypes)}");
        if (manifest.DateRange != null)
        {
            sb.AppendLine($"- **Date Range:** {manifest.DateRange.Start:yyyy-MM-dd} to {manifest.DateRange.End:yyyy-MM-dd}");
            sb.AppendLine($"- **Trading Days:** {manifest.DateRange.TradingDays}");
        }
        sb.AppendLine();
        sb.AppendLine("## Directory Structure");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.AppendLine($"{manifest.Name}/");
        sb.AppendLine("├── manifest.json           # Package manifest with checksums");
        sb.AppendLine("├── README.md               # This file");
        sb.AppendLine("├── data/                   # Market data files");
        sb.AppendLine("├── metadata/               # Data dictionary and schemas");
        sb.AppendLine("└── scripts/                # Loader scripts for various tools");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Usage");
        sb.AppendLine();
        sb.AppendLine("### Python");
        sb.AppendLine("```python");
        sb.AppendLine("# See scripts/load_data.py for full example");
        sb.AppendLine("import pandas as pd");
        sb.AppendLine("df = pd.read_json('data/..../file.jsonl', lines=True)");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("### R");
        sb.AppendLine("```r");
        sb.AppendLine("# See scripts/load_data.R for full example");
        sb.AppendLine("library(jsonlite)");
        sb.AppendLine("df <- stream_in(file('data/..../file.jsonl'))");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Verification");
        sb.AppendLine();
        sb.AppendLine("All files include SHA256 checksums in `manifest.json` for integrity verification.");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine($"*Generated by MarketDataCollector v{manifest.CreatorVersion}*");

        return sb.ToString();
    }

    private string GenerateDataDictionary(PackageManifest manifest)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Data Dictionary");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        if (manifest.Schemas != null)
        {
            foreach (var (eventType, schema) in manifest.Schemas)
            {
                sb.AppendLine($"## {eventType}");
                sb.AppendLine();
                sb.AppendLine("| Field | Type | Nullable | Description |");
                sb.AppendLine("|-------|------|----------|-------------|");

                foreach (var field in schema.Fields)
                {
                    sb.AppendLine($"| {field.Name} | {field.Type} | {(field.Nullable ? "Yes" : "No")} | {field.Description ?? ""} |");
                }

                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private string GeneratePythonLoader(PackageManifest manifest)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#!/usr/bin/env python3");
        sb.AppendLine("\"\"\"");
        sb.AppendLine($"Data Loader for {manifest.Name}");
        sb.AppendLine($"Package ID: {manifest.PackageId}");
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine("\"\"\"");
        sb.AppendLine();
        sb.AppendLine("import json");
        sb.AppendLine("import pandas as pd");
        sb.AppendLine("from pathlib import Path");
        sb.AppendLine("from typing import Optional, List");
        sb.AppendLine();
        sb.AppendLine("PACKAGE_DIR = Path(__file__).parent.parent");
        sb.AppendLine("DATA_DIR = PACKAGE_DIR / 'data'");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("def load_manifest() -> dict:");
        sb.AppendLine("    \"\"\"Load the package manifest.\"\"\"");
        sb.AppendLine("    with open(PACKAGE_DIR / 'manifest.json') as f:");
        sb.AppendLine("        return json.load(f)");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("def load_data(");
        sb.AppendLine("    symbol: Optional[str] = None,");
        sb.AppendLine("    event_type: Optional[str] = None,");
        sb.AppendLine("    date: Optional[str] = None");
        sb.AppendLine(") -> pd.DataFrame:");
        sb.AppendLine("    \"\"\"");
        sb.AppendLine("    Load market data into a pandas DataFrame.");
        sb.AppendLine("    ");
        sb.AppendLine("    Args:");
        sb.AppendLine("        symbol: Filter by symbol (e.g., 'AAPL')");
        sb.AppendLine("        event_type: Filter by event type (e.g., 'Trade', 'BboQuote')");
        sb.AppendLine("        date: Filter by date (e.g., '2024-01-15')");
        sb.AppendLine("    ");
        sb.AppendLine("    Returns:");
        sb.AppendLine("        DataFrame with matching data");
        sb.AppendLine("    \"\"\"");
        sb.AppendLine("    manifest = load_manifest()");
        sb.AppendLine("    dfs = []");
        sb.AppendLine("    ");
        sb.AppendLine("    for file_entry in manifest['files']:");
        sb.AppendLine("        # Apply filters");
        sb.AppendLine("        if symbol and file_entry.get('symbol', '').upper() != symbol.upper():");
        sb.AppendLine("            continue");
        sb.AppendLine("        if event_type and file_entry.get('eventType', '').lower() != event_type.lower():");
        sb.AppendLine("            continue");
        sb.AppendLine("        if date and file_entry.get('date', '')[:10] != date:");
        sb.AppendLine("            continue");
        sb.AppendLine("        ");
        sb.AppendLine("        file_path = PACKAGE_DIR / file_entry['path']");
        sb.AppendLine("        if file_path.exists():");
        sb.AppendLine("            df = pd.read_json(file_path, lines=True)");
        sb.AppendLine("            dfs.append(df)");
        sb.AppendLine("    ");
        sb.AppendLine("    if not dfs:");
        sb.AppendLine("        return pd.DataFrame()");
        sb.AppendLine("    ");
        sb.AppendLine("    return pd.concat(dfs, ignore_index=True)");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("def get_symbols() -> List[str]:");
        sb.AppendLine("    \"\"\"Get list of symbols in the package.\"\"\"");
        sb.AppendLine("    return load_manifest().get('symbols', [])");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("def get_event_types() -> List[str]:");
        sb.AppendLine("    \"\"\"Get list of event types in the package.\"\"\"");
        sb.AppendLine("    return load_manifest().get('eventTypes', [])");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("if __name__ == '__main__':");
        sb.AppendLine("    # Example usage");
        sb.AppendLine("    print(f'Symbols: {get_symbols()}')");
        sb.AppendLine("    print(f'Event Types: {get_event_types()}')");
        sb.AppendLine("    ");
        sb.AppendLine("    df = load_data()");
        sb.AppendLine("    print(f'\\nLoaded {len(df):,} records')");
        sb.AppendLine("    print(df.head())");

        return sb.ToString();
    }

    private string GenerateRLoader(PackageManifest manifest)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Data Loader for " + manifest.Name);
        sb.AppendLine("# Package ID: " + manifest.PackageId);
        sb.AppendLine("# Generated: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC");
        sb.AppendLine();
        sb.AppendLine("library(jsonlite)");
        sb.AppendLine("library(dplyr)");
        sb.AppendLine("library(purrr)");
        sb.AppendLine();
        sb.AppendLine("# Get package directory");
        sb.AppendLine("get_package_dir <- function() {");
        sb.AppendLine("  dirname(dirname(rstudioapi::getActiveDocumentContext()$path))");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# Load manifest");
        sb.AppendLine("load_manifest <- function() {");
        sb.AppendLine("  fromJSON(file.path(get_package_dir(), 'manifest.json'))");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# Load data with optional filters");
        sb.AppendLine("load_data <- function(symbol = NULL, event_type = NULL, date = NULL) {");
        sb.AppendLine("  manifest <- load_manifest()");
        sb.AppendLine("  package_dir <- get_package_dir()");
        sb.AppendLine("  ");
        sb.AppendLine("  files <- manifest$files");
        sb.AppendLine("  ");
        sb.AppendLine("  # Apply filters");
        sb.AppendLine("  if (!is.null(symbol)) {");
        sb.AppendLine("    files <- files[toupper(files$symbol) == toupper(symbol), ]");
        sb.AppendLine("  }");
        sb.AppendLine("  if (!is.null(event_type)) {");
        sb.AppendLine("    files <- files[tolower(files$eventType) == tolower(event_type), ]");
        sb.AppendLine("  }");
        sb.AppendLine("  ");
        sb.AppendLine("  # Load and combine files");
        sb.AppendLine("  dfs <- map(files$path, function(p) {");
        sb.AppendLine("    full_path <- file.path(package_dir, p)");
        sb.AppendLine("    if (file.exists(full_path)) {");
        sb.AppendLine("      stream_in(file(full_path), verbose = FALSE)");
        sb.AppendLine("    } else {");
        sb.AppendLine("      NULL");
        sb.AppendLine("    }");
        sb.AppendLine("  })");
        sb.AppendLine("  ");
        sb.AppendLine("  bind_rows(compact(dfs))");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# Get available symbols");
        sb.AppendLine("get_symbols <- function() {");
        sb.AppendLine("  load_manifest()$symbols");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# Get available event types");
        sb.AppendLine("get_event_types <- function() {");
        sb.AppendLine("  load_manifest()$eventTypes");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# Example usage:");
        sb.AppendLine("# df <- load_data()");
        sb.AppendLine("# df <- load_data(symbol = 'AAPL')");
        sb.AppendLine("# df <- load_data(event_type = 'Trade')");

        return sb.ToString();
    }

    private string GenerateImportScript(PackageManifest manifest, ImportScriptTarget target)
    {
        return target switch
        {
            ImportScriptTarget.Python => GeneratePythonImport(manifest),
            ImportScriptTarget.R => GenerateRImport(manifest),
            ImportScriptTarget.PostgreSql => GeneratePostgreSqlImport(manifest),
            ImportScriptTarget.ClickHouse => GenerateClickHouseImport(manifest),
            ImportScriptTarget.Spark => GenerateSparkImport(manifest),
            ImportScriptTarget.DuckDb => GenerateDuckDbImport(manifest),
            _ => $"-- Import script for {target} not yet implemented"
        };
    }

    private string GeneratePostgreSqlImport(PackageManifest manifest)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-- PostgreSQL Import Script");
        sb.AppendLine($"-- Package: {manifest.Name}");
        sb.AppendLine($"-- Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("-- Create tables");
        sb.AppendLine("CREATE TABLE IF NOT EXISTS trades (");
        sb.AppendLine("    id SERIAL PRIMARY KEY,");
        sb.AppendLine("    timestamp TIMESTAMPTZ NOT NULL,");
        sb.AppendLine("    symbol VARCHAR(20) NOT NULL,");
        sb.AppendLine("    price DECIMAL(18, 8) NOT NULL,");
        sb.AppendLine("    size BIGINT NOT NULL,");
        sb.AppendLine("    side VARCHAR(10),");
        sb.AppendLine("    exchange VARCHAR(20),");
        sb.AppendLine("    sequence_number BIGINT");
        sb.AppendLine(");");
        sb.AppendLine();
        sb.AppendLine("CREATE INDEX IF NOT EXISTS idx_trades_symbol_time ON trades(symbol, timestamp);");
        sb.AppendLine();
        sb.AppendLine("-- Note: For JSONL files, use PostgreSQL's COPY with JSON processing");
        sb.AppendLine("-- or load via Python/psycopg2");

        return sb.ToString();
    }

    private string GenerateClickHouseImport(PackageManifest manifest)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-- ClickHouse Import Script");
        sb.AppendLine($"-- Package: {manifest.Name}");
        sb.AppendLine();
        sb.AppendLine("CREATE TABLE IF NOT EXISTS trades (");
        sb.AppendLine("    timestamp DateTime64(9),");
        sb.AppendLine("    symbol LowCardinality(String),");
        sb.AppendLine("    price Decimal64(8),");
        sb.AppendLine("    size UInt64,");
        sb.AppendLine("    side LowCardinality(String),");
        sb.AppendLine("    exchange LowCardinality(String)");
        sb.AppendLine(") ENGINE = MergeTree()");
        sb.AppendLine("ORDER BY (symbol, timestamp);");

        return sb.ToString();
    }

    private string GenerateDuckDbImport(PackageManifest manifest)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-- DuckDB Import Script");
        sb.AppendLine($"-- Package: {manifest.Name}");
        sb.AppendLine();
        sb.AppendLine("-- Load JSONL files directly");
        sb.AppendLine("CREATE TABLE trades AS");
        sb.AppendLine("SELECT * FROM read_json_auto('data/**/*.jsonl');");

        return sb.ToString();
    }

    private string GeneratePythonImport(PackageManifest manifest)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#!/usr/bin/env python3");
        sb.AppendLine("\"\"\"");
        sb.AppendLine($"Database Import Script for {manifest.Name}");
        sb.AppendLine($"Package ID: {manifest.PackageId}");
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("This script imports market data from the package into a PostgreSQL database.");
        sb.AppendLine("Requires: psycopg2, pandas");
        sb.AppendLine("\"\"\"");
        sb.AppendLine();
        sb.AppendLine("import json");
        sb.AppendLine("import pandas as pd");
        sb.AppendLine("import psycopg2");
        sb.AppendLine("from psycopg2.extras import execute_values");
        sb.AppendLine("from pathlib import Path");
        sb.AppendLine("from typing import Optional");
        sb.AppendLine("import os");
        sb.AppendLine();
        sb.AppendLine("# Database connection settings (override via environment variables)");
        sb.AppendLine("DB_HOST = os.getenv('DB_HOST', 'localhost')");
        sb.AppendLine("DB_PORT = os.getenv('DB_PORT', '5432')");
        sb.AppendLine("DB_NAME = os.getenv('DB_NAME', 'marketdata')");
        sb.AppendLine("DB_USER = os.getenv('DB_USER', 'postgres')");
        sb.AppendLine("DB_PASSWORD = os.getenv('DB_PASSWORD', '')");
        sb.AppendLine();
        sb.AppendLine("PACKAGE_DIR = Path(__file__).parent.parent");
        sb.AppendLine("DATA_DIR = PACKAGE_DIR / 'data'");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("def get_connection():");
        sb.AppendLine("    \"\"\"Create a database connection.\"\"\"");
        sb.AppendLine("    return psycopg2.connect(");
        sb.AppendLine("        host=DB_HOST,");
        sb.AppendLine("        port=DB_PORT,");
        sb.AppendLine("        dbname=DB_NAME,");
        sb.AppendLine("        user=DB_USER,");
        sb.AppendLine("        password=DB_PASSWORD");
        sb.AppendLine("    )");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("def create_tables(conn):");
        sb.AppendLine("    \"\"\"Create database tables if they don't exist.\"\"\"");
        sb.AppendLine("    with conn.cursor() as cur:");
        sb.AppendLine("        cur.execute('''");
        sb.AppendLine("            CREATE TABLE IF NOT EXISTS trades (");
        sb.AppendLine("                id SERIAL PRIMARY KEY,");
        sb.AppendLine("                timestamp TIMESTAMPTZ NOT NULL,");
        sb.AppendLine("                symbol VARCHAR(20) NOT NULL,");
        sb.AppendLine("                price DECIMAL(18, 8) NOT NULL,");
        sb.AppendLine("                size BIGINT NOT NULL,");
        sb.AppendLine("                side VARCHAR(10),");
        sb.AppendLine("                exchange VARCHAR(20),");
        sb.AppendLine("                sequence_number BIGINT");
        sb.AppendLine("            );");
        sb.AppendLine("            CREATE INDEX IF NOT EXISTS idx_trades_symbol_time ON trades(symbol, timestamp);");
        sb.AppendLine("            ");
        sb.AppendLine("            CREATE TABLE IF NOT EXISTS quotes (");
        sb.AppendLine("                id SERIAL PRIMARY KEY,");
        sb.AppendLine("                timestamp TIMESTAMPTZ NOT NULL,");
        sb.AppendLine("                symbol VARCHAR(20) NOT NULL,");
        sb.AppendLine("                bid_price DECIMAL(18, 8),");
        sb.AppendLine("                bid_size BIGINT,");
        sb.AppendLine("                ask_price DECIMAL(18, 8),");
        sb.AppendLine("                ask_size BIGINT");
        sb.AppendLine("            );");
        sb.AppendLine("            CREATE INDEX IF NOT EXISTS idx_quotes_symbol_time ON quotes(symbol, timestamp);");
        sb.AppendLine("        ''')");
        sb.AppendLine("        conn.commit()");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("def import_trades(conn, df: pd.DataFrame) -> int:");
        sb.AppendLine("    \"\"\"Import trades DataFrame into database.\"\"\"");
        sb.AppendLine("    if df.empty:");
        sb.AppendLine("        return 0");
        sb.AppendLine("    ");
        sb.AppendLine("    columns = ['timestamp', 'symbol', 'price', 'size', 'side', 'exchange', 'sequence_number']");
        sb.AppendLine("    records = []");
        sb.AppendLine("    for _, row in df.iterrows():");
        sb.AppendLine("        records.append((");
        sb.AppendLine("            row.get('Timestamp') or row.get('timestamp'),");
        sb.AppendLine("            row.get('Symbol') or row.get('symbol'),");
        sb.AppendLine("            row.get('Price') or row.get('price'),");
        sb.AppendLine("            row.get('Size') or row.get('size'),");
        sb.AppendLine("            row.get('Side') or row.get('side'),");
        sb.AppendLine("            row.get('Exchange') or row.get('exchange'),");
        sb.AppendLine("            row.get('SequenceNumber') or row.get('sequence_number')");
        sb.AppendLine("        ))");
        sb.AppendLine("    ");
        sb.AppendLine("    with conn.cursor() as cur:");
        sb.AppendLine("        execute_values(cur, '''");
        sb.AppendLine("            INSERT INTO trades (timestamp, symbol, price, size, side, exchange, sequence_number)");
        sb.AppendLine("            VALUES %s");
        sb.AppendLine("        ''', records)");
        sb.AppendLine("        conn.commit()");
        sb.AppendLine("    ");
        sb.AppendLine("    return len(records)");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("def import_package():");
        sb.AppendLine("    \"\"\"Import all data from the package into the database.\"\"\"");
        sb.AppendLine("    # Load manifest");
        sb.AppendLine("    with open(PACKAGE_DIR / 'manifest.json') as f:");
        sb.AppendLine("        manifest = json.load(f)");
        sb.AppendLine("    ");
        sb.AppendLine("    print(f\"Importing package: {manifest['name']}\")");
        sb.AppendLine("    print(f\"Files: {manifest['totalFiles']}, Events: {manifest['totalEvents']:,}\")");
        sb.AppendLine("    ");
        sb.AppendLine("    conn = get_connection()");
        sb.AppendLine("    create_tables(conn)");
        sb.AppendLine("    ");
        sb.AppendLine("    total_imported = 0");
        sb.AppendLine("    for file_entry in manifest['files']:");
        sb.AppendLine("        file_path = PACKAGE_DIR / file_entry['path']");
        sb.AppendLine("        if not file_path.exists():");
        sb.AppendLine("            continue");
        sb.AppendLine("        ");
        sb.AppendLine("        event_type = file_entry.get('eventType', '').lower()");
        sb.AppendLine("        df = pd.read_json(file_path, lines=True)");
        sb.AppendLine("        ");
        sb.AppendLine("        if event_type == 'trade':");
        sb.AppendLine("            count = import_trades(conn, df)");
        sb.AppendLine("            total_imported += count");
        sb.AppendLine("            print(f\"  Imported {count:,} trades from {file_entry['path']}\")");
        sb.AppendLine("    ");
        sb.AppendLine("    conn.close()");
        sb.AppendLine("    print(f\"\\nTotal records imported: {total_imported:,}\")");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("if __name__ == '__main__':");
        sb.AppendLine("    import_package()");

        return sb.ToString();
    }

    private string GenerateRImport(PackageManifest manifest)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Database Import Script for " + manifest.Name);
        sb.AppendLine("# Package ID: " + manifest.PackageId);
        sb.AppendLine("# Generated: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC");
        sb.AppendLine("#");
        sb.AppendLine("# This script imports market data from the package into a PostgreSQL database.");
        sb.AppendLine("# Requires: DBI, RPostgres, jsonlite, dplyr");
        sb.AppendLine();
        sb.AppendLine("library(DBI)");
        sb.AppendLine("library(RPostgres)");
        sb.AppendLine("library(jsonlite)");
        sb.AppendLine("library(dplyr)");
        sb.AppendLine();
        sb.AppendLine("# Database connection settings (override via environment variables)");
        sb.AppendLine("DB_HOST <- Sys.getenv('DB_HOST', 'localhost')");
        sb.AppendLine("DB_PORT <- as.integer(Sys.getenv('DB_PORT', '5432'))");
        sb.AppendLine("DB_NAME <- Sys.getenv('DB_NAME', 'marketdata')");
        sb.AppendLine("DB_USER <- Sys.getenv('DB_USER', 'postgres')");
        sb.AppendLine("DB_PASSWORD <- Sys.getenv('DB_PASSWORD', '')");
        sb.AppendLine();
        sb.AppendLine("# Get package directory");
        sb.AppendLine("get_package_dir <- function() {");
        sb.AppendLine("  dirname(dirname(rstudioapi::getActiveDocumentContext()$path))");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# Create database connection");
        sb.AppendLine("get_connection <- function() {");
        sb.AppendLine("  dbConnect(");
        sb.AppendLine("    Postgres(),");
        sb.AppendLine("    host = DB_HOST,");
        sb.AppendLine("    port = DB_PORT,");
        sb.AppendLine("    dbname = DB_NAME,");
        sb.AppendLine("    user = DB_USER,");
        sb.AppendLine("    password = DB_PASSWORD");
        sb.AppendLine("  )");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# Create tables");
        sb.AppendLine("create_tables <- function(conn) {");
        sb.AppendLine("  dbExecute(conn, \"");
        sb.AppendLine("    CREATE TABLE IF NOT EXISTS trades (");
        sb.AppendLine("      id SERIAL PRIMARY KEY,");
        sb.AppendLine("      timestamp TIMESTAMPTZ NOT NULL,");
        sb.AppendLine("      symbol VARCHAR(20) NOT NULL,");
        sb.AppendLine("      price DECIMAL(18, 8) NOT NULL,");
        sb.AppendLine("      size BIGINT NOT NULL,");
        sb.AppendLine("      side VARCHAR(10),");
        sb.AppendLine("      exchange VARCHAR(20),");
        sb.AppendLine("      sequence_number BIGINT");
        sb.AppendLine("    );\")");
        sb.AppendLine("  dbExecute(conn, \"CREATE INDEX IF NOT EXISTS idx_trades_symbol_time ON trades(symbol, timestamp);\")");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# Import trades");
        sb.AppendLine("import_trades <- function(conn, df) {");
        sb.AppendLine("  if (nrow(df) == 0) return(0)");
        sb.AppendLine("  ");
        sb.AppendLine("  # Normalize column names");
        sb.AppendLine("  names(df) <- tolower(names(df))");
        sb.AppendLine("  ");
        sb.AppendLine("  # Select and rename columns");
        sb.AppendLine("  trades_df <- df %>%");
        sb.AppendLine("    select(");
        sb.AppendLine("      timestamp = any_of(c('timestamp', 'Timestamp')),");
        sb.AppendLine("      symbol = any_of(c('symbol', 'Symbol')),");
        sb.AppendLine("      price = any_of(c('price', 'Price')),");
        sb.AppendLine("      size = any_of(c('size', 'Size')),");
        sb.AppendLine("      side = any_of(c('side', 'Side')),");
        sb.AppendLine("      exchange = any_of(c('exchange', 'Exchange')),");
        sb.AppendLine("      sequence_number = any_of(c('sequencenumber', 'sequence_number'))");
        sb.AppendLine("    )");
        sb.AppendLine("  ");
        sb.AppendLine("  dbWriteTable(conn, 'trades', trades_df, append = TRUE, row.names = FALSE)");
        sb.AppendLine("  nrow(trades_df)");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# Main import function");
        sb.AppendLine("import_package <- function() {");
        sb.AppendLine("  package_dir <- get_package_dir()");
        sb.AppendLine("  manifest <- fromJSON(file.path(package_dir, 'manifest.json'))");
        sb.AppendLine("  ");
        sb.AppendLine("  cat(sprintf('Importing package: %s\\n', manifest$name))");
        sb.AppendLine("  cat(sprintf('Files: %d, Events: %s\\n', manifest$totalFiles, format(manifest$totalEvents, big.mark = ',')))");
        sb.AppendLine("  ");
        sb.AppendLine("  conn <- get_connection()");
        sb.AppendLine("  create_tables(conn)");
        sb.AppendLine("  ");
        sb.AppendLine("  total_imported <- 0");
        sb.AppendLine("  for (i in seq_len(nrow(manifest$files))) {");
        sb.AppendLine("    file_entry <- manifest$files[i, ]");
        sb.AppendLine("    file_path <- file.path(package_dir, file_entry$path)");
        sb.AppendLine("    ");
        sb.AppendLine("    if (!file.exists(file_path)) next");
        sb.AppendLine("    ");
        sb.AppendLine("    event_type <- tolower(file_entry$eventType)");
        sb.AppendLine("    df <- stream_in(file(file_path), verbose = FALSE)");
        sb.AppendLine("    ");
        sb.AppendLine("    if (event_type == 'trade') {");
        sb.AppendLine("      count <- import_trades(conn, df)");
        sb.AppendLine("      total_imported <- total_imported + count");
        sb.AppendLine("      cat(sprintf('  Imported %s trades from %s\\n', format(count, big.mark = ','), file_entry$path))");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine("  ");
        sb.AppendLine("  dbDisconnect(conn)");
        sb.AppendLine("  cat(sprintf('\\nTotal records imported: %s\\n', format(total_imported, big.mark = ',')))");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# Run import");
        sb.AppendLine("import_package()");

        return sb.ToString();
    }

    private string GenerateSparkImport(PackageManifest manifest)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#!/usr/bin/env python3");
        sb.AppendLine("\"\"\"");
        sb.AppendLine($"Apache Spark Import Script for {manifest.Name}");
        sb.AppendLine($"Package ID: {manifest.PackageId}");
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("This script loads market data from the package into Spark DataFrames.");
        sb.AppendLine("Requires: pyspark");
        sb.AppendLine("\"\"\"");
        sb.AppendLine();
        sb.AppendLine("import json");
        sb.AppendLine("from pathlib import Path");
        sb.AppendLine("from pyspark.sql import SparkSession");
        sb.AppendLine("from pyspark.sql.types import (");
        sb.AppendLine("    StructType, StructField, StringType, DecimalType, LongType, TimestampType");
        sb.AppendLine(")");
        sb.AppendLine();
        sb.AppendLine("PACKAGE_DIR = Path(__file__).parent.parent");
        sb.AppendLine("DATA_DIR = PACKAGE_DIR / 'data'");
        sb.AppendLine();
        sb.AppendLine("# Schema definitions");
        sb.AppendLine("TRADE_SCHEMA = StructType([");
        sb.AppendLine("    StructField('Timestamp', TimestampType(), False),");
        sb.AppendLine("    StructField('Symbol', StringType(), False),");
        sb.AppendLine("    StructField('Price', DecimalType(18, 8), False),");
        sb.AppendLine("    StructField('Size', LongType(), False),");
        sb.AppendLine("    StructField('Side', StringType(), True),");
        sb.AppendLine("    StructField('Exchange', StringType(), True),");
        sb.AppendLine("    StructField('SequenceNumber', LongType(), True)");
        sb.AppendLine("])");
        sb.AppendLine();
        sb.AppendLine("QUOTE_SCHEMA = StructType([");
        sb.AppendLine("    StructField('Timestamp', TimestampType(), False),");
        sb.AppendLine("    StructField('Symbol', StringType(), False),");
        sb.AppendLine("    StructField('BidPrice', DecimalType(18, 8), True),");
        sb.AppendLine("    StructField('BidSize', LongType(), True),");
        sb.AppendLine("    StructField('AskPrice', DecimalType(18, 8), True),");
        sb.AppendLine("    StructField('AskSize', LongType(), True)");
        sb.AppendLine("])");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("def create_spark_session(app_name: str = 'MarketDataImport') -> SparkSession:");
        sb.AppendLine("    \"\"\"Create a Spark session.\"\"\"");
        sb.AppendLine("    return SparkSession.builder \\");
        sb.AppendLine("        .appName(app_name) \\");
        sb.AppendLine("        .config('spark.sql.session.timeZone', 'UTC') \\");
        sb.AppendLine("        .getOrCreate()");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("def load_manifest():");
        sb.AppendLine("    \"\"\"Load the package manifest.\"\"\"");
        sb.AppendLine("    with open(PACKAGE_DIR / 'manifest.json') as f:");
        sb.AppendLine("        return json.load(f)");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("def load_trades(spark: SparkSession):");
        sb.AppendLine("    \"\"\"Load all trade data into a Spark DataFrame.\"\"\"");
        sb.AppendLine("    manifest = load_manifest()");
        sb.AppendLine("    trade_files = [");
        sb.AppendLine("        str(PACKAGE_DIR / f['path'])");
        sb.AppendLine("        for f in manifest['files']");
        sb.AppendLine("        if f.get('eventType', '').lower() == 'trade'");
        sb.AppendLine("    ]");
        sb.AppendLine("    ");
        sb.AppendLine("    if not trade_files:");
        sb.AppendLine("        return spark.createDataFrame([], TRADE_SCHEMA)");
        sb.AppendLine("    ");
        sb.AppendLine("    return spark.read.json(trade_files, schema=TRADE_SCHEMA)");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("def load_quotes(spark: SparkSession):");
        sb.AppendLine("    \"\"\"Load all quote data into a Spark DataFrame.\"\"\"");
        sb.AppendLine("    manifest = load_manifest()");
        sb.AppendLine("    quote_files = [");
        sb.AppendLine("        str(PACKAGE_DIR / f['path'])");
        sb.AppendLine("        for f in manifest['files']");
        sb.AppendLine("        if f.get('eventType', '').lower() in ('bboquote', 'quote')");
        sb.AppendLine("    ]");
        sb.AppendLine("    ");
        sb.AppendLine("    if not quote_files:");
        sb.AppendLine("        return spark.createDataFrame([], QUOTE_SCHEMA)");
        sb.AppendLine("    ");
        sb.AppendLine("    return spark.read.json(quote_files, schema=QUOTE_SCHEMA)");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("def load_all_data(spark: SparkSession):");
        sb.AppendLine("    \"\"\"Load all data from the package (infers schema).\"\"\"");
        sb.AppendLine("    return spark.read.json(str(DATA_DIR / '**' / '*.jsonl'))");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("def save_to_parquet(df, output_path: str, partition_by: list = None):");
        sb.AppendLine("    \"\"\"Save DataFrame to Parquet format.\"\"\"");
        sb.AppendLine("    writer = df.write.mode('overwrite')");
        sb.AppendLine("    if partition_by:");
        sb.AppendLine("        writer = writer.partitionBy(*partition_by)");
        sb.AppendLine("    writer.parquet(output_path)");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("def main():");
        sb.AppendLine("    \"\"\"Main entry point.\"\"\"");
        sb.AppendLine("    manifest = load_manifest()");
        sb.AppendLine("    print(f\"Loading package: {manifest['name']}\")");
        sb.AppendLine("    print(f\"Files: {manifest['totalFiles']}, Events: {manifest['totalEvents']:,}\")");
        sb.AppendLine("    ");
        sb.AppendLine("    spark = create_spark_session()");
        sb.AppendLine("    ");
        sb.AppendLine("    # Load trades");
        sb.AppendLine("    trades_df = load_trades(spark)");
        sb.AppendLine("    print(f\"\\nLoaded {trades_df.count():,} trades\")");
        sb.AppendLine("    trades_df.printSchema()");
        sb.AppendLine("    trades_df.show(5)");
        sb.AppendLine("    ");
        sb.AppendLine("    # Load quotes");
        sb.AppendLine("    quotes_df = load_quotes(spark)");
        sb.AppendLine("    print(f\"\\nLoaded {quotes_df.count():,} quotes\")");
        sb.AppendLine("    quotes_df.printSchema()");
        sb.AppendLine("    quotes_df.show(5)");
        sb.AppendLine("    ");
        sb.AppendLine("    # Example: Save to Parquet with partitioning");
        sb.AppendLine("    # save_to_parquet(trades_df, 'output/trades.parquet', ['Symbol'])");
        sb.AppendLine("    ");
        sb.AppendLine("    spark.stop()");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("if __name__ == '__main__':");
        sb.AppendLine("    main()");

        return sb.ToString();
    }

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
