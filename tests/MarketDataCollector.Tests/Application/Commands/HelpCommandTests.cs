using FluentAssertions;
using MarketDataCollector.Application.Commands;
using Xunit;

namespace MarketDataCollector.Tests.Application.Commands;

/// <summary>
/// Tests for the HelpCommand CLI handler.
/// </summary>
public sealed class HelpCommandTests
{
    [Fact]
    public void CanHandle_WithHelpFlag_ReturnsTrue()
    {
        var cmd = new HelpCommand();
        cmd.CanHandle(new[] { "--help" }).Should().BeTrue();
    }

    [Fact]
    public void CanHandle_WithShortHelpFlag_ReturnsTrue()
    {
        var cmd = new HelpCommand();
        cmd.CanHandle(new[] { "-h" }).Should().BeTrue();
    }

    [Fact]
    public void CanHandle_CaseInsensitive_ReturnsTrue()
    {
        var cmd = new HelpCommand();
        cmd.CanHandle(new[] { "--HELP" }).Should().BeTrue();
    }

    [Fact]
    public void CanHandle_WithOtherFlags_ReturnsFalse()
    {
        var cmd = new HelpCommand();
        cmd.CanHandle(new[] { "--selftest" }).Should().BeFalse();
    }

    [Fact]
    public void CanHandle_EmptyArgs_ReturnsFalse()
    {
        var cmd = new HelpCommand();
        cmd.CanHandle(Array.Empty<string>()).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsZero()
    {
        var cmd = new HelpCommand();
        var exitCode = await cmd.ExecuteAsync(new[] { "--help" });
        exitCode.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithMixedArgs_ReturnsZero()
    {
        var cmd = new HelpCommand();
        var exitCode = await cmd.ExecuteAsync(new[] { "--help", "--verbose" });
        exitCode.Should().Be(0);
    }
}
