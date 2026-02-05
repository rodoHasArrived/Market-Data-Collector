namespace MarketDataCollector.Application.Commands;

/// <summary>
/// Interface for CLI command handlers extracted from Program.cs.
/// Each implementation handles one or more related CLI flags.
/// </summary>
public interface ICliCommand
{
    /// <summary>
    /// Returns true if this command should handle the given args.
    /// </summary>
    bool CanHandle(string[] args);

    /// <summary>
    /// Executes the command. Returns the exit code (0 = success).
    /// </summary>
    Task<int> ExecuteAsync(string[] args, CancellationToken ct = default);
}
