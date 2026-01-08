using DataIngestion.TradeService.Configuration;
using DataIngestion.TradeService.Models;

namespace DataIngestion.TradeService.Services;

/// <summary>
/// Interface for trade validation.
/// </summary>
public interface ITradeValidator
{
    /// <summary>Validate a trade.</summary>
    ValidationResult Validate(ProcessedTrade trade);
}

/// <summary>
/// Trade validator implementation.
/// </summary>
public sealed class TradeValidator : ITradeValidator
{
    private readonly ValidationConfig _config;

    public TradeValidator(TradeServiceConfig config)
    {
        _config = config.Validation;
    }

    public ValidationResult Validate(ProcessedTrade trade)
    {
        var errors = new List<string>();

        // Symbol validation
        if (string.IsNullOrWhiteSpace(trade.Symbol))
        {
            errors.Add("Symbol is required");
        }

        // Price validation
        if (_config.ValidatePrice)
        {
            if (trade.Price <= 0)
            {
                errors.Add($"Price must be positive: {trade.Price}");
            }
            else if (trade.Price < _config.MinPrice)
            {
                errors.Add($"Price below minimum: {trade.Price} < {_config.MinPrice}");
            }
            else if (trade.Price > _config.MaxPrice)
            {
                errors.Add($"Price above maximum: {trade.Price} > {_config.MaxPrice}");
            }
        }

        // Size validation
        if (_config.ValidateSize)
        {
            if (trade.Size <= 0)
            {
                errors.Add($"Size must be positive: {trade.Size}");
            }
            else if (trade.Size > _config.MaxSize)
            {
                errors.Add($"Size above maximum: {trade.Size} > {_config.MaxSize}");
            }
        }

        // Timestamp validation
        if (trade.Timestamp == default)
        {
            errors.Add("Timestamp is required");
        }
        else if (trade.Timestamp > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            errors.Add($"Timestamp is in the future: {trade.Timestamp}");
        }
        else if (trade.Timestamp < DateTimeOffset.UtcNow.AddDays(-30))
        {
            errors.Add($"Timestamp is too old: {trade.Timestamp}");
        }

        // Aggressor side validation
        if (!string.IsNullOrEmpty(trade.AggressorSide))
        {
            var validSides = new[] { "buy", "sell", "unknown", "none" };
            if (!validSides.Contains(trade.AggressorSide.ToLowerInvariant()))
            {
                errors.Add($"Invalid aggressor side: {trade.AggressorSide}");
            }
        }

        return new ValidationResult(
            IsValid: errors.Count == 0,
            Errors: errors
        );
    }
}

/// <summary>
/// Validation result.
/// </summary>
public record ValidationResult(
    bool IsValid,
    List<string> Errors
);
