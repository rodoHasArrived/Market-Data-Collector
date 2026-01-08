using DataIngestion.ValidationService.Services;
using Microsoft.AspNetCore.Mvc;

namespace DataIngestion.ValidationService.Controllers;

[ApiController]
[Route("api/v1/quality")]
public class QualityController : ControllerBase
{
    private readonly IQualityMetricsAggregator _aggregator;
    private readonly IDataValidator _validator;

    public QualityController(IQualityMetricsAggregator aggregator, IDataValidator validator)
    {
        _aggregator = aggregator;
        _validator = validator;
    }

    [HttpGet("symbols/{symbol}")]
    public IActionResult GetSymbolQuality(string symbol)
    {
        var snapshot = _aggregator.GetSnapshot(symbol);
        return Ok(snapshot);
    }

    [HttpGet("symbols")]
    public IActionResult GetAllQuality()
    {
        return Ok(_aggregator.GetAllSnapshots());
    }

    [HttpPost("validate/trade")]
    public IActionResult ValidateTrade([FromBody] TradeValidationRequest request)
    {
        var result = _validator.ValidateTrade(new TradeValidationData(
            request.Symbol, request.Timestamp, request.Price, request.Size, request.AggressorSide, request.PreviousPrice));
        return Ok(result);
    }

    [HttpPost("validate/quote")]
    public IActionResult ValidateQuote([FromBody] QuoteValidationRequest request)
    {
        var result = _validator.ValidateQuote(new QuoteValidationData(
            request.Symbol, request.Timestamp, request.BidPrice, request.BidSize, request.AskPrice, request.AskSize));
        return Ok(result);
    }
}

public record TradeValidationRequest(
    string Symbol, DateTimeOffset Timestamp, decimal Price, long Size,
    string? AggressorSide = null, decimal? PreviousPrice = null);

public record QuoteValidationRequest(
    string Symbol, DateTimeOffset Timestamp,
    decimal BidPrice, long BidSize, decimal AskPrice, long AskSize);
