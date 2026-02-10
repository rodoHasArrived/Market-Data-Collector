using MarketDataCollector.Application.Subscriptions.Models;

namespace MarketDataCollector.Application.UI;

// ==================== NEW FEATURE DTOs ====================

public record HistoricalQueryRequest(
    string Symbol,
    DateOnly? From = null,
    DateOnly? To = null,
    string? DataType = null,
    int? Skip = null,
    int? Limit = null
);

public record DiagnosticBundleRequest(
    bool IncludeSystemInfo = true,
    bool IncludeConfiguration = true,
    bool IncludeMetrics = true,
    bool IncludeLogs = true,
    bool IncludeStorageInfo = true,
    bool IncludeEnvironmentVariables = true,
    int LogDays = 3
);

public record SampleDataRequest(
    string[]? Symbols = null,
    int DurationMinutes = 60,
    int MaxEvents = 10000,
    bool IncludeTrades = true,
    bool IncludeQuotes = true,
    bool IncludeDepth = true,
    bool IncludeBars = true
);

public record ConfigValidateRequest(string Json);

public record DryRunRequest(
    bool ValidateConfiguration = true,
    bool ValidateFileSystem = true,
    bool ValidateConnectivity = true,
    bool ValidateProviders = true,
    bool ValidateSymbols = true,
    bool ValidateResources = true
);

// Symbol search DTOs
public record FigiBulkLookupRequest(string[] Symbols);

// Symbol management DTOs
public record CreateTemplateDto(
    string Name,
    string? Description,
    TemplateCategory Category,
    string[] Symbols,
    TemplateSubscriptionDefaults? Defaults
);

public record CreateFromCurrentDto(string Name, string? Description);

public record IndexSubscribeRequestDto(
    int? MaxComponents,
    TemplateSubscriptionDefaults? Defaults,
    bool ReplaceExisting,
    string[]? FilterSectors
);

// Storage organization DTOs
public record FileSearchRequest(
    string[]? Symbols = null,
    string[]? Types = null,
    string[]? Sources = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    long? MinSize = null,
    long? MaxSize = null,
    double? MinQualityScore = null,
    string? PathPattern = null,
    int Skip = 0,
    int Take = 100
);

public record FacetedSearchRequest(
    string[]? Symbols = null,
    string[]? Types = null,
    string[]? Sources = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int MaxResults = 100
);

public record NaturalSearchRequest(string Query);

public record HealthCheckRequest(
    bool ValidateChecksums = true,
    bool CheckSequenceContinuity = true,
    bool IdentifyCorruption = true,
    string[]? Paths = null,
    int ParallelChecks = 4
);

public record RepairRequest(
    string? Strategy = null,
    bool DryRun = false,
    bool BackupBeforeRepair = true,
    string? BackupPath = null
);

public record DefragRequest(
    long MinFileSizeBytes = 1_048_576,
    int MaxFilesPerMerge = 100,
    bool PreserveOriginals = false
);

public record QualityReportRequest(
    string[]? Paths = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    double MinScoreThreshold = 1.0,
    bool IncludeRecommendations = true
);

public record TierMigrationRequest(
    string SourcePath,
    string? TargetTier = null,
    bool DeleteSource = false,
    bool VerifyChecksum = true,
    int ParallelFiles = 4
);

// Credential management DTOs
public record CredentialTestRequest(
    string Provider,
    string? ApiKey = null,
    string? ApiSecret = null,
    string? CredentialSource = null
);

public record OAuthTokenStoreRequest(
    string Provider,
    string AccessToken,
    string? TokenType = null,
    DateTimeOffset? ExpiresAt = null,
    string? RefreshToken = null,
    DateTimeOffset? RefreshTokenExpiresAt = null,
    string? Scope = null
);

// ==================== BULK SYMBOL MANAGEMENT DTOs ====================

public record WatchlistSymbolsRequest(
    string[] Symbols,
    bool SubscribeImmediately = true,
    bool UnsubscribeIfOrphaned = false
);

public record PortfolioImportOptionsDto(
    decimal? MinPositionValue = null,
    decimal? MinQuantity = null,
    string[]? AssetClasses = null,
    string[]? ExcludeSymbols = null,
    bool LongOnly = false,
    bool CreateWatchlist = false,
    string? WatchlistName = null,
    bool SubscribeTrades = true,
    bool SubscribeDepth = true,
    bool SkipExisting = true
);

public record ManualPortfolioEntryDto(
    string Symbol,
    decimal? Quantity = null,
    string? AssetClass = null
);

public record ManualPortfolioImportRequest(
    ManualPortfolioEntryDto[] Entries,
    bool CreateWatchlist = false,
    string? WatchlistName = null,
    bool SubscribeTrades = true,
    bool SubscribeDepth = true,
    bool SkipExisting = true
);
