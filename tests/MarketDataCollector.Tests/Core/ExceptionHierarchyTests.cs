using System.Net.Sockets;
using FluentAssertions;
using MarketDataCollector.Application.Exceptions;
using Xunit;

namespace MarketDataCollector.Tests.Core;

/// <summary>
/// Tests for the custom exception hierarchy (A7 improvement).
/// Validates that all exception types support proper exception chaining,
/// metadata preservation, and combined metadata+innerException constructors.
/// </summary>
public sealed class ExceptionHierarchyTests
{
    #region Base Exception

    [Fact]
    public void MarketDataCollectorException_MessageOnly()
    {
        var ex = new MarketDataCollectorException("test error");

        ex.Message.Should().Be("test error");
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void MarketDataCollectorException_WithInnerException()
    {
        var inner = new InvalidOperationException("root cause");
        var ex = new MarketDataCollectorException("wrapper", inner);

        ex.Message.Should().Be("wrapper");
        ex.InnerException.Should().BeSameAs(inner);
    }

    #endregion

    #region ConfigurationException

    [Fact]
    public void ConfigurationException_MetadataOnly()
    {
        var ex = new ConfigurationException("bad config", configPath: "/path/cfg.json", fieldName: "DataRoot");

        ex.Message.Should().Be("bad config");
        ex.ConfigPath.Should().Be("/path/cfg.json");
        ex.FieldName.Should().Be("DataRoot");
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void ConfigurationException_InnerExceptionOnly()
    {
        var inner = new IOException("file locked");
        var ex = new ConfigurationException("wrapped", inner);

        ex.Message.Should().Be("wrapped");
        ex.InnerException.Should().BeSameAs(inner);
        ex.ConfigPath.Should().BeNull();
        ex.FieldName.Should().BeNull();
    }

    [Fact]
    public void ConfigurationException_InnerExceptionAndMetadata()
    {
        var inner = new UnauthorizedAccessException("denied");
        var ex = new ConfigurationException("access error", inner, configPath: "/etc/app.json", fieldName: "ApiKey");

        ex.Message.Should().Be("access error");
        ex.InnerException.Should().BeSameAs(inner);
        ex.ConfigPath.Should().Be("/etc/app.json");
        ex.FieldName.Should().Be("ApiKey");
    }

    [Fact]
    public void ConfigurationException_IsMarketDataCollectorException()
    {
        var ex = new ConfigurationException("test");
        ex.Should().BeAssignableTo<MarketDataCollectorException>();
    }

    #endregion

    #region ConnectionException

    [Fact]
    public void ConnectionException_MetadataOnly()
    {
        var ex = new ConnectionException("conn failed", provider: "Alpaca", host: "stream.alpaca.markets", port: 443);

        ex.Provider.Should().Be("Alpaca");
        ex.Host.Should().Be("stream.alpaca.markets");
        ex.Port.Should().Be(443);
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void ConnectionException_InnerExceptionAndMetadata()
    {
        var inner = new SocketException();
        var ex = new ConnectionException("socket error", inner, provider: "Polygon", host: "ws.polygon.io", port: 443);

        ex.InnerException.Should().BeSameAs(inner);
        ex.Provider.Should().Be("Polygon");
        ex.Host.Should().Be("ws.polygon.io");
        ex.Port.Should().Be(443);
    }

    [Fact]
    public void ConnectionException_IsMarketDataCollectorException()
    {
        var ex = new ConnectionException("test");
        ex.Should().BeAssignableTo<MarketDataCollectorException>();
    }

    #endregion

    #region DataProviderException

    [Fact]
    public void DataProviderException_MetadataOnly()
    {
        var ex = new DataProviderException("api error", provider: "Tiingo", symbol: "SPY");

        ex.Provider.Should().Be("Tiingo");
        ex.Symbol.Should().Be("SPY");
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void DataProviderException_InnerExceptionAndMetadata()
    {
        var inner = new HttpRequestException("timeout");
        var ex = new DataProviderException("request failed", inner, provider: "Stooq", symbol: "AAPL");

        ex.InnerException.Should().BeSameAs(inner);
        ex.Provider.Should().Be("Stooq");
        ex.Symbol.Should().Be("AAPL");
    }

    [Fact]
    public void DataProviderException_IsMarketDataCollectorException()
    {
        var ex = new DataProviderException("test");
        ex.Should().BeAssignableTo<MarketDataCollectorException>();
    }

    #endregion

    #region RateLimitException

    [Fact]
    public void RateLimitException_MetadataOnly()
    {
        var ex = new RateLimitException(
            "rate limited",
            provider: "Polygon",
            symbol: "SPY",
            retryAfter: TimeSpan.FromSeconds(60),
            remainingRequests: 0,
            requestLimit: 200);

        ex.Provider.Should().Be("Polygon");
        ex.Symbol.Should().Be("SPY");
        ex.RetryAfter.Should().Be(TimeSpan.FromSeconds(60));
        ex.RemainingRequests.Should().Be(0);
        ex.RequestLimit.Should().Be(200);
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void RateLimitException_InnerExceptionAndMetadata()
    {
        var inner = new HttpRequestException("429");
        var ex = new RateLimitException(
            "too many requests",
            inner,
            provider: "Alpaca",
            symbol: "MSFT",
            retryAfter: TimeSpan.FromMinutes(1));

        ex.InnerException.Should().BeSameAs(inner);
        ex.Provider.Should().Be("Alpaca");
        ex.Symbol.Should().Be("MSFT");
        ex.RetryAfter.Should().Be(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void RateLimitException_IsDataProviderException()
    {
        var ex = new RateLimitException("test");
        ex.Should().BeAssignableTo<DataProviderException>();
        ex.Should().BeAssignableTo<MarketDataCollectorException>();
    }

    #endregion

    #region StorageException

    [Fact]
    public void StorageException_MetadataOnly()
    {
        var ex = new StorageException("write failed", path: "/data/live/SPY.jsonl");

        ex.Path.Should().Be("/data/live/SPY.jsonl");
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void StorageException_InnerExceptionAndMetadata()
    {
        var inner = new IOException("disk full");
        var ex = new StorageException("storage error", inner, path: "/data/archive");

        ex.InnerException.Should().BeSameAs(inner);
        ex.Path.Should().Be("/data/archive");
    }

    [Fact]
    public void StorageException_IsMarketDataCollectorException()
    {
        var ex = new StorageException("test");
        ex.Should().BeAssignableTo<MarketDataCollectorException>();
    }

    #endregion

    #region OperationTimeoutException

    [Fact]
    public void OperationTimeoutException_MetadataOnly()
    {
        var ex = new OperationTimeoutException(
            "timed out",
            operationName: "GetDailyBars",
            timeout: TimeSpan.FromSeconds(30),
            provider: "Tiingo");

        ex.OperationName.Should().Be("GetDailyBars");
        ex.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        ex.Provider.Should().Be("Tiingo");
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void OperationTimeoutException_InnerExceptionAndMetadata()
    {
        var inner = new TaskCanceledException("timed out");
        var ex = new OperationTimeoutException(
            "operation exceeded deadline",
            inner,
            operationName: "ConnectAsync",
            timeout: TimeSpan.FromSeconds(10),
            provider: "IB");

        ex.InnerException.Should().BeSameAs(inner);
        ex.OperationName.Should().Be("ConnectAsync");
        ex.Timeout.Should().Be(TimeSpan.FromSeconds(10));
        ex.Provider.Should().Be("IB");
    }

    [Fact]
    public void OperationTimeoutException_IsMarketDataCollectorException()
    {
        var ex = new OperationTimeoutException("test");
        ex.Should().BeAssignableTo<MarketDataCollectorException>();
    }

    #endregion

    #region SequenceValidationException

    [Fact]
    public void SequenceValidationException_MetadataOnly()
    {
        var ex = new SequenceValidationException(
            "gap detected",
            symbol: "SPY",
            expectedSequence: 100,
            actualSequence: 105,
            validationType: SequenceValidationType.Gap);

        ex.Symbol.Should().Be("SPY");
        ex.ExpectedSequence.Should().Be(100);
        ex.ActualSequence.Should().Be(105);
        ex.ValidationType.Should().Be(SequenceValidationType.Gap);
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void SequenceValidationException_InnerExceptionAndMetadata()
    {
        var inner = new InvalidOperationException("unexpected state");
        var ex = new SequenceValidationException(
            "sequence error",
            inner,
            symbol: "AAPL",
            expectedSequence: 50,
            actualSequence: 48,
            validationType: SequenceValidationType.OutOfOrder);

        ex.InnerException.Should().BeSameAs(inner);
        ex.Symbol.Should().Be("AAPL");
        ex.ExpectedSequence.Should().Be(50);
        ex.ActualSequence.Should().Be(48);
        ex.ValidationType.Should().Be(SequenceValidationType.OutOfOrder);
    }

    [Fact]
    public void SequenceValidationException_IsMarketDataCollectorException()
    {
        var ex = new SequenceValidationException("test");
        ex.Should().BeAssignableTo<MarketDataCollectorException>();
    }

    #endregion

    #region ValidationException

    [Fact]
    public void ValidationException_WithErrors()
    {
        var errors = new[]
        {
            new ValidationError("REQUIRED", "Symbol is required", "Symbol"),
            new ValidationError("INVALID", "Invalid date format", "From", "not-a-date")
        };

        var ex = new ValidationException("validation failed", errors, entityType: "BackfillRequest", entityId: "req-123");

        ex.Errors.Should().HaveCount(2);
        ex.Errors[0].Code.Should().Be("REQUIRED");
        ex.Errors[0].Field.Should().Be("Symbol");
        ex.Errors[1].AttemptedValue.Should().Be("not-a-date");
        ex.EntityType.Should().Be("BackfillRequest");
        ex.EntityId.Should().Be("req-123");
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void ValidationException_InnerExceptionAndMetadata()
    {
        var inner = new FormatException("bad format");
        var errors = new[] { new ValidationError("FORMAT", "Invalid format") };
        var ex = new ValidationException("validation error", inner, errors, entityType: "Config");

        ex.InnerException.Should().BeSameAs(inner);
        ex.Errors.Should().HaveCount(1);
        ex.EntityType.Should().Be("Config");
    }

    [Fact]
    public void ValidationException_InnerExceptionOnly_HasEmptyErrors()
    {
        var inner = new Exception("original");
        var ex = new ValidationException("wrapped", inner);

        ex.InnerException.Should().BeSameAs(inner);
        ex.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidationException_MessageOnly_HasEmptyErrors()
    {
        var ex = new ValidationException("simple");

        ex.Errors.Should().BeEmpty();
        ex.EntityType.Should().BeNull();
        ex.EntityId.Should().BeNull();
    }

    [Fact]
    public void ValidationException_IsMarketDataCollectorException()
    {
        var ex = new ValidationException("test");
        ex.Should().BeAssignableTo<MarketDataCollectorException>();
    }

    #endregion

    #region Exception Chain Depth

    [Fact]
    public void ExceptionChain_PreservesFullDepth()
    {
        var root = new IOException("disk error");
        var storage = new StorageException("write failed", root, path: "/data/file.jsonl");
        var provider = new DataProviderException("backfill failed", storage, provider: "Stooq", symbol: "SPY");

        provider.InnerException.Should().BeSameAs(storage);
        storage.InnerException.Should().BeSameAs(root);

        // Full chain accessible
        provider.InnerException!.InnerException.Should().BeSameAs(root);
        provider.InnerException!.InnerException!.Message.Should().Be("disk error");
    }

    #endregion
}
