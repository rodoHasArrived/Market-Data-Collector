using FluentAssertions;
using MarketDataCollector.Application.Commands;
using Serilog;
using Xunit;

namespace MarketDataCollector.Tests.Application.Commands;

/// <summary>
/// Tests for the SymbolCommands CLI handler.
/// Validates argument parsing and routing for all symbol management subcommands.
/// </summary>
public sealed class SymbolCommandsTests
{
    private static readonly ILogger Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console()
        .CreateLogger();

    // Note: SymbolCommands requires a SymbolManagementService which needs a ConfigStore.
    // For CanHandle tests we can use a stub since CanHandle doesn't touch the service.
    // For ExecuteAsync tests that require validation (missing value), we need the real command.

    [Theory]
    [InlineData("--symbols")]
    [InlineData("--symbols-monitored")]
    [InlineData("--symbols-archived")]
    [InlineData("--symbols-add")]
    [InlineData("--symbols-remove")]
    [InlineData("--symbol-status")]
    public void CanHandle_WithSymbolFlag_ReturnsTrue(string flag)
    {
        // Use a minimal stub - CanHandle only checks args, not the service
        var cmd = CreateCommandWithStubService();
        cmd.CanHandle(new[] { flag }).Should().BeTrue();
    }

    [Theory]
    [InlineData("--SYMBOLS")]
    [InlineData("--Symbols-Add")]
    [InlineData("--SYMBOL-STATUS")]
    public void CanHandle_CaseInsensitive_ReturnsTrue(string flag)
    {
        var cmd = CreateCommandWithStubService();
        cmd.CanHandle(new[] { flag }).Should().BeTrue();
    }

    [Fact]
    public void CanHandle_WithNonSymbolFlag_ReturnsFalse()
    {
        var cmd = CreateCommandWithStubService();
        cmd.CanHandle(new[] { "--help" }).Should().BeFalse();
    }

    [Fact]
    public void CanHandle_EmptyArgs_ReturnsFalse()
    {
        var cmd = CreateCommandWithStubService();
        cmd.CanHandle(Array.Empty<string>()).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_AddWithoutValue_ReturnsError()
    {
        var cmd = CreateCommandWithStubService();
        // --symbols-add without a value should return 1
        var exitCode = await cmd.ExecuteAsync(new[] { "--symbols-add" });
        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_RemoveWithoutValue_ReturnsError()
    {
        var cmd = CreateCommandWithStubService();
        var exitCode = await cmd.ExecuteAsync(new[] { "--symbols-remove" });
        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_StatusWithoutValue_ReturnsError()
    {
        var cmd = CreateCommandWithStubService();
        var exitCode = await cmd.ExecuteAsync(new[] { "--symbol-status" });
        exitCode.Should().Be(1);
    }

    [Fact]
    public void FormatBytes_FormatsCorrectly()
    {
        SymbolCommands.FormatBytes(0).Should().Be("0 B");
        SymbolCommands.FormatBytes(1023).Should().Be("1023 B");
        SymbolCommands.FormatBytes(1024).Should().Be("1 KB");
        SymbolCommands.FormatBytes(1024 * 1024).Should().Be("1 MB");
        SymbolCommands.FormatBytes(1024L * 1024 * 1024).Should().Be("1 GB");
        SymbolCommands.FormatBytes(1024L * 1024 * 1024 * 1024).Should().Be("1 TB");
    }

    [Fact]
    public void FormatBytes_HandlesPartialValues()
    {
        SymbolCommands.FormatBytes(1536).Should().Be("1.5 KB");
    }

    /// <summary>
    /// Creates a SymbolCommands with a stub SymbolManagementService.
    /// Uses a temp directory as config path to avoid file I/O.
    /// </summary>
    private static SymbolCommands CreateCommandWithStubService()
    {
        // SymbolManagementService constructor requires a ConfigStore and dataRoot.
        // For CanHandle tests and validation-failure tests, this won't be called
        // or will fail gracefully, which is acceptable for these tests.
        var configStore = new MarketDataCollector.Application.UI.ConfigStore(
            Path.Combine(Path.GetTempPath(), $"mdc-test-{Guid.NewGuid()}.json"));
        var service = new MarketDataCollector.Application.Subscriptions.Services.SymbolManagementService(
            configStore, Path.GetTempPath(), Logger);
        return new SymbolCommands(service, Logger);
    }
}
