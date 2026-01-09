using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service that analyzes existing data and provides smart recommendations
/// for backfill operations, gap repairs, and data quality improvements.
/// </summary>
public sealed class SmartRecommendationsService
{
    private static SmartRecommendationsService? _instance;
    private static readonly object _lock = new();

    private readonly DataCompletenessService _completenessService;
    private readonly StorageAnalyticsService _storageService;
    private readonly ConfigService _configService;

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static SmartRecommendationsService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new SmartRecommendationsService();
                }
            }
            return _instance;
        }
    }

    private SmartRecommendationsService()
    {
        _completenessService = new DataCompletenessService();
        _storageService = new StorageAnalyticsService();
        _configService = new ConfigService();
    }

    /// <summary>
    /// Generates smart recommendations based on current data state.
    /// </summary>
    public async Task<BackfillRecommendations> GetRecommendationsAsync(CancellationToken ct = default)
    {
        var recommendations = new BackfillRecommendations();

        try
        {
            // Load current config
            var config = await _configService.LoadConfigAsync();

            // Analyze existing data coverage
            var analytics = await _storageService.GetStorageAnalyticsAsync();

            // Generate recommendations
            recommendations.QuickActions = await GetQuickActionsAsync(config, analytics, ct);
            recommendations.SuggestedBackfills = await GetSuggestedBackfillsAsync(config, analytics, ct);
            recommendations.DataQualityIssues = await GetDataQualityIssuesAsync(config, ct);
            recommendations.Insights = await GetInsightsAsync(config, analytics, ct);

            recommendations.GeneratedAt = DateTime.UtcNow;
            recommendations.IsStale = false;
        }
        catch (Exception ex)
        {
            recommendations.ErrorMessage = ex.Message;
        }

        return recommendations;
    }

    private async Task<List<QuickAction>> GetQuickActionsAsync(
        Models.AppConfig? config,
        Models.StorageAnalytics? analytics,
        CancellationToken ct)
    {
        var actions = new List<QuickAction>();

        // Check for gaps in recent data
        var gapCount = await GetRecentGapCountAsync(ct);
        if (gapCount > 0)
        {
            actions.Add(new QuickAction
            {
                Id = "fill-recent-gaps",
                Title = $"Fill {gapCount} Recent Gap{(gapCount > 1 ? "s" : "")}",
                Description = "Automatically download missing data from the past 30 days",
                Icon = "\uE90F",
                ActionType = QuickActionType.FillGaps,
                Priority = 1,
                EstimatedTime = TimeSpan.FromMinutes(gapCount * 0.5)
            });
        }

        // Suggest extending date range for subscribed symbols
        if (config?.Symbols != null && config.Symbols.Length > 0)
        {
            var shortCoverageSymbols = await GetSymbolsWithShortCoverageAsync(config.Symbols, ct);
            if (shortCoverageSymbols.Count > 0)
            {
                actions.Add(new QuickAction
                {
                    Id = "extend-coverage",
                    Title = "Extend Historical Coverage",
                    Description = $"{shortCoverageSymbols.Count} symbols have less than 1 year of data",
                    Icon = "\uE823",
                    ActionType = QuickActionType.ExtendCoverage,
                    Priority = 2,
                    AffectedSymbols = shortCoverageSymbols.ToArray(),
                    EstimatedTime = TimeSpan.FromMinutes(shortCoverageSymbols.Count * 2)
                });
            }
        }

        // Suggest backfill for new subscribed symbols
        if (config?.Symbols != null)
        {
            var symbolsWithoutData = await GetSymbolsWithoutDataAsync(config.Symbols, ct);
            if (symbolsWithoutData.Count > 0)
            {
                actions.Add(new QuickAction
                {
                    Id = "backfill-new-symbols",
                    Title = $"Backfill {symbolsWithoutData.Count} New Symbol{(symbolsWithoutData.Count > 1 ? "s" : "")}",
                    Description = "Download 1 year of historical data for newly subscribed symbols",
                    Icon = "\uE787",
                    ActionType = QuickActionType.BackfillNew,
                    Priority = 1,
                    AffectedSymbols = symbolsWithoutData.ToArray(),
                    EstimatedTime = TimeSpan.FromMinutes(symbolsWithoutData.Count * 2)
                });
            }
        }

        // Suggest updating to latest data
        var staleSymbols = await GetStaleSymbolsAsync(ct);
        if (staleSymbols.Count > 0)
        {
            actions.Add(new QuickAction
            {
                Id = "update-to-latest",
                Title = "Update to Latest Data",
                Description = $"{staleSymbols.Count} symbols haven't been updated in 7+ days",
                Icon = "\uE72C",
                ActionType = QuickActionType.UpdateLatest,
                Priority = 3,
                AffectedSymbols = staleSymbols.ToArray(),
                EstimatedTime = TimeSpan.FromMinutes(1)
            });
        }

        return actions.OrderBy(a => a.Priority).ToList();
    }

    private async Task<List<SuggestedBackfill>> GetSuggestedBackfillsAsync(
        Models.AppConfig? config,
        Models.StorageAnalytics? analytics,
        CancellationToken ct)
    {
        var suggestions = new List<SuggestedBackfill>();

        // Popular market indices suggestion
        var popularETFs = new[] { "SPY", "QQQ", "IWM", "DIA", "VTI" };
        var missingETFs = await GetMissingSymbolsAsync(popularETFs, ct);
        if (missingETFs.Count > 0)
        {
            suggestions.Add(new SuggestedBackfill
            {
                Id = "popular-etfs",
                Title = "Major Market ETFs",
                Description = "Essential market benchmarks for analysis",
                Symbols = missingETFs.ToArray(),
                RecommendedDateRange = 365 * 5, // 5 years
                Reason = "These ETFs provide important market context for your analysis",
                Category = "Market Benchmarks"
            });
        }

        // Tech sector suggestion
        var techStocks = new[] { "AAPL", "MSFT", "GOOGL", "AMZN", "NVDA", "META", "TSLA" };
        var missingTech = await GetMissingSymbolsAsync(techStocks, ct);
        if (missingTech.Count > 0)
        {
            suggestions.Add(new SuggestedBackfill
            {
                Id = "tech-stocks",
                Title = "Tech Giants",
                Description = "Major technology companies",
                Symbols = missingTech.ToArray(),
                RecommendedDateRange = 365 * 3, // 3 years
                Reason = "High-volume stocks useful for backtesting and analysis",
                Category = "Technology Sector"
            });
        }

        // Sector diversification
        var sectorETFs = new[] { "XLF", "XLK", "XLE", "XLV", "XLI", "XLU", "XLP", "XLY", "XLB", "XLRE" };
        var missingSectors = await GetMissingSymbolsAsync(sectorETFs, ct);
        if (missingSectors.Count > 0)
        {
            suggestions.Add(new SuggestedBackfill
            {
                Id = "sector-etfs",
                Title = "Sector ETFs",
                Description = "Broad sector exposure for diversified analysis",
                Symbols = missingSectors.ToArray(),
                RecommendedDateRange = 365 * 3,
                Reason = "Useful for sector rotation and relative strength analysis",
                Category = "Sector Analysis"
            });
        }

        return suggestions;
    }

    private async Task<List<DataQualityIssue>> GetDataQualityIssuesAsync(
        Models.AppConfig? config,
        CancellationToken ct)
    {
        var issues = new List<DataQualityIssue>();

        // Simulate finding quality issues
        await Task.Delay(10, ct); // Placeholder for actual analysis

        // Example issues (in real implementation, these would come from actual data analysis)
        var gapCount = await GetRecentGapCountAsync(ct);
        if (gapCount > 0)
        {
            issues.Add(new DataQualityIssue
            {
                Id = "gaps-detected",
                Severity = IssueSeverity.Warning,
                Title = "Data Gaps Detected",
                Description = $"{gapCount} trading days with missing data in the last 30 days",
                AffectedCount = gapCount,
                SuggestedAction = "Run gap repair to fill missing data"
            });
        }

        return issues;
    }

    private async Task<List<InsightMessage>> GetInsightsAsync(
        Models.AppConfig? config,
        Models.StorageAnalytics? analytics,
        CancellationToken ct)
    {
        var insights = new List<InsightMessage>();

        await Task.Delay(10, ct); // Placeholder

        // Storage insights
        if (analytics != null)
        {
            var totalGb = analytics.TotalSizeBytes / (1024.0 * 1024.0 * 1024.0);
            if (totalGb > 10)
            {
                insights.Add(new InsightMessage
                {
                    Type = InsightType.Info,
                    Title = "Storage Usage",
                    Message = $"You have {totalGb:F1} GB of historical data stored locally."
                });
            }
        }

        // Coverage insights
        if (config?.Symbols != null && config.Symbols.Length > 0)
        {
            insights.Add(new InsightMessage
            {
                Type = InsightType.Success,
                Title = "Symbols Configured",
                Message = $"You have {config.Symbols.Length} symbols configured for data collection."
            });
        }

        return insights;
    }

    // Helper methods (simulated for demo - in real implementation these would query actual data)

    private async Task<int> GetRecentGapCountAsync(CancellationToken ct)
    {
        await Task.Delay(10, ct);
        return new Random().Next(0, 5); // Simulated
    }

    private async Task<List<string>> GetSymbolsWithShortCoverageAsync(Models.SymbolConfig[] symbols, CancellationToken ct)
    {
        await Task.Delay(10, ct);
        return symbols.Take(3).Where(s => !string.IsNullOrEmpty(s.Symbol)).Select(s => s.Symbol!).ToList();
    }

    private async Task<List<string>> GetSymbolsWithoutDataAsync(Models.SymbolConfig[] symbols, CancellationToken ct)
    {
        await Task.Delay(10, ct);
        return new List<string>(); // Simulated - in real implementation, check which symbols have no data
    }

    private async Task<List<string>> GetStaleSymbolsAsync(CancellationToken ct)
    {
        await Task.Delay(10, ct);
        return new List<string>(); // Simulated
    }

    private async Task<List<string>> GetMissingSymbolsAsync(string[] symbols, CancellationToken ct)
    {
        await Task.Delay(10, ct);
        return symbols.Take(3).ToList(); // Simulated - return some as "missing"
    }
}

/// <summary>
/// Container for all backfill recommendations.
/// </summary>
public class BackfillRecommendations
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public bool IsStale { get; set; }
    public string? ErrorMessage { get; set; }

    public List<QuickAction> QuickActions { get; set; } = new();
    public List<SuggestedBackfill> SuggestedBackfills { get; set; } = new();
    public List<DataQualityIssue> DataQualityIssues { get; set; } = new();
    public List<InsightMessage> Insights { get; set; } = new();
}

/// <summary>
/// A quick one-click action recommendation.
/// </summary>
public class QuickAction
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "\uE787";
    public QuickActionType ActionType { get; set; }
    public int Priority { get; set; } = 100;
    public string[]? AffectedSymbols { get; set; }
    public TimeSpan? EstimatedTime { get; set; }
}

/// <summary>
/// Types of quick actions.
/// </summary>
public enum QuickActionType
{
    FillGaps,
    ExtendCoverage,
    BackfillNew,
    UpdateLatest,
    ValidateData,
    Custom
}

/// <summary>
/// A suggested backfill operation.
/// </summary>
public class SuggestedBackfill
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string[] Symbols { get; set; } = Array.Empty<string>();
    public int RecommendedDateRange { get; set; } = 365;
    public string? Reason { get; set; }
    public string? Category { get; set; }
}

/// <summary>
/// A data quality issue.
/// </summary>
public class DataQualityIssue
{
    public string Id { get; set; } = string.Empty;
    public IssueSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int AffectedCount { get; set; }
    public string? SuggestedAction { get; set; }
}

/// <summary>
/// Issue severity levels.
/// </summary>
public enum IssueSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// An insight message.
/// </summary>
public class InsightMessage
{
    public InsightType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Insight message types.
/// </summary>
public enum InsightType
{
    Info,
    Success,
    Warning,
    Tip
}
