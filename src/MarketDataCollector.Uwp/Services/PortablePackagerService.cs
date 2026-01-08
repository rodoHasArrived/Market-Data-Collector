using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for creating portable, self-contained archive packages.
/// Implements Feature #28: Portable Data Packager
/// </summary>
public sealed class PortablePackagerService
{
    private readonly ManifestService _manifestService;
    private readonly ArchiveHealthService _archiveHealthService;
    private readonly SchemaService _schemaService;

    public PortablePackagerService(
        ManifestService manifestService,
        ArchiveHealthService archiveHealthService,
        SchemaService schemaService)
    {
        _manifestService = manifestService;
        _archiveHealthService = archiveHealthService;
        _schemaService = schemaService;
    }

    /// <summary>
    /// Creates a portable archive package from selected data.
    /// </summary>
    public async Task<PackageResult> CreatePackageAsync(
        PackageRequest request,
        IProgress<PackageProgress>? progress = null,
        CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;
        var tempDir = Path.Combine(Path.GetTempPath(), $"mdc_package_{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDir);
            progress?.Report(new PackageProgress(0, "Preparing package structure..."));

            // 1. Create package directory structure
            var dataDir = Path.Combine(tempDir, "data");
            var schemasDir = Path.Combine(tempDir, "schemas");
            var verificationDir = Path.Combine(tempDir, "verification");
            Directory.CreateDirectory(dataDir);
            Directory.CreateDirectory(schemasDir);
            Directory.CreateDirectory(verificationDir);

            // 2. Copy selected data files
            progress?.Report(new PackageProgress(10, "Copying data files..."));
            var copiedFiles = await CopyDataFilesAsync(request, dataDir, progress, ct);

            // 3. Generate schemas
            progress?.Report(new PackageProgress(50, "Generating schema documentation..."));
            await GenerateSchemasAsync(schemasDir, request.EventTypes, ct);

            // 4. Generate checksums
            progress?.Report(new PackageProgress(60, "Computing checksums..."));
            var checksums = await GenerateChecksumsAsync(tempDir, ct);
            var checksumPath = Path.Combine(verificationDir, "checksums.sha256");
            await File.WriteAllTextAsync(checksumPath, checksums, ct);

            // 5. Generate manifest
            progress?.Report(new PackageProgress(75, "Creating package manifest..."));
            var manifest = CreatePackageManifest(request, copiedFiles, startTime);
            var manifestPath = Path.Combine(tempDir, "manifest.json");
            await File.WriteAllTextAsync(manifestPath,
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), ct);

            // 6. Generate README
            progress?.Report(new PackageProgress(80, "Generating documentation..."));
            var readme = GenerateReadme(request, manifest);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "README.md"), readme, ct);

            // 7. Create archive
            progress?.Report(new PackageProgress(85, $"Creating {request.Format} archive..."));
            var outputPath = await CreateArchiveAsync(tempDir, request, ct);

            // 8. Encrypt if requested
            if (request.EncryptionEnabled && !string.IsNullOrEmpty(request.EncryptionPassword))
            {
                progress?.Report(new PackageProgress(95, "Encrypting archive..."));
                outputPath = await EncryptPackageAsync(outputPath, request.EncryptionPassword, ct);
            }

            progress?.Report(new PackageProgress(100, "Package created successfully!"));

            return new PackageResult
            {
                Success = true,
                OutputPath = outputPath,
                FileCount = copiedFiles.Count,
                TotalBytes = new FileInfo(outputPath).Length,
                CompressionRatio = CalculateCompressionRatio(copiedFiles, outputPath),
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            return new PackageResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = DateTime.UtcNow - startTime
            };
        }
        finally
        {
            // Cleanup temp directory
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// Verifies a portable package integrity.
    /// </summary>
    public async Task<PackageVerificationResult> VerifyPackageAsync(
        string packagePath,
        IProgress<PackageProgress>? progress = null,
        CancellationToken ct = default)
    {
        var issues = new List<string>();
        var tempDir = Path.Combine(Path.GetTempPath(), $"mdc_verify_{Guid.NewGuid():N}");

        try
        {
            progress?.Report(new PackageProgress(10, "Extracting package..."));
            ZipFile.ExtractToDirectory(packagePath, tempDir);

            // Verify manifest exists
            var manifestPath = Path.Combine(tempDir, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                issues.Add("Missing manifest.json");
            }

            // Verify checksums
            progress?.Report(new PackageProgress(40, "Verifying checksums..."));
            var checksumPath = Path.Combine(tempDir, "verification", "checksums.sha256");
            if (File.Exists(checksumPath))
            {
                var checksumIssues = await VerifyChecksumsAsync(tempDir, checksumPath, ct);
                issues.AddRange(checksumIssues);
            }
            else
            {
                issues.Add("Missing checksums file");
            }

            progress?.Report(new PackageProgress(100, "Verification complete"));

            return new PackageVerificationResult
            {
                IsValid = issues.Count == 0,
                Issues = issues,
                FileCount = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories).Length
            };
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// Extracts a portable package to a destination directory.
    /// </summary>
    public async Task<bool> ExtractPackageAsync(
        string packagePath,
        string destinationPath,
        string? decryptionPassword = null,
        IProgress<PackageProgress>? progress = null,
        CancellationToken ct = default)
    {
        var actualPath = packagePath;

        // Decrypt if needed
        if (packagePath.EndsWith(".enc"))
        {
            if (string.IsNullOrEmpty(decryptionPassword))
                throw new ArgumentException("Password required for encrypted package");

            progress?.Report(new PackageProgress(10, "Decrypting package..."));
            actualPath = await DecryptPackageAsync(packagePath, decryptionPassword, ct);
        }

        progress?.Report(new PackageProgress(30, "Extracting files..."));

        await Task.Run(() =>
        {
            ZipFile.ExtractToDirectory(actualPath, destinationPath, true);
        }, ct);

        if (actualPath != packagePath)
            File.Delete(actualPath);

        progress?.Report(new PackageProgress(100, "Extraction complete"));
        return true;
    }

    private async Task<List<PackagedFile>> CopyDataFilesAsync(
        PackageRequest request,
        string destDir,
        IProgress<PackageProgress>? progress,
        CancellationToken ct)
    {
        var files = new List<PackagedFile>();
        var sourceFiles = GetSourceFiles(request);
        var total = sourceFiles.Count;
        var copied = 0;

        foreach (var sourceFile in sourceFiles)
        {
            ct.ThrowIfCancellationRequested();

            var relativePath = GetRelativePath(sourceFile, request.SourcePath);
            var destPath = Path.Combine(destDir, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            await CopyFileAsync(sourceFile, destPath, ct);

            files.Add(new PackagedFile
            {
                RelativePath = relativePath,
                OriginalSize = new FileInfo(sourceFile).Length,
                Checksum = await ComputeFileChecksumAsync(destPath, ct)
            });

            copied++;
            var pct = 10 + (int)(40.0 * copied / total);
            progress?.Report(new PackageProgress(pct, $"Copying files... ({copied}/{total})"));
        }

        return files;
    }

    private List<string> GetSourceFiles(PackageRequest request)
    {
        var files = new List<string>();
        var searchPatterns = request.EventTypes?.Length > 0
            ? request.EventTypes.Select(t => $"*{t}*.jsonl*").ToArray()
            : new[] { "*.jsonl*" };

        foreach (var symbol in request.Symbols ?? new[] { "*" })
        {
            foreach (var pattern in searchPatterns)
            {
                var symbolPath = symbol == "*"
                    ? request.SourcePath
                    : Path.Combine(request.SourcePath, symbol);

                if (Directory.Exists(symbolPath))
                {
                    files.AddRange(Directory.GetFiles(symbolPath, pattern, SearchOption.AllDirectories)
                        .Where(f => MatchesDateRange(f, request.StartDate, request.EndDate)));
                }
            }
        }

        return files.Distinct().ToList();
    }

    private bool MatchesDateRange(string filePath, DateOnly? start, DateOnly? end)
    {
        // Extract date from filename (e.g., 2026-01-03.jsonl)
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        if (DateOnly.TryParse(fileName.Split('.')[0], out var fileDate))
        {
            if (start.HasValue && fileDate < start.Value) return false;
            if (end.HasValue && fileDate > end.Value) return false;
        }
        return true;
    }

    private async Task GenerateSchemasAsync(string schemasDir, string[]? eventTypes, CancellationToken ct)
    {
        var types = eventTypes ?? new[] { "Trade", "BboQuote", "LOBSnapshot", "HistoricalBar" };
        foreach (var type in types)
        {
            var schema = _schemaService.GetJsonSchema(type);
            if (!string.IsNullOrEmpty(schema))
            {
                await File.WriteAllTextAsync(
                    Path.Combine(schemasDir, $"{type}_schema.json"),
                    schema, ct);
            }
        }
    }

    private async Task<string> GenerateChecksumsAsync(string rootDir, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var files = Directory.GetFiles(rootDir, "*", SearchOption.AllDirectories)
            .Where(f => !f.Contains("checksums.sha256"));

        foreach (var file in files)
        {
            var checksum = await ComputeFileChecksumAsync(file, ct);
            var relativePath = Path.GetRelativePath(rootDir, file);
            sb.AppendLine($"{checksum}  {relativePath}");
        }

        return sb.ToString();
    }

    private async Task<string> ComputeFileChecksumAsync(string filePath, CancellationToken ct)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, ct);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private async Task<List<string>> VerifyChecksumsAsync(string rootDir, string checksumFile, CancellationToken ct)
    {
        var issues = new List<string>();
        var lines = await File.ReadAllLinesAsync(checksumFile, ct);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split("  ", 2);
            if (parts.Length != 2) continue;

            var expectedChecksum = parts[0];
            var relativePath = parts[1];
            var fullPath = Path.Combine(rootDir, relativePath);

            if (!File.Exists(fullPath))
            {
                issues.Add($"Missing file: {relativePath}");
                continue;
            }

            var actualChecksum = await ComputeFileChecksumAsync(fullPath, ct);
            if (!string.Equals(expectedChecksum, actualChecksum, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add($"Checksum mismatch: {relativePath}");
            }
        }

        return issues;
    }

    private PackageManifest CreatePackageManifest(
        PackageRequest request,
        List<PackagedFile> files,
        DateTime startTime)
    {
        var totalOriginalSize = files.Sum(f => f.OriginalSize);

        return new PackageManifest
        {
            ManifestVersion = "1.0",
            PackageName = request.PackageName,
            CreatedAt = startTime,
            Creator = Environment.UserName,
            Description = request.Description,
            SourcePath = request.SourcePath,
            Symbols = request.Symbols,
            EventTypes = request.EventTypes,
            DateRange = new DateRange
            {
                Start = request.StartDate,
                End = request.EndDate
            },
            TotalFiles = files.Count,
            TotalBytesOriginal = totalOriginalSize,
            Files = files
        };
    }

    private string GenerateReadme(PackageRequest request, PackageManifest manifest)
    {
        return $@"# Market Data Archive Package

## Package Information
- **Name**: {manifest.PackageName}
- **Created**: {manifest.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC
- **Creator**: {manifest.Creator}

## Contents
- **Symbols**: {string.Join(", ", manifest.Symbols ?? new[] { "All" })}
- **Event Types**: {string.Join(", ", manifest.EventTypes ?? new[] { "All" })}
- **Date Range**: {manifest.DateRange?.Start?.ToString(""yyyy-MM-dd"")} to {manifest.DateRange?.End?.ToString(""yyyy-MM-dd"")}
- **Total Files**: {manifest.TotalFiles:N0}
- **Original Size**: {FormatBytes(manifest.TotalBytesOriginal)}

## Directory Structure
```
{manifest.PackageName}/
├── manifest.json          # Package metadata
├── README.md              # This file
├── schemas/               # JSON schemas for event types
│   ├── Trade_schema.json
│   └── ...
├── data/                  # Market data files
│   ├── SYMBOL/
│   │   ├── Trade/
│   │   └── ...
└── verification/
    └── checksums.sha256   # File integrity checksums
```

## Verification
To verify package integrity, compare the SHA256 checksums in `verification/checksums.sha256`.

## Usage
Data files are in JSONL (JSON Lines) format, optionally gzip compressed.

### Python Example
```python
import pandas as pd
import gzip
import json

def load_trades(filepath):
    opener = gzip.open if filepath.endswith('.gz') else open
    with opener(filepath, 'rt') as f:
        return pd.DataFrame([json.loads(line) for line in f])
```

## License
This data package was created by Market Data Collector.
Refer to your data provider's terms of service for data usage rights.
";
    }

    private async Task<string> CreateArchiveAsync(string sourceDir, PackageRequest request, CancellationToken ct)
    {
        var outputPath = Path.Combine(
            request.OutputPath,
            $"{request.PackageName}.{GetExtension(request.Format)}");

        await Task.Run(() =>
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);

            switch (request.Format)
            {
                case PackageFormat.Zip:
                    ZipFile.CreateFromDirectory(sourceDir, outputPath, request.CompressionLevel, true);
                    break;
                case PackageFormat.TarGz:
                case PackageFormat.SevenZip:
                    // For simplicity, use ZIP - extend with 7z or tar.gz libraries as needed
                    ZipFile.CreateFromDirectory(sourceDir, outputPath, request.CompressionLevel, true);
                    break;
            }
        }, ct);

        return outputPath;
    }

    private async Task<string> EncryptPackageAsync(string sourcePath, string password, CancellationToken ct)
    {
        var encryptedPath = sourcePath + ".enc";

        await Task.Run(() =>
        {
            using var sourceStream = File.OpenRead(sourcePath);
            using var destStream = File.Create(encryptedPath);

            using var aes = Aes.Create();
            var key = DeriveKey(password, aes.KeySize / 8);
            aes.Key = key;
            aes.GenerateIV();

            destStream.Write(aes.IV, 0, aes.IV.Length);

            using var cryptoStream = new CryptoStream(destStream, aes.CreateEncryptor(), CryptoStreamMode.Write);
            sourceStream.CopyTo(cryptoStream);
        }, ct);

        File.Delete(sourcePath);
        return encryptedPath;
    }

    private async Task<string> DecryptPackageAsync(string encryptedPath, string password, CancellationToken ct)
    {
        var decryptedPath = encryptedPath.Replace(".enc", "");

        await Task.Run(() =>
        {
            using var sourceStream = File.OpenRead(encryptedPath);
            using var destStream = File.Create(decryptedPath);

            using var aes = Aes.Create();
            var iv = new byte[aes.BlockSize / 8];
            sourceStream.Read(iv, 0, iv.Length);
            aes.IV = iv;
            aes.Key = DeriveKey(password, aes.KeySize / 8);

            using var cryptoStream = new CryptoStream(sourceStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
            cryptoStream.CopyTo(destStream);
        }, ct);

        return decryptedPath;
    }

    private byte[] DeriveKey(string password, int keyBytes)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password,
            Encoding.UTF8.GetBytes("MarketDataCollector"), 100000, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(keyBytes);
    }

    private static async Task CopyFileAsync(string source, string dest, CancellationToken ct)
    {
        using var sourceStream = File.OpenRead(source);
        using var destStream = File.Create(dest);
        await sourceStream.CopyToAsync(destStream, ct);
    }

    private static string GetRelativePath(string fullPath, string basePath)
    {
        return Path.GetRelativePath(basePath, fullPath);
    }

    private static string GetExtension(PackageFormat format) => format switch
    {
        PackageFormat.TarGz => "tar.gz",
        PackageFormat.SevenZip => "7z",
        _ => "zip"
    };

    private static double CalculateCompressionRatio(List<PackagedFile> files, string archivePath)
    {
        var originalSize = files.Sum(f => f.OriginalSize);
        var compressedSize = new FileInfo(archivePath).Length;
        return originalSize > 0 ? (double)originalSize / compressedSize : 1;
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}

#region Models

public sealed record PackageRequest
{
    public required string PackageName { get; init; }
    public required string SourcePath { get; init; }
    public required string OutputPath { get; init; }
    public string? Description { get; init; }
    public string[]? Symbols { get; init; }
    public string[]? EventTypes { get; init; }
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public PackageFormat Format { get; init; } = PackageFormat.Zip;
    public CompressionLevel CompressionLevel { get; init; } = CompressionLevel.Optimal;
    public bool EncryptionEnabled { get; init; }
    public string? EncryptionPassword { get; init; }
    public bool SplitPackage { get; init; }
    public long? MaxSplitSizeBytes { get; init; }
}

public sealed record PackageResult
{
    public bool Success { get; init; }
    public string? OutputPath { get; init; }
    public int FileCount { get; init; }
    public long TotalBytes { get; init; }
    public double CompressionRatio { get; init; }
    public TimeSpan Duration { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed record PackageVerificationResult
{
    public bool IsValid { get; init; }
    public List<string> Issues { get; init; } = new();
    public int FileCount { get; init; }
}

public sealed record PackageProgress(int PercentComplete, string StatusMessage);

public sealed record PackageManifest
{
    public string ManifestVersion { get; init; } = "1.0";
    public string? PackageName { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? Creator { get; init; }
    public string? Description { get; init; }
    public string? SourcePath { get; init; }
    public string[]? Symbols { get; init; }
    public string[]? EventTypes { get; init; }
    public DateRange? DateRange { get; init; }
    public int TotalFiles { get; init; }
    public long TotalBytesOriginal { get; init; }
    public List<PackagedFile>? Files { get; init; }
}

public sealed record DateRange
{
    public DateOnly? Start { get; init; }
    public DateOnly? End { get; init; }
}

public sealed record PackagedFile
{
    public string? RelativePath { get; init; }
    public long OriginalSize { get; init; }
    public string? Checksum { get; init; }
}

public enum PackageFormat
{
    Zip,
    TarGz,
    SevenZip
}

#endregion
