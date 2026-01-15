using FluentValidation;
using Serilog;

namespace MarketDataCollector.Application.Config;

/// <summary>
/// FluentValidation-based configuration validator.
/// Inspired by best practices from StockSharp and QuantConnect LEAN projects.
/// </summary>
public static class ConfigValidationHelper
{
    public static bool ValidateAndLog(AppConfig config)
    {
        var validator = new AppConfigValidator();
        var result = validator.Validate(config);

        if (result.IsValid)
        {
            Log.Information("Configuration validation passed");
            return true;
        }

        var log = Log.ForContext("SourceContext", "ConfigValidation");
        log.Error("Configuration validation failed with {ErrorCount} error(s):", result.Errors.Count);

        foreach (var error in result.Errors)
        {
            log.Error("  - {PropertyName}: {ErrorMessage}", error.PropertyName, error.ErrorMessage);
        }

        log.Error("Please review your appsettings.json and correct the errors above.");
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

/// <summary>
/// Validates AppConfig using FluentValidation patterns.
/// </summary>
public class AppConfigValidator : AbstractValidator<AppConfig>
{
    public AppConfigValidator()
    {
        RuleFor(x => x.DataRoot)
            .NotEmpty()
            .WithMessage("DataRoot must be specified")
            .Must(BeValidPath)
            .WithMessage("DataRoot must be a valid directory path");

        RuleFor(x => x.DataSource)
            .IsInEnum()
            .WithMessage("DataSource must be IB, Alpaca, or Polygon");

        // Alpaca-specific validation
        When(x => x.DataSource == DataSourceKind.Alpaca, () =>
        {
            RuleFor(x => x.Alpaca)
                .NotNull()
                .WithMessage("Alpaca configuration is required when DataSource is set to Alpaca")
                .SetValidator(new AlpacaOptionsValidator()!);
        });

        // Storage configuration validation
        When(x => x.Storage != null, () =>
        {
            RuleFor(x => x.Storage)
                .SetValidator(new StorageConfigValidator()!);
        });

        // Symbol configuration validation
        When(x => x.Symbols != null && x.Symbols.Length > 0, () =>
        {
            RuleForEach(x => x.Symbols)
                .SetValidator(new SymbolConfigValidator());
        });
    }

    private static bool BeValidPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            // Check for invalid path characters
            var invalidChars = Path.GetInvalidPathChars();
            return !path.Any(c => invalidChars.Contains(c));
        }
        catch (Exception ex)
        {
            Log.ForContext("SourceContext", "ConfigValidation")
               .Warning(ex, "Path validation failed for path '{Path}'", path);
            return false;
        }
    }
}

/// <summary>
/// Validates AlpacaOptions configuration.
/// </summary>
public class AlpacaOptionsValidator : AbstractValidator<AlpacaOptions>
{
    public AlpacaOptionsValidator()
    {
        RuleFor(x => x.KeyId)
            .NotEmpty()
            .WithMessage("Alpaca KeyId is required")
            .MinimumLength(10)
            .WithMessage("Alpaca KeyId appears to be invalid (too short)");

        RuleFor(x => x.SecretKey)
            .NotEmpty()
            .WithMessage("Alpaca SecretKey is required")
            .MinimumLength(10)
            .WithMessage("Alpaca SecretKey appears to be invalid (too short)");

        RuleFor(x => x.Feed)
            .NotEmpty()
            .WithMessage("Alpaca Feed must be specified (e.g., 'iex', 'sip')")
            .Must(feed => feed == "iex" || feed == "sip")
            .WithMessage("Alpaca Feed must be either 'iex' or 'sip'");
    }
}

/// <summary>
/// Validates StorageConfig settings.
/// </summary>
public class StorageConfigValidator : AbstractValidator<StorageConfig>
{
    public StorageConfigValidator()
    {
        RuleFor(x => x.NamingConvention)
            .Must(BeValidNamingConvention)
            .WithMessage("NamingConvention must be one of: Flat, BySymbol, ByDate, ByType");

        RuleFor(x => x.DatePartition)
            .Must(BeValidDatePartition)
            .WithMessage("DatePartition must be one of: None, Daily, Hourly, Monthly");

        When(x => x.RetentionDays.HasValue, () =>
        {
            RuleFor(x => x.RetentionDays!.Value)
                .GreaterThan(0)
                .WithMessage("RetentionDays must be greater than 0");
        });

        When(x => x.MaxTotalMegabytes.HasValue, () =>
        {
            RuleFor(x => x.MaxTotalMegabytes!.Value)
                .GreaterThan(0)
                .WithMessage("MaxTotalMegabytes must be greater than 0");
        });
    }

    private static bool BeValidNamingConvention(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var valid = new[] { "flat", "bysymbol", "bydate", "bytype" };
        return valid.Contains(value.ToLowerInvariant());
    }

    private static bool BeValidDatePartition(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var valid = new[] { "none", "daily", "hourly", "monthly" };
        return valid.Contains(value.ToLowerInvariant());
    }
}

/// <summary>
/// Validates SymbolConfig settings.
/// </summary>
public class SymbolConfigValidator : AbstractValidator<SymbolConfig>
{
    public SymbolConfigValidator()
    {
        RuleFor(x => x.Symbol)
            .NotEmpty()
            .WithMessage("Symbol cannot be empty")
            .Matches(@"^[A-Z0-9\-\.\/]+$")
            .WithMessage("Symbol must contain only uppercase letters, numbers, hyphens, dots, or slashes");

        When(x => x.SubscribeDepth, () =>
        {
            RuleFor(x => x.DepthLevels)
                .GreaterThan(0)
                .WithMessage("DepthLevels must be greater than 0 when SubscribeDepth is true")
                .LessThanOrEqualTo(50)
                .WithMessage("DepthLevels should not exceed 50 (exchange limits typically apply)");
        });
    }
}
