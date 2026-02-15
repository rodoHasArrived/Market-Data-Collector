using FluentAssertions;
using MarketDataCollector.Infrastructure.Providers.InteractiveBrokers;
using Xunit;

namespace MarketDataCollector.Tests.Infrastructure.Providers;

/// <summary>
/// Unit tests for IBApiLimits, IBTickTypes, IBGenericTickTypes, IBDurationStrings,
/// IBBarSizes, IBWhatToShow, IBTickByTickTypes, IBErrorCodeMap, and IBApiError.
/// Covers B3 tranche 2 from the project roadmap — IB provider behavior validation.
/// </summary>
public sealed class IBApiLimitsTests
{
    #region Connection Limits

    [Fact]
    public void MaxClientsPerTWS_Is32()
    {
        IBApiLimits.MaxClientsPerTWS.Should().Be(32);
    }

    [Fact]
    public void MaxMessagesPerSecond_Is50()
    {
        IBApiLimits.MaxMessagesPerSecond.Should().Be(50);
    }

    #endregion

    #region Market Data Limits

    [Fact]
    public void DefaultMarketDataLines_Is100()
    {
        IBApiLimits.DefaultMarketDataLines.Should().Be(100);
    }

    [Fact]
    public void DepthSubscriptionLimits_AreReasonable()
    {
        IBApiLimits.MinDepthSubscriptions.Should().Be(3);
        IBApiLimits.MaxDepthSubscriptions.Should().Be(60);
        IBApiLimits.MinDepthSubscriptions.Should().BeLessThan(IBApiLimits.MaxDepthSubscriptions);
    }

    #endregion

    #region Historical Data Limits

    [Fact]
    public void MaxConcurrentHistoricalRequests_Is50()
    {
        IBApiLimits.MaxConcurrentHistoricalRequests.Should().Be(50);
    }

    [Fact]
    public void MaxHistoricalRequestsPer10Min_Is60()
    {
        IBApiLimits.MaxHistoricalRequestsPer10Min.Should().Be(60);
    }

    [Fact]
    public void HistoricalRequestWindow_Is10Minutes()
    {
        IBApiLimits.HistoricalRequestWindow.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void MinSecondsBetweenIdenticalRequests_Is15()
    {
        IBApiLimits.MinSecondsBetweenIdenticalRequests.Should().Be(15);
    }

    [Fact]
    public void BidAskRequestWeight_Is2()
    {
        IBApiLimits.BidAskRequestWeight.Should().Be(2);
    }

    [Fact]
    public void MaxHistoricalTicksPerRequest_Is1000()
    {
        IBApiLimits.MaxHistoricalTicksPerRequest.Should().Be(1000);
    }

    #endregion

    #region Connection Ports

    [Theory]
    [InlineData(7496, "TWS Live")]
    [InlineData(7497, "TWS Paper")]
    [InlineData(4001, "Gateway Live")]
    [InlineData(4002, "Gateway Paper")]
    public void ConnectionPorts_HaveExpectedValues(int expectedPort, string description)
    {
        _ = description; // Used for test readability
        var port = expectedPort switch
        {
            7496 => IBApiLimits.TwsLivePort,
            7497 => IBApiLimits.TwsPaperPort,
            4001 => IBApiLimits.GatewayLivePort,
            4002 => IBApiLimits.GatewayPaperPort,
            _ => throw new ArgumentException($"Unknown port {expectedPort}")
        };
        port.Should().Be(expectedPort);
    }

    [Fact]
    public void PaperPorts_DifferFromLivePorts()
    {
        IBApiLimits.TwsPaperPort.Should().NotBe(IBApiLimits.TwsLivePort);
        IBApiLimits.GatewayPaperPort.Should().NotBe(IBApiLimits.GatewayLivePort);
    }

    #endregion

    #region Error Codes

    [Fact]
    public void ErrorCodes_HaveExpectedValues()
    {
        IBApiLimits.ErrorHistoricalDataService.Should().Be(162);
        IBApiLimits.ErrorNoSecurityDefinition.Should().Be(200);
        IBApiLimits.ErrorMarketDataNotSubscribed.Should().Be(354);
        IBApiLimits.ErrorDelayedDataNotSubscribed.Should().Be(10167);
        IBApiLimits.ErrorCompetingLiveSession.Should().Be(10197);
    }

    #endregion
}

/// <summary>
/// Unit tests for IBTickTypes constants.
/// </summary>
public sealed class IBTickTypesTests
{
    [Fact]
    public void StandardTickTypes_HaveExpectedValues()
    {
        IBTickTypes.BidSize.Should().Be(0);
        IBTickTypes.BidPrice.Should().Be(1);
        IBTickTypes.AskPrice.Should().Be(2);
        IBTickTypes.AskSize.Should().Be(3);
        IBTickTypes.LastPrice.Should().Be(4);
        IBTickTypes.LastSize.Should().Be(5);
        IBTickTypes.High.Should().Be(6);
        IBTickTypes.Low.Should().Be(7);
        IBTickTypes.Volume.Should().Be(8);
        IBTickTypes.ClosePrice.Should().Be(9);
        IBTickTypes.Open.Should().Be(14);
    }

    [Fact]
    public void GenericTickTypes_HaveExpectedValues()
    {
        IBTickTypes.HistoricalVolatility.Should().Be(23);
        IBTickTypes.ImpliedVolatility.Should().Be(24);
        IBTickTypes.RTVolume.Should().Be(48);
        IBTickTypes.Shortable.Should().Be(46);
        IBTickTypes.TradeCount.Should().Be(54);
    }
}

/// <summary>
/// Unit tests for IBGenericTickTypes constants.
/// </summary>
public sealed class IBGenericTickTypesTests
{
    [Fact]
    public void GenericTickCodes_HaveExpectedValues()
    {
        IBGenericTickTypes.OptionVolume.Should().Be(100);
        IBGenericTickTypes.OptionOpenInterest.Should().Be(101);
        IBGenericTickTypes.HistoricalVolatility.Should().Be(104);
        IBGenericTickTypes.ImpliedVolatility.Should().Be(106);
        IBGenericTickTypes.RTVolume.Should().Be(233);
        IBGenericTickTypes.Shortable.Should().Be(236);
    }

    [Fact]
    public void DefaultEquityGenericTicks_ContainsRTVolumeAndShortable()
    {
        IBGenericTickTypes.DefaultEquityGenericTicks.Should().Contain("233");
        IBGenericTickTypes.DefaultEquityGenericTicks.Should().Contain("236");
    }

    [Fact]
    public void ComprehensiveEquityGenericTicks_ContainsAllUsefulTicks()
    {
        var ticks = IBGenericTickTypes.ComprehensiveEquityGenericTicks;
        ticks.Should().Contain("104"); // HistoricalVolatility
        ticks.Should().Contain("106"); // ImpliedVolatility
        ticks.Should().Contain("233"); // RTVolume
        ticks.Should().Contain("236"); // Shortable
        ticks.Should().Contain("456"); // Dividends
    }
}

/// <summary>
/// Unit tests for IBDurationStrings constants.
/// </summary>
public sealed class IBDurationStringsTests
{
    [Theory]
    [InlineData("60 S")]
    [InlineData("1800 S")]
    [InlineData("3600 S")]
    [InlineData("1 D")]
    [InlineData("5 D")]
    [InlineData("1 W")]
    [InlineData("1 M")]
    [InlineData("1 Y")]
    public void DurationStrings_HaveValidFormat(string expected)
    {
        var allDurations = new[]
        {
            IBDurationStrings.Seconds60,
            IBDurationStrings.Seconds1800,
            IBDurationStrings.Seconds3600,
            IBDurationStrings.Day1,
            IBDurationStrings.Days5,
            IBDurationStrings.Week1,
            IBDurationStrings.Month1,
            IBDurationStrings.Year1
        };

        allDurations.Should().Contain(expected);
    }
}

/// <summary>
/// Unit tests for IBBarSizes constants.
/// </summary>
public sealed class IBBarSizesTests
{
    [Theory]
    [InlineData("1 secs")]
    [InlineData("5 secs")]
    [InlineData("1 min")]
    [InlineData("5 mins")]
    [InlineData("1 hour")]
    [InlineData("1 day")]
    [InlineData("1 week")]
    [InlineData("1 month")]
    public void BarSizes_HaveValidFormat(string expected)
    {
        var allSizes = new[]
        {
            IBBarSizes.Secs1, IBBarSizes.Secs5, IBBarSizes.Secs10,
            IBBarSizes.Secs15, IBBarSizes.Secs30,
            IBBarSizes.Min1, IBBarSizes.Mins2, IBBarSizes.Mins3,
            IBBarSizes.Mins5, IBBarSizes.Mins10, IBBarSizes.Mins15,
            IBBarSizes.Mins20, IBBarSizes.Mins30,
            IBBarSizes.Hour1, IBBarSizes.Hours2, IBBarSizes.Hours3,
            IBBarSizes.Hours4, IBBarSizes.Hours8,
            IBBarSizes.Day1, IBBarSizes.Week1, IBBarSizes.Month1
        };

        allSizes.Should().Contain(expected);
    }
}

/// <summary>
/// Unit tests for IBWhatToShow constants.
/// </summary>
public sealed class IBWhatToShowTests
{
    [Fact]
    public void WhatToShow_ContainsAllExpectedValues()
    {
        IBWhatToShow.Trades.Should().Be("TRADES");
        IBWhatToShow.Midpoint.Should().Be("MIDPOINT");
        IBWhatToShow.Bid.Should().Be("BID");
        IBWhatToShow.Ask.Should().Be("ASK");
        IBWhatToShow.BidAsk.Should().Be("BID_ASK");
        IBWhatToShow.AdjustedLast.Should().Be("ADJUSTED_LAST");
        IBWhatToShow.HistoricalVolatility.Should().Be("HISTORICAL_VOLATILITY");
        IBWhatToShow.OptionImpliedVolatility.Should().Be("OPTION_IMPLIED_VOLATILITY");
    }
}

/// <summary>
/// Unit tests for IBErrorCodeMap.
/// </summary>
public sealed class IBErrorCodeMapTests
{
    [Theory]
    [InlineData(162, "Pacing violation")]
    [InlineData(200, "No security definition")]
    [InlineData(354, "not subscribed")]
    [InlineData(502, "connect")]
    [InlineData(504, "Not connected")]
    [InlineData(1100, "Connectivity lost")]
    public void GetErrorInfo_KnownCode_ReturnsDescription(int errorCode, string expectedSubstring)
    {
        var info = IBErrorCodeMap.GetErrorInfo(errorCode);

        info.Should().NotBeNull();
        info!.Description.Should().Contain(expectedSubstring, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetErrorInfo_UnknownCode_ReturnsNull()
    {
        var info = IBErrorCodeMap.GetErrorInfo(99999);
        info.Should().BeNull();
    }

    [Fact]
    public void GetAll_ReturnsNonEmptyDictionary()
    {
        var all = IBErrorCodeMap.GetAll();
        all.Should().NotBeEmpty();
        all.Should().ContainKey(162);
        all.Should().ContainKey(200);
        all.Should().ContainKey(354);
    }

    [Fact]
    public void FormatError_KnownCode_IncludesSeverity()
    {
        var formatted = IBErrorCodeMap.FormatError(502);

        formatted.Should().Contain("502");
        formatted.Should().Contain("Critical", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FormatError_UnknownCode_IncludesRawMessage()
    {
        var formatted = IBErrorCodeMap.FormatError(99999, "Something went wrong");

        formatted.Should().Contain("99999");
        formatted.Should().Contain("Something went wrong");
    }

    [Fact]
    public void InfoSeverity_MappedToCorrectCodes()
    {
        // Farm connection OK messages should be Info severity
        var info2104 = IBErrorCodeMap.GetErrorInfo(2104);
        info2104.Should().NotBeNull();
        info2104!.Severity.Should().Be(IBErrorSeverity.Info);

        var info2106 = IBErrorCodeMap.GetErrorInfo(2106);
        info2106.Should().NotBeNull();
        info2106!.Severity.Should().Be(IBErrorSeverity.Info);
    }

    [Fact]
    public void CriticalSeverity_MappedToConnectionErrors()
    {
        var err502 = IBErrorCodeMap.GetErrorInfo(502);
        err502!.Severity.Should().Be(IBErrorSeverity.Critical);

        var err504 = IBErrorCodeMap.GetErrorInfo(504);
        err504!.Severity.Should().Be(IBErrorSeverity.Critical);

        var err1100 = IBErrorCodeMap.GetErrorInfo(1100);
        err1100!.Severity.Should().Be(IBErrorSeverity.Critical);
    }
}

/// <summary>
/// Unit tests for IBApiError record.
/// </summary>
public sealed class IBApiErrorTests
{
    [Fact]
    public void IsPacingViolation_WithPacingErrorCode_ReturnsTrue()
    {
        var error = new IBApiError(1, IBApiLimits.ErrorPacingViolation, "Too many requests", null);
        error.IsPacingViolation.Should().BeTrue();
    }

    [Fact]
    public void IsPacingViolation_WithPacingMessageText_ReturnsTrue()
    {
        var error = new IBApiError(1, 999, "Historical data pacing violation", null);
        error.IsPacingViolation.Should().BeTrue();
    }

    [Fact]
    public void IsPacingViolation_WithUnrelatedError_ReturnsFalse()
    {
        var error = new IBApiError(1, 200, "No security definition found", null);
        error.IsPacingViolation.Should().BeFalse();
    }

    [Fact]
    public void IsMarketDataError_With354_ReturnsTrue()
    {
        var error = new IBApiError(1, IBApiLimits.ErrorMarketDataNotSubscribed, "Market data not subscribed", null);
        error.IsMarketDataError.Should().BeTrue();
    }

    [Fact]
    public void IsMarketDataError_With10167_ReturnsTrue()
    {
        var error = new IBApiError(1, IBApiLimits.ErrorDelayedDataNotSubscribed, "Delayed data not subscribed", null);
        error.IsMarketDataError.Should().BeTrue();
    }

    [Fact]
    public void IsSecurityNotFound_With200_ReturnsTrue()
    {
        var error = new IBApiError(1, IBApiLimits.ErrorNoSecurityDefinition, "No security definition found", null);
        error.IsSecurityNotFound.Should().BeTrue();
    }

    [Fact]
    public void IsSecurityNotFound_WithOtherCode_ReturnsFalse()
    {
        var error = new IBApiError(1, 354, "Market data not subscribed", null);
        error.IsSecurityNotFound.Should().BeFalse();
    }
}

/// <summary>
/// Unit tests for IB exception types.
/// </summary>
public sealed class IBExceptionTests
{
    [Fact]
    public void IBApiException_ContainsErrorCode()
    {
        var ex = new IBApiException(502, "Cannot connect to TWS");

        ex.ErrorCode.Should().Be(502);
        ex.Message.Should().Contain("502");
        ex.Message.Should().Contain("Cannot connect to TWS");
    }

    [Fact]
    public void IBApiException_WithInnerException_PreservesChain()
    {
        var inner = new TimeoutException("Connection timed out");
        var ex = new IBApiException(502, "Cannot connect to TWS", inner);

        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void IBPacingViolationException_HasRecommendedWait()
    {
        var ex = new IBPacingViolationException(162, "Pacing violation");

        ex.RecommendedWait.Should().Be(TimeSpan.FromSeconds(IBApiLimits.MinSecondsBetweenIdenticalRequests));
        ex.ErrorCode.Should().Be(162);
    }

    [Fact]
    public void IBMarketDataNotSubscribedException_StoresErrorCode()
    {
        var ex = new IBMarketDataNotSubscribedException(354, "Market data not subscribed");
        ex.ErrorCode.Should().Be(354);
    }

    [Fact]
    public void IBSecurityNotFoundException_StoresErrorCode()
    {
        var ex = new IBSecurityNotFoundException(200, "No security definition found");
        ex.ErrorCode.Should().Be(200);
    }
}
