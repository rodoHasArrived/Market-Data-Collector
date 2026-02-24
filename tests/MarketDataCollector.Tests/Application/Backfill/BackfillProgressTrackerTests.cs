using FluentAssertions;
using MarketDataCollector.Application.Backfill;
using Xunit;

namespace MarketDataCollector.Tests.Application.Backfill;

/// <summary>
/// Tests for BackfillProgressTracker (improvement 9.2).
/// Verifies progress tracking, ETA calculation, and per-symbol breakdowns.
/// </summary>
public sealed class BackfillProgressTrackerTests
{
    private readonly BackfillProgressTracker _sut = new();

    [Fact]
    public void StartJob_ReturnsJobId()
    {
        var jobId = _sut.StartJob("stooq", new[] { "AAPL", "SPY" }, null, null);

        jobId.Should().StartWith("bf_");
        jobId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetProgress_NewJob_ShowsRunningStatus()
    {
        var jobId = _sut.StartJob("stooq", new[] { "AAPL" }, null, null);

        var progress = _sut.GetProgress(jobId);

        progress.Should().NotBeNull();
        progress!.Status.Should().Be("running");
        progress.TotalSymbols.Should().Be(1);
        progress.CompletedSymbols.Should().Be(0);
        progress.ProgressPercent.Should().Be(0);
    }

    [Fact]
    public void StartSymbol_UpdatesCurrentSymbol()
    {
        var jobId = _sut.StartJob("stooq", new[] { "AAPL", "SPY" }, null, null);
        _sut.StartSymbol(jobId, "AAPL");

        var progress = _sut.GetProgress(jobId);

        progress!.CurrentSymbol.Should().Be("AAPL");
    }

    [Fact]
    public void CompleteSymbol_UpdatesProgress()
    {
        var jobId = _sut.StartJob("stooq", new[] { "AAPL", "SPY" }, null, null);

        _sut.StartSymbol(jobId, "AAPL");
        _sut.CompleteSymbol(jobId, "AAPL", 252);

        var progress = _sut.GetProgress(jobId);

        progress!.CompletedSymbols.Should().Be(1);
        progress.ProgressPercent.Should().Be(50.0);
        progress.SymbolDetails.Should().Contain(s => s.Symbol == "AAPL" && s.Status == "completed");
    }

    [Fact]
    public void FailSymbol_TracksFailure()
    {
        var jobId = _sut.StartJob("stooq", new[] { "AAPL" }, null, null);

        _sut.StartSymbol(jobId, "AAPL");
        _sut.FailSymbol(jobId, "AAPL", "Rate limit exceeded");

        var progress = _sut.GetProgress(jobId);

        progress!.FailedSymbols.Should().Be(1);
        progress.SymbolDetails.Should().Contain(s =>
            s.Symbol == "AAPL" && s.Status == "failed" && s.Error == "Rate limit exceeded");
    }

    [Fact]
    public void CompleteJob_SetsCompletedStatus()
    {
        var jobId = _sut.StartJob("stooq", new[] { "AAPL" }, null, null);
        _sut.StartSymbol(jobId, "AAPL");
        _sut.CompleteSymbol(jobId, "AAPL", 252);
        _sut.CompleteJob(jobId, true);

        var progress = _sut.GetProgress(jobId);

        progress!.Status.Should().Be("completed");
        progress.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void RecordBars_AccumulatesBarsWritten()
    {
        var jobId = _sut.StartJob("stooq", new[] { "AAPL" }, null, null);
        _sut.StartSymbol(jobId, "AAPL");

        _sut.RecordBars(jobId, "AAPL", 100);
        _sut.RecordBars(jobId, "AAPL", 152);

        var progress = _sut.GetProgress(jobId);

        progress!.TotalBarsWritten.Should().Be(252);
    }

    [Fact]
    public void GetProgress_AfterCompletion_CalculatesEta()
    {
        var jobId = _sut.StartJob("stooq", new[] { "AAPL", "SPY", "QQQ" }, null, null);

        // Complete first symbol
        _sut.StartSymbol(jobId, "AAPL");
        _sut.CompleteSymbol(jobId, "AAPL", 252);

        var progress = _sut.GetProgress(jobId);

        // After 1 of 3 symbols, should estimate remaining time
        progress!.EstimatedRemainingSeconds.Should().NotBeNull();
        progress.EstimatedCompletionTime.Should().NotBeNull();
        progress.ProgressPercent.Should().BeApproximately(33.3, 0.1);
    }

    [Fact]
    public void GetAllProgress_ReturnsAllJobs()
    {
        var jobId1 = _sut.StartJob("stooq", new[] { "AAPL" }, null, null);
        var jobId2 = _sut.StartJob("alpaca", new[] { "SPY" }, null, null);

        var allProgress = _sut.GetAllProgress();

        allProgress.Should().HaveCount(2);
        allProgress.Select(p => p.JobId).Should().Contain(jobId1).And.Contain(jobId2);
    }

    [Fact]
    public void GetProgress_UnknownJobId_ReturnsNull()
    {
        var progress = _sut.GetProgress("nonexistent");
        progress.Should().BeNull();
    }

    [Fact]
    public void FullWorkflow_TracksEntireLifecycle()
    {
        var symbols = new[] { "AAPL", "SPY" };
        var jobId = _sut.StartJob("stooq", symbols,
            new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));

        // Symbol 1: AAPL
        _sut.StartSymbol(jobId, "AAPL");
        _sut.RecordBars(jobId, "AAPL", 252);
        _sut.CompleteSymbol(jobId, "AAPL", 252);

        // Symbol 2: SPY
        _sut.StartSymbol(jobId, "SPY");
        _sut.RecordBars(jobId, "SPY", 252);
        _sut.CompleteSymbol(jobId, "SPY", 252);

        _sut.CompleteJob(jobId, true);

        var progress = _sut.GetProgress(jobId);

        progress!.Status.Should().Be("completed");
        progress.TotalSymbols.Should().Be(2);
        progress.CompletedSymbols.Should().Be(2);
        progress.FailedSymbols.Should().Be(0);
        progress.TotalBarsWritten.Should().Be(504);
        progress.ProgressPercent.Should().Be(100.0);
    }
}
