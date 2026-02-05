namespace MarketDataCollector.Application.Commands;

/// <summary>
/// Dispatches CLI arguments to the appropriate command handler.
/// Iterates through registered handlers and executes the first match.
/// </summary>
internal sealed class CommandDispatcher
{
    private readonly ICliCommand[] _commands;

    public CommandDispatcher(params ICliCommand[] commands)
    {
        _commands = commands;
    }

    /// <summary>
    /// Tries to dispatch the args to a registered command.
    /// Returns true if a command handled the args (caller should exit).
    /// Returns false if no command matched (caller should continue normal startup).
    /// </summary>
    public async Task<(bool Handled, int ExitCode)> TryDispatchAsync(string[] args, CancellationToken ct = default)
    {
        foreach (var command in _commands)
        {
            if (command.CanHandle(args))
            {
                var exitCode = await command.ExecuteAsync(args, ct);
                return (true, exitCode);
            }
        }

        return (false, 0);
    }
}
