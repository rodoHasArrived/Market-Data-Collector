using FluentAssertions;
using MarketDataCollector.Storage.Export;
using Xunit;

namespace MarketDataCollector.Tests.Storage;

/// <summary>
/// Tests for export preview (improvement 10.5) and data lineage manifest (improvement 11.1).
/// </summary>
public sealed class ExportPreviewAndLineageTests
{
    [Fact]
    public void ExportPreview_DefaultValues_AreCorrect()
    {
        var preview = new ExportPreview();

        preview.ProfileId.Should().BeEmpty();
        preview.Symbols.Should().BeEmpty();
        preview.EventTypes.Should().BeEmpty();
        preview.EstimatedRecords.Should().Be(0);
        preview.SampleData.Should().BeEmpty();
    }

    [Fact]
    public void ExportLineageManifest_DefaultVersion_IsCorrect()
    {
        var manifest = new ExportLineageManifest();

        manifest.CollectorVersion.Should().Be("1.6.2");
        manifest.GeneratedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        manifest.SourceProviders.Should().BeEmpty();
        manifest.Transformations.Should().BeEmpty();
        manifest.QualityChecks.Should().BeEmpty();
    }

    [Fact]
    public void ExportLineageManifest_WithProviders_TracksLineage()
    {
        var manifest = new ExportLineageManifest
        {
            ExportJobId = "test-job-123",
            Symbols = new[] { "AAPL", "SPY" },
            RecordCount = 10000,
            SourceProviders = new List<LineageProvider>
            {
                new() { Name = "alpaca", Type = "streaming", RecordCount = 5000 },
                new() { Name = "stooq", Type = "historical", RecordCount = 5000 }
            },
            Transformations = new List<string>
            {
                "Raw JSONL source ingestion",
                "Format conversion: JSONL -> Parquet",
                "Compression: snappy"
            },
            QualityChecks = new List<LineageQualityCheck>
            {
                new() { Check = "Record count validation", Passed = true, Details = "10,000 records" }
            }
        };

        manifest.SourceProviders.Should().HaveCount(2);
        manifest.Transformations.Should().HaveCount(3);
        manifest.QualityChecks.Should().ContainSingle()
            .Which.Passed.Should().BeTrue();
    }

    [Fact]
    public void LineagePipeline_DefaultValues()
    {
        var pipeline = new LineagePipeline();

        pipeline.StorageFormat.Should().Be("jsonl");
        pipeline.CompressionProfile.Should().Be("standard");
        pipeline.WalEnabled.Should().BeFalse();
        pipeline.DeduplicationEnabled.Should().BeFalse();
    }

    [Fact]
    public void ExportResult_IncludesLineageManifestPath()
    {
        var result = ExportResult.CreateSuccess("python-pandas", "/tmp/exports");
        result.LineageManifestPath.Should().BeNull("lineage not generated yet");

        result.LineageManifestPath = "/tmp/exports/lineage_manifest.json";
        result.LineageManifestPath.Should().EndWith("lineage_manifest.json");
    }

    [Fact]
    public void ExportPreview_PreviewAsync_NoSourceFiles_ReturnsEmpty()
    {
        // Test the service with a non-existent data root
        var service = new AnalysisExportService("/nonexistent/path");

        var request = new ExportRequest
        {
            ProfileId = "python-pandas",
            Symbols = new[] { "AAPL" },
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow,
            OutputDirectory = string.Empty
        };

        var preview = service.PreviewAsync(request).GetAwaiter().GetResult();

        preview.Should().NotBeNull();
        preview.EstimatedRecords.Should().Be(0);
        preview.SourceFileCount.Should().Be(0);
        preview.Symbols.Should().BeEmpty();
    }
}
