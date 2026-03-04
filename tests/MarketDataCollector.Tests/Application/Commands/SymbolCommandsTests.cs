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
        // --symbols-add without a value should return 2 (validation error)
        var result = await cmd.ExecuteAsync(new[] { "--symbols-add" });
        result.ExitCode.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_RemoveWithoutValue_ReturnsError()
    {
        var cmd = CreateCommandWithStubService();
        var result = await cmd.ExecuteAsync(new[] { "--symbols-remove" });
        result.ExitCode.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_StatusWithoutValue_ReturnsError()
    {
        var cmd = CreateCommandWithStubService();
        var result = await cmd.ExecuteAsync(new[] { "--symbol-status" });
        result.ExitCode.Should().Be(2);
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

    #region ParseSymbolFile Tests

    [Fact]
    public void ParseSymbolFile_OnePerLine_ParsesCorrectly()
    {
        using var tmp = TempFile.Create("AAPL\nMSFT\nGOOGL\n");
        var result = SymbolCommands.ParseSymbolFile(tmp.Path);
        result.Should().BeEquivalentTo(new[] { "AAPL", "MSFT", "GOOGL" });
    }

    [Fact]
    public void ParseSymbolFile_CommaSeparated_ParsesCorrectly()
    {
        using var tmp = TempFile.Create("AAPL,MSFT,GOOGL");
        var result = SymbolCommands.ParseSymbolFile(tmp.Path);
        result.Should().BeEquivalentTo(new[] { "AAPL", "MSFT", "GOOGL" });
    }

    [Fact]
    public void ParseSymbolFile_CsvWithHeader_ExtractsFirstColumn()
    {
        using var tmp = TempFile.Create("Symbol,Name,Exchange\nAAPL,Apple Inc.,NASDAQ\nMSFT,Microsoft,NASDAQ\n");
        var result = SymbolCommands.ParseSymbolFile(tmp.Path);
        result.Should().BeEquivalentTo(new[] { "AAPL", "MSFT" });
    }

    [Fact]
    public void ParseSymbolFile_SkipsComments()
    {
        using var tmp = TempFile.Create("# This is a comment\nAAPL\n# Another comment\nMSFT\n");
        var result = SymbolCommands.ParseSymbolFile(tmp.Path);
        result.Should().BeEquivalentTo(new[] { "AAPL", "MSFT" });
    }

    [Fact]
    public void ParseSymbolFile_SkipsEmptyLines()
    {
        using var tmp = TempFile.Create("AAPL\n\n\nMSFT\n  \nGOOGL\n");
        var result = SymbolCommands.ParseSymbolFile(tmp.Path);
        result.Should().BeEquivalentTo(new[] { "AAPL", "MSFT", "GOOGL" });
    }

    [Fact]
    public void ParseSymbolFile_InvalidSymbols_AreExcluded()
    {
        using var tmp = TempFile.Create("AAPL\nINVALID SYMBOL WITH SPACES\n@#$%\nMSFT\n");
        var result = SymbolCommands.ParseSymbolFile(tmp.Path);
        result.Should().BeEquivalentTo(new[] { "AAPL", "MSFT" });
    }

    [Fact]
    public void ParseSymbolFile_Deduplicates_CaseInsensitive()
    {
        using var tmp = TempFile.Create("AAPL\naapl\nAapl\nMSFT\n");
        var result = SymbolCommands.ParseSymbolFile(tmp.Path);
        result.Should().HaveCount(2);
        result.Should().Contain("AAPL");
        result.Should().Contain("MSFT");
    }

    [Fact]
    public void ParseSymbolFile_NormalizesToUpperCase()
    {
        using var tmp = TempFile.Create("aapl\nmsft\n");
        var result = SymbolCommands.ParseSymbolFile(tmp.Path);
        result.Should().BeEquivalentTo(new[] { "AAPL", "MSFT" });
    }

    [Fact]
    public void ParseSymbolFile_EmptyFile_ReturnsEmpty()
    {
        using var tmp = TempFile.Create("");
        var result = SymbolCommands.ParseSymbolFile(tmp.Path);
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseSymbolFile_SymbolsWithDotsAndSlashes_AreValid()
    {
        using var tmp = TempFile.Create("BRK.B\nBTC/USD\nSPY-P\n");
        var result = SymbolCommands.ParseSymbolFile(tmp.Path);
        result.Should().BeEquivalentTo(new[] { "BRK.B", "BTC/USD", "SPY-P" });
    }

    [Fact]
    public void ParseSymbolFile_SymbolTooLong_IsExcluded()
    {
        var longSymbol = new string('A', 21);
        using var tmp = TempFile.Create($"AAPL\n{longSymbol}\nMSFT\n");
        var result = SymbolCommands.ParseSymbolFile(tmp.Path);
        result.Should().BeEquivalentTo(new[] { "AAPL", "MSFT" });
    }

    [Fact]
    public void ParseSymbolFile_CommaSeparatedOnMultipleLines_ParsesAll()
    {
        using var tmp = TempFile.Create("AAPL,MSFT\nGOOGL,AMZN\n");
        var result = SymbolCommands.ParseSymbolFile(tmp.Path);
        result.Should().BeEquivalentTo(new[] { "AAPL", "MSFT", "GOOGL", "AMZN" });
    }

    [Fact]
    public void ParseSymbolFile_CsvWithTickerHeader_ExtractsFirstColumn()
    {
        using var tmp = TempFile.Create("Ticker,Name,Exchange\nAAPL,Apple Inc.,NASDAQ\nMSFT,Microsoft,NASDAQ\n");
        var result = SymbolCommands.ParseSymbolFile(tmp.Path);
        result.Should().BeEquivalentTo(new[] { "AAPL", "MSFT" });
    }

    [Fact]
    public void CanHandle_WithSymbolsImportFlag_ReturnsTrue()
    {
        var cmd = CreateCommandWithStubService();
        cmd.CanHandle(new[] { "--symbols-import" }).Should().BeTrue();
    }

    // Disposable helper so callers can delete the temp file after the test completes.
    private sealed class TempFile : IDisposable
    {
        public string Path { get; }

        private TempFile(string path) => Path = path;

        public static TempFile Create(string content)
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mdc-test-symbols-{Guid.NewGuid():N}.txt");
            File.WriteAllText(path, content);
            return new TempFile(path);
        }

        public void Dispose()
        {
            try { File.Delete(Path); } catch (IOException) { }
        }
    }

    #endregion

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
