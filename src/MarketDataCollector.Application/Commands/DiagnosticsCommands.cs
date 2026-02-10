using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.ResultTypes;
using MarketDataCollector.Application.Services;
using Serilog;

namespace MarketDataCollector.Application.Commands;

/// <summary>
/// Handles diagnostics-related CLI commands:
/// --quick-check, --test-connectivity, --error-codes, --show-config, --validate-credentials
/// </summary>
internal sealed class DiagnosticsCommands : ICliCommand
{
    private readonly AppConfig _cfg;
    private readonly string _cfgPath;
    private readonly ConfigurationService _configService;
    private readonly ILogger _log;

    public DiagnosticsCommands(AppConfig cfg, string cfgPath, ConfigurationService configService, ILogger log)
    {
        _cfg = cfg;
        _cfgPath = cfgPath;
        _configService = configService;
        _log = log;
    }

    public bool CanHandle(string[] args)
    {
        return args.Any(a =>
            a.Equals("--quick-check", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--test-connectivity", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--error-codes", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--show-config", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--validate-credentials", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<CliResult> ExecuteAsync(string[] args, CancellationToken ct = default)
    {
        if (args.Any(a => a.Equals("--quick-check", StringComparison.OrdinalIgnoreCase)))
        {
            _log.Information("Running quick configuration check...");
            var result = _configService.PerformQuickCheck(_cfg);
            var summary = new StartupSummary();
            summary.DisplayQuickCheck(result);
            return CliResult.FromBool(result.Success, ErrorCode.ConfigurationInvalid);
        }

        if (args.Any(a => a.Equals("--test-connectivity", StringComparison.OrdinalIgnoreCase)))
        {
            _log.Information("Testing provider connectivity...");
            var result = await _configService.TestConnectivityAsync(_cfg, ct);
            await using var tester = new ConnectivityTestService();
            tester.DisplaySummary(result);
            return CliResult.FromBool(result.AllReachable, ErrorCode.ConnectionFailed);
        }

        if (args.Any(a => a.Equals("--error-codes", StringComparison.OrdinalIgnoreCase)))
        {
            FriendlyErrorFormatter.DisplayErrorCodeReference();
            return CliResult.Ok();
        }

        if (args.Any(a => a.Equals("--show-config", StringComparison.OrdinalIgnoreCase)))
        {
            _configService.DisplayConfigSummary(_cfg, _cfgPath, args);
            return CliResult.Ok();
        }

        if (args.Any(a => a.Equals("--validate-credentials", StringComparison.OrdinalIgnoreCase)))
        {
            _log.Information("Validating API credentials...");
            var validationResult = await _configService.ValidateCredentialsAsync(_cfg, ct);
            await using var validationService = new CredentialValidationService();
            validationService.PrintSummary(validationResult);
            return CliResult.FromBool(validationResult.AllValid, ErrorCode.CredentialsInvalid);
        }

        return CliResult.Fail(ErrorCode.Unknown);
    }
}
