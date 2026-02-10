using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Application.ResultTypes;
using MarketDataCollector.Application.Services;
using Serilog;

namespace MarketDataCollector.Application.Commands;

/// <summary>
/// Handles the --check-schemas CLI command.
/// Verifies stored data schema compatibility.
/// </summary>
internal sealed class SchemaCheckCommand : ICliCommand
{
    private readonly AppConfig _cfg;
    private readonly ILogger _log;

    public SchemaCheckCommand(AppConfig cfg, ILogger log)
    {
        _cfg = cfg;
        _log = log;
    }

    public bool CanHandle(string[] args)
    {
        return args.Any(a => a.Equals("--check-schemas", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<CliResult> ExecuteAsync(string[] args, CancellationToken ct = default)
    {
        _log.Information("Checking stored data schema compatibility...");

        var schemaOptions = new SchemaValidationOptions
        {
            EnableVersionTracking = true,
            MaxFilesToCheck = int.TryParse(GetArgValue(args, "--max-files"), out var maxFiles) ? maxFiles : 100,
            FailOnFirstIncompatibility = args.Any(a => a.Equals("--fail-fast", StringComparison.OrdinalIgnoreCase))
        };

        await using var schemaService = new SchemaValidationService(schemaOptions, _cfg.DataRoot);
        var result = await schemaService.PerformStartupCheckAsync(ct);

        Console.WriteLine();
        if (result.Success)
        {
            Console.WriteLine("Schema Compatibility Check: PASSED");
            Console.WriteLine(new string('=', 50));
            Console.WriteLine($"  {result.Message}");
            Console.WriteLine($"  Current schema version: {SchemaValidationService.CurrentSchemaVersion}");
        }
        else
        {
            Console.WriteLine("Schema Compatibility Check: ISSUES FOUND");
            Console.WriteLine(new string('=', 50));
            Console.WriteLine($"  {result.Message}");
            Console.WriteLine();
            Console.WriteLine("  Incompatible files:");
            foreach (var incompat in result.Incompatibilities.Take(10))
            {
                var migratable = incompat.CanMigrate ? " (can migrate)" : "";
                Console.WriteLine($"    - {incompat.FilePath}");
                Console.WriteLine($"      Version: {incompat.DetectedVersion} (expected {incompat.ExpectedVersion}){migratable}");
            }
            if (result.Incompatibilities.Length > 10)
            {
                Console.WriteLine($"    ... and {result.Incompatibilities.Length - 10} more");
            }
        }
        Console.WriteLine();

        return CliResult.FromBool(result.Success, ErrorCode.SchemaMismatch);
    }

    private static string? GetArgValue(string[] args, string flag)
    {
        var idx = Array.FindIndex(args, a => a.Equals(flag, StringComparison.OrdinalIgnoreCase));
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
    }
}
