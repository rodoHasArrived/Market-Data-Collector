using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Service for integrating with QuantConnect Lean Engine for backtesting.
/// Enables configuration, data synchronization, and backtest execution.
/// </summary>
public sealed class LeanIntegrationService
{
    private static LeanIntegrationService? _instance;
    private static readonly object _lock = new();
    private readonly ApiClientService _apiClient;

    public static LeanIntegrationService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new LeanIntegrationService();
                }
            }
            return _instance;
        }
    }

    private LeanIntegrationService()
    {
        _apiClient = ApiClientService.Instance;
    }

    /// <summary>
    /// Raised when backtest status changes.
    /// </summary>
    public event EventHandler<BacktestStatusChangedEventArgs>? BacktestStatusChanged;

    /// <summary>
    /// Gets the current Lean integration status.
    /// </summary>
    public async Task<LeanStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var response = await _apiClient.GetWithResponseAsync<LeanStatus>(
            "/api/lean/status",
            ct);

        return response.Data ?? new LeanStatus();
    }

    /// <summary>
    /// Gets the Lean configuration.
    /// </summary>
    public async Task<LeanConfiguration> GetConfigurationAsync(CancellationToken ct = default)
    {
        var response = await _apiClient.GetWithResponseAsync<LeanConfiguration>(
            "/api/lean/config",
            ct);

        return response.Data ?? new LeanConfiguration();
    }

    /// <summary>
    /// Updates the Lean configuration.
    /// </summary>
    public async Task<bool> UpdateConfigurationAsync(
        LeanConfigurationUpdate config,
        CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<object>(
            "/api/lean/config",
            config,
            ct);

        return response.Success;
    }

    /// <summary>
    /// Verifies the Lean installation.
    /// </summary>
    public async Task<LeanVerificationResult> VerifyInstallationAsync(CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<LeanVerificationResponse>(
            "/api/lean/verify",
            null,
            ct);

        if (response.Success && response.Data != null)
        {
            return new LeanVerificationResult
            {
                Success = response.Data.IsValid,
                LeanPath = response.Data.LeanPath,
                Version = response.Data.Version,
                DataPath = response.Data.DataPath,
                Errors = response.Data.Errors?.ToList() ?? new List<string>(),
                Warnings = response.Data.Warnings?.ToList() ?? new List<string>()
            };
        }

        return new LeanVerificationResult
        {
            Success = false,
            Errors = new List<string> { response.ErrorMessage ?? "Verification failed" }
        };
    }

    /// <summary>
    /// Gets available algorithms.
    /// </summary>
    public async Task<AlgorithmListResult> GetAlgorithmsAsync(CancellationToken ct = default)
    {
        var response = await _apiClient.GetWithResponseAsync<AlgorithmListResponse>(
            "/api/lean/algorithms",
            ct);

        if (response.Success && response.Data != null)
        {
            return new AlgorithmListResult
            {
                Success = true,
                Algorithms = response.Data.Algorithms?.ToList() ?? new List<AlgorithmInfo>()
            };
        }

        return new AlgorithmListResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Failed to get algorithms"
        };
    }

    /// <summary>
    /// Syncs collected data to Lean format.
    /// </summary>
    public async Task<DataSyncResult> SyncDataAsync(
        DataSyncOptions options,
        CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<DataSyncResponse>(
            "/api/lean/sync",
            new
            {
                symbols = options.Symbols,
                fromDate = options.FromDate?.ToString("yyyy-MM-dd"),
                toDate = options.ToDate?.ToString("yyyy-MM-dd"),
                resolution = options.Resolution,
                overwrite = options.Overwrite
            },
            ct);

        if (response.Success && response.Data != null)
        {
            return new DataSyncResult
            {
                Success = response.Data.Success,
                SymbolsSynced = response.Data.SymbolsSynced,
                FilesCreated = response.Data.FilesCreated,
                BytesWritten = response.Data.BytesWritten,
                Errors = response.Data.Errors?.ToList() ?? new List<string>()
            };
        }

        return new DataSyncResult
        {
            Success = false,
            Errors = new List<string> { response.ErrorMessage ?? "Data sync failed" }
        };
    }

    /// <summary>
    /// Gets data synchronization status.
    /// </summary>
    public async Task<DataSyncStatus> GetDataSyncStatusAsync(CancellationToken ct = default)
    {
        var response = await _apiClient.GetWithResponseAsync<DataSyncStatus>(
            "/api/lean/sync/status",
            ct);

        return response.Data ?? new DataSyncStatus();
    }

    /// <summary>
    /// Starts a backtest.
    /// </summary>
    public async Task<BacktestStartResult> StartBacktestAsync(
        BacktestOptions options,
        CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<BacktestStartResponse>(
            "/api/lean/backtest/start",
            new
            {
                algorithmPath = options.AlgorithmPath,
                algorithmName = options.AlgorithmName,
                startDate = options.StartDate?.ToString("yyyy-MM-dd"),
                endDate = options.EndDate?.ToString("yyyy-MM-dd"),
                initialCapital = options.InitialCapital,
                parameters = options.Parameters
            },
            ct);

        if (response.Success && response.Data != null)
        {
            return new BacktestStartResult
            {
                Success = true,
                BacktestId = response.Data.BacktestId
            };
        }

        return new BacktestStartResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Failed to start backtest"
        };
    }

    /// <summary>
    /// Gets backtest status.
    /// </summary>
    public async Task<BacktestStatus> GetBacktestStatusAsync(string backtestId, CancellationToken ct = default)
    {
        var response = await _apiClient.GetWithResponseAsync<BacktestStatus>(
            $"/api/lean/backtest/{backtestId}/status",
            ct);

        return response.Data ?? new BacktestStatus { State = BacktestState.Unknown };
    }

    /// <summary>
    /// Gets backtest results.
    /// </summary>
    public async Task<BacktestResults> GetBacktestResultsAsync(string backtestId, CancellationToken ct = default)
    {
        var response = await _apiClient.GetWithResponseAsync<BacktestResults>(
            $"/api/lean/backtest/{backtestId}/results",
            ct);

        return response.Data ?? new BacktestResults();
    }

    /// <summary>
    /// Stops a running backtest.
    /// </summary>
    public async Task<bool> StopBacktestAsync(string backtestId, CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<object>(
            $"/api/lean/backtest/{backtestId}/stop",
            null,
            ct);

        return response.Success;
    }

    /// <summary>
    /// Gets backtest history.
    /// </summary>
    public async Task<BacktestHistoryResult> GetBacktestHistoryAsync(
        int limit = 20,
        CancellationToken ct = default)
    {
        var response = await _apiClient.GetWithResponseAsync<BacktestHistoryResponse>(
            $"/api/lean/backtest/history?limit={limit}",
            ct);

        if (response.Success && response.Data != null)
        {
            return new BacktestHistoryResult
            {
                Success = true,
                Backtests = response.Data.Backtests?.ToList() ?? new List<BacktestSummary>()
            };
        }

        return new BacktestHistoryResult
        {
            Success = false,
            Error = response.ErrorMessage ?? "Failed to get backtest history"
        };
    }

    /// <summary>
    /// Deletes a backtest and its results.
    /// </summary>
    public async Task<bool> DeleteBacktestAsync(string backtestId, CancellationToken ct = default)
    {
        var response = await _apiClient.PostWithResponseAsync<object>(
            $"/api/lean/backtest/{backtestId}/delete",
            null,
            ct);

        return response.Success;
    }
}

#region Event Args

public class BacktestStatusChangedEventArgs : EventArgs
{
    public string BacktestId { get; set; } = string.Empty;
    public BacktestState State { get; set; }
    public double Progress { get; set; }
}

#endregion

#region Configuration Classes

public class LeanStatus
{
    public bool IsInstalled { get; set; }
    public bool IsConfigured { get; set; }
    public bool DataSyncEnabled { get; set; }
    public string? Version { get; set; }
    public DateTime? LastSync { get; set; }
    public int SymbolsSynced { get; set; }
}

public class LeanConfiguration
{
    public string? LeanPath { get; set; }
    public string? DataPath { get; set; }
    public string? ResultsPath { get; set; }
    public bool AutoSync { get; set; }
    public string? DefaultResolution { get; set; }
    public List<string>? SyncSymbols { get; set; }
}

public class LeanConfigurationUpdate
{
    public string? LeanPath { get; set; }
    public string? DataPath { get; set; }
    public string? ResultsPath { get; set; }
    public bool? AutoSync { get; set; }
    public string? DefaultResolution { get; set; }
    public List<string>? SyncSymbols { get; set; }
}

public class LeanVerificationResult
{
    public bool Success { get; set; }
    public string? LeanPath { get; set; }
    public string? Version { get; set; }
    public string? DataPath { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

#endregion

#region Algorithm Classes

public class AlgorithmInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public bool IsValid { get; set; }
}

public class AlgorithmListResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<AlgorithmInfo> Algorithms { get; set; } = new();
}

#endregion

#region Data Sync Classes

public class DataSyncOptions
{
    public List<string>? Symbols { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public string Resolution { get; set; } = "Daily";
    public bool Overwrite { get; set; }
}

public class DataSyncResult
{
    public bool Success { get; set; }
    public int SymbolsSynced { get; set; }
    public int FilesCreated { get; set; }
    public long BytesWritten { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class DataSyncStatus
{
    public bool IsSyncing { get; set; }
    public double Progress { get; set; }
    public string? CurrentSymbol { get; set; }
    public int SymbolsCompleted { get; set; }
    public int TotalSymbols { get; set; }
}

#endregion

#region Backtest Classes

public class BacktestOptions
{
    public string? AlgorithmPath { get; set; }
    public string? AlgorithmName { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal InitialCapital { get; set; } = 100000m;
    public Dictionary<string, string>? Parameters { get; set; }
}

public class BacktestStartResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? BacktestId { get; set; }
}

public class BacktestStatus
{
    public string? BacktestId { get; set; }
    public BacktestState State { get; set; }
    public double Progress { get; set; }
    public DateTime? CurrentDate { get; set; }
    public DateTime? StartedAt { get; set; }
    public TimeSpan Elapsed { get; set; }
    public string? Error { get; set; }
}

public enum BacktestState
{
    Unknown,
    Initializing,
    Running,
    Completed,
    Failed,
    Cancelled
}

public class BacktestResults
{
    public string? BacktestId { get; set; }
    public string? AlgorithmName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal InitialCapital { get; set; }
    public decimal FinalCapital { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal AnnualizedReturn { get; set; }
    public decimal SharpeRatio { get; set; }
    public decimal MaxDrawdown { get; set; }
    public int TotalTrades { get; set; }
    public decimal WinRate { get; set; }
    public decimal ProfitFactor { get; set; }
    public List<LeanTradeRecord>? Trades { get; set; }
    public List<EquityPoint>? EquityCurve { get; set; }
}

public class LeanTradeRecord
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime EntryTime { get; set; }
    public DateTime ExitTime { get; set; }
    public string Direction { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal ExitPrice { get; set; }
    public decimal ProfitLoss { get; set; }
    public decimal ReturnPercent { get; set; }
}

public class EquityPoint
{
    public DateTime Date { get; set; }
    public decimal Equity { get; set; }
}

public class BacktestSummary
{
    public string BacktestId { get; set; } = string.Empty;
    public string AlgorithmName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public BacktestState State { get; set; }
    public decimal? TotalReturn { get; set; }
    public decimal? SharpeRatio { get; set; }
}

public class BacktestHistoryResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<BacktestSummary> Backtests { get; set; } = new();
}

#endregion

#region API Response Classes

public class LeanVerificationResponse
{
    public bool IsValid { get; set; }
    public string? LeanPath { get; set; }
    public string? Version { get; set; }
    public string? DataPath { get; set; }
    public string[]? Errors { get; set; }
    public string[]? Warnings { get; set; }
}

public class AlgorithmListResponse
{
    public List<AlgorithmInfo>? Algorithms { get; set; }
}

public class DataSyncResponse
{
    public bool Success { get; set; }
    public int SymbolsSynced { get; set; }
    public int FilesCreated { get; set; }
    public long BytesWritten { get; set; }
    public string[]? Errors { get; set; }
}

public class BacktestStartResponse
{
    public string? BacktestId { get; set; }
}

public class BacktestHistoryResponse
{
    public List<BacktestSummary>? Backtests { get; set; }
}

#endregion
