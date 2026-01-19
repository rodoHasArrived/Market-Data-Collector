using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using MarketDataCollector.Configuration;
using Serilog;

namespace MarketDataCollector.Providers;

/// <summary>
/// Wraps a Python subprocess data collector.
/// Communicates via stdin (config JSON) and stdout (JSON lines of market data).
/// Errors are read from stderr.
/// </summary>
/// <remarks>
/// This design provides:
/// - Process isolation (Python crash doesn't affect .NET)
/// - Language independence (easy to port collectors)
/// - Simple debugging (run Python script standalone)
/// - Easy testing (mock stdin/stdout)
/// </remarks>
public sealed class PythonSubprocessProvider : ISimplifiedMarketDataProvider
{
    private static readonly ILogger Logger = Log.ForContext<PythonSubprocessProvider>();

    public string Name { get; }

    private readonly string _pythonScriptPath;
    private readonly Dictionary<string, object> _config;

    private Process? _process;
    private StreamReader? _outputReader;
    private Task? _stderrReaderTask;
    private CancellationTokenSource? _processCts;

    private bool _isHealthy;
    private string? _healthError;
    private DateTime? _lastSuccessfulRead;

    public PythonSubprocessProvider(
        string name,
        string pythonScriptPath,
        Dictionary<string, object> config)
    {
        Name = name;
        _pythonScriptPath = pythonScriptPath;
        _config = config;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        try
        {
            Logger.Information("Starting Python subprocess for {Provider}: {Script}",
                Name, _pythonScriptPath);

            _processCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = GetPythonExecutable(),
                    Arguments = _pythonScriptPath,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(_pythonScriptPath)
                        ?? Directory.GetCurrentDirectory()
                }
            };

            _process.Start();

            // Send configuration to stdin as JSON
            var configJson = JsonSerializer.Serialize(_config);
            await _process.StandardInput.WriteLineAsync(configJson);
            await _process.StandardInput.FlushAsync();
            _process.StandardInput.Close();

            // Start reading output
            _outputReader = _process.StandardOutput;

            // Start background task to read stderr
            _stderrReaderTask = ReadStderrAsync(_processCts.Token);

            // Give it a moment to start and verify it's running
            await Task.Delay(500, ct);

            if (_process.HasExited)
            {
                var exitCode = _process.ExitCode;
                throw new InvalidOperationException(
                    $"Python process exited immediately with code {exitCode}. Check stderr for details.");
            }

            _isHealthy = true;
            _healthError = null;

            Logger.Information("Python subprocess started for {Provider} (PID: {Pid})",
                Name, _process.Id);
        }
        catch (Exception ex)
        {
            _isHealthy = false;
            _healthError = ex.Message;
            Logger.Error(ex, "Failed to start Python subprocess for {Provider}", Name);
            throw;
        }
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        Logger.Information("Disconnecting Python subprocess for {Provider}", Name);

        _processCts?.Cancel();

        if (_process != null && !_process.HasExited)
        {
            try
            {
                // Try graceful termination first
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromSeconds(5), ct);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Error during Python subprocess termination for {Provider}", Name);
            }
        }

        _process?.Dispose();
        _process = null;
        _outputReader = null;
    }

    public async IAsyncEnumerable<SimplifiedMarketData> StreamAsync(
        IEnumerable<string> symbols,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (_outputReader == null)
        {
            throw new InvalidOperationException($"{Name} not connected. Call ConnectAsync first.");
        }

        var symbolSet = symbols.ToHashSet(StringComparer.OrdinalIgnoreCase);

        while (!ct.IsCancellationRequested && _process?.HasExited != true)
        {
            string? line;
            try
            {
                line = await _outputReader.ReadLineAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Error reading from {Provider} stdout", Name);
                _isHealthy = false;
                _healthError = ex.Message;
                break;
            }

            if (line == null)
            {
                // End of stream
                break;
            }

            SimplifiedMarketData? data = null;
            try
            {
                data = ParseDataLine(line, symbolSet);
            }
            catch (JsonException ex)
            {
                Logger.Debug("Failed to parse JSON from {Provider}: {Line} - {Error}",
                    Name, line.Length > 100 ? line[..100] + "..." : line, ex.Message);
            }

            if (data != null)
            {
                _lastSuccessfulRead = DateTime.UtcNow;
                _isHealthy = true;
                _healthError = null;
                yield return data;
            }
        }

        // Check if process died
        if (_process?.HasExited == true)
        {
            var exitCode = _process.ExitCode;
            Logger.Warning("Python process for {Provider} exited with code {ExitCode}",
                Name, exitCode);
            _isHealthy = false;
            _healthError = $"Process exited with code {exitCode}";
        }
    }

    public Task<ProviderHealthStatus> GetHealthAsync(CancellationToken ct = default)
    {
        var isRunning = _process?.HasExited == false;

        if (!isRunning)
        {
            return Task.FromResult(ProviderHealthStatus.Unhealthy("Process not running"));
        }

        if (!_isHealthy)
        {
            return Task.FromResult(ProviderHealthStatus.Unhealthy(_healthError ?? "Unknown error"));
        }

        return Task.FromResult(ProviderHealthStatus.Healthy(_lastSuccessfulRead));
    }

    private SimplifiedMarketData? ParseDataLine(string line, HashSet<string> symbolFilter)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var json = JsonSerializer.Deserialize<JsonElement>(line);

        // Skip non-data messages (status, errors, etc.)
        if (!json.TryGetProperty("symbol", out var symbolElement))
            return null;

        var symbol = symbolElement.GetString();
        if (string.IsNullOrEmpty(symbol))
            return null;

        // Filter by requested symbols (if filter is not empty)
        if (symbolFilter.Count > 0 && !symbolFilter.Contains(symbol))
            return null;

        // Parse price (handle both numeric and string)
        decimal price = 0;
        if (json.TryGetProperty("price", out var priceElement))
        {
            price = priceElement.ValueKind switch
            {
                JsonValueKind.Number => priceElement.GetDecimal(),
                JsonValueKind.String => decimal.Parse(priceElement.GetString() ?? "0"),
                _ => 0
            };
        }

        // Parse volume
        long volume = 0;
        if (json.TryGetProperty("volume", out var volumeElement))
        {
            volume = volumeElement.ValueKind switch
            {
                JsonValueKind.Number => volumeElement.GetInt64(),
                JsonValueKind.String => long.Parse(volumeElement.GetString() ?? "0"),
                _ => 0
            };
        }

        // Parse timestamp
        var timestamp = DateTime.UtcNow;
        if (json.TryGetProperty("timestamp", out var timestampElement))
        {
            var tsString = timestampElement.GetString();
            if (!string.IsNullOrEmpty(tsString))
            {
                DateTime.TryParse(tsString, out timestamp);
            }
        }

        // Get source (default to provider name)
        var source = Name;
        if (json.TryGetProperty("source", out var sourceElement))
        {
            source = sourceElement.GetString() ?? Name;
        }

        return new SimplifiedMarketData(symbol, price, volume, timestamp, source);
    }

    private async Task ReadStderrAsync(CancellationToken ct)
    {
        if (_process == null) return;

        try
        {
            using var reader = _process.StandardError;
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line == null) break;

                // Try to parse as JSON status message
                try
                {
                    var json = JsonSerializer.Deserialize<JsonElement>(line);

                    if (json.TryGetProperty("status", out var status))
                    {
                        var statusStr = status.GetString();
                        if (statusStr == "connected")
                        {
                            Logger.Information("{Provider} connected successfully", Name);
                            _isHealthy = true;
                            _healthError = null;
                        }
                        else if (statusStr == "shutting_down")
                        {
                            Logger.Information("{Provider} shutting down", Name);
                        }
                    }

                    if (json.TryGetProperty("error", out var error))
                    {
                        var errorStr = error.GetString();
                        Logger.Error("{Provider} error: {Error}", Name, errorStr);
                        _isHealthy = false;
                        _healthError = errorStr;
                    }
                }
                catch (JsonException)
                {
                    // Not JSON, log as plain text
                    Logger.Debug("[{Provider}] {Line}", Name, line);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Error reading stderr from {Provider}", Name);
        }
    }

    private static string GetPythonExecutable()
    {
        // Try python3 first, fall back to python
        var python3 = Environment.OSVersion.Platform == PlatformID.Win32NT
            ? "python"
            : "python3";

        // Check if python3 is available
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = python3,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit(5000);
            if (process.ExitCode == 0)
                return python3;
        }
        catch
        {
            // python3 not available
        }

        return "python";
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _processCts?.Dispose();
    }
}
