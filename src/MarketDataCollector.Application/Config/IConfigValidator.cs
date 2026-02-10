using FluentValidation.Results;

namespace MarketDataCollector.Application.Config;

/// <summary>
/// Abstraction over configuration validation so that validation logic
/// can be composed as a pipeline: Field → Semantic → Connectivity.
/// </summary>
public interface IConfigValidator
{
    /// <summary>
    /// Validates the given configuration and returns a list of validation results.
    /// </summary>
    IReadOnlyList<ConfigValidationResult> Validate(AppConfig config);
}

/// <summary>
/// A single validation finding (error, warning, or info).
/// </summary>
public sealed record ConfigValidationResult(
    ConfigValidationSeverity Severity,
    string Property,
    string Message,
    string? Suggestion = null)
{
    public bool IsError => Severity == ConfigValidationSeverity.Error;
}

/// <summary>
/// Severity level of a configuration validation finding.
/// </summary>
public enum ConfigValidationSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>
/// Pipeline-based configuration validator that runs field-level, semantic,
/// and optional connectivity validation stages in order.
/// Consolidates ConfigValidationHelper, ConfigValidatorCli, and PreflightChecker
/// field-validation logic into a single composable pipeline.
/// </summary>
public sealed class ConfigValidationPipeline : IConfigValidator
{
    private readonly IReadOnlyList<IConfigValidationStage> _stages;

    public ConfigValidationPipeline(IEnumerable<IConfigValidationStage> stages)
    {
        _stages = stages?.ToList() ?? throw new ArgumentNullException(nameof(stages));
    }

    /// <summary>
    /// Creates the default pipeline with Field and Semantic stages.
    /// </summary>
    public static ConfigValidationPipeline CreateDefault()
    {
        return new ConfigValidationPipeline(new IConfigValidationStage[]
        {
            new FieldValidationStage(),
            new SemanticValidationStage()
        });
    }

    public IReadOnlyList<ConfigValidationResult> Validate(AppConfig config)
    {
        var results = new List<ConfigValidationResult>();

        foreach (var stage in _stages)
        {
            var stageResults = stage.Validate(config);
            results.AddRange(stageResults);

            // Stop running subsequent stages if the current one produced errors
            if (stageResults.Any(r => r.IsError))
                break;
        }

        return results;
    }
}

/// <summary>
/// A single stage in the configuration validation pipeline.
/// </summary>
public interface IConfigValidationStage
{
    IReadOnlyList<ConfigValidationResult> Validate(AppConfig config);
}

/// <summary>
/// Field-level validation using FluentValidation rules (AppConfigValidator).
/// </summary>
public sealed class FieldValidationStage : IConfigValidationStage
{
    public IReadOnlyList<ConfigValidationResult> Validate(AppConfig config)
    {
        var validator = new AppConfigValidator();
        var result = validator.Validate(config);

        return result.Errors
            .Select(e => new ConfigValidationResult(
                ConfigValidationSeverity.Error,
                e.PropertyName,
                e.ErrorMessage,
                GetSuggestion(e)))
            .ToList();
    }

    private static string? GetSuggestion(ValidationFailure error)
    {
        return error.PropertyName switch
        {
            "DataRoot" => "Set a valid directory path for storing market data",
            "Alpaca.KeyId" => "Set ALPACA__KEYID environment variable or update config",
            "Alpaca.SecretKey" => "Set ALPACA__SECRETKEY environment variable or update config",
            "Alpaca.Feed" => "Use 'iex' for free data or 'sip' for paid subscription",
            "StockSharp.Enabled" => "Set StockSharp:Enabled to true when using StockSharp",
            "StockSharp.ConnectorType" => "Use Rithmic, IQFeed, CQG, InteractiveBrokers, or Custom with AdapterType",
            var p when p.Contains("Symbol") => "Symbol must be 1-20 uppercase characters",
            var p when p.Contains("DepthLevels") => "Depth levels should be between 1 and 50",
            _ => null
        };
    }
}

/// <summary>
/// Semantic validation that checks cross-property constraints and configuration consistency.
/// Duplicate-symbol and retention-days checks are now handled by <see cref="AppConfigValidator"/>
/// in the field validation stage. This stage handles warning-level checks that should not
/// affect <see cref="FluentValidation.Results.ValidationResult.IsValid"/>.
/// </summary>
public sealed class SemanticValidationStage : IConfigValidationStage
{
    public IReadOnlyList<ConfigValidationResult> Validate(AppConfig config)
    {
        var results = new List<ConfigValidationResult>();

        // Warn if symbols are configured but none have subscriptions enabled
        if (config.Symbols is { Length: > 0 } &&
            !config.Symbols.Any(s => s.SubscribeTrades || s.SubscribeDepth))
        {
            results.Add(new ConfigValidationResult(
                ConfigValidationSeverity.Warning,
                "Symbols",
                "No symbols have trades or depth subscriptions enabled",
                "Enable SubscribeTrades or SubscribeDepth for at least one symbol"));
        }

        return results;
    }
}
