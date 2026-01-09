using System.Text.Json;
using System.Threading;
using MarketDataCollector.Application.Backfill;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Application.Pipeline;
using MarketDataCollector.Infrastructure.Providers.Backfill;
using MarketDataCollector.Storage;
using MarketDataCollector.Storage.Policies;
using MarketDataCollector.Storage.Sinks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ConfigStore>();
builder.Services.AddSingleton<BackfillCoordinator>();

var app = builder.Build();

app.MapGet("/", (ConfigStore store) =>
{
    var html = HtmlTemplates.Index(store.ConfigPath, store.GetStatusPath(), store.GetBackfillStatusPath());
    return Results.Content(html, "text/html");
});

app.MapGet("/api/config", (ConfigStore store) =>
{
    var cfg = store.Load();
    return Results.Json(new
    {
        dataRoot = cfg.DataRoot,
        compress = cfg.Compress,
        dataSource = cfg.DataSource.ToString(),
        alpaca = cfg.Alpaca,
        storage = cfg.Storage,
        symbols = cfg.Symbols ?? Array.Empty<SymbolConfig>(),
        backfill = cfg.Backfill
    }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
});

app.MapPost("/api/config/datasource", async (ConfigStore store, DataSourceRequest req) =>
{
    var cfg = store.Load();

    if (!Enum.TryParse<DataSourceKind>(req.DataSource, ignoreCase: true, out var ds))
        return Results.BadRequest("Invalid DataSource. Use 'IB' or 'Alpaca'.");

    var next = cfg with { DataSource = ds };
    await store.SaveAsync(next);

    return Results.Ok();
});

app.MapPost("/api/config/alpaca", async (ConfigStore store, AlpacaOptions alpaca) =>
{
    var cfg = store.Load();
    var next = cfg with { Alpaca = alpaca };
    await store.SaveAsync(next);
    return Results.Ok();
});

app.MapPost("/api/config/storage", async (ConfigStore store, StorageSettingsRequest req) =>
{
    var cfg = store.Load();
    var storage = new StorageConfig(
        NamingConvention: req.NamingConvention ?? "BySymbol",
        DatePartition: req.DatePartition ?? "Daily",
        IncludeProvider: req.IncludeProvider,
        FilePrefix: string.IsNullOrWhiteSpace(req.FilePrefix) ? null : req.FilePrefix
    );
    var next = cfg with
    {
        DataRoot = string.IsNullOrWhiteSpace(req.DataRoot) ? "data" : req.DataRoot,
        Compress = req.Compress,
        Storage = storage
    };
    await store.SaveAsync(next);
    return Results.Ok();
});

app.MapPost("/api/config/symbols", async (ConfigStore store, SymbolConfig symbol) =>
{
    if (string.IsNullOrWhiteSpace(symbol.Symbol))
        return Results.BadRequest("Symbol is required.");

    var cfg = store.Load();

    var list = (cfg.Symbols ?? Array.Empty<SymbolConfig>()).ToList();
    var idx = list.FindIndex(s => string.Equals(s.Symbol, symbol.Symbol, StringComparison.OrdinalIgnoreCase));
    if (idx >= 0) list[idx] = symbol;
    else list.Add(symbol);

    var next = cfg with { Symbols = list.ToArray() };
    await store.SaveAsync(next);

    return Results.Ok();
});

app.MapDelete("/api/config/symbols/{symbol}", async (ConfigStore store, string symbol) =>
{
    var cfg = store.Load();
    var list = (cfg.Symbols ?? Array.Empty<SymbolConfig>()).ToList();
    list.RemoveAll(s => string.Equals(s.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
    var next = cfg with { Symbols = list.ToArray() };
    await store.SaveAsync(next);
    return Results.Ok();
});

// Data Sources API endpoints
app.MapGet("/api/config/datasources", (ConfigStore store) =>
{
    var cfg = store.Load();
    return Results.Json(new
    {
        sources = cfg.DataSources?.Sources ?? Array.Empty<DataSourceConfig>(),
        defaultRealTimeSourceId = cfg.DataSources?.DefaultRealTimeSourceId,
        defaultHistoricalSourceId = cfg.DataSources?.DefaultHistoricalSourceId,
        enableFailover = cfg.DataSources?.EnableFailover ?? true,
        failoverTimeoutSeconds = cfg.DataSources?.FailoverTimeoutSeconds ?? 30
    }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
});

app.MapPost("/api/config/datasources", async (ConfigStore store, DataSourceConfigRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Name))
        return Results.BadRequest("Name is required.");

    var cfg = store.Load();
    var dataSources = cfg.DataSources ?? new DataSourcesConfig();
    var sources = (dataSources.Sources ?? Array.Empty<DataSourceConfig>()).ToList();

    var id = string.IsNullOrWhiteSpace(req.Id) ? Guid.NewGuid().ToString("N") : req.Id;
    var source = new DataSourceConfig(
        Id: id,
        Name: req.Name,
        Provider: Enum.TryParse<DataSourceKind>(req.Provider, ignoreCase: true, out var p) ? p : DataSourceKind.IB,
        Enabled: req.Enabled,
        Type: Enum.TryParse<DataSourceType>(req.Type, ignoreCase: true, out var t) ? t : DataSourceType.RealTime,
        Priority: req.Priority,
        Alpaca: req.Alpaca,
        Polygon: req.Polygon,
        IB: req.IB,
        Symbols: req.Symbols,
        Description: req.Description,
        Tags: req.Tags
    );

    var idx = sources.FindIndex(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
    if (idx >= 0) sources[idx] = source;
    else sources.Add(source);

    var next = cfg with { DataSources = dataSources with { Sources = sources.ToArray() } };
    await store.SaveAsync(next);

    return Results.Ok(new { id });
});

app.MapDelete("/api/config/datasources/{id}", async (ConfigStore store, string id) =>
{
    var cfg = store.Load();
    var dataSources = cfg.DataSources ?? new DataSourcesConfig();
    var sources = (dataSources.Sources ?? Array.Empty<DataSourceConfig>()).ToList();

    sources.RemoveAll(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

    var next = cfg with { DataSources = dataSources with { Sources = sources.ToArray() } };
    await store.SaveAsync(next);

    return Results.Ok();
});

app.MapPost("/api/config/datasources/{id}/toggle", async (ConfigStore store, string id, ToggleRequest req) =>
{
    var cfg = store.Load();
    var dataSources = cfg.DataSources ?? new DataSourcesConfig();
    var sources = (dataSources.Sources ?? Array.Empty<DataSourceConfig>()).ToList();

    var source = sources.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
    if (source == null)
        return Results.NotFound();

    var idx = sources.IndexOf(source);
    sources[idx] = source with { Enabled = req.Enabled };

    var next = cfg with { DataSources = dataSources with { Sources = sources.ToArray() } };
    await store.SaveAsync(next);

    return Results.Ok();
});

app.MapPost("/api/config/datasources/defaults", async (ConfigStore store, DefaultSourcesRequest req) =>
{
    var cfg = store.Load();
    var dataSources = cfg.DataSources ?? new DataSourcesConfig();

    var next = cfg with
    {
        DataSources = dataSources with
        {
            DefaultRealTimeSourceId = req.DefaultRealTimeSourceId,
            DefaultHistoricalSourceId = req.DefaultHistoricalSourceId
        }
    };
    await store.SaveAsync(next);

    return Results.Ok();
});

app.MapPost("/api/config/datasources/failover", async (ConfigStore store, FailoverSettingsRequest req) =>
{
    var cfg = store.Load();
    var dataSources = cfg.DataSources ?? new DataSourcesConfig();

    var next = cfg with
    {
        DataSources = dataSources with
        {
            EnableFailover = req.EnableFailover,
            FailoverTimeoutSeconds = req.FailoverTimeoutSeconds
        }
    };
    await store.SaveAsync(next);

    return Results.Ok();
});

app.MapGet("/api/status", (ConfigStore store) =>
{
    var status = store.TryLoadStatusJson();
    return status is null ? Results.NotFound() : Results.Content(status, "application/json");
});

app.MapGet("/api/backfill/providers", (BackfillCoordinator backfill) =>
{
    var providers = backfill.DescribeProviders();
    return Results.Json(providers, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
});

app.MapGet("/api/backfill/status", (BackfillCoordinator backfill) =>
{
    var status = backfill.TryReadLast();
    return status is null
        ? Results.NotFound()
        : Results.Json(status, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });
});

app.MapPost("/api/backfill/run", async (BackfillCoordinator backfill, BackfillRequestDto req) =>
{
    if (req.Symbols is null || req.Symbols.Length == 0)
        return Results.BadRequest("At least one symbol is required.");

    try
    {
        var request = new BackfillRequest(
            string.IsNullOrWhiteSpace(req.Provider) ? "stooq" : req.Provider!,
            req.Symbols,
            req.From,
            req.To);

        var result = await backfill.RunAsync(request);
        return Results.Json(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

// ============================================================
// Multi-Provider API Endpoints
// ============================================================

// Provider Comparison View
app.MapGet("/api/providers/comparison", (ConfigStore store) =>
{
    // Return mock comparison data for now - real implementation would use MultiProviderService
    var comparison = new ProviderComparisonResponse(
        Timestamp: DateTimeOffset.UtcNow,
        Providers: Array.Empty<ProviderMetricsResponse>(),
        TotalProviders: 0,
        HealthyProviders: 0
    );
    return Results.Json(comparison, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
});

app.MapGet("/api/providers/status", (ConfigStore store) =>
{
    // Return provider connection status
    var cfg = store.Load();
    var sources = cfg.DataSources?.Sources ?? Array.Empty<DataSourceConfig>();

    var status = sources.Select(s => new ProviderStatusResponse(
        ProviderId: s.Id,
        Name: s.Name,
        ProviderType: s.Provider.ToString(),
        IsConnected: s.Enabled, // Simplified - real impl checks actual connection
        IsEnabled: s.Enabled,
        Priority: s.Priority,
        ActiveSubscriptions: 0,
        LastHeartbeat: DateTimeOffset.UtcNow
    )).ToArray();

    return Results.Json(status, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
});

app.MapGet("/api/providers/metrics", (ConfigStore store) =>
{
    // Return detailed metrics for all providers
    var cfg = store.Load();
    var sources = cfg.DataSources?.Sources ?? Array.Empty<DataSourceConfig>();

    var metrics = sources.Select(s => new ProviderMetricsResponse(
        ProviderId: s.Id,
        ProviderType: s.Provider.ToString(),
        TradesReceived: 0,
        DepthUpdatesReceived: 0,
        QuotesReceived: 0,
        ConnectionAttempts: 0,
        ConnectionFailures: 0,
        MessagesDropped: 0,
        ActiveSubscriptions: 0,
        AverageLatencyMs: 0,
        MinLatencyMs: 0,
        MaxLatencyMs: 0,
        DataQualityScore: 100,
        ConnectionSuccessRate: 100,
        Timestamp: DateTimeOffset.UtcNow
    )).ToArray();

    return Results.Json(metrics, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
});

app.MapGet("/api/providers/{providerId}/metrics", (ConfigStore store, string providerId) =>
{
    var cfg = store.Load();
    var source = cfg.DataSources?.Sources?.FirstOrDefault(s =>
        string.Equals(s.Id, providerId, StringComparison.OrdinalIgnoreCase));

    if (source == null)
        return Results.NotFound();

    var metrics = new ProviderMetricsResponse(
        ProviderId: source.Id,
        ProviderType: source.Provider.ToString(),
        TradesReceived: 0,
        DepthUpdatesReceived: 0,
        QuotesReceived: 0,
        ConnectionAttempts: 0,
        ConnectionFailures: 0,
        MessagesDropped: 0,
        ActiveSubscriptions: 0,
        AverageLatencyMs: 0,
        MinLatencyMs: 0,
        MaxLatencyMs: 0,
        DataQualityScore: 100,
        ConnectionSuccessRate: 100,
        Timestamp: DateTimeOffset.UtcNow
    );

    return Results.Json(metrics, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
});

// ============================================================
// Failover Configuration API Endpoints
// ============================================================

app.MapGet("/api/failover/config", (ConfigStore store) =>
{
    var cfg = store.Load();
    var dataSources = cfg.DataSources ?? new DataSourcesConfig();

    var response = new FailoverConfigResponse(
        EnableFailover: dataSources.EnableFailover,
        HealthCheckIntervalSeconds: dataSources.HealthCheckIntervalSeconds,
        AutoRecover: dataSources.AutoRecover,
        FailoverTimeoutSeconds: dataSources.FailoverTimeoutSeconds,
        Rules: (dataSources.FailoverRules ?? Array.Empty<FailoverRuleConfig>())
            .Select(r => new FailoverRuleResponse(
                Id: r.Id,
                PrimaryProviderId: r.PrimaryProviderId,
                BackupProviderIds: r.BackupProviderIds,
                FailoverThreshold: r.FailoverThreshold,
                RecoveryThreshold: r.RecoveryThreshold,
                DataQualityThreshold: r.DataQualityThreshold,
                MaxLatencyMs: r.MaxLatencyMs,
                IsInFailoverState: false,
                CurrentActiveProviderId: r.PrimaryProviderId
            )).ToArray()
    );

    return Results.Json(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
});

app.MapPost("/api/failover/config", async (ConfigStore store, FailoverConfigRequest req) =>
{
    var cfg = store.Load();
    var dataSources = cfg.DataSources ?? new DataSourcesConfig();

    var next = cfg with
    {
        DataSources = dataSources with
        {
            EnableFailover = req.EnableFailover,
            HealthCheckIntervalSeconds = req.HealthCheckIntervalSeconds,
            AutoRecover = req.AutoRecover,
            FailoverTimeoutSeconds = req.FailoverTimeoutSeconds
        }
    };
    await store.SaveAsync(next);

    return Results.Ok();
});

app.MapGet("/api/failover/rules", (ConfigStore store) =>
{
    var cfg = store.Load();
    var rules = cfg.DataSources?.FailoverRules ?? Array.Empty<FailoverRuleConfig>();

    var response = rules.Select(r => new FailoverRuleResponse(
        Id: r.Id,
        PrimaryProviderId: r.PrimaryProviderId,
        BackupProviderIds: r.BackupProviderIds,
        FailoverThreshold: r.FailoverThreshold,
        RecoveryThreshold: r.RecoveryThreshold,
        DataQualityThreshold: r.DataQualityThreshold,
        MaxLatencyMs: r.MaxLatencyMs,
        IsInFailoverState: false,
        CurrentActiveProviderId: r.PrimaryProviderId
    )).ToArray();

    return Results.Json(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
});

app.MapPost("/api/failover/rules", async (ConfigStore store, FailoverRuleRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.PrimaryProviderId))
        return Results.BadRequest("PrimaryProviderId is required.");

    if (req.BackupProviderIds is null || req.BackupProviderIds.Length == 0)
        return Results.BadRequest("At least one backup provider is required.");

    var cfg = store.Load();
    var dataSources = cfg.DataSources ?? new DataSourcesConfig();
    var rules = (dataSources.FailoverRules ?? Array.Empty<FailoverRuleConfig>()).ToList();

    var id = string.IsNullOrWhiteSpace(req.Id) ? Guid.NewGuid().ToString("N") : req.Id;
    var rule = new FailoverRuleConfig(
        Id: id,
        PrimaryProviderId: req.PrimaryProviderId,
        BackupProviderIds: req.BackupProviderIds,
        FailoverThreshold: req.FailoverThreshold,
        RecoveryThreshold: req.RecoveryThreshold,
        DataQualityThreshold: req.DataQualityThreshold,
        MaxLatencyMs: req.MaxLatencyMs
    );

    var idx = rules.FindIndex(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
    if (idx >= 0) rules[idx] = rule;
    else rules.Add(rule);

    var next = cfg with { DataSources = dataSources with { FailoverRules = rules.ToArray() } };
    await store.SaveAsync(next);

    return Results.Ok(new { id });
});

app.MapDelete("/api/failover/rules/{id}", async (ConfigStore store, string id) =>
{
    var cfg = store.Load();
    var dataSources = cfg.DataSources ?? new DataSourcesConfig();
    var rules = (dataSources.FailoverRules ?? Array.Empty<FailoverRuleConfig>()).ToList();

    var removed = rules.RemoveAll(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase)) > 0;
    if (!removed)
        return Results.NotFound();

    var next = cfg with { DataSources = dataSources with { FailoverRules = rules.ToArray() } };
    await store.SaveAsync(next);

    return Results.Ok();
});

app.MapPost("/api/failover/force/{ruleId}", (ConfigStore store, string ruleId, ForceFailoverRequest req) =>
{
    // In a real implementation, this would trigger actual failover via MultiProviderService
    return Results.Ok(new { success = true, message = $"Failover triggered for rule {ruleId} to provider {req.TargetProviderId}" });
});

app.MapGet("/api/failover/health", (ConfigStore store) =>
{
    var cfg = store.Load();
    var sources = cfg.DataSources?.Sources ?? Array.Empty<DataSourceConfig>();

    var health = sources.Select(s => new ProviderHealthResponse(
        ProviderId: s.Id,
        ConsecutiveFailures: 0,
        ConsecutiveSuccesses: s.Enabled ? 10 : 0,
        LastIssueTime: null,
        LastSuccessTime: s.Enabled ? DateTimeOffset.UtcNow : null,
        RecentIssues: Array.Empty<HealthIssueResponse>()
    )).ToArray();

    return Results.Json(health, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
});

// ============================================================
// Symbol Mapping API Endpoints
// ============================================================

app.MapGet("/api/symbols/mappings", (ConfigStore store) =>
{
    var cfg = store.Load();
    var mappings = cfg.DataSources?.SymbolMappings?.Mappings ?? Array.Empty<SymbolMappingConfig>();

    var response = mappings.Select(m => new SymbolMappingResponse(
        CanonicalSymbol: m.CanonicalSymbol,
        IbSymbol: m.IbSymbol,
        AlpacaSymbol: m.AlpacaSymbol,
        PolygonSymbol: m.PolygonSymbol,
        YahooSymbol: m.YahooSymbol,
        Name: m.Name,
        Figi: m.Figi
    )).ToArray();

    return Results.Json(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
});

app.MapPost("/api/symbols/mappings", async (ConfigStore store, SymbolMappingRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.CanonicalSymbol))
        return Results.BadRequest("CanonicalSymbol is required.");

    var cfg = store.Load();
    var dataSources = cfg.DataSources ?? new DataSourcesConfig();
    var symbolMappings = dataSources.SymbolMappings ?? new SymbolMappingsConfig();
    var mappings = (symbolMappings.Mappings ?? Array.Empty<SymbolMappingConfig>()).ToList();

    var mapping = new SymbolMappingConfig(
        CanonicalSymbol: req.CanonicalSymbol.ToUpperInvariant(),
        IbSymbol: req.IbSymbol,
        AlpacaSymbol: req.AlpacaSymbol,
        PolygonSymbol: req.PolygonSymbol,
        YahooSymbol: req.YahooSymbol,
        Name: req.Name,
        Figi: req.Figi
    );

    var idx = mappings.FindIndex(m =>
        string.Equals(m.CanonicalSymbol, req.CanonicalSymbol, StringComparison.OrdinalIgnoreCase));
    if (idx >= 0) mappings[idx] = mapping;
    else mappings.Add(mapping);

    var next = cfg with
    {
        DataSources = dataSources with
        {
            SymbolMappings = symbolMappings with { Mappings = mappings.ToArray() }
        }
    };
    await store.SaveAsync(next);

    return Results.Ok();
});

app.MapDelete("/api/symbols/mappings/{symbol}", async (ConfigStore store, string symbol) =>
{
    var cfg = store.Load();
    var dataSources = cfg.DataSources ?? new DataSourcesConfig();
    var symbolMappings = dataSources.SymbolMappings ?? new SymbolMappingsConfig();
    var mappings = (symbolMappings.Mappings ?? Array.Empty<SymbolMappingConfig>()).ToList();

    var removed = mappings.RemoveAll(m =>
        string.Equals(m.CanonicalSymbol, symbol, StringComparison.OrdinalIgnoreCase)) > 0;
    if (!removed)
        return Results.NotFound();

    var next = cfg with
    {
        DataSources = dataSources with
        {
            SymbolMappings = symbolMappings with { Mappings = mappings.ToArray() }
        }
    };
    await store.SaveAsync(next);

    return Results.Ok();
});

app.MapGet("/api/symbols/mappings/{symbol}", (ConfigStore store, string symbol) =>
{
    var cfg = store.Load();
    var mapping = cfg.DataSources?.SymbolMappings?.Mappings?
        .FirstOrDefault(m => string.Equals(m.CanonicalSymbol, symbol, StringComparison.OrdinalIgnoreCase));

    if (mapping == null)
        return Results.NotFound();

    var response = new SymbolMappingResponse(
        CanonicalSymbol: mapping.CanonicalSymbol,
        IbSymbol: mapping.IbSymbol,
        AlpacaSymbol: mapping.AlpacaSymbol,
        PolygonSymbol: mapping.PolygonSymbol,
        YahooSymbol: mapping.YahooSymbol,
        Name: mapping.Name,
        Figi: mapping.Figi
    );

    return Results.Json(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
});

app.MapPost("/api/symbols/mappings/import", async (ConfigStore store, HttpRequest request) =>
{
    // Import symbol mappings from CSV
    if (!request.HasFormContentType)
        return Results.BadRequest("Content must be multipart/form-data");

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");

    if (file == null || file.Length == 0)
        return Results.BadRequest("No file uploaded");

    var mappings = new List<SymbolMappingConfig>();

    using (var reader = new StreamReader(file.OpenReadStream()))
    {
        var headerParsed = false;
        var providerColumns = new Dictionary<int, string>();

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',');
            if (!headerParsed)
            {
                // Parse header
                for (var i = 1; i < parts.Length; i++)
                {
                    providerColumns[i] = parts[i].Trim().ToLowerInvariant();
                }
                headerParsed = true;
                continue;
            }

            if (parts.Length < 2) continue;

            var canonicalSymbol = parts[0].Trim();
            if (string.IsNullOrWhiteSpace(canonicalSymbol)) continue;

            string? ibSymbol = null, alpacaSymbol = null, polygonSymbol = null, yahooSymbol = null, name = null, figi = null;

            foreach (var (colIndex, colName) in providerColumns)
            {
                if (colIndex < parts.Length)
                {
                    var value = parts[colIndex].Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        switch (colName)
                        {
                            case "ib": ibSymbol = value; break;
                            case "alpaca": alpacaSymbol = value; break;
                            case "polygon": polygonSymbol = value; break;
                            case "yahoo": yahooSymbol = value; break;
                            case "name": name = value; break;
                            case "figi": figi = value; break;
                        }
                    }
                }
            }

            mappings.Add(new SymbolMappingConfig(
                CanonicalSymbol: canonicalSymbol.ToUpperInvariant(),
                IbSymbol: ibSymbol,
                AlpacaSymbol: alpacaSymbol,
                PolygonSymbol: polygonSymbol,
                YahooSymbol: yahooSymbol,
                Name: name,
                Figi: figi
            ));
        }
    }

    // Merge with existing mappings
    var cfg = store.Load();
    var dataSources = cfg.DataSources ?? new DataSourcesConfig();
    var symbolMappings = dataSources.SymbolMappings ?? new SymbolMappingsConfig();
    var existingMappings = (symbolMappings.Mappings ?? Array.Empty<SymbolMappingConfig>()).ToList();

    foreach (var mapping in mappings)
    {
        var idx = existingMappings.FindIndex(m =>
            string.Equals(m.CanonicalSymbol, mapping.CanonicalSymbol, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) existingMappings[idx] = mapping;
        else existingMappings.Add(mapping);
    }

    var next = cfg with
    {
        DataSources = dataSources with
        {
            SymbolMappings = symbolMappings with { Mappings = existingMappings.ToArray() }
        }
    };
    await store.SaveAsync(next);

    return Results.Ok(new { imported = mappings.Count });
});

// ============================================================
// Dashboard Views
// ============================================================

app.MapGet("/providers", (ConfigStore store) =>
{
    var html = HtmlTemplates.ProvidersView(store.ConfigPath);
    return Results.Content(html, "text/html");
});

app.Run();

public record DataSourceRequest(string DataSource);
public record StorageSettingsRequest(string? DataRoot, bool Compress, string? NamingConvention, string? DatePartition, bool IncludeProvider, string? FilePrefix);

// Data Sources API DTOs
public record DataSourceConfigRequest(
    string? Id,
    string Name,
    string Provider = "IB",
    bool Enabled = true,
    string Type = "RealTime",
    int Priority = 100,
    AlpacaOptions? Alpaca = null,
    PolygonOptions? Polygon = null,
    IBOptions? IB = null,
    string[]? Symbols = null,
    string? Description = null,
    string[]? Tags = null);

public record ToggleRequest(bool Enabled);
public record DefaultSourcesRequest(string? DefaultRealTimeSourceId, string? DefaultHistoricalSourceId);
public record FailoverSettingsRequest(bool EnableFailover, int FailoverTimeoutSeconds);

// Multi-Provider DTOs
public record ProviderComparisonResponse(
    DateTimeOffset Timestamp,
    ProviderMetricsResponse[] Providers,
    int TotalProviders,
    int HealthyProviders
);

public record ProviderStatusResponse(
    string ProviderId,
    string Name,
    string ProviderType,
    bool IsConnected,
    bool IsEnabled,
    int Priority,
    int ActiveSubscriptions,
    DateTimeOffset? LastHeartbeat
);

public record ProviderMetricsResponse(
    string ProviderId,
    string ProviderType,
    long TradesReceived,
    long DepthUpdatesReceived,
    long QuotesReceived,
    long ConnectionAttempts,
    long ConnectionFailures,
    long MessagesDropped,
    long ActiveSubscriptions,
    double AverageLatencyMs,
    double MinLatencyMs,
    double MaxLatencyMs,
    double DataQualityScore,
    double ConnectionSuccessRate,
    DateTimeOffset Timestamp
);

// Failover DTOs
public record FailoverConfigResponse(
    bool EnableFailover,
    int HealthCheckIntervalSeconds,
    bool AutoRecover,
    int FailoverTimeoutSeconds,
    FailoverRuleResponse[] Rules
);

public record FailoverConfigRequest(
    bool EnableFailover,
    int HealthCheckIntervalSeconds = 10,
    bool AutoRecover = true,
    int FailoverTimeoutSeconds = 30
);

public record FailoverRuleResponse(
    string Id,
    string PrimaryProviderId,
    string[] BackupProviderIds,
    int FailoverThreshold,
    int RecoveryThreshold,
    double DataQualityThreshold,
    double MaxLatencyMs,
    bool IsInFailoverState,
    string? CurrentActiveProviderId
);

public record FailoverRuleRequest(
    string? Id,
    string PrimaryProviderId,
    string[] BackupProviderIds,
    int FailoverThreshold = 3,
    int RecoveryThreshold = 5,
    double DataQualityThreshold = 0,
    double MaxLatencyMs = 0
);

public record ForceFailoverRequest(string TargetProviderId);

public record ProviderHealthResponse(
    string ProviderId,
    int ConsecutiveFailures,
    int ConsecutiveSuccesses,
    DateTimeOffset? LastIssueTime,
    DateTimeOffset? LastSuccessTime,
    HealthIssueResponse[] RecentIssues
);

public record HealthIssueResponse(
    string Type,
    string? Message,
    DateTimeOffset Timestamp
);

// Symbol Mapping DTOs
public record SymbolMappingResponse(
    string CanonicalSymbol,
    string? IbSymbol,
    string? AlpacaSymbol,
    string? PolygonSymbol,
    string? YahooSymbol,
    string? Name,
    string? Figi
);

public record SymbolMappingRequest(
    string CanonicalSymbol,
    string? IbSymbol = null,
    string? AlpacaSymbol = null,
    string? PolygonSymbol = null,
    string? YahooSymbol = null,
    string? Name = null,
    string? Figi = null
);

public static class HtmlTemplates
{
    public static string Index(string configPath, string statusPath, string backfillPath) => $@"
<!doctype html>
<html>
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width,initial-scale=1"" />
  <title>MDC Terminal</title>
  <style>
    :root {{
      --bg-primary: #0d1117;
      --bg-secondary: #161b22;
      --bg-tertiary: #21262d;
      --bg-hover: #30363d;
      --border-default: #30363d;
      --border-muted: #21262d;
      --text-primary: #e6edf3;
      --text-secondary: #8b949e;
      --text-muted: #6e7681;
      --accent-green: #3fb950;
      --accent-green-dim: #238636;
      --accent-blue: #58a6ff;
      --accent-purple: #a371f7;
      --accent-red: #f85149;
      --accent-orange: #d29922;
      --accent-cyan: #39c5cf;
      --glow-green: 0 0 20px rgba(63, 185, 80, 0.3);
      --glow-blue: 0 0 20px rgba(88, 166, 255, 0.3);
      --glow-red: 0 0 20px rgba(248, 81, 73, 0.3);
      --font-mono: 'JetBrains Mono', 'Fira Code', 'SF Mono', Consolas, monospace;
      --font-sans: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
    }}

    * {{ box-sizing: border-box; }}

    body {{
      font-family: var(--font-sans);
      margin: 0;
      padding: 0;
      background: var(--bg-primary);
      color: var(--text-primary);
      min-height: 100vh;
    }}

    /* Scanline effect overlay */
    body::before {{
      content: '';
      position: fixed;
      top: 0;
      left: 0;
      width: 100%;
      height: 100%;
      background: repeating-linear-gradient(
        0deg,
        transparent,
        transparent 2px,
        rgba(0, 0, 0, 0.03) 2px,
        rgba(0, 0, 0, 0.03) 4px
      );
      pointer-events: none;
      z-index: 1000;
    }}

    /* Top Navigation Bar */
    .top-bar {{
      background: var(--bg-secondary);
      border-bottom: 1px solid var(--border-default);
      padding: 12px 24px;
      display: flex;
      align-items: center;
      justify-content: space-between;
      position: sticky;
      top: 0;
      z-index: 100;
      backdrop-filter: blur(10px);
    }}

    .logo {{
      display: flex;
      align-items: center;
      gap: 12px;
    }}

    .logo-icon {{
      width: 32px;
      height: 32px;
      background: linear-gradient(135deg, var(--accent-green) 0%, var(--accent-cyan) 100%);
      border-radius: 8px;
      display: flex;
      align-items: center;
      justify-content: center;
      font-family: var(--font-mono);
      font-weight: 700;
      font-size: 14px;
      color: var(--bg-primary);
    }}

    .logo-text {{
      font-family: var(--font-mono);
      font-size: 18px;
      font-weight: 600;
      background: linear-gradient(90deg, var(--accent-green), var(--accent-cyan));
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
      background-clip: text;
    }}

    .logo-version {{
      font-size: 11px;
      color: var(--text-muted);
      background: var(--bg-tertiary);
      padding: 2px 8px;
      border-radius: 4px;
      font-family: var(--font-mono);
    }}

    /* Command Palette */
    .cmd-palette {{
      display: flex;
      align-items: center;
      background: var(--bg-tertiary);
      border: 1px solid var(--border-default);
      border-radius: 8px;
      padding: 8px 16px;
      min-width: 400px;
      cursor: pointer;
      transition: all 0.2s ease;
    }}

    .cmd-palette:hover {{
      border-color: var(--accent-blue);
      box-shadow: var(--glow-blue);
    }}

    .cmd-palette-icon {{
      color: var(--text-muted);
      margin-right: 12px;
    }}

    .cmd-palette-text {{
      color: var(--text-muted);
      flex: 1;
      font-size: 14px;
    }}

    .cmd-palette-shortcut {{
      display: flex;
      gap: 4px;
    }}

    .kbd {{
      background: var(--bg-secondary);
      border: 1px solid var(--border-default);
      border-radius: 4px;
      padding: 2px 6px;
      font-family: var(--font-mono);
      font-size: 11px;
      color: var(--text-secondary);
    }}

    /* Status indicators in top bar */
    .top-status {{
      display: flex;
      align-items: center;
      gap: 16px;
    }}

    .status-indicator {{
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 6px 12px;
      background: var(--bg-tertiary);
      border-radius: 6px;
      font-size: 13px;
    }}

    .status-dot {{
      width: 8px;
      height: 8px;
      border-radius: 50%;
      animation: pulse 2s infinite;
    }}

    .status-dot.connected {{ background: var(--accent-green); box-shadow: 0 0 10px var(--accent-green); }}
    .status-dot.disconnected {{ background: var(--accent-red); box-shadow: 0 0 10px var(--accent-red); }}
    .status-dot.warning {{ background: var(--accent-orange); box-shadow: 0 0 10px var(--accent-orange); }}

    @keyframes pulse {{
      0%, 100% {{ opacity: 1; }}
      50% {{ opacity: 0.5; }}
    }}

    /* Main container */
    .main-container {{
      display: flex;
      min-height: calc(100vh - 60px);
    }}

    /* Sidebar */
    .sidebar {{
      width: 240px;
      background: var(--bg-secondary);
      border-right: 1px solid var(--border-default);
      padding: 16px 0;
      display: flex;
      flex-direction: column;
    }}

    .nav-section {{
      padding: 0 12px;
      margin-bottom: 24px;
    }}

    .nav-section-title {{
      font-size: 11px;
      font-weight: 600;
      color: var(--text-muted);
      text-transform: uppercase;
      letter-spacing: 0.5px;
      padding: 0 12px;
      margin-bottom: 8px;
    }}

    .nav-item {{
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 10px 12px;
      border-radius: 6px;
      color: var(--text-secondary);
      cursor: pointer;
      transition: all 0.15s ease;
      font-size: 14px;
    }}

    .nav-item:hover {{
      background: var(--bg-tertiary);
      color: var(--text-primary);
    }}

    .nav-item.active {{
      background: var(--bg-tertiary);
      color: var(--accent-green);
      border-left: 2px solid var(--accent-green);
      margin-left: -2px;
    }}

    .nav-item-icon {{
      width: 18px;
      text-align: center;
    }}

    .nav-item-badge {{
      margin-left: auto;
      background: var(--accent-green-dim);
      color: var(--accent-green);
      padding: 2px 8px;
      border-radius: 10px;
      font-size: 11px;
      font-weight: 600;
    }}

    /* Main content */
    .content {{
      flex: 1;
      padding: 24px;
      overflow-y: auto;
    }}

    /* Section headers */
    .section-header {{
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 16px;
    }}

    .section-title {{
      font-size: 18px;
      font-weight: 600;
      color: var(--text-primary);
      display: flex;
      align-items: center;
      gap: 10px;
    }}

    .section-title-icon {{
      color: var(--accent-green);
    }}

    /* Card styles - Console inspired */
    .row {{ display: flex; gap: 20px; flex-wrap: wrap; margin-bottom: 24px; }}

    .card {{
      background: var(--bg-secondary);
      border: 1px solid var(--border-default);
      border-radius: 12px;
      padding: 20px;
      min-width: 320px;
      position: relative;
      overflow: hidden;
      transition: all 0.2s ease;
    }}

    .card:hover {{
      border-color: var(--accent-blue);
      box-shadow: 0 4px 20px rgba(0, 0, 0, 0.3);
    }}

    .card::before {{
      content: '';
      position: absolute;
      top: 0;
      left: 0;
      right: 0;
      height: 2px;
      background: linear-gradient(90deg, var(--accent-green), var(--accent-cyan));
      opacity: 0;
      transition: opacity 0.2s ease;
    }}

    .card:hover::before {{
      opacity: 1;
    }}

    .card-header {{
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 16px;
    }}

    .card-title {{
      font-size: 14px;
      font-weight: 600;
      color: var(--text-primary);
      display: flex;
      align-items: center;
      gap: 8px;
    }}

    .card-title-icon {{
      color: var(--accent-green);
    }}

    h2, h3, h4 {{
      color: var(--text-primary);
      margin: 0 0 16px 0;
      font-weight: 600;
    }}

    h3 {{
      font-size: 16px;
      display: flex;
      align-items: center;
      gap: 10px;
    }}

    h3::before {{
      content: '>';
      color: var(--accent-green);
      font-family: var(--font-mono);
    }}

    h4 {{ font-size: 14px; color: var(--text-secondary); margin-top: 20px; }}

    /* Metric cards */
    .metrics-grid {{
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 16px;
      margin-bottom: 24px;
    }}

    .metric-card {{
      background: var(--bg-secondary);
      border: 1px solid var(--border-default);
      border-radius: 12px;
      padding: 20px;
      position: relative;
      overflow: hidden;
    }}

    .metric-card.success {{ border-left: 3px solid var(--accent-green); }}
    .metric-card.danger {{ border-left: 3px solid var(--accent-red); }}
    .metric-card.warning {{ border-left: 3px solid var(--accent-orange); }}
    .metric-card.info {{ border-left: 3px solid var(--accent-blue); }}

    .metric-value {{
      font-family: var(--font-mono);
      font-size: 32px;
      font-weight: 700;
      line-height: 1;
      margin-bottom: 8px;
    }}

    .metric-value.success {{ color: var(--accent-green); }}
    .metric-value.danger {{ color: var(--accent-red); }}
    .metric-value.warning {{ color: var(--accent-orange); }}
    .metric-value.info {{ color: var(--accent-blue); }}

    .metric-label {{
      font-size: 12px;
      color: var(--text-muted);
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }}

    .metric-trend {{
      position: absolute;
      top: 16px;
      right: 16px;
      font-size: 12px;
      display: flex;
      align-items: center;
      gap: 4px;
    }}

    .metric-trend.up {{ color: var(--accent-green); }}
    .metric-trend.down {{ color: var(--accent-red); }}

    /* Terminal-style log display */
    .terminal {{
      background: var(--bg-primary);
      border: 1px solid var(--border-default);
      border-radius: 8px;
      font-family: var(--font-mono);
      font-size: 13px;
      overflow: hidden;
    }}

    .terminal-header {{
      background: var(--bg-tertiary);
      padding: 10px 16px;
      display: flex;
      align-items: center;
      gap: 8px;
      border-bottom: 1px solid var(--border-default);
    }}

    .terminal-dot {{
      width: 12px;
      height: 12px;
      border-radius: 50%;
    }}

    .terminal-dot.red {{ background: #ff5f56; }}
    .terminal-dot.yellow {{ background: #ffbd2e; }}
    .terminal-dot.green {{ background: #27c93f; }}

    .terminal-title {{
      color: var(--text-muted);
      font-size: 12px;
      margin-left: 8px;
    }}

    .terminal-body {{
      padding: 16px;
      max-height: 200px;
      overflow-y: auto;
    }}

    .terminal-line {{
      display: flex;
      gap: 12px;
      padding: 2px 0;
    }}

    .terminal-prompt {{
      color: var(--accent-green);
    }}

    .terminal-time {{
      color: var(--text-muted);
      min-width: 80px;
    }}

    .terminal-msg {{
      color: var(--text-secondary);
    }}

    .terminal-msg.success {{ color: var(--accent-green); }}
    .terminal-msg.error {{ color: var(--accent-red); }}
    .terminal-msg.warning {{ color: var(--accent-orange); }}

    /* Tables */
    table {{
      border-collapse: collapse;
      width: 100%;
      font-size: 13px;
    }}

    th, td {{
      padding: 12px 16px;
      text-align: left;
      border-bottom: 1px solid var(--border-muted);
    }}

    th {{
      font-weight: 600;
      color: var(--text-muted);
      text-transform: uppercase;
      font-size: 11px;
      letter-spacing: 0.5px;
      background: var(--bg-tertiary);
    }}

    tr:hover td {{
      background: var(--bg-tertiary);
    }}

    /* Form elements */
    .form-group {{
      margin-bottom: 16px;
    }}

    .form-group label {{
      display: block;
      margin-bottom: 6px;
      font-size: 13px;
      font-weight: 500;
      color: var(--text-secondary);
    }}

    .form-row {{
      display: flex;
      gap: 16px;
    }}

    .form-row > div {{
      flex: 1;
    }}

    input, select, textarea {{
      width: 100%;
      padding: 10px 14px;
      font-size: 14px;
      font-family: var(--font-mono);
      background: var(--bg-primary);
      border: 1px solid var(--border-default);
      border-radius: 8px;
      color: var(--text-primary);
      transition: all 0.15s ease;
    }}

    input:focus, select:focus, textarea:focus {{
      outline: none;
      border-color: var(--accent-blue);
      box-shadow: 0 0 0 3px rgba(88, 166, 255, 0.15);
    }}

    input::placeholder {{
      color: var(--text-muted);
    }}

    input[type=""checkbox""] {{
      width: auto;
      margin-right: 8px;
      accent-color: var(--accent-green);
    }}

    select {{
      cursor: pointer;
      appearance: none;
      background-image: url(""data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='12' height='12' fill='%238b949e' viewBox='0 0 16 16'%3E%3Cpath d='M8 11L3 6h10l-5 5z'/%3E%3C/svg%3E"");
      background-repeat: no-repeat;
      background-position: right 12px center;
      padding-right: 36px;
    }}

    /* Buttons */
    button {{
      padding: 10px 20px;
      font-size: 14px;
      font-weight: 500;
      cursor: pointer;
      border-radius: 8px;
      border: 1px solid transparent;
      transition: all 0.15s ease;
      display: inline-flex;
      align-items: center;
      gap: 8px;
    }}

    .btn-primary {{
      background: linear-gradient(135deg, var(--accent-green-dim), var(--accent-green));
      color: white;
      border: none;
    }}

    .btn-primary:hover {{
      box-shadow: var(--glow-green);
      transform: translateY(-1px);
    }}

    .btn-secondary {{
      background: var(--bg-tertiary);
      color: var(--text-primary);
      border: 1px solid var(--border-default);
    }}

    .btn-secondary:hover {{
      background: var(--bg-hover);
      border-color: var(--accent-blue);
    }}

    .btn-danger {{
      background: rgba(248, 81, 73, 0.15);
      color: var(--accent-red);
      border: 1px solid var(--accent-red);
      padding: 6px 12px;
      font-size: 12px;
    }}

    .btn-danger:hover {{
      background: var(--accent-red);
      color: white;
      box-shadow: var(--glow-red);
    }}

    .btn-icon {{
      background: transparent;
      border: none;
      color: var(--text-muted);
      padding: 8px;
      border-radius: 6px;
    }}

    .btn-icon:hover {{
      background: var(--bg-tertiary);
      color: var(--text-primary);
    }}

    /* Tags and badges */
    .tag {{
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 4px 10px;
      border-radius: 6px;
      font-size: 12px;
      font-weight: 600;
      font-family: var(--font-mono);
    }}

    .tag-ib {{
      background: rgba(88, 166, 255, 0.15);
      color: var(--accent-blue);
      border: 1px solid rgba(88, 166, 255, 0.3);
    }}

    .tag-alpaca {{
      background: rgba(210, 153, 34, 0.15);
      color: var(--accent-orange);
      border: 1px solid rgba(210, 153, 34, 0.3);
    }}

    .tag-polygon {{
      background: rgba(163, 113, 247, 0.15);
      color: var(--accent-purple);
      border: 1px solid rgba(163, 113, 247, 0.3);
    }}

    .provider-badge {{
      font-size: 10px;
      padding: 2px 6px;
      border-radius: 4px;
      font-family: var(--font-mono);
      margin-left: 6px;
    }}

    .ib-only {{
      background: rgba(88, 166, 255, 0.15);
      color: var(--accent-blue);
    }}

    .alpaca-only {{
      background: rgba(210, 153, 34, 0.15);
      color: var(--accent-orange);
    }}

    /* Status indicators */
    .muted {{ color: var(--text-muted); font-size: 13px; }}
    .good {{ color: var(--accent-green); font-weight: 600; }}
    .bad {{ color: var(--accent-red); font-weight: 600; }}

    /* Code blocks */
    code {{
      font-family: var(--font-mono);
      background: var(--bg-tertiary);
      padding: 2px 8px;
      border-radius: 4px;
      font-size: 12px;
      color: var(--accent-cyan);
    }}

    /* Sections */
    .provider-section {{
      border-top: 1px solid var(--border-muted);
      margin-top: 20px;
      padding-top: 20px;
    }}

    .hidden {{ display: none !important; }}

    /* Collapsible sections */
    .collapsible-header {{
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 12px 16px;
      background: var(--bg-tertiary);
      border-radius: 8px;
      cursor: pointer;
      transition: all 0.15s ease;
    }}

    .collapsible-header:hover {{
      background: var(--bg-hover);
    }}

    .collapsible-icon {{
      transition: transform 0.2s ease;
    }}

    .collapsible-icon.open {{
      transform: rotate(180deg);
    }}

    /* Toast notifications */
    .toast-container {{
      position: fixed;
      bottom: 24px;
      right: 24px;
      z-index: 1001;
      display: flex;
      flex-direction: column;
      gap: 8px;
    }}

    .toast {{
      background: var(--bg-secondary);
      border: 1px solid var(--border-default);
      border-radius: 8px;
      padding: 12px 20px;
      display: flex;
      align-items: center;
      gap: 12px;
      box-shadow: 0 8px 30px rgba(0, 0, 0, 0.4);
      animation: slideIn 0.3s ease;
      min-width: 300px;
    }}

    .toast.success {{ border-left: 3px solid var(--accent-green); }}
    .toast.error {{ border-left: 3px solid var(--accent-red); }}
    .toast.warning {{ border-left: 3px solid var(--accent-orange); }}
    .toast.info {{ border-left: 3px solid var(--accent-blue); }}

    @keyframes slideIn {{
      from {{ transform: translateX(100%); opacity: 0; }}
      to {{ transform: translateX(0); opacity: 1; }}
    }}

    /* Loading spinner */
    .spinner {{
      width: 20px;
      height: 20px;
      border: 2px solid var(--border-default);
      border-top-color: var(--accent-green);
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }}

    @keyframes spin {{
      to {{ transform: rotate(360deg); }}
    }}

    /* Progress bar */
    .progress-bar {{
      height: 4px;
      background: var(--bg-tertiary);
      border-radius: 2px;
      overflow: hidden;
    }}

    .progress-bar-fill {{
      height: 100%;
      background: linear-gradient(90deg, var(--accent-green), var(--accent-cyan));
      border-radius: 2px;
      transition: width 0.3s ease;
    }}

    /* Path display */
    .path-display {{
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 12px 16px;
      background: var(--bg-primary);
      border: 1px solid var(--border-muted);
      border-radius: 8px;
      margin-bottom: 20px;
    }}

    .path-label {{
      color: var(--text-muted);
      font-size: 12px;
      min-width: 60px;
    }}

    .path-value {{
      font-family: var(--font-mono);
      font-size: 12px;
      color: var(--accent-cyan);
    }}

    /* Responsive */
    @media (max-width: 1024px) {{
      .sidebar {{ display: none; }}
      .cmd-palette {{ min-width: 200px; }}
    }}

    @media (max-width: 768px) {{
      .top-bar {{ padding: 12px 16px; }}
      .content {{ padding: 16px; }}
      .form-row {{ flex-direction: column; }}
      .cmd-palette {{ display: none; }}
    }}

    /* Keyboard shortcuts tooltip */
    .shortcut-hint {{
      position: absolute;
      bottom: 12px;
      right: 12px;
      display: flex;
      gap: 4px;
      opacity: 0;
      transition: opacity 0.2s ease;
    }}

    .card:hover .shortcut-hint {{
      opacity: 1;
    }}

    /* Live indicator */
    .live-indicator {{
      display: flex;
      align-items: center;
      gap: 6px;
      font-size: 11px;
      color: var(--accent-green);
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }}

    .live-indicator::before {{
      content: '';
      width: 6px;
      height: 6px;
      background: var(--accent-green);
      border-radius: 50%;
      animation: pulse 1.5s infinite;
    }}
  </style>
</head>
<body>
  <!-- Top Navigation Bar -->
  <div class=""top-bar"">
    <div class=""logo"">
      <div class=""logo-icon"">MD</div>
      <span class=""logo-text"">MDC Terminal</span>
      <span class=""logo-version"">v2.0</span>
    </div>

    <div class=""cmd-palette"" onclick=""openCommandPalette()"" title=""Quick actions (Ctrl+K)"">
      <span class=""cmd-palette-icon"">&#x1F50D;</span>
      <span class=""cmd-palette-text"">Search commands...</span>
      <div class=""cmd-palette-shortcut"">
        <span class=""kbd"">Ctrl</span>
        <span class=""kbd"">K</span>
      </div>
    </div>

    <div class=""top-status"">
      <div class=""status-indicator"">
        <div id=""topStatusDot"" class=""status-dot disconnected""></div>
        <span id=""topStatusText"">Disconnected</span>
      </div>
      <div class=""live-indicator"" id=""liveIndicator"" style=""display:none;"">
        LIVE
      </div>
    </div>
  </div>

  <div class=""main-container"">
    <!-- Sidebar Navigation -->
    <nav class=""sidebar"">
      <div class=""nav-section"">
        <div class=""nav-section-title"">Overview</div>
        <div class=""nav-item active"" onclick=""scrollToSection('status')"">
          <span class=""nav-item-icon"">&#x1F4CA;</span>
          Status
        </div>
        <div class=""nav-item"" onclick=""scrollToSection('datasource')"">
          <span class=""nav-item-icon"">&#x1F517;</span>
          Provider
        </div>
      </div>

      <div class=""nav-section"">
        <div class=""nav-section-title"">Configuration</div>
        <div class=""nav-item"" onclick=""scrollToSection('storage')"">
          <span class=""nav-item-icon"">&#x1F4BE;</span>
          Storage
        </div>
        <div class=""nav-item"" onclick=""scrollToSection('datasources')"">
          <span class=""nav-item-icon"">&#x2699;</span>
          Data Sources
          <span class=""nav-item-badge"" id=""dsCount"">0</span>
        </div>
        <div class=""nav-item"" onclick=""scrollToSection('symbols')"">
          <span class=""nav-item-icon"">&#x1F4C8;</span>
          Symbols
          <span class=""nav-item-badge"" id=""symCount"">0</span>
        </div>
      </div>

      <div class=""nav-section"">
        <div class=""nav-section-title"">Operations</div>
        <div class=""nav-item"" onclick=""scrollToSection('backfill')"">
          <span class=""nav-item-icon"">&#x23F3;</span>
          Backfill
        </div>
      </div>
    </nav>

    <!-- Main Content -->
    <main class=""content"">
      <!-- Path Display -->
      <div class=""path-display"">
        <span class=""path-label"">Config</span>
        <code class=""path-value"">{Escape(configPath)}</code>
      </div>

      <!-- Metrics Grid -->
      <section id=""status"">
        <div class=""metrics-grid"">
          <div class=""metric-card success"">
            <div class=""metric-trend up"" id=""publishedTrend""></div>
            <div class=""metric-value success"" id=""publishedValue"">0</div>
            <div class=""metric-label"">Published Events</div>
          </div>
          <div class=""metric-card danger"">
            <div class=""metric-trend"" id=""droppedTrend""></div>
            <div class=""metric-value danger"" id=""droppedValue"">0</div>
            <div class=""metric-label"">Dropped Events</div>
          </div>
          <div class=""metric-card warning"">
            <div class=""metric-trend"" id=""integrityTrend""></div>
            <div class=""metric-value warning"" id=""integrityValue"">0</div>
            <div class=""metric-label"">Integrity Events</div>
          </div>
          <div class=""metric-card info"">
            <div class=""metric-trend up"" id=""barsTrend""></div>
            <div class=""metric-value info"" id=""barsValue"">0</div>
            <div class=""metric-label"">Historical Bars</div>
          </div>
        </div>

        <!-- Terminal-style activity log -->
        <div class=""terminal"" style=""margin-bottom: 24px;"">
          <div class=""terminal-header"">
            <div class=""terminal-dot red""></div>
            <div class=""terminal-dot yellow""></div>
            <div class=""terminal-dot green""></div>
            <span class=""terminal-title"">Activity Log</span>
          </div>
          <div class=""terminal-body"" id=""activityLog"">
            <div class=""terminal-line"">
              <span class=""terminal-prompt"">$</span>
              <span class=""terminal-time"">--:--:--</span>
              <span class=""terminal-msg"">Waiting for connection...</span>
            </div>
          </div>
        </div>
      </section>

  <div class=""row"" id=""datasource"">
    <!-- Data Source Panel -->
    <div class=""card"" style=""flex:1; min-width: 400px;"">
      <h3>Data Provider</h3>
      <div class=""form-group"">
        <label>Active Provider</label>
        <select id=""dataSource"" onchange=""updateDataSource()"">
          <option value=""IB"">Interactive Brokers (IB)</option>
          <option value=""Alpaca"">Alpaca</option>
        </select>
      </div>
      <div id=""providerStatus"" class=""muted"" style=""padding: 12px; background: var(--bg-tertiary); border-radius: 6px; margin-bottom: 16px;""></div>

      <!-- Alpaca Settings -->
      <div id=""alpacaSettings"" class=""provider-section hidden"">
        <h4>Alpaca Configuration</h4>
        <div class=""form-group"">
          <label>API Key ID</label>
          <input id=""alpacaKeyId"" type=""text"" placeholder=""PKXXXXXXXXXXXXXXXX"" />
        </div>
        <div class=""form-group"">
          <label>Secret Key</label>
          <input id=""alpacaSecretKey"" type=""password"" placeholder=""Enter your secret key"" />
        </div>
        <div class=""form-row"">
          <div class=""form-group"">
            <label>Data Feed</label>
            <select id=""alpacaFeed"">
              <option value=""iex"">IEX (Free Tier)</option>
              <option value=""sip"">SIP (Paid - Full Market)</option>
              <option value=""delayed_sip"">Delayed SIP</option>
            </select>
          </div>
          <div class=""form-group"">
            <label>Environment</label>
            <select id=""alpacaSandbox"">
              <option value=""false"">Production</option>
              <option value=""true"">Sandbox (Paper)</option>
            </select>
          </div>
        </div>
        <div class=""form-group"">
          <label><input type=""checkbox"" id=""alpacaSubscribeQuotes"" /> Subscribe to Quotes (BBO)</label>
        </div>
        <button class=""btn-primary"" onclick=""saveAlpacaSettings()"">
          <span>&#x1F4BE;</span> Save Alpaca Settings
        </button>
        <div id=""alpacaMsg"" class=""muted"" style=""margin-top: 12px;""></div>
      </div>
    </div>
  </div>

  <div class=""row"" id=""storage"">
    <!-- Storage Settings Panel -->
    <div class=""card"" style=""flex:1; min-width: 500px;"">
      <h3>Storage Configuration</h3>
      <div class=""form-row"">
        <div class=""form-group"" style=""flex: 2"">
          <label>Data Root Path</label>
          <input id=""dataRoot"" type=""text"" placeholder=""./data"" />
        </div>
        <div class=""form-group"" style=""flex: 1"">
          <label>Compression</label>
          <select id=""compress"">
            <option value=""false"">Disabled</option>
            <option value=""true"">GZIP Enabled</option>
          </select>
        </div>
      </div>
      <div class=""form-row"">
        <div class=""form-group"">
          <label>Naming Convention</label>
          <select id=""namingConvention"">
            <option value=""Flat"">Flat (root/symbol_type_date.jsonl)</option>
            <option value=""BySymbol"" selected>By Symbol (root/symbol/type/date.jsonl)</option>
            <option value=""ByDate"">By Date (root/date/symbol/type.jsonl)</option>
            <option value=""ByType"">By Type (root/type/symbol/date.jsonl)</option>
          </select>
        </div>
        <div class=""form-group"">
          <label>Date Partitioning</label>
          <select id=""datePartition"">
            <option value=""None"">None (single file)</option>
            <option value=""Daily"" selected>Daily (yyyy-MM-dd)</option>
            <option value=""Hourly"">Hourly (yyyy-MM-dd_HH)</option>
            <option value=""Monthly"">Monthly (yyyy-MM)</option>
          </select>
        </div>
      </div>
      <div class=""form-row"">
        <div class=""form-group"">
          <label>File Prefix (optional)</label>
          <input id=""filePrefix"" type=""text"" placeholder=""market_"" />
        </div>
        <div class=""form-group"">
          <label>Include Provider in Path</label>
          <select id=""includeProvider"">
            <option value=""false"" selected>No</option>
            <option value=""true"">Yes</option>
          </select>
        </div>
      </div>
      <div id=""storagePreview"" style=""margin: 16px 0; padding: 16px; background: var(--bg-primary); border: 1px solid var(--border-muted); border-radius: 8px;"">
        <div style=""font-size: 11px; color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.5px; margin-bottom: 8px;"">Preview Path</div>
        <code id=""previewPath"" style=""font-size: 14px;"">data/AAPL/Trade/2024-01-15.jsonl</code>
      </div>
      <button class=""btn-primary"" onclick=""saveStorageSettings()"">
        <span>&#x1F4BE;</span> Save Storage Settings
      </button>
      <div id=""storageMsg"" class=""muted"" style=""margin-top: 12px;""></div>
    </div>
  </div>

  <div class=""row"" id=""datasources"">
    <!-- Data Sources Panel -->
    <div class=""card"" style=""flex:1; min-width: 700px;"">
      <h3>Data Sources</h3>
      <p class=""muted"" style=""margin-bottom: 20px;"">Configure multiple data sources for real-time and historical data collection with automatic failover.</p>

      <!-- Failover Settings -->
      <div style=""background: var(--bg-tertiary); padding: 16px; border-radius: 8px; margin-bottom: 20px;"">
        <div class=""form-row"" style=""align-items: flex-end;"">
          <div class=""form-group"" style=""flex: 1; margin-bottom: 0;"">
            <label style=""display: flex; align-items: center; gap: 8px; cursor: pointer;"">
              <input type=""checkbox"" id=""enableFailover"" checked />
              <span>Enable Automatic Failover</span>
            </label>
          </div>
          <div class=""form-group"" style=""flex: 1; margin-bottom: 0;"">
            <label>Failover Timeout</label>
            <input id=""failoverTimeout"" type=""number"" value=""30"" min=""5"" max=""300"" style=""width: 80px;"" />
            <span class=""muted"" style=""margin-left: 8px;"">seconds</span>
          </div>
          <div class=""form-group"" style=""flex: 0; margin-bottom: 0;"">
            <button class=""btn-secondary"" onclick=""saveFailoverSettings()"">
              <span>&#x2699;</span> Save
            </button>
          </div>
        </div>
      </div>

      <table id=""dataSourcesTable"">
        <thead>
          <tr>
            <th style=""width: 60px;"">Status</th>
            <th>Name</th>
            <th style=""width: 100px;"">Provider</th>
            <th style=""width: 100px;"">Type</th>
            <th style=""width: 80px;"">Priority</th>
            <th style=""width: 120px;"">Actions</th>
          </tr>
        </thead>
        <tbody></tbody>
      </table>

      <h4 style=""margin-top: 24px"">Add/Edit Data Source</h4>
      <input type=""hidden"" id=""dsId"" />
      <div class=""form-row"">
        <div class=""form-group"">
          <label>Name *</label>
          <input id=""dsName"" placeholder=""My Data Source"" />
        </div>
        <div class=""form-group"">
          <label>Provider *</label>
          <select id=""dsProvider"" onchange=""updateDsProviderUI()"">
            <option value=""IB"">Interactive Brokers (IB)</option>
            <option value=""Alpaca"">Alpaca</option>
            <option value=""Polygon"">Polygon.io</option>
          </select>
        </div>
        <div class=""form-group"">
          <label>Type *</label>
          <select id=""dsType"">
            <option value=""RealTime"">Real-Time</option>
            <option value=""Historical"">Historical</option>
            <option value=""Both"">Both</option>
          </select>
        </div>
        <div class=""form-group"">
          <label>Priority</label>
          <input id=""dsPriority"" type=""number"" value=""100"" min=""1"" max=""1000"" />
        </div>
      </div>
      <div class=""form-row"">
        <div class=""form-group"" style=""flex: 2"">
          <label>Description</label>
          <input id=""dsDescription"" placeholder=""Optional description"" />
        </div>
        <div class=""form-group"" style=""flex: 1"">
          <label>Symbols (comma separated)</label>
          <input id=""dsSymbols"" placeholder=""AAPL, MSFT"" />
        </div>
      </div>

      <div id=""dsIbSettings"" class=""provider-section"">
        <p class=""muted"">IB Settings</p>
        <div class=""form-row"">
          <div class=""form-group""><label>Host</label><input id=""dsIbHost"" value=""127.0.0.1"" /></div>
          <div class=""form-group""><label>Port</label><input id=""dsIbPort"" type=""number"" value=""7496"" /></div>
          <div class=""form-group""><label>Client ID</label><input id=""dsIbClientId"" type=""number"" value=""0"" /></div>
        </div>
        <div class=""form-group"">
          <label><input type=""checkbox"" id=""dsIbPaper"" /> Paper Trading</label>
          <label style=""margin-left: 16px;""><input type=""checkbox"" id=""dsIbDepth"" checked /> Subscribe Depth</label>
          <label style=""margin-left: 16px;""><input type=""checkbox"" id=""dsIbTick"" checked /> Tick-by-Tick</label>
        </div>
      </div>

      <div id=""dsAlpacaSettings"" class=""provider-section hidden"">
        <p class=""muted"">Alpaca Settings</p>
        <div class=""form-row"">
          <div class=""form-group"">
            <label>Feed</label>
            <select id=""dsAlpacaFeed""><option value=""iex"">IEX (free)</option><option value=""sip"">SIP (paid)</option><option value=""delayed_sip"">Delayed SIP</option></select>
          </div>
          <div class=""form-group"">
            <label>Environment</label>
            <select id=""dsAlpacaSandbox""><option value=""false"">Production</option><option value=""true"">Sandbox</option></select>
          </div>
        </div>
        <div class=""form-group""><label><input type=""checkbox"" id=""dsAlpacaQuotes"" /> Subscribe to Quotes</label></div>
      </div>

      <div id=""dsPolygonSettings"" class=""provider-section hidden"">
        <p class=""muted"">Polygon.io Settings</p>
        <div class=""form-row"">
          <div class=""form-group""><label>API Key</label><input id=""dsPolygonKey"" type=""password"" /></div>
          <div class=""form-group"">
            <label>Feed</label>
            <select id=""dsPolygonFeed""><option value=""stocks"">Stocks</option><option value=""options"">Options</option><option value=""forex"">Forex</option><option value=""crypto"">Crypto</option></select>
          </div>
        </div>
        <div class=""form-group"">
          <label><input type=""checkbox"" id=""dsPolygonDelayed"" /> Use Delayed Data</label>
          <label style=""margin-left: 16px;""><input type=""checkbox"" id=""dsPolygonTrades"" checked /> Trades</label>
          <label style=""margin-left: 16px;""><input type=""checkbox"" id=""dsPolygonQuotes"" /> Quotes</label>
          <label style=""margin-left: 16px;""><input type=""checkbox"" id=""dsPolygonAggs"" /> Aggregates</label>
        </div>
      </div>

      <div style=""margin-top: 16px;"">
        <button class=""btn-primary"" onclick=""saveDataSource()"">Save Data Source</button>
        <button onclick=""clearDsForm()"" style=""margin-left: 8px;"">Clear</button>
      </div>
      <div id=""dsMsg"" class=""muted"" style=""margin-top: 8px;""></div>
    </div>
  </div>

  <div class=""row"" id=""backfill"">
    <div class=""card"" style=""flex:1; min-width: 500px;"">
      <h3>Historical Backfill</h3>
      <p id=""backfillHelp"" class=""muted"" style=""margin-bottom: 20px;"">Download historical EOD bars to fill data gaps from free providers.</p>

      <div class=""form-row"">
        <div class=""form-group"">
          <label>Data Provider</label>
          <select id=""backfillProvider""></select>
        </div>
        <div class=""form-group"" style=""flex: 2"">
          <label>Symbols (comma separated)</label>
          <input id=""backfillSymbols"" placeholder=""AAPL, MSFT, GOOGL"" />
        </div>
      </div>
      <div class=""form-row"">
        <div class=""form-group"">
          <label>Start Date (UTC)</label>
          <input id=""backfillFrom"" type=""date"" />
        </div>
        <div class=""form-group"">
          <label>End Date (UTC)</label>
          <input id=""backfillTo"" type=""date"" />
        </div>
      </div>

      <button class=""btn-primary"" onclick=""runBackfill()"" id=""backfillBtn"">
        <span>&#x23F3;</span> Start Backfill
      </button>

      <!-- Backfill Status Terminal -->
      <div class=""terminal"" style=""margin-top: 16px;"">
        <div class=""terminal-header"">
          <div class=""terminal-dot red""></div>
          <div class=""terminal-dot yellow""></div>
          <div class=""terminal-dot green""></div>
          <span class=""terminal-title"">Backfill Status</span>
        </div>
        <div class=""terminal-body"" id=""backfillStatus"" style=""min-height: 60px;"">
          <div class=""terminal-line"">
            <span class=""terminal-prompt"">$</span>
            <span class=""terminal-msg"">Ready to start backfill operation...</span>
          </div>
        </div>
      </div>
    </div>
  </div>

  <div class=""row"" id=""symbols"">
    <!-- Symbols Panel -->
    <div class=""card"" style=""flex:1; min-width: 700px;"">
      <h3>Subscribed Symbols</h3>
      <table id=""symbolsTable"">
        <thead>
          <tr>
            <th>Symbol</th>
            <th>Trades</th>
            <th>Depth</th>
            <th>Levels</th>
            <th>LocalSymbol <span class=""provider-badge ib-only"">IB</span></th>
            <th>Exchange <span class=""provider-badge ib-only"">IB</span></th>
            <th style=""width: 80px;"">Actions</th>
          </tr>
        </thead>
        <tbody></tbody>
      </table>

      <h4 style=""margin-top:24px"">Add New Symbol</h4>
      <div class=""form-row"">
        <div class=""form-group"">
          <label>Symbol *</label>
          <input id=""sym"" placeholder=""AAPL"" style=""text-transform: uppercase;"" />
        </div>
        <div class=""form-group"">
          <label>Trades Stream</label>
          <select id=""trades"">
            <option value=""true"" selected>Enabled</option>
            <option value=""false"">Disabled</option>
          </select>
        </div>
        <div class=""form-group"">
          <label>Depth Stream</label>
          <select id=""depth"">
            <option value=""true"">Enabled</option>
            <option value=""false"" selected>Disabled</option>
          </select>
        </div>
        <div class=""form-group"">
          <label>Depth Levels</label>
          <input id=""levels"" value=""10"" type=""number"" min=""1"" max=""50"" />
        </div>
      </div>

      <!-- IB-specific fields -->
      <div id=""ibFields"" style=""background: var(--bg-tertiary); padding: 16px; border-radius: 8px; margin-top: 16px;"">
        <div style=""display: flex; align-items: center; gap: 8px; margin-bottom: 16px;"">
          <span style=""color: var(--accent-blue);"">&#x1F517;</span>
          <span class=""muted"">Interactive Brokers Options</span>
          <span class=""provider-badge ib-only"">IB only</span>
        </div>
        <div class=""form-row"">
          <div class=""form-group"">
            <label>LocalSymbol</label>
            <input id=""localsym"" placeholder=""PCG PRA"" />
          </div>
          <div class=""form-group"">
            <label>Exchange</label>
            <input id=""exch"" value=""SMART"" />
          </div>
          <div class=""form-group"">
            <label>Primary Exchange</label>
            <input id=""pexch"" placeholder=""NYSE"" />
          </div>
        </div>
      </div>

      <div style=""margin-top: 20px; display: flex; gap: 12px;"">
        <button class=""btn-primary"" onclick=""addSymbol()"">
          <span>&#x2795;</span> Add Symbol
        </button>
        <button class=""btn-secondary"" onclick=""document.getElementById('sym').value=''; document.getElementById('localsym').value=''; document.getElementById('pexch').value='';"">
          Clear Form
        </button>
      </div>

      <div id=""msg"" class=""muted"" style=""margin-top:12px""></div>
    </div>
  </div>

    </main>
  </div>

  <!-- Toast Container -->
  <div class=""toast-container"" id=""toastContainer""></div>

<script>
let currentDataSource = 'IB';
let backfillProviders = [];
let dataSources = [];
let activityLogs = [];
let prevMetrics = {{ published: 0, dropped: 0, integrity: 0, bars: 0 }};

// Utility Functions
function formatNumber(num) {{
  if (num >= 1000000) return (num / 1000000).toFixed(1) + 'M';
  if (num >= 1000) return (num / 1000).toFixed(1) + 'K';
  return num.toString();
}}

function getCurrentTime() {{
  return new Date().toLocaleTimeString('en-US', {{ hour12: false }});
}}

// Toast Notification System
function showToast(message, type = 'info') {{
  const container = document.getElementById('toastContainer');
  const toast = document.createElement('div');
  toast.className = `toast ${{type}}`;

  const icons = {{ success: '&#x2705;', error: '&#x274C;', warning: '&#x26A0;', info: '&#x2139;' }};
  toast.innerHTML = `<span>${{icons[type] || icons.info}}</span><span>${{message}}</span>`;

  container.appendChild(toast);
  setTimeout(() => toast.remove(), 4000);
}}

// Activity Log
function addLog(message, type = '') {{
  const logBody = document.getElementById('activityLog');
  const time = getCurrentTime();

  activityLogs.push({{ time, message, type }});
  if (activityLogs.length > 50) activityLogs.shift();

  const line = document.createElement('div');
  line.className = 'terminal-line';
  line.innerHTML = `
    <span class="terminal-prompt">$</span>
    <span class="terminal-time">${{time}}</span>
    <span class="terminal-msg ${{type}}">${{message}}</span>
  `;

  logBody.appendChild(line);
  logBody.scrollTop = logBody.scrollHeight;

  // Keep only last 20 lines visible
  while (logBody.children.length > 20) {{
    logBody.removeChild(logBody.firstChild);
  }}
}}

// Navigation
function scrollToSection(sectionId) {{
  const section = document.getElementById(sectionId);
  if (section) {{
    section.scrollIntoView({{ behavior: 'smooth', block: 'start' }});

    // Update active nav item
    document.querySelectorAll('.nav-item').forEach(item => item.classList.remove('active'));
    const navItem = document.querySelector(`.nav-item[onclick*="${{sectionId}}"]`);
    if (navItem) navItem.classList.add('active');
  }}
}}

// Command Palette
function openCommandPalette() {{
  const commands = [
    {{ name: 'Go to Status', action: () => scrollToSection('status') }},
    {{ name: 'Go to Provider', action: () => scrollToSection('datasource') }},
    {{ name: 'Go to Storage', action: () => scrollToSection('storage') }},
    {{ name: 'Go to Data Sources', action: () => scrollToSection('datasources') }},
    {{ name: 'Go to Symbols', action: () => scrollToSection('symbols') }},
    {{ name: 'Go to Backfill', action: () => scrollToSection('backfill') }},
    {{ name: 'Refresh Status', action: () => loadStatus() }},
    {{ name: 'Save All Settings', action: () => {{ saveStorageSettings(); saveAlpacaSettings(); }} }},
  ];

  const cmd = prompt('Command (type to search):\n' + commands.map((c, i) => `${{i+1}}. ${{c.name}}`).join('\n'));
  if (cmd) {{
    const idx = parseInt(cmd) - 1;
    if (idx >= 0 && idx < commands.length) {{
      commands[idx].action();
    }}
  }}
}}

// Keyboard shortcuts
document.addEventListener('keydown', (e) => {{
  if (e.ctrlKey && e.key === 'k') {{
    e.preventDefault();
    openCommandPalette();
  }}
  if (e.key === '1' && e.altKey) scrollToSection('status');
  if (e.key === '2' && e.altKey) scrollToSection('storage');
  if (e.key === '3' && e.altKey) scrollToSection('symbols');
  if (e.key === 'r' && e.ctrlKey && !e.shiftKey) {{
    e.preventDefault();
    loadStatus();
    showToast('Status refreshed', 'info');
  }}
}});

// Data Sources Management
async function loadDataSources() {{
  try {{
    const r = await fetch('/api/config/datasources');
    if (!r.ok) return;
    const data = await r.json();

    document.getElementById('enableFailover').checked = data.enableFailover !== false;
    document.getElementById('failoverTimeout').value = data.failoverTimeoutSeconds || 30;

    dataSources = data.sources || [];
    document.getElementById('dsCount').textContent = dataSources.length;
    renderDataSourcesTable();
  }} catch (e) {{
    console.warn('Unable to load data sources', e);
  }}
}}

function renderDataSourcesTable() {{
  const tbody = document.querySelector('#dataSourcesTable tbody');
  tbody.innerHTML = '';

  if (dataSources.length === 0) {{
    tbody.innerHTML = '<tr><td colspan=""6"" class=""muted"" style=""text-align: center; padding: 24px;"">No data sources configured. Add one below.</td></tr>';
    return;
  }}

  for (const ds of dataSources) {{
    const tr = document.createElement('tr');
    const tagClass = ds.provider === 'IB' ? 'tag-ib' : (ds.provider === 'Alpaca' ? 'tag-alpaca' : 'tag-polygon');
    const statusColor = ds.enabled ? 'var(--accent-green)' : 'var(--text-muted)';
    tr.innerHTML = `
      <td>
        <label style="display: flex; align-items: center; gap: 8px; cursor: pointer;">
          <input type="checkbox" ${{ds.enabled ? 'checked' : ''}} onchange="toggleDataSource('${{ds.id}}', this.checked)" />
          <span style="width: 8px; height: 8px; border-radius: 50%; background: ${{statusColor}};"></span>
        </label>
      </td>
      <td><span style="font-weight: 600; font-family: var(--font-mono);">${{ds.name}}</span></td>
      <td><span class="tag ${{tagClass}}">${{ds.provider}}</span></td>
      <td style="color: var(--text-secondary);">${{ds.type}}</td>
      <td style="font-family: var(--font-mono);">${{ds.priority}}</td>
      <td>
        <button class="btn-secondary" onclick="editDataSource('${{ds.id}}')" style="padding: 6px 12px; font-size: 12px; margin-right: 4px;">Edit</button>
        <button class="btn-danger" onclick="deleteDataSource('${{ds.id}}')">Delete</button>
      </td>
    `;
    tbody.appendChild(tr);
  }}
}}

function updateDsProviderUI() {{
  const provider = document.getElementById('dsProvider').value;
  document.getElementById('dsIbSettings').classList.toggle('hidden', provider !== 'IB');
  document.getElementById('dsAlpacaSettings').classList.toggle('hidden', provider !== 'Alpaca');
  document.getElementById('dsPolygonSettings').classList.toggle('hidden', provider !== 'Polygon');
}}

function clearDsForm() {{
  document.getElementById('dsId').value = '';
  document.getElementById('dsName').value = '';
  document.getElementById('dsProvider').value = 'IB';
  document.getElementById('dsType').value = 'RealTime';
  document.getElementById('dsPriority').value = '100';
  document.getElementById('dsDescription').value = '';
  document.getElementById('dsSymbols').value = '';

  // IB defaults
  document.getElementById('dsIbHost').value = '127.0.0.1';
  document.getElementById('dsIbPort').value = '7496';
  document.getElementById('dsIbClientId').value = '0';
  document.getElementById('dsIbPaper').checked = false;
  document.getElementById('dsIbDepth').checked = true;
  document.getElementById('dsIbTick').checked = true;

  // Alpaca defaults
  document.getElementById('dsAlpacaFeed').value = 'iex';
  document.getElementById('dsAlpacaSandbox').value = 'false';
  document.getElementById('dsAlpacaQuotes').checked = false;

  // Polygon defaults
  document.getElementById('dsPolygonKey').value = '';
  document.getElementById('dsPolygonFeed').value = 'stocks';
  document.getElementById('dsPolygonDelayed').checked = false;
  document.getElementById('dsPolygonTrades').checked = true;
  document.getElementById('dsPolygonQuotes').checked = false;
  document.getElementById('dsPolygonAggs').checked = false;

  updateDsProviderUI();
}}

function editDataSource(id) {{
  const ds = dataSources.find(s => s.id === id);
  if (!ds) return;

  document.getElementById('dsId').value = ds.id;
  document.getElementById('dsName').value = ds.name;
  document.getElementById('dsProvider').value = ds.provider;
  document.getElementById('dsType').value = ds.type;
  document.getElementById('dsPriority').value = ds.priority;
  document.getElementById('dsDescription').value = ds.description || '';
  document.getElementById('dsSymbols').value = (ds.symbols || []).join(', ');

  if (ds.ib) {{
    document.getElementById('dsIbHost').value = ds.ib.host || '127.0.0.1';
    document.getElementById('dsIbPort').value = ds.ib.port || 7496;
    document.getElementById('dsIbClientId').value = ds.ib.clientId || 0;
    document.getElementById('dsIbPaper').checked = ds.ib.usePaperTrading || false;
    document.getElementById('dsIbDepth').checked = ds.ib.subscribeDepth !== false;
    document.getElementById('dsIbTick').checked = ds.ib.tickByTick !== false;
  }}

  if (ds.alpaca) {{
    document.getElementById('dsAlpacaFeed').value = ds.alpaca.feed || 'iex';
    document.getElementById('dsAlpacaSandbox').value = ds.alpaca.useSandbox ? 'true' : 'false';
    document.getElementById('dsAlpacaQuotes').checked = ds.alpaca.subscribeQuotes || false;
  }}

  if (ds.polygon) {{
    document.getElementById('dsPolygonKey').value = ds.polygon.apiKey || '';
    document.getElementById('dsPolygonFeed').value = ds.polygon.feed || 'stocks';
    document.getElementById('dsPolygonDelayed').checked = ds.polygon.useDelayed || false;
    document.getElementById('dsPolygonTrades').checked = ds.polygon.subscribeTrades !== false;
    document.getElementById('dsPolygonQuotes').checked = ds.polygon.subscribeQuotes || false;
    document.getElementById('dsPolygonAggs').checked = ds.polygon.subscribeAggregates || false;
  }}

  updateDsProviderUI();
}}

async function saveDataSource() {{
  const name = document.getElementById('dsName').value.trim();
  if (!name) {{
    document.getElementById('dsMsg').textContent = 'Name is required.';
    return;
  }}

  const provider = document.getElementById('dsProvider').value;
  const symbols = document.getElementById('dsSymbols').value
    .split(',')
    .map(s => s.trim().toUpperCase())
    .filter(s => s);

  const payload = {{
    id: document.getElementById('dsId').value || null,
    name: name,
    provider: provider,
    type: document.getElementById('dsType').value,
    priority: parseInt(document.getElementById('dsPriority').value) || 100,
    description: document.getElementById('dsDescription').value || null,
    symbols: symbols.length ? symbols : null,
    enabled: true
  }};

  if (provider === 'IB') {{
    payload.ib = {{
      host: document.getElementById('dsIbHost').value || '127.0.0.1',
      port: parseInt(document.getElementById('dsIbPort').value) || 7496,
      clientId: parseInt(document.getElementById('dsIbClientId').value) || 0,
      usePaperTrading: document.getElementById('dsIbPaper').checked,
      subscribeDepth: document.getElementById('dsIbDepth').checked,
      tickByTick: document.getElementById('dsIbTick').checked
    }};
  }} else if (provider === 'Alpaca') {{
    payload.alpaca = {{
      feed: document.getElementById('dsAlpacaFeed').value,
      useSandbox: document.getElementById('dsAlpacaSandbox').value === 'true',
      subscribeQuotes: document.getElementById('dsAlpacaQuotes').checked
    }};
  }} else if (provider === 'Polygon') {{
    payload.polygon = {{
      apiKey: document.getElementById('dsPolygonKey').value || null,
      feed: document.getElementById('dsPolygonFeed').value,
      useDelayed: document.getElementById('dsPolygonDelayed').checked,
      subscribeTrades: document.getElementById('dsPolygonTrades').checked,
      subscribeQuotes: document.getElementById('dsPolygonQuotes').checked,
      subscribeAggregates: document.getElementById('dsPolygonAggs').checked
    }};
  }}

  const r = await fetch('/api/config/datasources', {{
    method: 'POST',
    headers: {{ 'Content-Type': 'application/json' }},
    body: JSON.stringify(payload)
  }});

  const msg = document.getElementById('dsMsg');
  if (r.ok) {{
    msg.textContent = 'Data source saved successfully.';
    clearDsForm();
    await loadDataSources();
  }} else {{
    msg.textContent = 'Error saving data source.';
  }}
}}

async function deleteDataSource(id) {{
  if (!confirm('Delete this data source?')) return;

  const r = await fetch(`/api/config/datasources/${{encodeURIComponent(id)}}`, {{
    method: 'DELETE'
  }});

  if (r.ok) {{
    await loadDataSources();
  }}
}}

async function toggleDataSource(id, enabled) {{
  await fetch(`/api/config/datasources/${{encodeURIComponent(id)}}/toggle`, {{
    method: 'POST',
    headers: {{ 'Content-Type': 'application/json' }},
    body: JSON.stringify({{ enabled }})
  }});
}}

async function saveFailoverSettings() {{
  const r = await fetch('/api/config/datasources/failover', {{
    method: 'POST',
    headers: {{ 'Content-Type': 'application/json' }},
    body: JSON.stringify({{
      enableFailover: document.getElementById('enableFailover').checked,
      failoverTimeoutSeconds: parseInt(document.getElementById('failoverTimeout').value) || 30
    }})
  }});

  const msg = document.getElementById('dsMsg');
  msg.textContent = r.ok ? 'Failover settings saved.' : 'Error saving failover settings.';
}}

async function loadBackfillProviders(selectedProvider) {
  try {
    const r = await fetch('/api/backfill/providers');
    if (!r.ok) return;
    backfillProviders = await r.json();
    const select = document.getElementById('backfillProvider');
    if (!select) return;
    select.innerHTML = '';
    for (const p of backfillProviders) {
      const opt = document.createElement('option');
      opt.value = p.name;
      opt.textContent = p.displayName || p.name;
      select.appendChild(opt);
    }
    if (selectedProvider) {
      select.value = selectedProvider;
    }
    const help = document.getElementById('backfillHelp');
    if (help && backfillProviders.length) {
      help.textContent = backfillProviders.map(p => `${{p.displayName || p.name}}: ${{p.description || ''}}`).join(' • ');
    }
  } catch (e) {
    console.warn('Unable to load backfill providers', e);
  }
}

async function loadConfig() {{
  const r = await fetch('/api/config');
  const cfg = await r.json();

  // Update data source selector
  currentDataSource = cfg.dataSource || 'IB';
  document.getElementById('dataSource').value = currentDataSource;
  updateProviderUI();

  // Load Alpaca settings if available
  if (cfg.alpaca) {{
    document.getElementById('alpacaKeyId').value = cfg.alpaca.keyId || '';
    document.getElementById('alpacaSecretKey').value = cfg.alpaca.secretKey || '';
    document.getElementById('alpacaFeed').value = cfg.alpaca.feed || 'iex';
    document.getElementById('alpacaSandbox').value = cfg.alpaca.useSandbox ? 'true' : 'false';
    document.getElementById('alpacaSubscribeQuotes').checked = cfg.alpaca.subscribeQuotes || false;
  }}

  // Load storage settings
  document.getElementById('dataRoot').value = cfg.dataRoot || 'data';
  document.getElementById('compress').value = cfg.compress ? 'true' : 'false';
  if (cfg.storage) {{
    document.getElementById('namingConvention').value = cfg.storage.namingConvention || 'BySymbol';
    document.getElementById('datePartition').value = cfg.storage.datePartition || 'Daily';
    document.getElementById('includeProvider').value = cfg.storage.includeProvider ? 'true' : 'false';
    document.getElementById('filePrefix').value = cfg.storage.filePrefix || '';
  }}
  updateStoragePreview();

  await loadBackfillProviders(cfg.backfill ? cfg.backfill.provider : null);
  if (cfg.backfill) {{
    if (cfg.backfill.symbols) document.getElementById('backfillSymbols').value = cfg.backfill.symbols.join(',');
    if (cfg.backfill.from) document.getElementById('backfillFrom').value = cfg.backfill.from;
    if (cfg.backfill.to) document.getElementById('backfillTo').value = cfg.backfill.to;
    if (cfg.backfill.provider) document.getElementById('backfillProvider').value = cfg.backfill.provider;
  }}

  // Update symbols table
  const symbols = cfg.symbols || [];
  document.getElementById('symCount').textContent = symbols.length;

  const tbody = document.querySelector('#symbolsTable tbody');
  tbody.innerHTML = '';

  if (symbols.length === 0) {{
    tbody.innerHTML = '<tr><td colspan="7" class="muted" style="text-align: center; padding: 24px;">No symbols configured. Add one below.</td></tr>';
  }} else {{
    for (const s of symbols) {{
      const tr = document.createElement('tr');
      tr.innerHTML = `
        <td><span style="font-weight: 600; font-family: var(--font-mono); color: var(--accent-cyan);">${{s.symbol}}</span></td>
        <td>${{s.subscribeTrades ? '<span class="good">ON</span>' : '<span class="muted">OFF</span>'}}</td>
        <td>${{s.subscribeDepth ? '<span class="good">ON</span>' : '<span class="muted">OFF</span>'}}</td>
        <td style="font-family: var(--font-mono);">${{s.depthLevels || 10}}</td>
        <td style="color: var(--text-secondary);">${{s.localSymbol || '-'}}</td>
        <td style="color: var(--text-secondary);">${{s.exchange || '-'}}</td>
        <td><button class="btn-danger" onclick="deleteSymbol('${{s.symbol}}')">Delete</button></td>
      `;
      tbody.appendChild(tr);
    }}
  }}

  addLog('Configuration loaded successfully', 'success');
}}

function updateProviderUI() {{
  const isAlpaca = currentDataSource === 'Alpaca';

  // Show/hide Alpaca settings
  document.getElementById('alpacaSettings').classList.toggle('hidden', !isAlpaca);

  // Show/hide IB-specific fields
  document.getElementById('ibFields').classList.toggle('hidden', isAlpaca);

  // Update provider status message
  const statusDiv = document.getElementById('providerStatus');
  if (isAlpaca) {{
    statusDiv.innerHTML = '<span class=""tag tag-alpaca"">Alpaca</span> WebSocket streaming for trades and quotes';
  }} else {{
    statusDiv.innerHTML = '<span class=""tag tag-ib"">Interactive Brokers</span> TWS/Gateway connection for L2 depth and trades';
  }}
}}

async function updateDataSource() {{
  const ds = document.getElementById('dataSource').value;
  currentDataSource = ds;
  updateProviderUI();

  const r = await fetch('/api/config/datasource', {{
    method: 'POST',
    headers: {{'Content-Type': 'application/json'}},
    body: JSON.stringify({{ dataSource: ds }})
  }});

  if (r.ok) {{
    document.getElementById('msg').textContent = 'Data source updated. Restart collector to apply changes.';
  }}
}}

async function saveAlpacaSettings() {{
  const payload = {{
    keyId: document.getElementById('alpacaKeyId').value,
    secretKey: document.getElementById('alpacaSecretKey').value,
    feed: document.getElementById('alpacaFeed').value,
    useSandbox: document.getElementById('alpacaSandbox').value === 'true',
    subscribeQuotes: document.getElementById('alpacaSubscribeQuotes').checked
  }};

  const r = await fetch('/api/config/alpaca', {{
    method: 'POST',
    headers: {{'Content-Type': 'application/json'}},
    body: JSON.stringify(payload)
  }});

  const msgDiv = document.getElementById('alpacaMsg');
  msgDiv.textContent = r.ok ? 'Alpaca settings saved. Restart collector to apply.' : 'Error saving settings.';
}}

async function saveStorageSettings() {{
  const payload = {{
    dataRoot: document.getElementById('dataRoot').value,
    compress: document.getElementById('compress').value === 'true',
    namingConvention: document.getElementById('namingConvention').value,
    datePartition: document.getElementById('datePartition').value,
    includeProvider: document.getElementById('includeProvider').value === 'true',
    filePrefix: document.getElementById('filePrefix').value
  }};

  const r = await fetch('/api/config/storage', {{
    method: 'POST',
    headers: {{'Content-Type': 'application/json'}},
    body: JSON.stringify(payload)
  }});

  const msgDiv = document.getElementById('storageMsg');
  msgDiv.textContent = r.ok ? 'Storage settings saved. Restart collector to apply.' : 'Error saving settings.';
}}

function updateStoragePreview() {{
  const root = document.getElementById('dataRoot').value || 'data';
  const compress = document.getElementById('compress').value === 'true';
  const naming = document.getElementById('namingConvention').value;
  const partition = document.getElementById('datePartition').value;
  const prefix = document.getElementById('filePrefix').value;
  const ext = compress ? '.jsonl.gz' : '.jsonl';
  const pfx = prefix ? prefix + '_' : '';

  let dateStr = '';
  if (partition === 'Daily') dateStr = '2024-01-15';
  else if (partition === 'Hourly') dateStr = '2024-01-15_14';
  else if (partition === 'Monthly') dateStr = '2024-01';

  let path = '';
  if (naming === 'Flat') {{
    path = dateStr ? `${{root}}/${{pfx}}AAPL_Trade_${{dateStr}}${{ext}}` : `${{root}}/${{pfx}}AAPL_Trade${{ext}}`;
  }} else if (naming === 'BySymbol') {{
    path = dateStr ? `${{root}}/AAPL/Trade/${{pfx}}${{dateStr}}${{ext}}` : `${{root}}/AAPL/Trade/${{pfx}}data${{ext}}`;
  }} else if (naming === 'ByDate') {{
    path = dateStr ? `${{root}}/${{dateStr}}/AAPL/${{pfx}}Trade${{ext}}` : `${{root}}/AAPL/${{pfx}}Trade${{ext}}`;
  }} else if (naming === 'ByType') {{
    path = dateStr ? `${{root}}/Trade/AAPL/${{pfx}}${{dateStr}}${{ext}}` : `${{root}}/Trade/AAPL/${{pfx}}data${{ext}}`;
  }}

  document.getElementById('previewPath').textContent = path;
}}

// Add event listeners for live preview updates
['dataRoot', 'compress', 'namingConvention', 'datePartition', 'filePrefix'].forEach(id => {{
  document.getElementById(id).addEventListener('change', updateStoragePreview);
  document.getElementById(id).addEventListener('input', updateStoragePreview);
}});

async function loadStatus() {{
  try {{
    const r = await fetch('/api/status');
    const topDot = document.getElementById('topStatusDot');
    const topText = document.getElementById('topStatusText');
    const liveIndicator = document.getElementById('liveIndicator');

    if (!r.ok) {{
      topDot.className = 'status-dot disconnected';
      topText.textContent = 'Disconnected';
      liveIndicator.style.display = 'none';
      return;
    }}

    const s = await r.json();
    const isConnected = s.isConnected !== false;

    // Update top status
    topDot.className = isConnected ? 'status-dot connected' : 'status-dot disconnected';
    topText.textContent = isConnected ? 'Connected' : 'Disconnected';
    liveIndicator.style.display = isConnected ? 'flex' : 'none';

    // Update metric cards
    const metrics = s.metrics || {{}};
    const published = metrics.published || 0;
    const dropped = metrics.dropped || 0;
    const integrity = metrics.integrity || 0;
    const bars = metrics.historicalBars || 0;

    document.getElementById('publishedValue').textContent = formatNumber(published);
    document.getElementById('droppedValue').textContent = formatNumber(dropped);
    document.getElementById('integrityValue').textContent = formatNumber(integrity);
    document.getElementById('barsValue').textContent = formatNumber(bars);

    // Update trends
    if (published > prevMetrics.published) {{
      document.getElementById('publishedTrend').innerHTML = `<span>&#x2191;</span> +${{formatNumber(published - prevMetrics.published)}}`;
      document.getElementById('publishedTrend').className = 'metric-trend up';
    }}

    if (dropped > prevMetrics.dropped) {{
      document.getElementById('droppedTrend').innerHTML = `<span>&#x2191;</span> +${{dropped - prevMetrics.dropped}}`;
      document.getElementById('droppedTrend').className = 'metric-trend down';
      addLog(`Dropped events increased: +${{dropped - prevMetrics.dropped}}`, 'warning');
    }}

    prevMetrics = {{ published, dropped, integrity, bars }};

    // Log connection changes
    if (isConnected && topText.textContent !== 'Connected') {{
      addLog('Connection established', 'success');
    }}

  }} catch (e) {{
    document.getElementById('topStatusDot').className = 'status-dot disconnected';
    document.getElementById('topStatusText').textContent = 'Error';
    document.getElementById('liveIndicator').style.display = 'none';
  }}
}}

async function loadBackfillStatus() {{
  const box = document.getElementById('backfillStatus');
  try {{
    const r = await fetch('/api/backfill/status');
    if (!r.ok) {{
      box.innerHTML = `<div class="terminal-line"><span class="terminal-prompt">$</span><span class="terminal-msg">Ready to start backfill operation...</span></div>`;
      return;
    }}
    const status = await r.json();
    box.innerHTML = formatBackfillStatus(status);
  }} catch (e) {{
    box.innerHTML = `<div class="terminal-line"><span class="terminal-prompt">$</span><span class="terminal-msg error">Unable to load backfill status</span></div>`;
  }}
}}

function formatBackfillStatus(status) {{
  if (!status) return `<div class="terminal-line"><span class="terminal-prompt">$</span><span class="terminal-msg">No backfill runs yet.</span></div>`;

  const started = status.startedUtc ? new Date(status.startedUtc).toLocaleString() : 'n/a';
  const completed = status.completedUtc ? new Date(status.completedUtc).toLocaleString() : 'n/a';
  const statusClass = status.success ? 'success' : 'error';
  const statusText = status.success ? 'SUCCESS' : 'FAILED';
  const symbols = (status.symbols || []).join(', ');

  let html = `
    <div class="terminal-line">
      <span class="terminal-prompt">$</span>
      <span class="terminal-msg ${{statusClass}}">[${{statusText}}] Backfill completed</span>
    </div>
    <div class="terminal-line">
      <span class="terminal-prompt">&nbsp;</span>
      <span class="terminal-msg">Provider: ${{status.provider}} | Bars written: ${{status.barsWritten || 0}}</span>
    </div>
    <div class="terminal-line">
      <span class="terminal-prompt">&nbsp;</span>
      <span class="terminal-msg">Symbols: ${{symbols || 'n/a'}}</span>
    </div>
    <div class="terminal-line">
      <span class="terminal-prompt">&nbsp;</span>
      <span class="terminal-msg" style="color: var(--text-muted);">Started: ${{started}} | Completed: ${{completed}}</span>
    </div>
  `;

  if (status.error) {{
    html += `<div class="terminal-line"><span class="terminal-prompt">!</span><span class="terminal-msg error">${{status.error}}</span></div>`;
  }}

  return html;
}}

async function runBackfill() {{
  const statusBox = document.getElementById('backfillStatus');
  const btn = document.getElementById('backfillBtn');
  const provider = document.getElementById('backfillProvider').value || 'stooq';
  const symbols = (document.getElementById('backfillSymbols').value || '')
    .split(',')
    .map(s => s.trim().toUpperCase())
    .filter(s => s);
  const from = document.getElementById('backfillFrom').value || null;
  const to = document.getElementById('backfillTo').value || null;

  if (!symbols.length) {{
    statusBox.innerHTML = `<div class="terminal-line"><span class="terminal-prompt">!</span><span class="terminal-msg warning">Please enter at least one symbol to backfill.</span></div>`;
    showToast('Please enter at least one symbol', 'warning');
    return;
  }}

  // Show loading state
  btn.disabled = true;
  btn.innerHTML = '<span class="spinner"></span> Running...';

  statusBox.innerHTML = `
    <div class="terminal-line"><span class="terminal-prompt">$</span><span class="terminal-msg">Initializing backfill for ${{symbols.join(', ')}}...</span></div>
    <div class="terminal-line"><span class="terminal-prompt">$</span><span class="terminal-msg">Provider: ${{provider}}</span></div>
  `;

  addLog(`Starting backfill: ${{symbols.join(', ')}}`, 'success');

  const payload = {{ provider, symbols, from, to }};
  try {{
    const r = await fetch('/api/backfill/run', {{
      method: 'POST',
      headers: {{ 'Content-Type': 'application/json' }},
      body: JSON.stringify(payload)
    }});

    if (!r.ok) {{
      const msg = await r.text();
      statusBox.innerHTML = `<div class="terminal-line"><span class="terminal-prompt">!</span><span class="terminal-msg error">${{msg || 'Backfill failed'}}</span></div>`;
      showToast('Backfill failed', 'error');
      addLog('Backfill failed: ' + (msg || 'Unknown error'), 'error');
      return;
    }}

    const result = await r.json();
    statusBox.innerHTML = formatBackfillStatus(result);
    showToast(`Backfill completed: ${{result.barsWritten || 0}} bars written`, result.success ? 'success' : 'error');
    addLog(`Backfill completed: ${{result.barsWritten || 0}} bars written`, result.success ? 'success' : 'error');
  }} finally {{
    btn.disabled = false;
    btn.innerHTML = '<span>&#x23F3;</span> Start Backfill';
  }}
}}

async function addSymbol() {{
  const symbol = document.getElementById('sym').value.trim().toUpperCase();
  if (!symbol) {{
    document.getElementById('msg').textContent = 'Symbol is required.';
    showToast('Symbol is required', 'warning');
    return;
  }}

  const payload = {{
    symbol: symbol,
    subscribeTrades: document.getElementById('trades').value === 'true',
    subscribeDepth: document.getElementById('depth').value === 'true',
    depthLevels: parseInt(document.getElementById('levels').value || '10', 10),
    securityType: 'STK',
    exchange: document.getElementById('exch').value || 'SMART',
    currency: 'USD',
    primaryExchange: document.getElementById('pexch').value || null,
    localSymbol: document.getElementById('localsym').value || null
  }};

  const r = await fetch('/api/config/symbols', {{
    method: 'POST',
    headers: {{'Content-Type': 'application/json'}},
    body: JSON.stringify(payload)
  }});

  const msg = document.getElementById('msg');
  if (r.ok) {{
    msg.innerHTML = `<span class="good">Symbol ${{symbol}} added successfully.</span>`;
    showToast(`Symbol ${{symbol}} added`, 'success');
    addLog(`Symbol added: ${{symbol}}`, 'success');
    document.getElementById('sym').value = '';
    document.getElementById('localsym').value = '';
    document.getElementById('pexch').value = '';
    await loadConfig();
  }} else {{
    msg.innerHTML = '<span class="bad">Error adding symbol.</span>';
    showToast('Failed to add symbol', 'error');
  }}
}}

async function deleteSymbol(symbol) {{
  if (!confirm(`Delete symbol ${{symbol}}?`)) return;

  const r = await fetch(`/api/config/symbols/${{encodeURIComponent(symbol)}}`, {{
    method: 'DELETE'
  }});

  if (r.ok) {{
    await loadConfig();
  }}
}}

// Initial load
loadConfig();
loadStatus();
loadBackfillStatus();
loadDataSources();
setInterval(loadStatus, 2000);
setInterval(loadBackfillStatus, 5000);
</script>
</body>
</html>";

    public static string ProvidersView(string configPath) => $@"
<!doctype html>
<html>
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width,initial-scale=1"" />
  <title>MDC - Provider Comparison</title>
  <style>
    :root {{
      --bg-primary: #0d1117;
      --bg-secondary: #161b22;
      --bg-tertiary: #21262d;
      --bg-hover: #30363d;
      --border-default: #30363d;
      --border-muted: #21262d;
      --text-primary: #e6edf3;
      --text-secondary: #8b949e;
      --text-muted: #6e7681;
      --accent-green: #3fb950;
      --accent-green-dim: #238636;
      --accent-blue: #58a6ff;
      --accent-purple: #a371f7;
      --accent-red: #f85149;
      --accent-orange: #d29922;
      --accent-cyan: #39c5cf;
      --glow-green: 0 0 20px rgba(63, 185, 80, 0.3);
      --glow-blue: 0 0 20px rgba(88, 166, 255, 0.3);
      --glow-red: 0 0 20px rgba(248, 81, 73, 0.3);
      --font-mono: 'JetBrains Mono', 'Fira Code', 'SF Mono', Consolas, monospace;
      --font-sans: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
    }}

    * {{ box-sizing: border-box; }}

    body {{
      font-family: var(--font-sans);
      margin: 0;
      padding: 20px;
      background: var(--bg-primary);
      color: var(--text-primary);
      min-height: 100vh;
    }}

    .header {{
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 24px;
      padding-bottom: 16px;
      border-bottom: 1px solid var(--border-default);
    }}

    .header h1 {{
      font-size: 24px;
      margin: 0;
      display: flex;
      align-items: center;
      gap: 12px;
    }}

    .header h1::before {{
      content: '>';
      color: var(--accent-green);
      font-family: var(--font-mono);
    }}

    .nav-link {{
      color: var(--accent-blue);
      text-decoration: none;
      font-size: 14px;
    }}

    .nav-link:hover {{
      text-decoration: underline;
    }}

    .tabs {{
      display: flex;
      gap: 4px;
      margin-bottom: 24px;
      border-bottom: 1px solid var(--border-default);
    }}

    .tab {{
      padding: 12px 20px;
      background: transparent;
      border: none;
      color: var(--text-secondary);
      cursor: pointer;
      font-size: 14px;
      border-bottom: 2px solid transparent;
      margin-bottom: -1px;
      transition: all 0.2s;
    }}

    .tab:hover {{
      color: var(--text-primary);
      background: var(--bg-secondary);
    }}

    .tab.active {{
      color: var(--accent-green);
      border-bottom-color: var(--accent-green);
    }}

    .tab-content {{
      display: none;
    }}

    .tab-content.active {{
      display: block;
    }}

    .card {{
      background: var(--bg-secondary);
      border: 1px solid var(--border-default);
      border-radius: 12px;
      padding: 20px;
      margin-bottom: 20px;
    }}

    .card-header {{
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 16px;
    }}

    .card-title {{
      font-size: 16px;
      font-weight: 600;
      display: flex;
      align-items: center;
      gap: 8px;
    }}

    .card-title::before {{
      content: '>';
      color: var(--accent-green);
      font-family: var(--font-mono);
    }}

    /* Provider comparison grid */
    .providers-grid {{
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
      gap: 16px;
    }}

    .provider-card {{
      background: var(--bg-tertiary);
      border: 1px solid var(--border-default);
      border-radius: 8px;
      padding: 16px;
      position: relative;
    }}

    .provider-card.connected {{
      border-left: 3px solid var(--accent-green);
    }}

    .provider-card.disconnected {{
      border-left: 3px solid var(--accent-red);
      opacity: 0.7;
    }}

    .provider-header {{
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 12px;
    }}

    .provider-name {{
      font-weight: 600;
      font-size: 16px;
    }}

    .provider-type {{
      font-size: 12px;
      color: var(--text-muted);
      background: var(--bg-secondary);
      padding: 2px 8px;
      border-radius: 4px;
      font-family: var(--font-mono);
    }}

    .status-badge {{
      display: inline-flex;
      align-items: center;
      gap: 6px;
      font-size: 12px;
      padding: 4px 10px;
      border-radius: 12px;
    }}

    .status-badge.online {{
      background: rgba(63, 185, 80, 0.15);
      color: var(--accent-green);
    }}

    .status-badge.offline {{
      background: rgba(248, 81, 73, 0.15);
      color: var(--accent-red);
    }}

    .status-dot {{
      width: 6px;
      height: 6px;
      border-radius: 50%;
      background: currentColor;
    }}

    .metrics-grid {{
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 12px;
    }}

    .metric {{
      background: var(--bg-secondary);
      padding: 12px;
      border-radius: 6px;
    }}

    .metric-value {{
      font-family: var(--font-mono);
      font-size: 20px;
      font-weight: 700;
      color: var(--text-primary);
    }}

    .metric-value.success {{ color: var(--accent-green); }}
    .metric-value.warning {{ color: var(--accent-orange); }}
    .metric-value.danger {{ color: var(--accent-red); }}

    .metric-label {{
      font-size: 11px;
      color: var(--text-muted);
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }}

    /* Comparison table */
    .comparison-table {{
      width: 100%;
      border-collapse: collapse;
      font-size: 13px;
    }}

    .comparison-table th,
    .comparison-table td {{
      padding: 12px 16px;
      text-align: left;
      border-bottom: 1px solid var(--border-muted);
    }}

    .comparison-table th {{
      color: var(--text-muted);
      font-weight: 600;
      text-transform: uppercase;
      font-size: 11px;
      letter-spacing: 0.5px;
      background: var(--bg-tertiary);
    }}

    .comparison-table tr:hover {{
      background: var(--bg-tertiary);
    }}

    .quality-bar {{
      width: 100px;
      height: 8px;
      background: var(--bg-tertiary);
      border-radius: 4px;
      overflow: hidden;
    }}

    .quality-bar-fill {{
      height: 100%;
      border-radius: 4px;
      transition: width 0.3s;
    }}

    .quality-bar-fill.excellent {{ background: var(--accent-green); }}
    .quality-bar-fill.good {{ background: var(--accent-blue); }}
    .quality-bar-fill.fair {{ background: var(--accent-orange); }}
    .quality-bar-fill.poor {{ background: var(--accent-red); }}

    /* Failover rules section */
    .rule-card {{
      background: var(--bg-tertiary);
      border: 1px solid var(--border-default);
      border-radius: 8px;
      padding: 16px;
      margin-bottom: 12px;
    }}

    .rule-header {{
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 12px;
    }}

    .rule-chain {{
      display: flex;
      align-items: center;
      gap: 8px;
      font-family: var(--font-mono);
      font-size: 13px;
    }}

    .rule-provider {{
      background: var(--bg-secondary);
      padding: 4px 10px;
      border-radius: 4px;
    }}

    .rule-provider.primary {{
      border: 1px solid var(--accent-green);
      color: var(--accent-green);
    }}

    .rule-provider.backup {{
      border: 1px solid var(--border-default);
      color: var(--text-secondary);
    }}

    .rule-provider.active {{
      background: var(--accent-green-dim);
      color: var(--accent-green);
    }}

    .rule-arrow {{
      color: var(--text-muted);
    }}

    .rule-status {{
      font-size: 12px;
      padding: 4px 10px;
      border-radius: 4px;
    }}

    .rule-status.normal {{
      background: rgba(63, 185, 80, 0.15);
      color: var(--accent-green);
    }}

    .rule-status.failover {{
      background: rgba(248, 81, 73, 0.15);
      color: var(--accent-red);
    }}

    .rule-thresholds {{
      display: flex;
      gap: 16px;
      font-size: 12px;
      color: var(--text-secondary);
    }}

    /* Symbol mappings */
    .mapping-table {{
      width: 100%;
      border-collapse: collapse;
      font-size: 13px;
    }}

    .mapping-table th,
    .mapping-table td {{
      padding: 10px 12px;
      text-align: left;
      border-bottom: 1px solid var(--border-muted);
    }}

    .mapping-table th {{
      background: var(--bg-tertiary);
      color: var(--text-muted);
      font-size: 11px;
      text-transform: uppercase;
      font-weight: 600;
    }}

    .mapping-table td {{
      font-family: var(--font-mono);
    }}

    .mapping-table tr:hover {{
      background: var(--bg-tertiary);
    }}

    .symbol-canonical {{
      font-weight: 600;
      color: var(--accent-cyan);
    }}

    .symbol-mapped {{
      color: var(--text-secondary);
    }}

    .symbol-empty {{
      color: var(--text-muted);
      font-style: italic;
    }}

    /* Forms */
    .form-group {{
      margin-bottom: 16px;
    }}

    .form-label {{
      display: block;
      font-size: 12px;
      color: var(--text-muted);
      margin-bottom: 6px;
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }}

    .form-input, .form-select {{
      width: 100%;
      padding: 10px 12px;
      background: var(--bg-tertiary);
      border: 1px solid var(--border-default);
      border-radius: 6px;
      color: var(--text-primary);
      font-size: 14px;
      font-family: var(--font-mono);
    }}

    .form-input:focus, .form-select:focus {{
      outline: none;
      border-color: var(--accent-blue);
      box-shadow: var(--glow-blue);
    }}

    .form-row {{
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
      gap: 12px;
    }}

    .btn {{
      padding: 10px 20px;
      border: none;
      border-radius: 6px;
      font-size: 14px;
      cursor: pointer;
      transition: all 0.2s;
    }}

    .btn-primary {{
      background: var(--accent-green-dim);
      color: var(--accent-green);
    }}

    .btn-primary:hover {{
      background: var(--accent-green);
      color: var(--bg-primary);
    }}

    .btn-danger {{
      background: rgba(248, 81, 73, 0.15);
      color: var(--accent-red);
    }}

    .btn-danger:hover {{
      background: var(--accent-red);
      color: var(--bg-primary);
    }}

    .btn-secondary {{
      background: var(--bg-tertiary);
      color: var(--text-secondary);
      border: 1px solid var(--border-default);
    }}

    .btn-secondary:hover {{
      background: var(--bg-hover);
      color: var(--text-primary);
    }}

    .actions {{
      display: flex;
      gap: 8px;
      margin-top: 16px;
    }}

    /* Empty state */
    .empty-state {{
      text-align: center;
      padding: 40px 20px;
      color: var(--text-muted);
    }}

    .empty-state-icon {{
      font-size: 48px;
      margin-bottom: 16px;
      opacity: 0.5;
    }}
  </style>
</head>
<body>
  <div class=""header"">
    <h1>Provider Comparison</h1>
    <a href=""/"" class=""nav-link"">Back to Dashboard</a>
  </div>

  <div class=""tabs"">
    <button class=""tab active"" data-tab=""comparison"">Comparison View</button>
    <button class=""tab"" data-tab=""failover"">Failover Rules</button>
    <button class=""tab"" data-tab=""mappings"">Symbol Mappings</button>
  </div>

  <!-- Comparison Tab -->
  <div id=""comparison"" class=""tab-content active"">
    <div class=""card"">
      <div class=""card-header"">
        <span class=""card-title"">Active Providers</span>
        <span id=""provider-count"" style=""color: var(--text-muted); font-size: 13px;"">Loading...</span>
      </div>
      <div id=""providers-grid"" class=""providers-grid"">
        <div class=""empty-state"">
          <div class=""empty-state-icon"">&#128268;</div>
          <p>Loading provider data...</p>
        </div>
      </div>
    </div>

    <div class=""card"">
      <div class=""card-header"">
        <span class=""card-title"">Metrics Comparison</span>
      </div>
      <table class=""comparison-table"">
        <thead>
          <tr>
            <th>Provider</th>
            <th>Trades</th>
            <th>Quotes</th>
            <th>Depth</th>
            <th>Latency (avg)</th>
            <th>Quality Score</th>
            <th>Connection</th>
          </tr>
        </thead>
        <tbody id=""metrics-body"">
          <tr>
            <td colspan=""7"" style=""text-align: center; color: var(--text-muted);"">Loading...</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>

  <!-- Failover Rules Tab -->
  <div id=""failover"" class=""tab-content"">
    <div class=""card"">
      <div class=""card-header"">
        <span class=""card-title"">Failover Configuration</span>
      </div>
      <div class=""form-row"">
        <div class=""form-group"">
          <label class=""form-label"">Enable Failover</label>
          <select id=""enable-failover"" class=""form-select"">
            <option value=""true"">Enabled</option>
            <option value=""false"">Disabled</option>
          </select>
        </div>
        <div class=""form-group"">
          <label class=""form-label"">Health Check Interval (s)</label>
          <input type=""number"" id=""health-interval"" class=""form-input"" value=""10"" min=""5"" max=""300"" />
        </div>
        <div class=""form-group"">
          <label class=""form-label"">Auto Recovery</label>
          <select id=""auto-recover"" class=""form-select"">
            <option value=""true"">Enabled</option>
            <option value=""false"">Disabled</option>
          </select>
        </div>
        <div class=""form-group"">
          <label class=""form-label"">Timeout (s)</label>
          <input type=""number"" id=""failover-timeout"" class=""form-input"" value=""30"" min=""5"" max=""300"" />
        </div>
      </div>
      <button class=""btn btn-primary"" onclick=""saveFailoverConfig()"">Save Configuration</button>
    </div>

    <div class=""card"">
      <div class=""card-header"">
        <span class=""card-title"">Failover Rules</span>
        <button class=""btn btn-primary"" onclick=""showAddRuleForm()"">Add Rule</button>
      </div>
      <div id=""rules-list"">
        <div class=""empty-state"">
          <div class=""empty-state-icon"">&#9881;</div>
          <p>No failover rules configured</p>
        </div>
      </div>
    </div>

    <div id=""add-rule-form"" class=""card"" style=""display: none;"">
      <div class=""card-header"">
        <span class=""card-title"">Add Failover Rule</span>
      </div>
      <div class=""form-group"">
        <label class=""form-label"">Primary Provider</label>
        <select id=""rule-primary"" class=""form-select"">
          <option value="""">Select provider...</option>
        </select>
      </div>
      <div class=""form-group"">
        <label class=""form-label"">Backup Providers (comma-separated IDs)</label>
        <input type=""text"" id=""rule-backups"" class=""form-input"" placeholder=""backup1, backup2"" />
      </div>
      <div class=""form-row"">
        <div class=""form-group"">
          <label class=""form-label"">Failover Threshold</label>
          <input type=""number"" id=""rule-failover-threshold"" class=""form-input"" value=""3"" min=""1"" />
        </div>
        <div class=""form-group"">
          <label class=""form-label"">Recovery Threshold</label>
          <input type=""number"" id=""rule-recovery-threshold"" class=""form-input"" value=""5"" min=""1"" />
        </div>
        <div class=""form-group"">
          <label class=""form-label"">Quality Threshold (0-100)</label>
          <input type=""number"" id=""rule-quality-threshold"" class=""form-input"" value=""0"" min=""0"" max=""100"" />
        </div>
        <div class=""form-group"">
          <label class=""form-label"">Max Latency (ms, 0=disabled)</label>
          <input type=""number"" id=""rule-max-latency"" class=""form-input"" value=""0"" min=""0"" />
        </div>
      </div>
      <div class=""actions"">
        <button class=""btn btn-primary"" onclick=""saveRule()"">Save Rule</button>
        <button class=""btn btn-secondary"" onclick=""hideAddRuleForm()"">Cancel</button>
      </div>
    </div>
  </div>

  <!-- Symbol Mappings Tab -->
  <div id=""mappings"" class=""tab-content"">
    <div class=""card"">
      <div class=""card-header"">
        <span class=""card-title"">Symbol Mappings</span>
        <div style=""display: flex; gap: 8px;"">
          <button class=""btn btn-secondary"" onclick=""showImportForm()"">Import CSV</button>
          <button class=""btn btn-primary"" onclick=""showAddMappingForm()"">Add Mapping</button>
        </div>
      </div>
      <table class=""mapping-table"">
        <thead>
          <tr>
            <th>Canonical</th>
            <th>IB</th>
            <th>Alpaca</th>
            <th>Polygon</th>
            <th>Yahoo</th>
            <th>Name</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody id=""mappings-body"">
          <tr>
            <td colspan=""7"" style=""text-align: center; color: var(--text-muted);"">Loading...</td>
          </tr>
        </tbody>
      </table>
    </div>

    <div id=""add-mapping-form"" class=""card"" style=""display: none;"">
      <div class=""card-header"">
        <span class=""card-title"">Add Symbol Mapping</span>
      </div>
      <div class=""form-row"">
        <div class=""form-group"">
          <label class=""form-label"">Canonical Symbol*</label>
          <input type=""text"" id=""mapping-canonical"" class=""form-input"" placeholder=""AAPL"" />
        </div>
        <div class=""form-group"">
          <label class=""form-label"">Name</label>
          <input type=""text"" id=""mapping-name"" class=""form-input"" placeholder=""Apple Inc."" />
        </div>
      </div>
      <div class=""form-row"">
        <div class=""form-group"">
          <label class=""form-label"">IB Symbol</label>
          <input type=""text"" id=""mapping-ib"" class=""form-input"" />
        </div>
        <div class=""form-group"">
          <label class=""form-label"">Alpaca Symbol</label>
          <input type=""text"" id=""mapping-alpaca"" class=""form-input"" />
        </div>
        <div class=""form-group"">
          <label class=""form-label"">Polygon Symbol</label>
          <input type=""text"" id=""mapping-polygon"" class=""form-input"" />
        </div>
        <div class=""form-group"">
          <label class=""form-label"">Yahoo Symbol</label>
          <input type=""text"" id=""mapping-yahoo"" class=""form-input"" />
        </div>
      </div>
      <div class=""form-group"">
        <label class=""form-label"">FIGI</label>
        <input type=""text"" id=""mapping-figi"" class=""form-input"" placeholder=""BBG000B9XRY4"" />
      </div>
      <div class=""actions"">
        <button class=""btn btn-primary"" onclick=""saveMapping()"">Save Mapping</button>
        <button class=""btn btn-secondary"" onclick=""hideAddMappingForm()"">Cancel</button>
      </div>
    </div>
  </div>

<script>
// Tab switching
document.querySelectorAll('.tab').forEach(tab => {{
  tab.addEventListener('click', () => {{
    document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
    document.querySelectorAll('.tab-content').forEach(c => c.classList.remove('active'));
    tab.classList.add('active');
    document.getElementById(tab.dataset.tab).classList.add('active');
  }});
}});

// Load provider status and metrics
async function loadProviderData() {{
  try {{
    const [statusRes, metricsRes] = await Promise.all([
      fetch('/api/providers/status'),
      fetch('/api/providers/metrics')
    ]);

    const status = await statusRes.json();
    const metrics = await metricsRes.json();

    renderProviderCards(status, metrics);
    renderMetricsTable(metrics);
    populateProviderSelects(status);
  }} catch (e) {{
    console.error('Failed to load provider data:', e);
  }}
}}

function renderProviderCards(status, metrics) {{
  const grid = document.getElementById('providers-grid');
  const count = document.getElementById('provider-count');

  if (!status || status.length === 0) {{
    grid.innerHTML = `
      <div class=""empty-state"">
        <div class=""empty-state-icon"">&#128268;</div>
        <p>No providers configured. Add data sources in the dashboard.</p>
      </div>
    `;
    count.textContent = '0 providers';
    return;
  }}

  const connected = status.filter(s => s.isConnected).length;
  count.textContent = `${{connected}}/${{status.length}} connected`;

  grid.innerHTML = status.map(s => {{
    const m = metrics.find(m => m.providerId === s.providerId) || {{}};
    return `
      <div class=""provider-card ${{s.isConnected ? 'connected' : 'disconnected'}}"">
        <div class=""provider-header"">
          <div>
            <div class=""provider-name"">${{s.name || s.providerId}}</div>
            <span class=""provider-type"">${{s.providerType}}</span>
          </div>
          <span class=""status-badge ${{s.isConnected ? 'online' : 'offline'}}"">
            <span class=""status-dot""></span>
            ${{s.isConnected ? 'Online' : 'Offline'}}
          </span>
        </div>
        <div class=""metrics-grid"">
          <div class=""metric"">
            <div class=""metric-value"">${{formatNumber(m.tradesReceived || 0)}}</div>
            <div class=""metric-label"">Trades</div>
          </div>
          <div class=""metric"">
            <div class=""metric-value"">${{formatNumber(m.quotesReceived || 0)}}</div>
            <div class=""metric-label"">Quotes</div>
          </div>
          <div class=""metric"">
            <div class=""metric-value ${{getLatencyClass(m.averageLatencyMs)}}"">${{(m.averageLatencyMs || 0).toFixed(1)}}ms</div>
            <div class=""metric-label"">Avg Latency</div>
          </div>
          <div class=""metric"">
            <div class=""metric-value ${{getQualityClass(m.dataQualityScore)}}"">${{(m.dataQualityScore || 100).toFixed(0)}}%</div>
            <div class=""metric-label"">Quality</div>
          </div>
        </div>
      </div>
    `;
  }}).join('');
}}

function renderMetricsTable(metrics) {{
  const tbody = document.getElementById('metrics-body');

  if (!metrics || metrics.length === 0) {{
    tbody.innerHTML = '<tr><td colspan=""7"" style=""text-align: center; color: var(--text-muted);"">No metrics available</td></tr>';
    return;
  }}

  tbody.innerHTML = metrics.map(m => `
    <tr>
      <td style=""font-weight: 600;"">${{m.providerId}}</td>
      <td>${{formatNumber(m.tradesReceived)}}</td>
      <td>${{formatNumber(m.quotesReceived)}}</td>
      <td>${{formatNumber(m.depthUpdatesReceived)}}</td>
      <td>${{m.averageLatencyMs.toFixed(1)}}ms</td>
      <td>
        <div style=""display: flex; align-items: center; gap: 8px;"">
          <div class=""quality-bar"">
            <div class=""quality-bar-fill ${{getQualityBarClass(m.dataQualityScore)}}"" style=""width: ${{m.dataQualityScore}}%""></div>
          </div>
          <span>${{m.dataQualityScore.toFixed(0)}}%</span>
        </div>
      </td>
      <td>${{m.connectionSuccessRate.toFixed(0)}}%</td>
    </tr>
  `).join('');
}}

// Failover configuration
async function loadFailoverConfig() {{
  try {{
    const res = await fetch('/api/failover/config');
    const config = await res.json();

    document.getElementById('enable-failover').value = config.enableFailover.toString();
    document.getElementById('health-interval').value = config.healthCheckIntervalSeconds;
    document.getElementById('auto-recover').value = config.autoRecover.toString();
    document.getElementById('failover-timeout').value = config.failoverTimeoutSeconds;

    renderRules(config.rules);
  }} catch (e) {{
    console.error('Failed to load failover config:', e);
  }}
}}

function renderRules(rules) {{
  const container = document.getElementById('rules-list');

  if (!rules || rules.length === 0) {{
    container.innerHTML = `
      <div class=""empty-state"">
        <div class=""empty-state-icon"">&#9881;</div>
        <p>No failover rules configured</p>
      </div>
    `;
    return;
  }}

  container.innerHTML = rules.map(r => `
    <div class=""rule-card"">
      <div class=""rule-header"">
        <div class=""rule-chain"">
          <span class=""rule-provider primary ${{r.currentActiveProviderId === r.primaryProviderId ? 'active' : ''}}"">
            ${{r.primaryProviderId}}
          </span>
          ${{r.backupProviderIds.map(b => `
            <span class=""rule-arrow"">&#8594;</span>
            <span class=""rule-provider backup ${{r.currentActiveProviderId === b ? 'active' : ''}}"">${{b}}</span>
          `).join('')}}
        </div>
        <div style=""display: flex; gap: 8px; align-items: center;"">
          <span class=""rule-status ${{r.isInFailoverState ? 'failover' : 'normal'}}"">
            ${{r.isInFailoverState ? 'In Failover' : 'Normal'}}
          </span>
          <button class=""btn btn-danger"" onclick=""deleteRule('${{r.id}}')"" style=""padding: 4px 8px; font-size: 12px;"">Delete</button>
        </div>
      </div>
      <div class=""rule-thresholds"">
        <span>Failover after ${{r.failoverThreshold}} failures</span>
        <span>Recover after ${{r.recoveryThreshold}} successes</span>
        ${{r.dataQualityThreshold > 0 ? `<span>Min quality: ${{r.dataQualityThreshold}}%</span>` : ''}}
        ${{r.maxLatencyMs > 0 ? `<span>Max latency: ${{r.maxLatencyMs}}ms</span>` : ''}}
      </div>
    </div>
  `).join('');
}}

async function saveFailoverConfig() {{
  try {{
    const res = await fetch('/api/failover/config', {{
      method: 'POST',
      headers: {{ 'Content-Type': 'application/json' }},
      body: JSON.stringify({{
        enableFailover: document.getElementById('enable-failover').value === 'true',
        healthCheckIntervalSeconds: parseInt(document.getElementById('health-interval').value),
        autoRecover: document.getElementById('auto-recover').value === 'true',
        failoverTimeoutSeconds: parseInt(document.getElementById('failover-timeout').value)
      }})
    }});
    if (res.ok) alert('Configuration saved!');
  }} catch (e) {{
    alert('Failed to save: ' + e.message);
  }}
}}

function showAddRuleForm() {{
  document.getElementById('add-rule-form').style.display = 'block';
}}

function hideAddRuleForm() {{
  document.getElementById('add-rule-form').style.display = 'none';
}}

async function saveRule() {{
  const primary = document.getElementById('rule-primary').value;
  const backups = document.getElementById('rule-backups').value.split(',').map(s => s.trim()).filter(s => s);

  if (!primary || backups.length === 0) {{
    alert('Please select a primary provider and at least one backup.');
    return;
  }}

  try {{
    const res = await fetch('/api/failover/rules', {{
      method: 'POST',
      headers: {{ 'Content-Type': 'application/json' }},
      body: JSON.stringify({{
        primaryProviderId: primary,
        backupProviderIds: backups,
        failoverThreshold: parseInt(document.getElementById('rule-failover-threshold').value),
        recoveryThreshold: parseInt(document.getElementById('rule-recovery-threshold').value),
        dataQualityThreshold: parseFloat(document.getElementById('rule-quality-threshold').value),
        maxLatencyMs: parseFloat(document.getElementById('rule-max-latency').value)
      }})
    }});
    if (res.ok) {{
      hideAddRuleForm();
      loadFailoverConfig();
    }}
  }} catch (e) {{
    alert('Failed to save rule: ' + e.message);
  }}
}}

async function deleteRule(id) {{
  if (!confirm('Delete this failover rule?')) return;
  try {{
    await fetch(`/api/failover/rules/${{id}}`, {{ method: 'DELETE' }});
    loadFailoverConfig();
  }} catch (e) {{
    alert('Failed to delete: ' + e.message);
  }}
}}

// Symbol mappings
async function loadMappings() {{
  try {{
    const res = await fetch('/api/symbols/mappings');
    const mappings = await res.json();
    renderMappings(mappings);
  }} catch (e) {{
    console.error('Failed to load mappings:', e);
  }}
}}

function renderMappings(mappings) {{
  const tbody = document.getElementById('mappings-body');

  if (!mappings || mappings.length === 0) {{
    tbody.innerHTML = '<tr><td colspan=""7"" style=""text-align: center; color: var(--text-muted);"">No symbol mappings configured</td></tr>';
    return;
  }}

  tbody.innerHTML = mappings.map(m => `
    <tr>
      <td class=""symbol-canonical"">${{m.canonicalSymbol}}</td>
      <td class=""${{m.ibSymbol ? 'symbol-mapped' : 'symbol-empty'}}"">${{m.ibSymbol || '-'}}</td>
      <td class=""${{m.alpacaSymbol ? 'symbol-mapped' : 'symbol-empty'}}"">${{m.alpacaSymbol || '-'}}</td>
      <td class=""${{m.polygonSymbol ? 'symbol-mapped' : 'symbol-empty'}}"">${{m.polygonSymbol || '-'}}</td>
      <td class=""${{m.yahooSymbol ? 'symbol-mapped' : 'symbol-empty'}}"">${{m.yahooSymbol || '-'}}</td>
      <td style=""font-family: var(--font-sans); color: var(--text-secondary);"">${{m.name || '-'}}</td>
      <td>
        <button class=""btn btn-danger"" onclick=""deleteMapping('${{m.canonicalSymbol}}')"" style=""padding: 4px 8px; font-size: 12px;"">Delete</button>
      </td>
    </tr>
  `).join('');
}}

function showAddMappingForm() {{
  document.getElementById('add-mapping-form').style.display = 'block';
}}

function hideAddMappingForm() {{
  document.getElementById('add-mapping-form').style.display = 'none';
}}

function showImportForm() {{
  // Create file input and trigger click
  const input = document.createElement('input');
  input.type = 'file';
  input.accept = '.csv';
  input.onchange = async (e) => {{
    const file = e.target.files[0];
    if (!file) return;

    const formData = new FormData();
    formData.append('file', file);

    try {{
      const res = await fetch('/api/symbols/mappings/import', {{
        method: 'POST',
        body: formData
      }});
      const result = await res.json();
      alert(`Imported ${{result.imported}} symbol mappings`);
      loadMappings();
    }} catch (e) {{
      alert('Import failed: ' + e.message);
    }}
  }};
  input.click();
}}

async function saveMapping() {{
  const canonical = document.getElementById('mapping-canonical').value.trim();
  if (!canonical) {{
    alert('Canonical symbol is required');
    return;
  }}

  try {{
    const res = await fetch('/api/symbols/mappings', {{
      method: 'POST',
      headers: {{ 'Content-Type': 'application/json' }},
      body: JSON.stringify({{
        canonicalSymbol: canonical,
        ibSymbol: document.getElementById('mapping-ib').value.trim() || null,
        alpacaSymbol: document.getElementById('mapping-alpaca').value.trim() || null,
        polygonSymbol: document.getElementById('mapping-polygon').value.trim() || null,
        yahooSymbol: document.getElementById('mapping-yahoo').value.trim() || null,
        name: document.getElementById('mapping-name').value.trim() || null,
        figi: document.getElementById('mapping-figi').value.trim() || null
      }})
    }});
    if (res.ok) {{
      hideAddMappingForm();
      loadMappings();
      // Clear form
      document.getElementById('mapping-canonical').value = '';
      document.getElementById('mapping-ib').value = '';
      document.getElementById('mapping-alpaca').value = '';
      document.getElementById('mapping-polygon').value = '';
      document.getElementById('mapping-yahoo').value = '';
      document.getElementById('mapping-name').value = '';
      document.getElementById('mapping-figi').value = '';
    }}
  }} catch (e) {{
    alert('Failed to save: ' + e.message);
  }}
}}

async function deleteMapping(symbol) {{
  if (!confirm(`Delete mapping for ${{symbol}}?`)) return;
  try {{
    await fetch(`/api/symbols/mappings/${{encodeURIComponent(symbol)}}`, {{ method: 'DELETE' }});
    loadMappings();
  }} catch (e) {{
    alert('Failed to delete: ' + e.message);
  }}
}}

function populateProviderSelects(status) {{
  const select = document.getElementById('rule-primary');
  select.innerHTML = '<option value="""">Select provider...</option>' +
    status.map(s => `<option value=""${{s.providerId}}"">${{s.name || s.providerId}} (${{s.providerType}})</option>`).join('');
}}

// Utility functions
function formatNumber(n) {{
  if (n >= 1e6) return (n/1e6).toFixed(1) + 'M';
  if (n >= 1e3) return (n/1e3).toFixed(1) + 'K';
  return n.toString();
}}

function getLatencyClass(ms) {{
  if (ms < 10) return 'success';
  if (ms < 50) return '';
  if (ms < 100) return 'warning';
  return 'danger';
}}

function getQualityClass(score) {{
  if (score >= 90) return 'success';
  if (score >= 70) return '';
  if (score >= 50) return 'warning';
  return 'danger';
}}

function getQualityBarClass(score) {{
  if (score >= 90) return 'excellent';
  if (score >= 70) return 'good';
  if (score >= 50) return 'fair';
  return 'poor';
}}

// Initialize
loadProviderData();
loadFailoverConfig();
loadMappings();

// Refresh every 5 seconds
setInterval(loadProviderData, 5000);
</script>
</body>
</html>";

    private static string Escape(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}

public sealed class ConfigStore
{
    public string ConfigPath { get; }

    public ConfigStore()
    {
        // Config lives at solution root by convention.
        ConfigPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "appsettings.json"));
    }

    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return new AppConfig();
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json, AppConfigJsonOptions.Read) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public async Task SaveAsync(AppConfig cfg)
    {
        var json = JsonSerializer.Serialize(cfg, AppConfigJsonOptions.Write);
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        await File.WriteAllTextAsync(ConfigPath, json);
    }

    public string? TryLoadStatusJson()
    {
        try
        {
            var statusPath = GetStatusPath();
            return File.Exists(statusPath) ? File.ReadAllText(statusPath) : null;
        }
        catch
        {
            return null;
        }
    }

    public string GetStatusPath(AppConfig? cfg = null)
    {
        cfg ??= Load();
        var root = GetDataRoot(cfg);
        return Path.Combine(root, "_status", "status.json");
    }

    public string GetBackfillStatusPath(AppConfig? cfg = null)
    {
        cfg ??= Load();
        var root = GetDataRoot(cfg);
        return Path.Combine(root, "_status", "backfill.json");
    }

    public BackfillResult? TryLoadBackfillStatus()
    {
        var cfg = Load();
        var store = new BackfillStatusStore(GetDataRoot(cfg));
        return store.TryRead();
    }

    public string GetDataRoot(AppConfig? cfg = null)
    {
        cfg ??= Load();
        var root = string.IsNullOrWhiteSpace(cfg.DataRoot) ? "data" : cfg.DataRoot;
        var baseDir = Path.GetDirectoryName(ConfigPath)!;
        return Path.GetFullPath(Path.Combine(baseDir, root));
    }
}

public sealed class BackfillCoordinator
{
    private readonly ConfigStore _store;
    private readonly ILogger _log = LoggingSetup.ForContext<BackfillCoordinator>();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private BackfillResult? _lastRun;

    public BackfillCoordinator(ConfigStore store)
    {
        _store = store;
        _lastRun = store.TryLoadBackfillStatus();
    }

    public IEnumerable<object> DescribeProviders()
    {
        var service = CreateService();
        return service.Providers
            .Select(p => new { p.Name, p.DisplayName, p.Description });
    }

    public BackfillResult? TryReadLast() => _lastRun ?? _store.TryLoadBackfillStatus();

    public async Task<BackfillResult> RunAsync(BackfillRequest request, CancellationToken ct = default)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, ct).ConfigureAwait(false))
            throw new InvalidOperationException("A backfill is already running. Please try again after it completes.");

        try
        {
            var cfg = _store.Load();
            var storageOpt = cfg.Storage?.ToStorageOptions(cfg.DataRoot, cfg.Compress)
                ?? new StorageOptions
                {
                    RootPath = cfg.DataRoot,
                    Compress = cfg.Compress,
                    NamingConvention = FileNamingConvention.BySymbol,
                    DatePartition = DatePartition.Daily
                };

            var policy = new JsonlStoragePolicy(storageOpt);
            await using var sink = new JsonlStorageSink(storageOpt, policy);
            await using var pipeline = new EventPipeline(sink, capacity: 20_000, enablePeriodicFlush: false);

            // Keep pipeline counters scoped per run
            Metrics.Reset();

            var service = CreateService();
            var result = await service.RunAsync(request, pipeline, ct).ConfigureAwait(false);

            var statusStore = new BackfillStatusStore(_store.GetDataRoot(cfg));
            await statusStore.WriteAsync(result).ConfigureAwait(false);
            _lastRun = result;
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    private HistoricalBackfillService CreateService()
    {
        var providers = new IHistoricalDataProvider[]
        {
            new StooqHistoricalDataProvider()
        };
        return new HistoricalBackfillService(providers, _log);
    }
}

public record BackfillRequestDto(string? Provider, string[] Symbols, DateOnly? From, DateOnly? To);
