using FluentValidation;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Application.Config;

/// <summary>
/// FluentValidation validator for AppConfig.
/// Validates configuration on startup to catch errors early.
/// </summary>
public sealed class AppConfigValidator : AbstractValidator<AppConfig>
{
    public AppConfigValidator()
    {
        RuleFor(x => x.DataRoot)
            .NotEmpty()
            .WithMessage("DataRoot is required. Specify a directory for data storage.");

        When(x => x.DataSource == DataSourceKind.Alpaca, () =>
        {
            RuleFor(x => x.Alpaca)
                .NotNull()
                .WithMessage("Alpaca configuration is required when DataSource is Alpaca.");

            When(x => x.Alpaca != null, () =>
            {
                RuleFor(x => x.Alpaca!.KeyId)
                    .NotEmpty()
                    .Must(k => k != "__SET_ME__")
                    .WithMessage("Alpaca KeyId is required. Set it in appsettings.json or use ALPACA_KEY_ID environment variable.");

                RuleFor(x => x.Alpaca!.SecretKey)
                    .NotEmpty()
                    .Must(k => k != "__SET_ME__")
                    .WithMessage("Alpaca SecretKey is required. Set it in appsettings.json or use ALPACA_SECRET_KEY environment variable.");

                RuleFor(x => x.Alpaca!.Feed)
                    .NotEmpty()
                    .Must(f => f == "iex" || f == "sip")
                    .WithMessage("Alpaca Feed must be 'iex' or 'sip'.");
            });
        });

        RuleForEach(x => x.Symbols)
            .SetValidator(new SymbolConfigValidator());
    }
}

/// <summary>
/// Validator for individual symbol configurations.
/// </summary>
public sealed class SymbolConfigValidator : AbstractValidator<SymbolConfig>
{
    private static readonly string[] ValidSecurityTypes = { "STK", "OPT", "FUT", "CASH", "IND", "CFD", "BOND", "CMDTY", "FUND", "WAR" };
    private static readonly string[] ValidCurrencies = { "USD", "EUR", "GBP", "JPY", "CHF", "CAD", "AUD", "HKD", "SGD" };

    public SymbolConfigValidator()
    {
        RuleFor(x => x.Symbol)
            .NotEmpty()
            .WithMessage("Symbol is required.")
            .MaximumLength(20)
            .WithMessage("Symbol must be 20 characters or less.");

        RuleFor(x => x.DepthLevels)
            .InclusiveBetween(1, 50)
            .WithMessage("DepthLevels must be between 1 and 50.");

        RuleFor(x => x.SecurityType)
            .NotEmpty()
            .Must(s => ValidSecurityTypes.Contains(s))
            .WithMessage($"SecurityType must be one of: {string.Join(", ", ValidSecurityTypes)}");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Must(c => ValidCurrencies.Contains(c))
            .WithMessage($"Currency must be one of: {string.Join(", ", ValidCurrencies)}");

        RuleFor(x => x.Exchange)
            .NotEmpty()
            .WithMessage("Exchange is required.");
    }
}

/// <summary>
/// Helper class to validate configuration and log results.
/// </summary>
public static class ConfigValidationHelper
{
    private static readonly ILogger Log = LoggingSetup.ForContext("ConfigValidator");

    /// <summary>
    /// Validates the configuration and logs any errors.
    /// </summary>
    /// <param name="config">Configuration to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool ValidateAndLog(AppConfig config)
    {
        var validator = new AppConfigValidator();
        var result = validator.Validate(config);

        if (result.IsValid)
        {
            Log.Information("Configuration validation passed");
            return true;
        }

        Log.Error("Configuration validation failed with {ErrorCount} error(s):", result.Errors.Count);

        foreach (var error in result.Errors)
        {
            Log.Error("  - {PropertyName}: {ErrorMessage}", error.PropertyName, error.ErrorMessage);
        }

        Log.Error("Please review your appsettings.json file. See docs/CONFIGURATION.md for help.");
        return false;
    }

    /// <summary>
    /// Validates and throws if configuration is invalid.
    /// </summary>
    public static void ValidateOrThrow(AppConfig config)
    {
        if (!ValidateAndLog(config))
        {
            throw new InvalidOperationException(
                "Configuration validation failed. Check the logs for details and review appsettings.json.");
        }
    }
}
