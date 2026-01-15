using DataIngestion.Contracts.Messages;
using DataIngestion.ValidationService.Configuration;
using Serilog;

namespace DataIngestion.ValidationService.Services;

public interface IDataValidator
{
    ValidationResult ValidateTrade(TradeValidationData trade);
    ValidationResult ValidateQuote(QuoteValidationData quote);
    ValidationResult ValidateOrderBook(OrderBookValidationData orderBook);
}

public record TradeValidationData(
    string Symbol, DateTimeOffset Timestamp, decimal Price, long Size,
    string? AggressorSide, decimal? PreviousPrice);

public record QuoteValidationData(
    string Symbol, DateTimeOffset Timestamp,
    decimal BidPrice, long BidSize, decimal AskPrice, long AskSize);

public record OrderBookValidationData(
    string Symbol, DateTimeOffset Timestamp,
    IReadOnlyList<(decimal Price, long Size)> Bids,
    IReadOnlyList<(decimal Price, long Size)> Asks);

public record ValidationResult(bool IsValid, List<ValidationIssue> Issues);

public sealed class DataValidator : IDataValidator
{
    private readonly ValidationServiceConfig _config;
    private readonly ValidationMetrics _metrics;
    private readonly Serilog.ILogger _log = Log.ForContext<DataValidator>();

    public DataValidator(ValidationServiceConfig config, ValidationMetrics metrics)
    {
        _config = config;
        _metrics = metrics;
    }

    public ValidationResult ValidateTrade(TradeValidationData trade)
    {
        var issues = new List<ValidationIssue>();
        var rules = _config.TradeRules;

        // Price validation
        if (trade.Price <= 0)
            issues.Add(new("TRADE_PRICE_ZERO", "Price must be positive", ValidationSeverity.Error));
        else if (trade.Price < rules.MinPrice)
            issues.Add(new("TRADE_PRICE_LOW", $"Price below minimum: {trade.Price}", ValidationSeverity.Warning));
        else if (trade.Price > rules.MaxPrice)
            issues.Add(new("TRADE_PRICE_HIGH", $"Price above maximum: {trade.Price}", ValidationSeverity.Error));

        // Size validation
        if (trade.Size <= 0)
            issues.Add(new("TRADE_SIZE_ZERO", "Size must be positive", ValidationSeverity.Error));
        else if (trade.Size > rules.MaxSize)
            issues.Add(new("TRADE_SIZE_HIGH", $"Size above maximum: {trade.Size}", ValidationSeverity.Warning));

        // Timestamp validation
        var now = DateTimeOffset.UtcNow;
        if (trade.Timestamp > now.AddSeconds(rules.MaxFutureTimestampSeconds))
            issues.Add(new("TRADE_FUTURE_TS", "Timestamp is in the future", ValidationSeverity.Error));
        if (trade.Timestamp < now.AddDays(-rules.MaxPastTimestampDays))
            issues.Add(new("TRADE_OLD_TS", "Timestamp is too old", ValidationSeverity.Warning));

        // Price change validation
        if (trade.PreviousPrice.HasValue && trade.PreviousPrice > 0)
        {
            var changePercent = Math.Abs((trade.Price - trade.PreviousPrice.Value) / trade.PreviousPrice.Value * 100);
            if (changePercent > rules.MaxPriceChangePercent)
                issues.Add(new("TRADE_PRICE_SPIKE", $"Price change {changePercent:F2}% exceeds threshold", ValidationSeverity.Warning));
        }

        _metrics.RecordValidation(issues.Count == 0);
        return new ValidationResult(issues.All(i => i.Severity != ValidationSeverity.Error), issues);
    }

    public ValidationResult ValidateQuote(QuoteValidationData quote)
    {
        var issues = new List<ValidationIssue>();
        var rules = _config.QuoteRules;

        // Crossed/locked check
        if (quote.BidPrice > quote.AskPrice)
        {
            issues.Add(new("QUOTE_CROSSED", "Bid price exceeds ask price",
                rules.RejectCrossedQuotes ? ValidationSeverity.Error : ValidationSeverity.Warning));
        }
        else if (quote.BidPrice == quote.AskPrice)
        {
            issues.Add(new("QUOTE_LOCKED", "Bid equals ask (locked)",
                rules.RejectLockedQuotes ? ValidationSeverity.Error : ValidationSeverity.Warning));
        }

        // Spread validation
        if (quote.BidPrice > 0 && quote.AskPrice > quote.BidPrice)
        {
            var spread = quote.AskPrice - quote.BidPrice;
            var midPrice = (quote.BidPrice + quote.AskPrice) / 2;
            var spreadBps = spread / midPrice * 10000;

            if (spreadBps > rules.MaxSpreadBps)
                issues.Add(new("QUOTE_WIDE_SPREAD", $"Spread {spreadBps:F0}bps exceeds threshold", ValidationSeverity.Warning));
        }

        // Size validation
        if (quote.BidSize <= 0)
            issues.Add(new("QUOTE_BID_SIZE_ZERO", "Bid size must be positive", ValidationSeverity.Warning));
        if (quote.AskSize <= 0)
            issues.Add(new("QUOTE_ASK_SIZE_ZERO", "Ask size must be positive", ValidationSeverity.Warning));

        _metrics.RecordValidation(issues.Count == 0);
        return new ValidationResult(issues.All(i => i.Severity != ValidationSeverity.Error), issues);
    }

    public ValidationResult ValidateOrderBook(OrderBookValidationData orderBook)
    {
        var issues = new List<ValidationIssue>();
        var rules = _config.OrderBookRules;

        // Level count validation
        var totalLevels = orderBook.Bids.Count + orderBook.Asks.Count;
        if (totalLevels < rules.MinLevels)
            issues.Add(new("OB_TOO_FEW_LEVELS", $"Only {totalLevels} levels", ValidationSeverity.Warning));
        if (orderBook.Bids.Count > rules.MaxLevels || orderBook.Asks.Count > rules.MaxLevels)
            issues.Add(new("OB_TOO_MANY_LEVELS", "Exceeds max levels", ValidationSeverity.Warning));

        // Sort order validation
        if (rules.RequireSortedLevels)
        {
            for (int i = 1; i < orderBook.Bids.Count; i++)
            {
                if (orderBook.Bids[i].Price > orderBook.Bids[i - 1].Price)
                {
                    issues.Add(new("OB_BIDS_UNSORTED", "Bids not in descending order", ValidationSeverity.Error));
                    break;
                }
            }
            for (int i = 1; i < orderBook.Asks.Count; i++)
            {
                if (orderBook.Asks[i].Price < orderBook.Asks[i - 1].Price)
                {
                    issues.Add(new("OB_ASKS_UNSORTED", "Asks not in ascending order", ValidationSeverity.Error));
                    break;
                }
            }
        }

        // Crossed book check
        if (orderBook.Bids.Count > 0 && orderBook.Asks.Count > 0)
        {
            if (orderBook.Bids[0].Price >= orderBook.Asks[0].Price)
                issues.Add(new("OB_CROSSED", "Order book is crossed", ValidationSeverity.Error));
        }

        _metrics.RecordValidation(issues.Count == 0);
        return new ValidationResult(issues.All(i => i.Severity != ValidationSeverity.Error), issues);
    }
}
