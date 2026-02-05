using MarketDataCollector.Application.Config;
using MarketDataCollector.Storage.Packaging;
using Serilog;

namespace MarketDataCollector.Application.Commands;

/// <summary>
/// Handles all package-related CLI commands:
/// --package, --import-package, --list-package, --validate-package
/// </summary>
internal sealed class PackageCommands : ICliCommand
{
    private readonly AppConfig _cfg;
    private readonly ILogger _log;

    public PackageCommands(AppConfig cfg, ILogger log)
    {
        _cfg = cfg;
        _log = log;
    }

    public bool CanHandle(string[] args)
    {
        return args.Any(a =>
            a.Equals("--package", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--import-package", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--list-package", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--validate-package", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken ct = default)
    {
        if (args.Any(a => a.Equals("--package", StringComparison.OrdinalIgnoreCase)))
            return await RunCreateAsync(args, ct);

        if (args.Any(a => a.Equals("--import-package", StringComparison.OrdinalIgnoreCase)))
        {
            var path = GetArgValue(args, "--import-package");
            if (string.IsNullOrWhiteSpace(path))
            {
                Console.Error.WriteLine("Error: --import-package requires a path to the package file");
                return 1;
            }
            return await RunImportAsync(path, args, ct);
        }

        if (args.Any(a => a.Equals("--list-package", StringComparison.OrdinalIgnoreCase)))
        {
            var path = GetArgValue(args, "--list-package");
            if (string.IsNullOrWhiteSpace(path))
            {
                Console.Error.WriteLine("Error: --list-package requires a path to the package file");
                return 1;
            }
            return await RunListAsync(path, ct);
        }

        if (args.Any(a => a.Equals("--validate-package", StringComparison.OrdinalIgnoreCase)))
        {
            var path = GetArgValue(args, "--validate-package");
            if (string.IsNullOrWhiteSpace(path))
            {
                Console.Error.WriteLine("Error: --validate-package requires a path to the package file");
                return 1;
            }
            return await RunValidateAsync(path, ct);
        }

        return 1;
    }

    private async Task<int> RunCreateAsync(string[] args, CancellationToken ct)
    {
        _log.Information("Creating portable data package...");

        var options = new PackageOptions
        {
            Name = GetArgValue(args, "--package-name") ?? $"market-data-{DateTime.UtcNow:yyyyMMdd}",
            Description = GetArgValue(args, "--package-description"),
            OutputDirectory = GetArgValue(args, "--package-output") ?? "packages",
            IncludeQualityReport = !args.Any(a => a.Equals("--no-quality-report", StringComparison.OrdinalIgnoreCase)),
            IncludeDataDictionary = !args.Any(a => a.Equals("--no-data-dictionary", StringComparison.OrdinalIgnoreCase)),
            IncludeLoaderScripts = !args.Any(a => a.Equals("--no-loader-scripts", StringComparison.OrdinalIgnoreCase)),
            VerifyChecksums = !args.Any(a => a.Equals("--skip-checksums", StringComparison.OrdinalIgnoreCase))
        };

        var symbolsArg = GetArgValue(args, "--package-symbols");
        if (!string.IsNullOrWhiteSpace(symbolsArg))
            options.Symbols = symbolsArg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var eventTypesArg = GetArgValue(args, "--package-events");
        if (!string.IsNullOrWhiteSpace(eventTypesArg))
            options.EventTypes = eventTypesArg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var fromArg = GetArgValue(args, "--package-from");
        if (DateTime.TryParse(fromArg, out var from))
            options.StartDate = from;

        var toArg = GetArgValue(args, "--package-to");
        if (DateTime.TryParse(toArg, out var to))
            options.EndDate = to;

        var formatArg = GetArgValue(args, "--package-format");
        if (!string.IsNullOrWhiteSpace(formatArg))
        {
            options.Format = formatArg.ToLowerInvariant() switch
            {
                "zip" => PackageFormat.Zip,
                "tar.gz" or "targz" or "tgz" => PackageFormat.TarGz,
                "7z" or "7zip" => PackageFormat.SevenZip,
                _ => PackageFormat.Zip
            };
        }

        var compressionArg = GetArgValue(args, "--package-compression");
        if (!string.IsNullOrWhiteSpace(compressionArg))
        {
            options.CompressionLevel = compressionArg.ToLowerInvariant() switch
            {
                "none" => PackageCompressionLevel.None,
                "fast" => PackageCompressionLevel.Fast,
                "balanced" => PackageCompressionLevel.Balanced,
                "maximum" or "max" => PackageCompressionLevel.Maximum,
                _ => PackageCompressionLevel.Balanced
            };
        }

        var packager = new PortableDataPackager(_cfg.DataRoot);
        packager.ProgressChanged += (_, progress) =>
        {
            var percent = progress.TotalFiles > 0
                ? (double)progress.FilesProcessed / progress.TotalFiles * 100
                : 0;
            Console.Write($"\r[{progress.Stage}] {progress.FilesProcessed}/{progress.TotalFiles} files ({percent:F1}%)    ");
        };

        var result = await packager.CreatePackageAsync(options, ct);
        Console.WriteLine();

        if (result.Success)
        {
            Console.WriteLine();
            Console.WriteLine($"  Package: {result.PackagePath}");
            Console.WriteLine($"  Size: {result.PackageSizeBytes:N0} bytes");
            Console.WriteLine($"  Files: {result.FilesIncluded:N0}");
            Console.WriteLine($"  Events: {result.TotalEvents:N0}");
            Console.WriteLine($"  Symbols: {string.Join(", ", result.Symbols)}");

            if (result.Warnings.Length > 0)
            {
                Console.WriteLine("Warnings:");
                foreach (var warning in result.Warnings)
                    Console.WriteLine($"  - {warning}");
            }

            _log.Information("Package created: {PackagePath} ({SizeBytes:N0} bytes)",
                result.PackagePath, result.PackageSizeBytes);
            return 0;
        }

        Console.Error.WriteLine($"Error: {result.Error}");
        _log.Error("Package creation failed: {Error}", result.Error);
        return 1;
    }

    private async Task<int> RunImportAsync(string packagePath, string[] args, CancellationToken ct)
    {
        _log.Information("Importing package: {PackagePath}", packagePath);

        var destinationDir = GetArgValue(args, "--import-destination") ?? _cfg.DataRoot;
        var validateChecksums = !args.Any(a => a.Equals("--skip-validation", StringComparison.OrdinalIgnoreCase));
        var mergeWithExisting = args.Any(a => a.Equals("--merge", StringComparison.OrdinalIgnoreCase));

        var packager = new PortableDataPackager(_cfg.DataRoot);
        packager.ProgressChanged += (_, progress) =>
        {
            var percent = progress.TotalFiles > 0
                ? (double)progress.FilesProcessed / progress.TotalFiles * 100
                : 0;
            Console.Write($"\r[{progress.Stage}] {progress.FilesProcessed}/{progress.TotalFiles} files ({percent:F1}%)    ");
        };

        var result = await packager.ImportPackageAsync(packagePath, destinationDir, validateChecksums, mergeWithExisting, ct);
        Console.WriteLine();

        if (result.Success)
        {
            Console.WriteLine($"  Source: {result.SourcePath}");
            Console.WriteLine($"  Files Extracted: {result.FilesExtracted:N0}");
            Console.WriteLine($"  Bytes Extracted: {result.BytesExtracted:N0}");

            if (result.Warnings.Length > 0)
            {
                Console.WriteLine("Warnings:");
                foreach (var warning in result.Warnings)
                    Console.WriteLine($"  - {warning}");
            }

            _log.Information("Package imported: {FilesExtracted} files", result.FilesExtracted);
            return 0;
        }

        Console.Error.WriteLine($"Error: {result.Error}");
        if (result.ValidationErrors?.Length > 0)
        {
            Console.Error.WriteLine("\nValidation Errors:");
            foreach (var error in result.ValidationErrors)
                Console.Error.WriteLine($"  - {error.FilePath}: {error.Message}");
        }

        _log.Error("Package import failed: {Error}", result.Error);
        return 1;
    }

    private async Task<int> RunListAsync(string packagePath, CancellationToken ct)
    {
        _log.Information("Listing package contents: {PackagePath}", packagePath);

        var packager = new PortableDataPackager(".");

        try
        {
            var contents = await packager.ListPackageContentsAsync(packagePath, ct);

            Console.WriteLine($"  Name: {contents.Name}");
            Console.WriteLine($"  Package ID: {contents.PackageId}");
            if (!string.IsNullOrEmpty(contents.Description))
                Console.WriteLine($"  Description: {contents.Description}");
            Console.WriteLine($"  Files: {contents.TotalFiles:N0}");
            Console.WriteLine($"  Events: {contents.TotalEvents:N0}");
            Console.WriteLine($"  Symbols: {string.Join(", ", contents.Symbols)}");
            Console.WriteLine($"  Event Types: {string.Join(", ", contents.EventTypes)}");
            Console.WriteLine();

            foreach (var file in contents.Files.Take(20))
            {
                var size = file.SizeBytes > 1024 * 1024
                    ? $"{file.SizeBytes / (1024.0 * 1024.0):F1} MB"
                    : $"{file.SizeBytes / 1024.0:F1} KB";
                Console.WriteLine($"    {file.Path} ({size}, {file.EventCount:N0} events)");
            }

            if (contents.Files.Length > 20)
                Console.WriteLine($"    ... and {contents.Files.Length - 20} more files");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error reading package: {ex.Message}");
            _log.Error(ex, "Failed to list package contents");
            return 1;
        }
    }

    private async Task<int> RunValidateAsync(string packagePath, CancellationToken ct)
    {
        _log.Information("Validating package: {PackagePath}", packagePath);

        var packager = new PortableDataPackager(".");
        var result = await packager.ValidatePackageAsync(packagePath, ct);

        if (result.IsValid)
        {
            Console.WriteLine($"  Package: {packagePath} - VALID");
            if (result.Manifest != null)
            {
                Console.WriteLine($"  Name: {result.Manifest.Name}");
                Console.WriteLine($"  Files: {result.Manifest.TotalFiles:N0}");
            }
            _log.Information("Package validation passed: {PackagePath}", packagePath);
            return 0;
        }

        Console.WriteLine($"  Package: {packagePath} - INVALID");
        if (!string.IsNullOrEmpty(result.Error))
            Console.WriteLine($"  Error: {result.Error}");

        if (result.Issues?.Length > 0)
        {
            Console.WriteLine("\n  Issues:");
            foreach (var issue in result.Issues)
                Console.WriteLine($"    - {issue}");
        }

        if (result.MissingFiles?.Length > 0)
        {
            Console.WriteLine("\n  Missing Files:");
            foreach (var file in result.MissingFiles.Take(10))
                Console.WriteLine($"    - {file}");
        }

        _log.Warning("Package validation failed: {PackagePath}", packagePath);
        return 1;
    }

    private static string? GetArgValue(string[] args, string flag)
    {
        var idx = Array.FindIndex(args, a => a.Equals(flag, StringComparison.OrdinalIgnoreCase));
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
    }
}
