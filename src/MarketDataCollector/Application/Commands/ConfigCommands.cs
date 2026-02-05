using MarketDataCollector.Application.Config;
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
        return args.Any(a =>
            a.Equals("--wizard", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--auto-config", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--detect-providers", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--generate-config", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken ct = default)
    {
        if (args.Any(a => a.Equals("--wizard", StringComparison.OrdinalIgnoreCase)))
        {
            _log.Information("Starting configuration wizard...");
            var result = await _configService.RunWizardAsync(ct);
            return result.Success ? 0 : 1;
        }

        if (args.Any(a => a.Equals("--auto-config", StringComparison.OrdinalIgnoreCase)))
        {
            _log.Information("Running auto-configuration...");
            var result = _configService.RunAutoConfig();
            return result.Success ? 0 : 1;
        }

        if (args.Any(a => a.Equals("--detect-providers", StringComparison.OrdinalIgnoreCase)))
        {
            _configService.PrintProviderDetection();
            return 0;
        }

        if (args.Any(a => a.Equals("--generate-config", StringComparison.OrdinalIgnoreCase)))
        {
            return RunGenerateConfig(args);
        }

        return 1;
    }

    private int RunGenerateConfig(string[] args)
    {
        var templateName = GetArgValue(args, "--template") ?? "minimal";
        var outputPath = GetArgValue(args, "--output") ?? "config/appsettings.generated.json";

        var generator = new ConfigTemplateGenerator();
        var template = generator.GetTemplate(templateName);

        if (template == null)
        {
            Console.Error.WriteLine($"Unknown template: {templateName}");
            Console.Error.WriteLine("Available templates: minimal, full, alpaca, stocksharp, backfill, production, docker");
            return 1;
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

        return 0;
    }

    private static string? GetArgValue(string[] args, string flag)
    {
        var idx = Array.FindIndex(args, a => a.Equals(flag, StringComparison.OrdinalIgnoreCase));
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
    }
}
