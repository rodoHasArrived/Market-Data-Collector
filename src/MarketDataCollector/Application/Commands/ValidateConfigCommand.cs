using MarketDataCollector.Application.Services;
using Serilog;

namespace MarketDataCollector.Application.Commands;

/// <summary>
/// Handles the --validate-config CLI command.
/// Validates configuration without starting the collector.
/// </summary>
internal sealed class ValidateConfigCommand : ICliCommand
{
    private readonly ConfigurationService _configService;
    private readonly string _defaultConfigPath;
    private readonly ILogger _log;

    public ValidateConfigCommand(ConfigurationService configService, string defaultConfigPath, ILogger log)
    {
        _configService = configService;
        _defaultConfigPath = defaultConfigPath;
        _log = log;
    }

    public bool CanHandle(string[] args)
    {
        return CliArguments.HasFlag(args, "--validate-config");
    }

    public Task<int> ExecuteAsync(string[] args, CancellationToken ct = default)
    {
        var configPathArg = CliArguments.GetValue(args, "--config") ?? _defaultConfigPath;
        var exitCode = _configService.ValidateConfig(configPathArg);
        return Task.FromResult(exitCode);
    }
}
