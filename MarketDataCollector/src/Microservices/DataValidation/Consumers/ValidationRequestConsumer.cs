using System.Text.Json;
using DataIngestion.Contracts.Messages;
using DataIngestion.ValidationService.Services;
using MassTransit;
using Serilog;

namespace DataIngestion.ValidationService.Consumers;

public sealed class ValidationRequestConsumer : IConsumer<IValidateIngestionData>
{
    private readonly IDataValidator _validator;
    private readonly IQualityMetricsAggregator _aggregator;
    private readonly Serilog.ILogger _log = Log.ForContext<ValidationRequestConsumer>();

    public ValidationRequestConsumer(IDataValidator validator, IQualityMetricsAggregator aggregator)
    {
        _validator = validator;
        _aggregator = aggregator;
    }

    public async Task Consume(ConsumeContext<IValidateIngestionData> context)
    {
        var msg = context.Message;

        DataIngestion.ValidationService.Services.ValidationResult result = msg.ValidationType switch
        {
            DataValidationType.Trade => ValidateTrade(msg),
            DataValidationType.Quote => ValidateQuote(msg),
            DataValidationType.OrderBook => ValidateOrderBook(msg),
            _ => new DataIngestion.ValidationService.Services.ValidationResult(true, new List<ValidationIssue>())
        };

        _aggregator.RecordValidation(msg.Symbol, msg.ValidationType.ToString(), result.IsValid, result.Issues);

        // Respond with validation result
        await context.RespondAsync<IDataValidationResult>(new DataValidationResultMessage
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = msg.CorrelationId,
            Timestamp = DateTimeOffset.UtcNow,
            Source = "ValidationService",
            SchemaVersion = 1,
            Symbol = msg.Symbol,
            ValidationType = msg.ValidationType,
            IsValid = result.IsValid,
            Issues = result.Issues,
            HighestSeverity = result.Issues.Count > 0
                ? result.Issues.Max(i => i.Severity)
                : ValidationSeverity.Info,
            ValidationDuration = TimeSpan.FromMilliseconds(1)
        });
    }

    private DataIngestion.ValidationService.Services.ValidationResult ValidateTrade(IValidateIngestionData msg)
    {
        try
        {
            var data = JsonSerializer.Deserialize<TradeData>(JsonSerializer.Serialize(msg.Data));
            if (data == null) return new DataIngestion.ValidationService.Services.ValidationResult(false, [new("PARSE_ERROR", "Failed to parse trade data", ValidationSeverity.Error)]);

            return _validator.ValidateTrade(new TradeValidationData(
                msg.Symbol, data.Timestamp, data.Price, data.Size, data.AggressorSide, null));
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error validating trade for {Symbol}", msg.Symbol);
            return new DataIngestion.ValidationService.Services.ValidationResult(false, [new("VALIDATION_ERROR", ex.Message, ValidationSeverity.Error)]);
        }
    }

    private DataIngestion.ValidationService.Services.ValidationResult ValidateQuote(IValidateIngestionData msg)
    {
        try
        {
            var data = JsonSerializer.Deserialize<QuoteData>(JsonSerializer.Serialize(msg.Data));
            if (data == null) return new DataIngestion.ValidationService.Services.ValidationResult(false, [new("PARSE_ERROR", "Failed to parse quote data", ValidationSeverity.Error)]);

            return _validator.ValidateQuote(new QuoteValidationData(
                msg.Symbol, data.Timestamp, data.BidPrice, data.BidSize, data.AskPrice, data.AskSize));
        }
        catch (Exception ex)
        {
            return new DataIngestion.ValidationService.Services.ValidationResult(false, [new("VALIDATION_ERROR", ex.Message, ValidationSeverity.Error)]);
        }
    }

    private DataIngestion.ValidationService.Services.ValidationResult ValidateOrderBook(IValidateIngestionData msg)
    {
        // Simplified order book validation
        return new DataIngestion.ValidationService.Services.ValidationResult(true, new List<ValidationIssue>());
    }

    private record TradeData(DateTimeOffset Timestamp, decimal Price, long Size, string? AggressorSide);
    private record QuoteData(DateTimeOffset Timestamp, decimal BidPrice, long BidSize, decimal AskPrice, long AskSize);
}

internal class DataValidationResultMessage : IDataValidationResult
{
    public Guid MessageId { get; init; }
    public Guid CorrelationId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public required string Source { get; init; }
    public int SchemaVersion { get; init; }
    public required string Symbol { get; init; }
    public DataValidationType ValidationType { get; init; }
    public bool IsValid { get; init; }
    public required IReadOnlyList<ValidationIssue> Issues { get; init; }
    public ValidationSeverity HighestSeverity { get; init; }
    public TimeSpan ValidationDuration { get; init; }
}
