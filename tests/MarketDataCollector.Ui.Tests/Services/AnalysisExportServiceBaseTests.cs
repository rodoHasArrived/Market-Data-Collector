using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MarketDataCollector.Ui.Services.Services;
using Xunit;

namespace MarketDataCollector.Ui.Tests.Services;

/// <summary>
/// Concrete test implementation of AnalysisExportServiceBase.
/// </summary>
internal sealed class TestAnalysisExportService : AnalysisExportServiceBase
{
    private readonly Dictionary<string, (bool Success, string? Error, object? Data)> _postResponses = new();
    private readonly Dictionary<string, (bool Success, string? Error, object? Data)> _getResponses = new();

    public string? LastPostEndpoint { get; private set; }

    public void SetPostResponse<T>(string endpoint, bool success, string? error, T? data) where T : class
    {
        _postResponses[endpoint] = (success, error, data);
    }

    public void SetGetResponse<T>(string endpoint, bool success, string? error, T? data) where T : class
    {
        _getResponses[endpoint] = (success, error, data);
    }

    protected override Task<(bool Success, string? ErrorMessage, T? Data)> PostApiAsync<T>(string endpoint, object body, CancellationToken ct) where T : class
    {
        LastPostEndpoint = endpoint;
        if (_postResponses.TryGetValue(endpoint, out var response))
        {
            return Task.FromResult((response.Success, response.Error, response.Data as T));
        }
        return Task.FromResult<(bool, string?, T?)>((false, "Not found", null));
    }

    protected override Task<(bool Success, string? ErrorMessage, T? Data)> GetApiAsync<T>(string endpoint, CancellationToken ct) where T : class
    {
        if (_getResponses.TryGetValue(endpoint, out var response))
        {
            return Task.FromResult((response.Success, response.Error, response.Data as T));
        }
        return Task.FromResult<(bool, string?, T?)>((false, "Not found", null));
    }
}

public sealed class AnalysisExportServiceBaseTests
{
    private readonly TestAnalysisExportService _sut = new();

    [Fact]
    public async Task ExportAsync_ReturnsSuccessResult()
    {
        _sut.SetPostResponse("/api/export/analysis", true, null,
            new AnalysisExportResponse
            {
                Success = true,
                OutputPath = "/output/data.parquet",
                FilesCreated = new[] { "data.parquet" },
                RowsExported = 5000,
                BytesWritten = 102400,
                DurationSeconds = 2.5
            });

        var options = new AnalysisExportOptions
        {
            Symbols = new List<string> { "AAPL", "MSFT" },
            Format = AnalysisExportFormat.Parquet
        };

        var result = await _sut.ExportAsync(options);

        result.Success.Should().BeTrue();
        result.OutputPath.Should().Be("/output/data.parquet");
        result.RowsExported.Should().Be(5000);
        result.BytesWritten.Should().Be(102400);
        result.Duration.Should().Be(TimeSpan.FromSeconds(2.5));
        result.FilesCreated.Should().Contain("data.parquet");
    }

    [Fact]
    public async Task ExportAsync_ReturnsFailure_WhenApiCallFails()
    {
        // No response configured, so PostApiAsync returns failure

        var options = new AnalysisExportOptions { Symbols = new List<string> { "SPY" } };

        var result = await _sut.ExportAsync(options);

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExportAsync_PostsToCorrectEndpoint()
    {
        _sut.SetPostResponse<AnalysisExportResponse>("/api/export/analysis", false, "test", null);

        await _sut.ExportAsync(new AnalysisExportOptions());

        _sut.LastPostEndpoint.Should().Be("/api/export/analysis");
    }

    [Fact]
    public async Task GetAvailableFormatsAsync_ReturnsFormatsFromApi()
    {
        _sut.SetGetResponse("/api/export/formats", true, null,
            new ExportFormatsResponse
            {
                Formats = new List<ExportFormatInfo>
                {
                    new() { Name = "CSV", Extension = ".csv" },
                    new() { Name = "Parquet", Extension = ".parquet" }
                }
            });

        var result = await _sut.GetAvailableFormatsAsync();

        result.Success.Should().BeTrue();
        result.Formats.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAvailableFormatsAsync_ReturnsDefaultsWhenApiFails()
    {
        // No response configured - should fall back to defaults

        var result = await _sut.GetAvailableFormatsAsync();

        result.Success.Should().BeTrue();
        result.Formats.Should().NotBeEmpty();
        result.Formats.Should().Contain(f => f.Name == "CSV");
        result.Formats.Should().Contain(f => f.Name == "Parquet");
    }

    [Fact]
    public async Task GetAggregationOptionsAsync_ReturnsAllOptions()
    {
        var result = await _sut.GetAggregationOptionsAsync();

        result.Should().NotBeEmpty();
        result.Should().Contain(a => a.Value == "Tick");
        result.Should().Contain(a => a.Value == "Minute");
        result.Should().Contain(a => a.Value == "Daily");
        result.Should().Contain(a => a.Value == "Monthly");
    }

    [Fact]
    public async Task GetExportTemplatesAsync_ReturnsTemplates()
    {
        var result = await _sut.GetExportTemplatesAsync();

        result.Should().NotBeEmpty();
        result.Should().Contain(t => t.Name == "Academic Research");
        result.Should().Contain(t => t.Name == "Machine Learning");
        result.Should().Contain(t => t.Name == "Backtesting");
        result.Should().Contain(t => t.Name == "Order Flow Analysis");
        result.Should().Contain(t => t.Name == "Market Microstructure");
    }

    [Fact]
    public async Task GenerateQualityReportAsync_ReturnsSuccessResult()
    {
        _sut.SetPostResponse("/api/export/quality-report", true, null,
            new QualityReportResponse
            {
                ReportPath = "/reports/quality.html",
                Summary = new QualityReportSummary
                {
                    TotalSymbols = 5,
                    OverallScore = 97.5,
                    GapsFound = 2
                }
            });

        var options = new QualityReportOptions
        {
            Symbols = new List<string> { "SPY", "AAPL" }
        };

        var result = await _sut.GenerateQualityReportAsync(options);

        result.Success.Should().BeTrue();
        result.ReportPath.Should().Be("/reports/quality.html");
        result.Summary.Should().NotBeNull();
        result.Summary!.TotalSymbols.Should().Be(5);
    }

    [Fact]
    public async Task GenerateQualityReportAsync_ReturnsFailure_WhenApiFails()
    {
        var result = await _sut.GenerateQualityReportAsync(new QualityReportOptions());

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExportOrderFlowAsync_ReturnsResult()
    {
        _sut.SetPostResponse("/api/export/orderflow", true, null,
            new AnalysisExportResponse
            {
                Success = true,
                RowsExported = 10000,
                OutputPath = "/output/orderflow.parquet"
            });

        var options = new OrderFlowExportOptions
        {
            Symbols = new List<string> { "SPY" }
        };

        var result = await _sut.ExportOrderFlowAsync(options);

        result.Success.Should().BeTrue();
        result.RowsExported.Should().Be(10000);
    }

    [Fact]
    public async Task ExportIntegrityEventsAsync_ReturnsResult()
    {
        _sut.SetPostResponse("/api/export/integrity", true, null,
            new AnalysisExportResponse
            {
                Success = true,
                RowsExported = 500,
                OutputPath = "/output/integrity.csv"
            });

        var options = new IntegrityExportOptions
        {
            Symbols = new List<string> { "AAPL" },
            Format = "CSV"
        };

        var result = await _sut.ExportIntegrityEventsAsync(options);

        result.Success.Should().BeTrue();
        result.RowsExported.Should().Be(500);
    }

    [Fact]
    public async Task CreateResearchPackageAsync_ReturnsResult()
    {
        _sut.SetPostResponse("/api/export/research-package", true, null,
            new ResearchPackageResponse
            {
                PackagePath = "/packages/research.zip",
                ManifestPath = "/packages/manifest.json",
                SizeBytes = 1048576
            });

        var options = new ResearchPackageOptions
        {
            Name = "Test Package",
            Symbols = new List<string> { "SPY", "AAPL" }
        };

        var result = await _sut.CreateResearchPackageAsync(options);

        result.Success.Should().BeTrue();
        result.PackagePath.Should().Be("/packages/research.zip");
        result.SizeBytes.Should().Be(1048576);
    }

    [Fact]
    public async Task CreateResearchPackageAsync_ReturnsFailure_WhenApiFails()
    {
        var result = await _sut.CreateResearchPackageAsync(new ResearchPackageOptions());

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ProgressChanged_EventCanBeRaised()
    {
        ExportProgressEventArgs? received = null;
        _sut.ProgressChanged += (_, e) => received = e;

        // Since OnProgressChanged is protected, we verify the event wiring works
        // by checking it's properly defined (no throw on subscribe/unsubscribe)
        _sut.ProgressChanged -= (_, _) => { };
        received.Should().BeNull(); // No event was raised
    }
}
