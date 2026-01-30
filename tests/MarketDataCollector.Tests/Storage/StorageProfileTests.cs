using FluentAssertions;
using MarketDataCollector.Storage;
using Xunit;

namespace MarketDataCollector.Tests.Storage;

/// <summary>
/// Tests for storage profile functionality.
/// Storage profiles simplify configuration by pre-setting compression,
/// partitioning, manifests, and retention for common use cases.
/// </summary>
public class StorageProfileTests
{
    [Fact]
    public void GetPresets_ReturnsAllDefinedProfiles()
    {
        // Act
        var presets = StorageProfilePresets.GetPresets().ToList();

        // Assert
        presets.Should().HaveCount(3);
        presets.Select(p => p.Id).Should().Contain("Research");
        presets.Select(p => p.Id).Should().Contain("LowLatency");
        presets.Select(p => p.Id).Should().Contain("Archival");
    }

    [Theory]
    [InlineData("Research")]
    [InlineData("research")]
    [InlineData("RESEARCH")]
    public void ApplyProfile_Research_SetsExpectedDefaults(string profileName)
    {
        // Arrange
        var options = new StorageOptions { RootPath = "data" };

        // Act
        var result = StorageProfilePresets.ApplyProfile(profileName, options);

        // Assert
        result.RootPath.Should().Be("data"); // Preserves original value
        result.Compress.Should().BeTrue("Research profile enables compression");
        result.DatePartition.Should().Be(DatePartition.Daily);
        result.GenerateManifests.Should().BeTrue("Research profile enables manifests");
    }

    [Theory]
    [InlineData("LowLatency")]
    [InlineData("lowlatency")]
    [InlineData("LOWLATENCY")]
    public void ApplyProfile_LowLatency_SetsExpectedDefaults(string profileName)
    {
        // Arrange
        var options = new StorageOptions { RootPath = "data" };

        // Act
        var result = StorageProfilePresets.ApplyProfile(profileName, options);

        // Assert
        result.RootPath.Should().Be("data"); // Preserves original value
        result.Compress.Should().BeFalse("LowLatency profile disables compression for speed");
        result.PartitionStrategy.DateGranularity.Should().Be(DatePartition.Hourly);
        result.GenerateManifests.Should().BeFalse("LowLatency profile disables manifests for speed");
    }

    [Theory]
    [InlineData("Archival")]
    [InlineData("archival")]
    [InlineData("ARCHIVAL")]
    public void ApplyProfile_Archival_SetsExpectedDefaults(string profileName)
    {
        // Arrange
        var options = new StorageOptions { RootPath = "data" };

        // Act
        var result = StorageProfilePresets.ApplyProfile(profileName, options);

        // Assert
        result.RootPath.Should().Be("data"); // Preserves original value
        result.Compress.Should().BeTrue("Archival profile enables compression");
        result.DatePartition.Should().Be(DatePartition.Monthly);
        result.GenerateManifests.Should().BeTrue("Archival profile enables manifests with checksums");
        result.RetentionDays.Should().Be(3650, "Archival profile sets 10-year retention");
    }

    [Fact]
    public void ApplyProfile_NullOrEmpty_ReturnsOriginalOptions()
    {
        // Arrange
        var options = new StorageOptions
        {
            RootPath = "custom/path",
            Compress = false,
            NamingConvention = FileNamingConvention.ByDate
        };

        // Act
        var resultNull = StorageProfilePresets.ApplyProfile(null!, options);
        var resultEmpty = StorageProfilePresets.ApplyProfile("", options);
        var resultWhitespace = StorageProfilePresets.ApplyProfile("   ", options);

        // Assert - all should return unchanged options
        resultNull.Should().BeEquivalentTo(options);
        resultEmpty.Should().BeEquivalentTo(options);
        resultWhitespace.Should().BeEquivalentTo(options);
    }

    [Fact]
    public void ApplyProfile_UnknownProfile_ReturnsOriginalOptions()
    {
        // Arrange
        var options = new StorageOptions
        {
            RootPath = "custom/path",
            Compress = false
        };

        // Act
        var result = StorageProfilePresets.ApplyProfile("NonExistentProfile", options);

        // Assert
        result.Should().BeEquivalentTo(options);
    }

    [Fact]
    public void ApplyProfile_PreservesExplicitOverrides()
    {
        // Arrange
        var options = new StorageOptions
        {
            RootPath = "my/custom/path",
            NamingConvention = FileNamingConvention.ByType,
            FilePrefix = "custom_prefix_"
        };

        // Act
        var result = StorageProfilePresets.ApplyProfile("Research", options);

        // Assert - profile settings applied but explicit settings preserved
        result.RootPath.Should().Be("my/custom/path");
        result.NamingConvention.Should().Be(FileNamingConvention.ByType);
        result.FilePrefix.Should().Be("custom_prefix_");
    }

    [Fact]
    public void GetPresets_AllPresetsHaveRequiredMetadata()
    {
        // Act
        var presets = StorageProfilePresets.GetPresets();

        // Assert
        foreach (var preset in presets)
        {
            preset.Id.Should().NotBeNullOrWhiteSpace("Every preset must have an ID");
            preset.Label.Should().NotBeNullOrWhiteSpace("Every preset must have a label");
            preset.Description.Should().NotBeNullOrWhiteSpace("Every preset must have a description");
        }
    }

    [Fact]
    public void ApplyProfile_DoesNotMutateOriginalOptions()
    {
        // Arrange
        var original = new StorageOptions
        {
            RootPath = "original/path",
            Compress = false,
            DatePartition = DatePartition.None
        };

        // Act
        var result = StorageProfilePresets.ApplyProfile("Research", original);

        // Assert - original should be unchanged
        original.Compress.Should().BeFalse();
        original.DatePartition.Should().Be(DatePartition.None);

        // Result should have profile defaults
        result.Compress.Should().BeTrue();
        result.DatePartition.Should().Be(DatePartition.Daily);
    }

    [Fact]
    public void ResearchProfile_IsDefaultProfile()
    {
        // The Research profile is the recommended default for most users
        // This test documents that expectation

        // Arrange
        var options = new StorageOptions { RootPath = "data" };

        // Act - Apply Research profile (the default)
        var result = StorageProfilePresets.ApplyProfile("Research", options);

        // Assert - Research profile provides balanced defaults
        result.Compress.Should().BeTrue("Research profile enables compression for space efficiency");
        result.DatePartition.Should().Be(DatePartition.Daily, "Research profile uses daily partitions for analysis");
        result.GenerateManifests.Should().BeTrue("Research profile enables manifests for data discovery");
    }
}
