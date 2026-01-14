using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for system diagnostics including dry-run, preflight checks, and diagnostic bundles.
/// Provides comprehensive system validation and troubleshooting capabilities.
/// </summary>
public sealed class DiagnosticsService
{
    private static DiagnosticsService? _instance;
    private static readonly object _lock = new();
    private readonly ApiClientService _apiClient;

    public static DiagnosticsService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new DiagnosticsService();
                }
            }
            return _instance;
        }
    }

    private DiagnosticsService()
    {
        _apiClient = ApiClientService.Instance;
    }

    /// <summary>
    /// Runs a dry-run validation of the configuration without starting data collection.
    /// </summary>
    public async Task<DryRunResult> RunDryRunAsync(CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<DryRunResponse>(
            "/api/diagnostics/dry-run",
            null,
            ct);

        if (response.Success && response.Data != null)
        {
            return new DryRunResult
            {
                Success = response.Data.Success,
                ConfigurationValid = response.Data.ConfigurationValid,
                CredentialsValid = response.Data.CredentialsValid,
                StorageWritable = response.Data.StorageWritable,
                ProvidersReachable = response.Data.ProvidersReachable,
                SymbolsValidated = response.Data.SymbolsValidated,
                Warnings = response.Data.Warnings?.ToList() ?? new List<string>(),
                Errors = response.Data.Errors?.ToList() ?? new List<string>(),
                ValidationDetails = response.Data.ValidationDetails?.ToList() ?? new List<ValidationDetail>()
            };
        }

        return new DryRunResult
        {
            Success = false,
            Errors = new List<string> { response.ErrorMessage ?? "Failed to run dry-run validation" }
        };
    }

    /// <summary>
    /// Runs preflight checks to verify system readiness.
    /// </summary>
    public async Task<PreflightResult> RunPreflightCheckAsync(CancellationToken ct = default)
    {
        var result = new PreflightResult();
        var checks = new List<PreflightCheck>();

        // Check service connectivity
        var serviceHealth = await _apiClient.CheckHealthAsync(ct);
        checks.Add(new PreflightCheck
        {
            Name = "Service Connectivity",
            Category = "Network",
            Passed = serviceHealth.IsReachable,
            Message = serviceHealth.IsReachable
                ? $"Service reachable (latency: {serviceHealth.LatencyMs:F0}ms)"
                : serviceHealth.ErrorMessage ?? "Service not reachable",
            Severity = serviceHealth.IsReachable ? CheckSeverity.Info : CheckSeverity.Critical
        });

        // Check provider status
        var providerResponse = await _apiClient.GetWithResponseAsync<ProviderStatusResponse>(
            "/api/diagnostics/providers",
            ct);

        if (providerResponse.Success && providerResponse.Data?.Providers != null)
        {
            foreach (var provider in providerResponse.Data.Providers)
            {
                checks.Add(new PreflightCheck
                {
                    Name = provider.Name,
                    Category = "Providers",
                    Passed = provider.IsAvailable,
                    Message = provider.IsAvailable
                        ? $"Available (enabled: {provider.IsEnabled})"
                        : provider.Error ?? "Not available",
                    Severity = provider.IsAvailable ? CheckSeverity.Info :
                               provider.IsEnabled ? CheckSeverity.Warning : CheckSeverity.Info
                });
            }
        }

        // Check storage
        var storageResponse = await _apiClient.GetWithResponseAsync<StorageStatusResponse>(
            "/api/diagnostics/storage",
            ct);

        if (storageResponse.Success && storageResponse.Data != null)
        {
            var storage = storageResponse.Data;
            checks.Add(new PreflightCheck
            {
                Name = "Storage Path",
                Category = "Storage",
                Passed = storage.PathExists,
                Message = storage.PathExists
                    ? $"Path exists: {storage.Path}"
                    : $"Path does not exist: {storage.Path}",
                Severity = storage.PathExists ? CheckSeverity.Info : CheckSeverity.Critical
            });

            checks.Add(new PreflightCheck
            {
                Name = "Storage Writable",
                Category = "Storage",
                Passed = storage.IsWritable,
                Message = storage.IsWritable ? "Storage is writable" : "Storage is not writable",
                Severity = storage.IsWritable ? CheckSeverity.Info : CheckSeverity.Critical
            });

            checks.Add(new PreflightCheck
            {
                Name = "Free Disk Space",
                Category = "Storage",
                Passed = storage.FreeSpaceGb > 1,
                Message = $"{storage.FreeSpaceGb:F1} GB free",
                Severity = storage.FreeSpaceGb > 10 ? CheckSeverity.Info :
                           storage.FreeSpaceGb > 1 ? CheckSeverity.Warning : CheckSeverity.Critical
            });
        }

        // Check configuration
        var configResponse = await _apiClient.GetWithResponseAsync<ConfigStatusResponse>(
            "/api/diagnostics/config",
            ct);

        if (configResponse.Success && configResponse.Data != null)
        {
            var config = configResponse.Data;
            checks.Add(new PreflightCheck
            {
                Name = "Configuration File",
                Category = "Configuration",
                Passed = config.FileExists,
                Message = config.FileExists
                    ? $"Config loaded: {config.FilePath}"
                    : "Configuration file not found",
                Severity = config.FileExists ? CheckSeverity.Info : CheckSeverity.Critical
            });

            checks.Add(new PreflightCheck
            {
                Name = "Symbols Configured",
                Category = "Configuration",
                Passed = config.SymbolCount > 0,
                Message = $"{config.SymbolCount} symbols configured",
                Severity = config.SymbolCount > 0 ? CheckSeverity.Info : CheckSeverity.Warning
            });
        }

        result.Checks = checks;
        result.PassedCount = checks.Count(c => c.Passed);
        result.FailedCount = checks.Count(c => !c.Passed);
        result.Success = !checks.Any(c => !c.Passed && c.Severity == CheckSeverity.Critical);

        return result;
    }

    /// <summary>
    /// Generates a diagnostic bundle for troubleshooting.
    /// </summary>
    public async Task<DiagnosticBundleResult> GenerateDiagnosticBundleAsync(
        DiagnosticBundleOptions options,
        CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<DiagnosticBundleResponse>(
            "/api/diagnostics/bundle",
            new
            {
                includeLogs = options.IncludeLogs,
                includeConfig = options.IncludeConfig,
                includeMetrics = options.IncludeMetrics,
                includeSampleData = options.IncludeSampleData,
                logDays = options.LogDays,
                redactSecrets = options.RedactSecrets
            },
            ct);

        if (response.Success && response.Data != null)
        {
            return new DiagnosticBundleResult
            {
                Success = true,
                BundlePath = response.Data.BundlePath,
                FileSizeBytes = response.Data.FileSizeBytes,
                IncludedFiles = response.Data.IncludedFiles?.ToList() ?? new List<string>()
            };
        }

        return new DiagnosticBundleResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Failed to generate diagnostic bundle"
        };
    }

    /// <summary>
    /// Gets the current system metrics.
    /// </summary>
    public async Task<SystemMetrics> GetSystemMetricsAsync(CancellationToken ct = default)
    {
        var response = await _apiClient.GetWithResponseAsync<SystemMetrics>(
            "/api/diagnostics/metrics",
            ct);

        return response.Data ?? new SystemMetrics();
    }

    /// <summary>
    /// Validates a specific configuration setting.
    /// </summary>
    public async Task<ValidationResult> ValidateConfigurationAsync(
        string settingName,
        string value,
        CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<ValidationResult>(
            "/api/diagnostics/validate",
            new { setting = settingName, value },
            ct);

        return response.Data ?? new ValidationResult { Valid = false, Error = response.ErrorMessage };
    }

    /// <summary>
    /// Tests connectivity to a specific provider.
    /// </summary>
    public async Task<ProviderTestResult> TestProviderAsync(string providerName, CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<ProviderTestResult>(
            $"/api/diagnostics/providers/{providerName}/test",
            null,
            ct);

        return response.Data ?? new ProviderTestResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Failed to test provider"
        };
    }
}

#region Result Classes

public class DryRunResult
{
    public bool Success { get; set; }
    public bool ConfigurationValid { get; set; }
    public bool CredentialsValid { get; set; }
    public bool StorageWritable { get; set; }
    public bool ProvidersReachable { get; set; }
    public int SymbolsValidated { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<ValidationDetail> ValidationDetails { get; set; } = new();
}

public class ValidationDetail
{
    public string Category { get; set; } = string.Empty;
    public string Item { get; set; } = string.Empty;
    public bool Valid { get; set; }
    public string? Message { get; set; }
}

public class PreflightResult
{
    public bool Success { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public List<PreflightCheck> Checks { get; set; } = new();
}

public class PreflightCheck
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Message { get; set; } = string.Empty;
    public CheckSeverity Severity { get; set; }
}

public enum CheckSeverity
{
    Info,
    Warning,
    Critical
}

public class DiagnosticBundleOptions
{
    public bool IncludeLogs { get; set; } = true;
    public bool IncludeConfig { get; set; } = true;
    public bool IncludeMetrics { get; set; } = true;
    public bool IncludeSampleData { get; set; } = false;
    public int LogDays { get; set; } = 7;
    public bool RedactSecrets { get; set; } = true;
}

public class DiagnosticBundleResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? BundlePath { get; set; }
    public long FileSizeBytes { get; set; }
    public List<string> IncludedFiles { get; set; } = new();
}

public class SystemMetrics
{
    public double CpuUsagePercent { get; set; }
    public long MemoryUsedBytes { get; set; }
    public long MemoryTotalBytes { get; set; }
    public double DiskUsagePercent { get; set; }
    public int ActiveConnections { get; set; }
    public int ActiveSubscriptions { get; set; }
    public long EventsPerSecond { get; set; }
    public long TotalEventsProcessed { get; set; }
    public TimeSpan Uptime { get; set; }
}

public class ValidationResult
{
    public bool Valid { get; set; }
    public string? Error { get; set; }
    public string? Suggestion { get; set; }
}

public class ProviderTestResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public double LatencyMs { get; set; }
    public string? Version { get; set; }
    public Dictionary<string, string>? Capabilities { get; set; }
}

#endregion

#region API Response Classes

public class DryRunResponse
{
    public bool Success { get; set; }
    public bool ConfigurationValid { get; set; }
    public bool CredentialsValid { get; set; }
    public bool StorageWritable { get; set; }
    public bool ProvidersReachable { get; set; }
    public int SymbolsValidated { get; set; }
    public string[]? Warnings { get; set; }
    public string[]? Errors { get; set; }
    public List<ValidationDetail>? ValidationDetails { get; set; }
}

public class ProviderStatusResponse
{
    public List<ProviderInfo>? Providers { get; set; }
}

public class ProviderInfo
{
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsAvailable { get; set; }
    public string? Error { get; set; }
}

public class StorageStatusResponse
{
    public string Path { get; set; } = string.Empty;
    public bool PathExists { get; set; }
    public bool IsWritable { get; set; }
    public double FreeSpaceGb { get; set; }
}

public class ConfigStatusResponse
{
    public bool FileExists { get; set; }
    public string? FilePath { get; set; }
    public int SymbolCount { get; set; }
}

public class DiagnosticBundleResponse
{
    public string? BundlePath { get; set; }
    public long FileSizeBytes { get; set; }
    public string[]? IncludedFiles { get; set; }
}

#endregion
