using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.ResultTypes;
using MarketDataCollector.Application.Services;
using Serilog;

namespace MarketDataCollector.Application.Commands;

/// <summary>
/// Handles configuration setup CLI commands:
/// --wizard, --auto-config, --detect-providers, --generate-config
/// </summary>
internal sealed class ConfigCommands : ICliCommand
{
    private readonly ConfigurationService _configService;
    private readonly ILogger _log;

    public ConfigCommands(ConfigurationService configService, ILogger log)
    {
        _configService = configService;
        _log = log;
    }

    public bool CanHandle(string[] args)
    {
        return CliArguments.HasFlag(args, "--wizard") ||
            CliArguments.HasFlag(args, "--auto-config") ||
            CliArguments.HasFlag(args, "--detect-providers") ||
            CliArguments.HasFlag(args, "--generate-config") ||
            CliArguments.HasFlag(args, "--apply-preset");
    }

    public async Task<CliResult> ExecuteAsync(string[] args, CancellationToken ct = default)
    {
        if (CliArguments.HasFlag(args, "--wizard"))
        {
            _log.Information("Starting configuration wizard...");
            var result = await _configService.RunWizardAsync(ct);
            return CliResult.FromBool(result.Success, ErrorCode.ConfigurationInvalid);
        }

        if (CliArguments.HasFlag(args, "--auto-config"))
        {
            _log.Information("Running auto-configuration...");
            var result = _configService.RunAutoConfig();
            return CliResult.FromBool(result.Success, ErrorCode.ConfigurationInvalid);
        }

        if (CliArguments.HasFlag(args, "--detect-providers"))
        {
            _configService.PrintProviderDetection();
            return CliResult.Ok();
        }

        if (CliArguments.HasFlag(args, "--generate-config"))
        {
            return RunGenerateConfig(args);
        }

        if (CliArguments.HasFlag(args, "--apply-preset"))
        {
            return RunApplyPreset(args);
        }

        return CliResult.Fail(ErrorCode.Unknown);
    }

    private CliResult RunGenerateConfig(string[] args)
    {
        var templateName = CliArguments.GetValue(args, "--template") ?? "minimal";
        var outputPath = CliArguments.GetValue(args, "--output") ?? "config/appsettings.generated.json";

        var generator = new ConfigTemplateGenerator();
        var template = generator.GetTemplate(templateName);

        if (template == null)
        {
            Console.Error.WriteLine($"Unknown template: {templateName}");
            Console.Error.WriteLine("Available templates: minimal, full, alpaca, stocksharp, backfill, production, docker");
            return CliResult.Fail(ErrorCode.NotFound);
        }

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(outputPath, template.Json);
        Console.WriteLine($"Generated {template.Name} configuration template: {outputPath}");

        if (template.EnvironmentVariables?.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Required environment variables:");
            foreach (var (key, desc) in template.EnvironmentVariables)
                Console.WriteLine($"  {key}: {desc}");
        }

        return CliResult.Ok();
    }

    private CliResult RunApplyPreset(string[] args)
    {
        var presetName = CliArguments.GetValue(args, "--apply-preset")?.ToLowerInvariant();
        var outputPath = CliArguments.GetValue(args, "--output") ?? "config/appsettings.json";

        if (string.IsNullOrWhiteSpace(presetName))
        {
            Console.WriteLine("Available configuration presets:");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine();
            Console.WriteLine("  researcher    - Optimized for quantitative research & analysis");
            Console.WriteLine("                  Compression enabled, Research storage profile,");
            Console.WriteLine("                  S&P 500 top 20 symbols, backfill enabled");
            Console.WriteLine();
            Console.WriteLine("  daytrader     - Optimized for real-time trading & monitoring");
            Console.WriteLine("                  Low-latency storage, L2 depth data,");
            Console.WriteLine("                  Major indices with full depth, no compression");
            Console.WriteLine();
            Console.WriteLine("  developer     - Lightweight config for development & testing");
            Console.WriteLine("                  Minimal symbols (SPY), 30-day retention,");
            Console.WriteLine("                  Sandbox mode, backfill enabled");
            Console.WriteLine();
            Console.WriteLine("  production    - Full production deployment");
            Console.WriteLine("                  Archival storage, compression, all features,");
            Console.WriteLine("                  Extended symbol list, backfill enabled");
            Console.WriteLine();
            Console.WriteLine("  backfill-only - Historical data collection only");
            Console.WriteLine("                  Backfill enabled, no real-time streaming,");
            Console.WriteLine("                  Archival storage, S&P 500 top 20 symbols");
            Console.WriteLine();
            Console.WriteLine("Usage: --apply-preset <name> [--output <path>]");
            Console.WriteLine("Example: --apply-preset researcher --output config/appsettings.json");
            return CliResult.Ok();
        }

        var (useCase, symbolPreset, description) = presetName switch
        {
            "researcher" or "research" => (UseCase.Research, SymbolPreset.SP500Top20, "Research & Analysis"),
            "daytrader" or "trading" => (UseCase.RealTimeTrading, SymbolPreset.USMajorIndices, "Real-Time Trading"),
            "developer" or "dev" => (UseCase.Development, SymbolPreset.Custom, "Development & Testing"),
            "production" or "prod" => (UseCase.Production, SymbolPreset.SP500Top20, "Production Deployment"),
            "backfill-only" or "backfill" => (UseCase.BackfillOnly, SymbolPreset.SP500Top20, "Historical Backfill Only"),
            _ => (UseCase.Development, SymbolPreset.Custom, (string?)null)
        };

        if (description is null)
        {
            Console.Error.WriteLine($"Unknown preset: {presetName}");
            Console.Error.WriteLine("Run --apply-preset without a name to see available presets.");
            return CliResult.Fail(ErrorCode.NotFound);
        }

        var autoConfig = new AutoConfigurationService();
        var config = autoConfig.GenerateFirstTimeConfig(new FirstTimeConfigOptions(
            UseCase: useCase,
            SymbolPreset: symbolPreset,
            EnableBackfill: useCase != UseCase.RealTimeTrading,
            EnableCompression: useCase != UseCase.Development && useCase != UseCase.RealTimeTrading
        ));

        var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(outputPath, json);

        Console.WriteLine();
        Console.WriteLine($"Applied '{presetName}' preset ({description})");
        Console.WriteLine(new string('=', 50));
        Console.WriteLine($"  Output: {outputPath}");
        Console.WriteLine($"  Use case: {useCase}");
        Console.WriteLine($"  Symbols: {config.Symbols?.Length ?? 0} configured");
        Console.WriteLine($"  Storage profile: {config.Storage?.Profile ?? "default"}");
        Console.WriteLine($"  Compression: {(config.Compress == true ? "enabled" : "disabled")}");
        Console.WriteLine($"  Backfill: {(config.Backfill?.Enabled == true ? "enabled" : "disabled")}");

        var detected = autoConfig.DetectAvailableProviders();
        var withCreds = detected.Where(p => p.HasCredentials && p.MissingCredentials.Length == 0).ToList();
        if (withCreds.Count > 0)
        {
            Console.WriteLine($"  Detected providers: {string.Join(", ", withCreds.Select(p => p.DisplayName))}");
        }

        Console.WriteLine();
        Console.WriteLine("Next steps:");
        Console.WriteLine("  1. Review the generated configuration");
        Console.WriteLine("  2. Set any required environment variables (API keys)");
        Console.WriteLine("  3. Run: dotnet run --project src/MarketDataCollector -- --validate-config");
        Console.WriteLine();

        _log.Information("Applied configuration preset: {Preset} ({Description}) to {OutputPath}",
            presetName, description, outputPath);

        return CliResult.Ok();
    }

}
