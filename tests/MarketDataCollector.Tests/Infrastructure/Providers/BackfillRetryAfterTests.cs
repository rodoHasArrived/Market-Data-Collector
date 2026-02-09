using System.Net.Http;
using MarketDataCollector.Infrastructure.Providers.Backfill;
using Xunit;

namespace MarketDataCollector.Tests.Infrastructure.Providers;

/// <summary>
/// Unit tests for Retry-After header parsing in BackfillWorkerService.
/// Part of improvement #17.
/// </summary>
public sealed class BackfillRetryAfterTests
{
    [Fact]
    public void TryExtractRetryAfter_NoRetryAfterInMessage_ReturnsNull()
    {
        var ex = new Exception("HTTP 429 Too Many Requests");
        var result = BackfillWorkerService.TryExtractRetryAfter(ex);
        Assert.Null(result);
    }

    [Fact]
    public void TryExtractRetryAfter_DeltaSeconds_ReturnsTimeSpan()
    {
        var ex = new Exception("Rate limited. Retry-After: 120");
        var result = BackfillWorkerService.TryExtractRetryAfter(ex);
        Assert.NotNull(result);
        Assert.Equal(120, result.Value.TotalSeconds, 1);
    }

    [Fact]
    public void TryExtractRetryAfter_LargeValue_CappedAt5Minutes()
    {
        var ex = new Exception("Rate limited. Retry-After: 600");
        var result = BackfillWorkerService.TryExtractRetryAfter(ex);
        Assert.NotNull(result);
        Assert.Equal(300, result.Value.TotalSeconds, 1); // Capped at 300s (5 min)
    }

    [Fact]
    public void TryExtractRetryAfter_NestedHttpException_Extracts()
    {
        var inner = new HttpRequestException("HTTP 429. Retry-After: 30");
        var outer = new Exception("Backfill request failed", inner);
        var result = BackfillWorkerService.TryExtractRetryAfter(outer);
        Assert.NotNull(result);
        Assert.Equal(30, result.Value.TotalSeconds, 1);
    }

    [Fact]
    public void TryExtractRetryAfter_CaseInsensitive()
    {
        var ex = new Exception("retry-after: 45");
        var result = BackfillWorkerService.TryExtractRetryAfter(ex);
        Assert.NotNull(result);
        Assert.Equal(45, result.Value.TotalSeconds, 1);
    }

    [Fact]
    public void TryExtractRetryAfter_ZeroOrNegative_ReturnsNull()
    {
        var ex = new Exception("Retry-After: 0");
        var result = BackfillWorkerService.TryExtractRetryAfter(ex);
        Assert.Null(result);
    }

    [Fact]
    public void TryExtractRetryAfter_NonNumericNonDate_ReturnsNull()
    {
        var ex = new Exception("Retry-After: invalid");
        var result = BackfillWorkerService.TryExtractRetryAfter(ex);
        Assert.Null(result);
    }
}
