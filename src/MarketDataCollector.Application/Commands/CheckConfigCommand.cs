using System.Text;
using MarketDataCollector.Application.ResultTypes;
using MarketDataCollector.Application.Services;
using Serilog;

namespace MarketDataCollector.Application.Commands;

/// <summary>
/// Handles the --check-config CLI command.
/// Performs offline-only configuration validation: parses config, validates required fields,
/// checks credential environment variables, and validates symbol configs.
/// Does NOT perform filesystem, connectivity, or resource checks.
/// Useful for CI/CD pipelines and air-gapped environments.
/// </summary>
internal sealed class CheckConfigCommand : ICliCommand
{
    private readonly AppConfig _cfg;
    private readonly ConfigurationService _configService;
    private readonly ILogger _log;

    public CheckConfigCommand(AppConfig cfg, ConfigurationService configService, ILogger log)
    {
        _cfg = cfg;
        _configService = configService;
        _log = log;
    }

    public bool CanHandle(string[] args)
    {
        return CliArguments.HasFlag(args, "--check-config");
    }

    public async Task<CliResult> ExecuteAsync(string[] args, CancellationToken ct = default)
    {
        _log.Information("Running offline config check...");

        // Use dry-run with only config, provider, and symbol validation (no filesystem/connectivity/resources)
        var options = new DryRunOptions(
            ValidateConfiguration: true,
            ValidateFileSystem: false,
            ValidateConnectivity: false,
            ValidateProviders: true,
            ValidateSymbols: true,
            ValidateResources: false
        );

        var result = await _configService.DryRunValidationAsync(_cfg, options, ct);

        // Also check credential environment variables for enabled providers
        var credentialIssues = CheckCredentialEnvironmentVariables(_cfg);

        // Generate concise report
        var sb = new StringBuilder();
        sb.AppendLine("╔═══════════════════════════════════════════╗");
        sb.AppendLine("║         CONFIG CHECK REPORT               ║");
        sb.AppendLine("╚═══════════════════════════════════════════╝");
        sb.AppendLine();

        // Config section
        if (result.ConfigurationValidation != null)
        {
            AppendSection(sb, "Configuration", result.ConfigurationValidation);
        }

        // Provider section
        if (result.ProviderValidation != null)
        {
            AppendSection(sb, "Providers", result.ProviderValidation);
        }

        // Symbol section
        if (result.SymbolValidation != null)
        {
            AppendSection(sb, "Symbols", result.SymbolValidation);
        }

        // Credential env var section
        if (credentialIssues.Count > 0)
        {
            sb.AppendLine("  Credential Environment Variables:");
            foreach (var issue in credentialIssues)
            {
                sb.AppendLine($"    ⚠ {issue}");
            }
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("  ✓ Credential environment variables: OK");
            sb.AppendLine();
        }

        var overallSuccess = result.OverallSuccess && credentialIssues.Count == 0;
        sb.AppendLine($"  Overall: {(overallSuccess ? "✓ PASS" : "✗ FAIL")}");
        sb.AppendLine("═══════════════════════════════════════════");

        Console.WriteLine(sb.ToString());

        return overallSuccess
            ? CliResult.Ok()
            : CliResult.Fail(ErrorCode.ConfigurationInvalid);
    }

    private static List<string> CheckCredentialEnvironmentVariables(AppConfig cfg)
    {
        var issues = new List<string>();

        // Check provider-specific credential env vars based on active provider
        switch (cfg.DataSource)
        {
            case DataSourceKind.Alpaca:
                CheckEnvVar(issues, "ALPACA__KEYID", "Alpaca Key ID");
                CheckEnvVar(issues, "ALPACA__SECRETKEY", "Alpaca Secret Key");
                break;
            case DataSourceKind.Polygon:
                CheckEnvVar(issues, "POLYGON__APIKEY", "Polygon API Key");
                break;
            case DataSourceKind.IB:
                // IB uses TWS/Gateway, no env var credentials
                break;
        }

        // Check common optional env vars
        if (cfg.Backfill?.Enabled == true)
        {
            var provider = cfg.Backfill.Provider?.ToLowerInvariant();
            switch (provider)
            {
                case "tiingo":
                    CheckEnvVar(issues, "TIINGO__TOKEN", "Tiingo API Token");
                    break;
                case "finnhub":
                    CheckEnvVar(issues, "FINNHUB__TOKEN", "Finnhub API Token");
                    break;
                case "alphavantage":
                    CheckEnvVar(issues, "ALPHAVANTAGE__APIKEY", "Alpha Vantage API Key");
                    break;
            }
        }

        return issues;
    }

    private static void CheckEnvVar(List<string> issues, string envVar, string description)
    {
        var value = Environment.GetEnvironmentVariable(envVar);
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add($"{description}: environment variable {envVar} is not set");
        }
    }

    private static void AppendSection(StringBuilder sb, string name, ValidationSection section)
    {
        var status = section.Success ? "✓" : "✗";
        sb.AppendLine($"  {status} {name}");

        foreach (var check in section.Checks)
        {
            var checkStatus = check.Passed ? "    ✓" : "    ✗";
            sb.AppendLine($"{checkStatus} {check.Name}: {check.Message}");
        }

        foreach (var warning in section.Warnings)
        {
            sb.AppendLine($"    ⚠ {warning}");
        }

        foreach (var error in section.Errors)
        {
            sb.AppendLine($"    ✗ {error}");
        }

        sb.AppendLine();
    }
}
