using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Manages installation and lifecycle operations for the local backend service.
/// </summary>
public sealed class BackendServiceManager
{
    private static readonly Lazy<BackendServiceManager> _instance = new(() => new BackendServiceManager());

    private readonly string _stateDirectory;
    private readonly string _installationFilePath;
    private readonly string _runtimeFilePath;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _operationLock = new(1, 1);

    public static BackendServiceManager Instance => _instance.Value;

    private BackendServiceManager()
    {
        var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _stateDirectory = Path.Combine(appDataRoot, "MarketDataCollector", "service");
        _installationFilePath = Path.Combine(_stateDirectory, "backend-installation.json");
        _runtimeFilePath = Path.Combine(_stateDirectory, "backend-runtime.json");

        Directory.CreateDirectory(_stateDirectory);

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    public async Task<BackendServiceOperationResult> InstallAsync(string? executablePath = null, CancellationToken ct = default)
    {
        await _operationLock.WaitAsync(ct);
        try
        {
            var resolvedPath = ResolveExecutablePath(executablePath);
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                return BackendServiceOperationResult.Failed("Backend executable not found.");
            }

            var install = new BackendInstallationInfo
            {
                ExecutablePath = resolvedPath,
                InstalledAtUtc = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(install, SerializerOptions);
            await File.WriteAllTextAsync(_installationFilePath, json, ct);

            LoggingService.Instance.LogInfo("Backend service installation updated", ("ExecutablePath", resolvedPath));
            return BackendServiceOperationResult.SuccessResult("Backend service registered.");
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Failed to install backend service", ex);
            return BackendServiceOperationResult.Failed($"Installation failed: {ex.Message}");
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<BackendServiceOperationResult> StartAsync(CancellationToken ct = default)
    {
        await _operationLock.WaitAsync(ct);
        try
        {
            var status = await GetStatusCoreAsync(ct);
            if (status.IsRunning)
            {
                return BackendServiceOperationResult.SuccessResult("Backend is already running.");
            }

            var installation = await ReadInstallationInfoAsync(ct);
            if (installation is null || !File.Exists(installation.ExecutablePath))
            {
                var resolvedPath = ResolveExecutablePath(null);
                if (string.IsNullOrWhiteSpace(resolvedPath))
                {
                    return BackendServiceOperationResult.Failed("No backend installation found.");
                }

                installation = new BackendInstallationInfo
                {
                    ExecutablePath = resolvedPath,
                    InstalledAtUtc = DateTime.UtcNow
                };

                await File.WriteAllTextAsync(_installationFilePath, JsonSerializer.Serialize(installation, SerializerOptions), ct);
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = installation.ExecutablePath,
                WorkingDirectory = Path.GetDirectoryName(installation.ExecutablePath) ?? AppDomain.CurrentDomain.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = Process.Start(processStartInfo);
            if (process is null)
            {
                return BackendServiceOperationResult.Failed("Failed to start backend process.");
            }

            var runtime = new BackendRuntimeInfo
            {
                ProcessId = process.Id,
                StartedAtUtc = DateTime.UtcNow
            };

            await File.WriteAllTextAsync(_runtimeFilePath, JsonSerializer.Serialize(runtime, SerializerOptions), ct);

            var becameHealthy = await WaitForHealthyAsync(TimeSpan.FromSeconds(15), ct);
            var message = becameHealthy
                ? "Backend service started and passed health checks."
                : "Backend process started, but health checks are still warming up.";

            LoggingService.Instance.LogInfo("Backend service start requested", ("Pid", process.Id.ToString()));
            return BackendServiceOperationResult.SuccessResult(message);
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Failed to start backend service", ex);
            return BackendServiceOperationResult.Failed($"Start failed: {ex.Message}");
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<BackendServiceOperationResult> StopAsync(CancellationToken ct = default)
    {
        await _operationLock.WaitAsync(ct);
        try
        {
            var runtime = await ReadRuntimeInfoAsync(ct);
            if (runtime is null)
            {
                return BackendServiceOperationResult.SuccessResult("Backend runtime was not tracked.");
            }

            var process = TryGetProcess(runtime.ProcessId);
            if (process is null)
            {
                DeleteFileIfExists(_runtimeFilePath);
                return BackendServiceOperationResult.SuccessResult("Backend process already stopped.");
            }

            try
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(ct);
            }
            finally
            {
                DeleteFileIfExists(_runtimeFilePath);
                process.Dispose();
            }

            LoggingService.Instance.LogInfo("Backend service stopped", ("Pid", runtime.ProcessId.ToString()));
            return BackendServiceOperationResult.SuccessResult("Backend service stopped.");
        }
        catch (Exception ex)
        {
            LoggingService.Instance.LogError("Failed to stop backend service", ex);
            return BackendServiceOperationResult.Failed($"Stop failed: {ex.Message}");
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<BackendServiceOperationResult> RestartAsync(CancellationToken ct = default)
    {
        var stopResult = await StopAsync(ct);
        if (!stopResult.Success)
        {
            return stopResult;
        }

        return await StartAsync(ct);
    }

    public async Task<BackendServiceStatus> GetStatusAsync(CancellationToken ct = default)
    {
        await _operationLock.WaitAsync(ct);
        try
        {
            return await GetStatusCoreAsync(ct);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task<BackendServiceStatus> GetStatusCoreAsync(CancellationToken ct)
    {
        var installation = await ReadInstallationInfoAsync(ct);
        var runtime = await ReadRuntimeInfoAsync(ct);

        var process = runtime is not null ? TryGetProcess(runtime.ProcessId) : null;
        var isHealthy = await IsHealthyAsync(ct);

        if (runtime is not null && process is null)
        {
            DeleteFileIfExists(_runtimeFilePath);
        }

        return new BackendServiceStatus
        {
            IsInstalled = installation is not null,
            IsRunning = process is not null || isHealthy,
            IsHealthy = isHealthy,
            ProcessId = process?.Id,
            ExecutablePath = installation?.ExecutablePath,
            LastCheckedAtUtc = DateTime.UtcNow,
            StatusMessage = BuildStatusMessage(installation is not null, process is not null, isHealthy)
        };
    }

    private static string? ResolveExecutablePath(string? preferredPath)
    {
        if (!string.IsNullOrWhiteSpace(preferredPath) && File.Exists(preferredPath))
        {
            return preferredPath;
        }

        var configuredPath = Environment.GetEnvironmentVariable("MDC_BACKEND_PATH", EnvironmentVariableTarget.User);
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDirectory, "MarketDataCollector.exe"),
            Path.Combine(baseDirectory, "MarketDataCollector", "MarketDataCollector.exe"),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "MarketDataCollector", "MarketDataCollector.exe")),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "MarketDataCollector", "bin", "Release", "net9.0", "MarketDataCollector.exe")),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "MarketDataCollector", "bin", "Debug", "net9.0", "MarketDataCollector.exe"))
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private async Task<bool> WaitForHealthyAsync(TimeSpan timeout, CancellationToken ct)
    {
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < timeout)
        {
            if (await IsHealthyAsync(ct))
            {
                return true;
            }

            await Task.Delay(400, ct);
        }

        return false;
    }

    private async Task<bool> IsHealthyAsync(CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{ConnectionService.Instance.ServiceUrl}/healthz", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<BackendInstallationInfo?> ReadInstallationInfoAsync(CancellationToken ct)
    {
        if (!File.Exists(_installationFilePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(_installationFilePath, ct);
        return JsonSerializer.Deserialize<BackendInstallationInfo>(json, SerializerOptions);
    }

    private async Task<BackendRuntimeInfo?> ReadRuntimeInfoAsync(CancellationToken ct)
    {
        if (!File.Exists(_runtimeFilePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(_runtimeFilePath, ct);
        return JsonSerializer.Deserialize<BackendRuntimeInfo>(json, SerializerOptions);
    }

    private static Process? TryGetProcess(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return process.HasExited ? null : process;
        }
        catch
        {
            return null;
        }
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string BuildStatusMessage(bool installed, bool processRunning, bool healthy)
    {
        if (!installed)
        {
            return "Backend is not installed for lifecycle management yet.";
        }

        if (processRunning && healthy)
        {
            return "Backend is running and healthy.";
        }

        if (processRunning)
        {
            return "Backend process is running, waiting for healthy response.";
        }

        if (healthy)
        {
            return "Backend is reachable (managed externally).";
        }

        return "Backend is stopped.";
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
}

public sealed class BackendServiceStatus
{
    public bool IsInstalled { get; init; }
    public bool IsRunning { get; init; }
    public bool IsHealthy { get; init; }
    public int? ProcessId { get; init; }
    public string? ExecutablePath { get; init; }
    public DateTime LastCheckedAtUtc { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
}

public sealed class BackendServiceOperationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;

    public static BackendServiceOperationResult SuccessResult(string message) => new() { Success = true, Message = message };
    public static BackendServiceOperationResult Failed(string message) => new() { Success = false, Message = message };
}

public sealed class BackendInstallationInfo
{
    public string ExecutablePath { get; init; } = string.Empty;
    public DateTime InstalledAtUtc { get; init; }
}

public sealed class BackendRuntimeInfo
{
    public int ProcessId { get; init; }
    public DateTime StartedAtUtc { get; init; }
}
