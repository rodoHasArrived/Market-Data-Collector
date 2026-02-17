using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarketDataCollector.Contracts.Configuration;

namespace MarketDataCollector.Ui.Services;

/// <summary>
/// Service for managing backfill provider configuration in the desktop UI.
/// Provides metadata descriptors, fallback chain preview, dry-run planning,
/// and audit trail for provider configuration changes.
/// </summary>
public sealed class BackfillProviderConfigService
{
    private static BackfillProviderConfigService? _instance;
    private static readonly object _lock = new();
    private readonly ApiClientService _apiClient;
    private readonly List<ProviderConfigAuditEntryDto> _auditLog = new();
    private readonly object _auditLock = new();

    public static BackfillProviderConfigService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new BackfillProviderConfigService();
                }
            }
            return _instance;
        }
    }

    private BackfillProviderConfigService()
    {
        _apiClient = ApiClientService.Instance;
    }

    /// <summary>
    /// Gets metadata descriptors for all known backfill providers.
    /// Used to drive dynamic UI generation in the provider settings panel.
    /// </summary>
    public Task<List<BackfillProviderMetadataDto>> GetProviderMetadataAsync(CancellationToken ct = default)
    {
        // Return well-known provider metadata. In a full implementation this would
        // come from the backend via /api/backfill/providers/metadata, but for desktop
        // offline-first use we provide defaults that work without a running server.
        var metadata = new List<BackfillProviderMetadataDto>
        {
            new()
            {
                ProviderId = "alpaca",
                DisplayName = "Alpaca",
                Description = "Bars, trades, and quotes via REST API. Free IEX tier available.",
                DataTypes = ["Bars", "Trades", "Quotes"],
                RequiresApiKey = true,
                FreeTier = true,
                DefaultPriority = 5,
                DefaultRateLimitPerMinute = 200,
            },
            new()
            {
                ProviderId = "polygon",
                DisplayName = "Polygon",
                Description = "Full market data including aggregates. Tiered subscription plans.",
                DataTypes = ["Bars", "Trades", "Quotes", "Aggregates"],
                RequiresApiKey = true,
                FreeTier = false,
                DefaultPriority = 12,
                DefaultRateLimitPerMinute = 5,
            },
            new()
            {
                ProviderId = "tiingo",
                DisplayName = "Tiingo",
                Description = "Daily bars and end-of-day data. Free tier available.",
                DataTypes = ["Daily bars"],
                RequiresApiKey = true,
                FreeTier = true,
                DefaultPriority = 15,
                DefaultRateLimitPerHour = 500,
            },
            new()
            {
                ProviderId = "finnhub",
                DisplayName = "Finnhub",
                Description = "Daily bars with international coverage. Free tier available.",
                DataTypes = ["Daily bars"],
                RequiresApiKey = true,
                FreeTier = true,
                DefaultPriority = 20,
                DefaultRateLimitPerMinute = 60,
            },
            new()
            {
                ProviderId = "stooq",
                DisplayName = "Stooq",
                Description = "Free daily bar data. No API key required.",
                DataTypes = ["Daily bars"],
                RequiresApiKey = false,
                FreeTier = true,
                DefaultPriority = 25,
            },
            new()
            {
                ProviderId = "yahoo",
                DisplayName = "Yahoo Finance",
                Description = "Unofficial daily bar data. No API key required.",
                DataTypes = ["Daily bars"],
                RequiresApiKey = false,
                FreeTier = true,
                DefaultPriority = 30,
            },
            new()
            {
                ProviderId = "alphavantage",
                DisplayName = "Alpha Vantage",
                Description = "Daily bars with strict rate limits. Free tier available.",
                DataTypes = ["Daily bars"],
                RequiresApiKey = true,
                FreeTier = true,
                DefaultPriority = 35,
                DefaultRateLimitPerMinute = 5,
                DefaultRateLimitPerHour = 500,
            },
            new()
            {
                ProviderId = "nasdaqdatalink",
                DisplayName = "Nasdaq Data Link",
                Description = "Various market data sets. Subscription required for most datasets.",
                DataTypes = ["Various"],
                RequiresApiKey = true,
                FreeTier = false,
                DefaultPriority = 40,
            },
        };

        return Task.FromResult(metadata);
    }

    /// <summary>
    /// Gets combined status view of all providers including config, health, and rate limit usage.
    /// </summary>
    public async Task<List<BackfillProviderStatusDto>> GetProviderStatusesAsync(
        BackfillProvidersConfigDto? config,
        CancellationToken ct = default)
    {
        var metadata = await GetProviderMetadataAsync(ct);
        var result = new List<BackfillProviderStatusDto>();

        foreach (var meta in metadata)
        {
            var options = GetProviderOptionsFromConfig(config, meta.ProviderId);
            var effectiveSource = DetermineConfigSource(options, meta);

            result.Add(new BackfillProviderStatusDto
            {
                Metadata = meta with { ConfigSource = effectiveSource },
                Options = options ?? new BackfillProviderOptionsDto
                {
                    Enabled = true,
                    Priority = meta.DefaultPriority,
                    RateLimitPerMinute = meta.DefaultRateLimitPerMinute,
                    RateLimitPerHour = meta.DefaultRateLimitPerHour,
                },
                EffectiveConfigSource = effectiveSource,
            });
        }

        // Sort by effective priority (enabled first, then by priority)
        result.Sort((a, b) =>
        {
            if (a.Options.Enabled != b.Options.Enabled)
                return a.Options.Enabled ? -1 : 1;
            var pa = a.Options.Priority ?? a.Metadata.DefaultPriority;
            var pb = b.Options.Priority ?? b.Metadata.DefaultPriority;
            return pa.CompareTo(pb);
        });

        return result;
    }

    /// <summary>
    /// Gets the effective fallback chain sorted by priority (enabled providers only).
    /// </summary>
    public async Task<List<BackfillProviderStatusDto>> GetFallbackChainAsync(
        BackfillProvidersConfigDto? config,
        CancellationToken ct = default)
    {
        var allStatuses = await GetProviderStatusesAsync(config, ct);
        return allStatuses.Where(s => s.Options.Enabled).ToList();
    }

    /// <summary>
    /// Generates a dry-run backfill plan showing which providers would be selected per symbol.
    /// </summary>
    public async Task<BackfillDryRunPlanDto> GenerateDryRunPlanAsync(
        BackfillProvidersConfigDto? config,
        string[] symbols,
        CancellationToken ct = default)
    {
        var fallbackChain = await GetFallbackChainAsync(config, ct);
        var warnings = new List<string>();
        var errors = new List<string>();
        var symbolPlans = new List<BackfillSymbolPlanDto>();

        if (fallbackChain.Count == 0)
        {
            errors.Add("No enabled providers available. Enable at least one provider.");
            return new BackfillDryRunPlanDto
            {
                Symbols = [],
                Warnings = warnings.ToArray(),
                ValidationErrors = errors.ToArray(),
            };
        }

        var providerSequence = fallbackChain
            .Select(p => p.Metadata.ProviderId)
            .ToArray();

        foreach (var symbol in symbols)
        {
            symbolPlans.Add(new BackfillSymbolPlanDto
            {
                Symbol = symbol,
                ProviderSequence = providerSequence,
                SelectedProvider = providerSequence.FirstOrDefault(),
                Reason = $"Highest priority enabled provider (priority {fallbackChain[0].Options.Priority ?? fallbackChain[0].Metadata.DefaultPriority})",
            });
        }

        // Check for throttled providers
        foreach (var provider in fallbackChain.Where(p => p.IsThrottled))
        {
            warnings.Add($"Provider '{provider.Metadata.DisplayName}' is currently throttled. Requests may be delayed.");
        }

        // Check for duplicate priorities
        var duplicatePriorities = fallbackChain
            .Where(p => p.Options.Priority != null)
            .GroupBy(p => p.Options.Priority!.Value)
            .Where(g => g.Count() > 1);

        foreach (var group in duplicatePriorities)
        {
            var names = string.Join(", ", group.Select(g => g.Metadata.DisplayName));
            warnings.Add($"Providers share priority {group.Key}: {names}. Fallback order may be ambiguous.");
        }

        return new BackfillDryRunPlanDto
        {
            Symbols = symbolPlans.ToArray(),
            Warnings = warnings.ToArray(),
            ValidationErrors = errors.ToArray(),
        };
    }

    /// <summary>
    /// Gets the default options for a provider, using its metadata defaults.
    /// </summary>
    public async Task<BackfillProviderOptionsDto> GetDefaultOptionsAsync(
        string providerId,
        CancellationToken ct = default)
    {
        var metadata = await GetProviderMetadataAsync(ct);
        var meta = metadata.FirstOrDefault(m =>
            string.Equals(m.ProviderId, providerId, StringComparison.OrdinalIgnoreCase));

        if (meta == null)
        {
            return new BackfillProviderOptionsDto { Enabled = true };
        }

        return new BackfillProviderOptionsDto
        {
            Enabled = true,
            Priority = meta.DefaultPriority,
            RateLimitPerMinute = meta.DefaultRateLimitPerMinute,
            RateLimitPerHour = meta.DefaultRateLimitPerHour,
        };
    }

    /// <summary>
    /// Records a configuration change in the audit log.
    /// </summary>
    public void RecordAuditEntry(
        string providerId,
        string action,
        string? previousValue,
        string? newValue)
    {
        var entry = new ProviderConfigAuditEntryDto
        {
            Timestamp = DateTime.UtcNow,
            ProviderId = providerId,
            Action = action,
            PreviousValue = previousValue,
            NewValue = newValue,
            Source = "desktop",
        };

        lock (_auditLock)
        {
            _auditLog.Add(entry);

            // Keep only the last 500 entries
            if (_auditLog.Count > 500)
            {
                _auditLog.RemoveRange(0, _auditLog.Count - 500);
            }
        }
    }

    /// <summary>
    /// Gets the audit log of provider configuration changes.
    /// </summary>
    public List<ProviderConfigAuditEntryDto> GetAuditLog(int maxEntries = 100)
    {
        lock (_auditLock)
        {
            return _auditLog
                .OrderByDescending(e => e.Timestamp)
                .Take(maxEntries)
                .ToList();
        }
    }

    private static BackfillProviderOptionsDto? GetProviderOptionsFromConfig(
        BackfillProvidersConfigDto? config,
        string providerId)
    {
        if (config == null) return null;

        return providerId.ToLowerInvariant() switch
        {
            "alpaca" => config.Alpaca,
            "polygon" => config.Polygon,
            "tiingo" => config.Tiingo,
            "finnhub" => config.Finnhub,
            "stooq" => config.Stooq,
            "yahoo" => config.Yahoo,
            "alphavantage" => config.AlphaVantage,
            "nasdaqdatalink" => config.NasdaqDataLink,
            _ => null,
        };
    }

    private static string DetermineConfigSource(
        BackfillProviderOptionsDto? options,
        BackfillProviderMetadataDto metadata)
    {
        if (options == null) return "default";

        // Check if all fields match defaults — if so, it's effectively the default config
        if (options.Priority == metadata.DefaultPriority
            && options.RateLimitPerMinute == metadata.DefaultRateLimitPerMinute
            && options.RateLimitPerHour == metadata.DefaultRateLimitPerHour)
        {
            return "default";
        }

        // Check if credentials are set via environment variables
        if (metadata.RequiresApiKey && metadata.HasCredentials)
        {
            return "env";
        }

        return "user";
    }
}
