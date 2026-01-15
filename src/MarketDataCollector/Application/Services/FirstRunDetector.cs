using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Application.Services;

/// <summary>
/// Detects first-run conditions and guides new users through setup.
/// Provides a friendly onboarding experience when the program is run for the first time.
/// </summary>
public sealed class FirstRunDetector
{
    private readonly ILogger _log = LoggingSetup.ForContext<FirstRunDetector>();
    private const string FirstRunMarkerFile = ".mdc-initialized";

    /// <summary>
    /// Result of first-run detection.
    /// </summary>
    public sealed record FirstRunStatus(
        bool IsFirstRun,
        bool ConfigExists,
        bool DataDirectoryExists,
        bool HasCredentials,
        IReadOnlyList<string> Issues,
        IReadOnlyList<string> Suggestions
    );

    /// <summary>
    /// Checks if this is a first-run situation and returns status with guidance.
    /// </summary>
    public FirstRunStatus Detect(string configPath, string dataRoot)
    {
        var issues = new List<string>();
        var suggestions = new List<string>();

        // Check for config file
        var configExists = File.Exists(configPath);
        if (!configExists)
        {
            issues.Add("No configuration file found");
            suggestions.Add("Run --wizard for interactive setup or --auto-config for quick setup");
        }

        // Check for data directory
        var dataExists = Directory.Exists(dataRoot);

        // Check for initialization marker
        var markerPath = Path.Combine(dataRoot, FirstRunMarkerFile);
        var hasMarker = File.Exists(markerPath);

        // Check for credentials in environment
        var hasCredentials = CheckForCredentials();
        if (!hasCredentials)
        {
            issues.Add("No API credentials detected in environment");
            suggestions.Add("Set ALPACA_KEY_ID and ALPACA_SECRET_KEY environment variables");
            suggestions.Add("Or run --wizard to configure providers interactively");
        }

        // Determine if this is truly a first run
        var isFirstRun = !configExists || (!hasMarker && !dataExists);

        if (isFirstRun)
        {
            _log.Information("First-run detected: config={ConfigExists}, data={DataExists}, marker={HasMarker}",
                configExists, dataExists, hasMarker);
        }

        return new FirstRunStatus(
            IsFirstRun: isFirstRun,
            ConfigExists: configExists,
            DataDirectoryExists: dataExists,
            HasCredentials: hasCredentials,
            Issues: issues,
            Suggestions: suggestions
        );
    }

    /// <summary>
    /// Marks the application as initialized (no longer first-run).
    /// </summary>
    public void MarkInitialized(string dataRoot)
    {
        try
        {
            if (!Directory.Exists(dataRoot))
            {
                Directory.CreateDirectory(dataRoot);
            }

            var markerPath = Path.Combine(dataRoot, FirstRunMarkerFile);
            File.WriteAllText(markerPath, $"Initialized: {DateTimeOffset.UtcNow:O}\n");
            _log.Debug("First-run marker created at {Path}", markerPath);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to create first-run marker");
        }
    }

    /// <summary>
    /// Displays a friendly welcome message for first-time users.
    /// </summary>
    public void DisplayWelcome(FirstRunStatus status, TextWriter? output = null)
    {
        output ??= Console.Out;

        output.WriteLine();
        output.WriteLine("╔══════════════════════════════════════════════════════════════════════╗");
        output.WriteLine("║            Welcome to Market Data Collector!                         ║");
        output.WriteLine("╚══════════════════════════════════════════════════════════════════════╝");
        output.WriteLine();

        if (status.IsFirstRun)
        {
            output.WriteLine("  It looks like this is your first time running the collector.");
            output.WriteLine();

            if (status.Issues.Count > 0)
            {
                output.WriteLine("  Current Status:");
                foreach (var issue in status.Issues)
                {
                    output.WriteLine($"    - {issue}");
                }
                output.WriteLine();
            }

            output.WriteLine("  Getting Started Options:");
            output.WriteLine();
            output.WriteLine("    1. Interactive Setup (Recommended for new users):");
            output.WriteLine("       MarketDataCollector --wizard");
            output.WriteLine();
            output.WriteLine("    2. Quick Auto-Configuration:");
            output.WriteLine("       # First set your API credentials:");
            output.WriteLine("       export ALPACA_KEY_ID=your-key-id");
            output.WriteLine("       export ALPACA_SECRET_KEY=your-secret-key");
            output.WriteLine("       # Then run:");
            output.WriteLine("       MarketDataCollector --auto-config");
            output.WriteLine();
            output.WriteLine("    3. Generate a Configuration Template:");
            output.WriteLine("       MarketDataCollector --generate-config --template alpaca");
            output.WriteLine();
            output.WriteLine("    4. Check Available Providers:");
            output.WriteLine("       MarketDataCollector --detect-providers");
            output.WriteLine();
        }
        else
        {
            output.WriteLine("  Configuration detected. Ready to collect market data.");
            output.WriteLine();

            if (status.Suggestions.Count > 0)
            {
                output.WriteLine("  Suggestions:");
                foreach (var suggestion in status.Suggestions)
                {
                    output.WriteLine($"    - {suggestion}");
                }
                output.WriteLine();
            }
        }

        output.WriteLine("  For help: MarketDataCollector --help");
        output.WriteLine("  Documentation: ./docs/HELP.md");
        output.WriteLine();
    }

    /// <summary>
    /// Prompts the user to choose a setup option.
    /// </summary>
    public async Task<SetupChoice> PromptForSetupAsync(CancellationToken ct = default)
    {
        Console.WriteLine();
        Console.WriteLine("How would you like to proceed?");
        Console.WriteLine();
        Console.WriteLine("  1. Run interactive setup wizard");
        Console.WriteLine("  2. Run quick auto-configuration");
        Console.WriteLine("  3. Continue with default settings");
        Console.WriteLine("  4. Exit and configure manually");
        Console.WriteLine();
        Console.Write("Choice [1-4] (default: 1): ");

        var input = await Task.Run(Console.ReadLine, ct);

        return input?.Trim() switch
        {
            "1" or "" => SetupChoice.Wizard,
            "2" => SetupChoice.AutoConfig,
            "3" => SetupChoice.ContinueWithDefaults,
            "4" => SetupChoice.Exit,
            _ => SetupChoice.Wizard
        };
    }

    private static bool CheckForCredentials()
    {
        // Check for any common provider credentials
        var credentialVars = new[]
        {
            "ALPACA_KEY_ID",
            "ALPACA_SECRET_KEY",
            "POLYGON_API_KEY",
            "TIINGO_API_TOKEN",
            "FINNHUB_API_KEY"
        };

        return credentialVars.Any(v => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(v)));
    }
}

/// <summary>
/// User's choice for setup action.
/// </summary>
public enum SetupChoice
{
    Wizard,
    AutoConfig,
    ContinueWithDefaults,
    Exit
}
