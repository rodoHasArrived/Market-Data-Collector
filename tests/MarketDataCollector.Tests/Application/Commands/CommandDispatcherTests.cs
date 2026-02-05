using FluentAssertions;
using MarketDataCollector.Application.Commands;
using Xunit;

namespace MarketDataCollector.Tests.Application.Commands;

/// <summary>
/// Tests for the CommandDispatcher.
/// Validates command routing and fallback behavior.
/// </summary>
public class CommandDispatcherTests
{
    [Fact]
    public async Task TryDispatchAsync_WithMatchingCommand_ReturnsHandled()
    {
        var command = new TestCommand("--test", exitCode: 0);
        var dispatcher = new CommandDispatcher(command);

        var (handled, exitCode) = await dispatcher.TryDispatchAsync(new[] { "--test" });

        handled.Should().BeTrue();
        exitCode.Should().Be(0);
    }

    [Fact]
    public async Task TryDispatchAsync_WithNoMatchingCommand_ReturnsNotHandled()
    {
        var command = new TestCommand("--test", exitCode: 0);
        var dispatcher = new CommandDispatcher(command);

        var (handled, exitCode) = await dispatcher.TryDispatchAsync(new[] { "--other" });

        handled.Should().BeFalse();
        exitCode.Should().Be(0);
    }

    [Fact]
    public async Task TryDispatchAsync_WithFailingCommand_ReturnsErrorCode()
    {
        var command = new TestCommand("--fail", exitCode: 1);
        var dispatcher = new CommandDispatcher(command);

        var (handled, exitCode) = await dispatcher.TryDispatchAsync(new[] { "--fail" });

        handled.Should().BeTrue();
        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task TryDispatchAsync_WithMultipleCommands_DispatchesToFirst()
    {
        var cmd1 = new TestCommand("--first", exitCode: 10);
        var cmd2 = new TestCommand("--second", exitCode: 20);
        var dispatcher = new CommandDispatcher(cmd1, cmd2);

        var (handled, exitCode) = await dispatcher.TryDispatchAsync(new[] { "--second" });

        handled.Should().BeTrue();
        exitCode.Should().Be(20);
    }

    [Fact]
    public async Task TryDispatchAsync_EmptyArgs_ReturnsNotHandled()
    {
        var command = new TestCommand("--test", exitCode: 0);
        var dispatcher = new CommandDispatcher(command);

        var (handled, _) = await dispatcher.TryDispatchAsync(Array.Empty<string>());

        handled.Should().BeFalse();
    }

    private sealed class TestCommand : ICliCommand
    {
        private readonly string _flag;
        private readonly int _exitCode;

        public TestCommand(string flag, int exitCode)
        {
            _flag = flag;
            _exitCode = exitCode;
        }

        public bool CanHandle(string[] args) => args.Contains(_flag);

        public Task<int> ExecuteAsync(string[] args, CancellationToken ct = default)
            => Task.FromResult(_exitCode);
    }
}
