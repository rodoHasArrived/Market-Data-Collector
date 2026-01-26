using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.Storage;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for managing the symbol catalog - provides access to popular symbols,
/// commonly traded securities, and symbol categories loaded from configuration.
/// </summary>
public sealed class SymbolCatalogService
{
    private static SymbolCatalogService? _instance;
    private static readonly object _lock = new();

    private SymbolCatalog? _catalog;
    private DateTime _lastLoadTime;
    private const int CatalogCacheDurationMinutes = 30;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static SymbolCatalogService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new SymbolCatalogService();
                }
            }
            return _instance;
        }
    }

    private SymbolCatalogService() { }

    /// <summary>
    /// Gets the symbol catalog, loading from file if needed.
    /// </summary>
    public async Task<SymbolCatalog> GetCatalogAsync()
    {
        // Check if we need to reload
        if (_catalog == null || (DateTime.UtcNow - _lastLoadTime).TotalMinutes > CatalogCacheDurationMinutes)
        {
            await LoadCatalogAsync();
        }

        return _catalog ?? GetDefaultCatalog();
    }

    /// <summary>
    /// Gets all symbols across all categories.
    /// </summary>
    public async Task<IReadOnlyList<CatalogSymbol>> GetAllSymbolsAsync()
    {
        var catalog = await GetCatalogAsync();
        return catalog.Categories
            .SelectMany(c => c.Symbols.Select(s => new CatalogSymbol(s, c.Name, c.Icon)))
            .ToList();
    }

    /// <summary>
    /// Searches for symbols matching the query.
    /// </summary>
    public async Task<IReadOnlyList<CatalogSymbol>> SearchAsync(string query, int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<CatalogSymbol>();

        var normalizedQuery = query.Trim().ToUpperInvariant();
        var catalog = await GetCatalogAsync();

        var results = new List<(CatalogSymbol Symbol, int Score)>();

        foreach (var category in catalog.Categories)
        {
            foreach (var symbol in category.Symbols)
            {
                var score = CalculateMatchScore(symbol, normalizedQuery);
                if (score > 0)
                {
                    results.Add((new CatalogSymbol(symbol, category.Name, category.Icon), score));
                }
            }
        }

        return results
            .OrderByDescending(r => r.Score)
            .Take(limit)
            .Select(r => r.Symbol)
            .ToList();
    }

    /// <summary>
    /// Gets symbols from a specific category.
    /// </summary>
    public async Task<IReadOnlyList<CatalogSymbol>> GetCategorySymbolsAsync(string categoryName)
    {
        var catalog = await GetCatalogAsync();
        var category = catalog.Categories
            .FirstOrDefault(c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));

        if (category == null)
            return Array.Empty<CatalogSymbol>();

        return category.Symbols
            .Select(s => new CatalogSymbol(s, category.Name, category.Icon))
            .ToList();
    }

    /// <summary>
    /// Gets available category names.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetCategoriesAsync()
    {
        var catalog = await GetCatalogAsync();
        return catalog.Categories.Select(c => c.Name).ToList();
    }

    /// <summary>
    /// Reloads the catalog from disk.
    /// </summary>
    public async Task ReloadAsync()
    {
        _catalog = null;
        await LoadCatalogAsync();
    }

    private async Task LoadCatalogAsync()
    {
        try
        {
            // Try to load from local app data first (user-customized)
            var localFolder = ApplicationData.Current.LocalFolder;
            var userCatalogFile = await TryGetFileAsync(localFolder, "symbol-catalog.json");

            if (userCatalogFile != null)
            {
                var json = await FileIO.ReadTextAsync(userCatalogFile);
                _catalog = JsonSerializer.Deserialize<SymbolCatalog>(json, JsonOptions);
                _lastLoadTime = DateTime.UtcNow;
                return;
            }

            // Try to load from app installation folder (shipped with app)
            var installFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
            var assetsFolder = await TryGetFolderAsync(installFolder, "Assets");
            if (assetsFolder != null)
            {
                var defaultCatalogFile = await TryGetFileAsync(assetsFolder, "symbol-catalog.json");
                if (defaultCatalogFile != null)
                {
                    var json = await FileIO.ReadTextAsync(defaultCatalogFile);
                    _catalog = JsonSerializer.Deserialize<SymbolCatalog>(json, JsonOptions);
                    _lastLoadTime = DateTime.UtcNow;
                    return;
                }
            }

            // Fall back to default catalog
            _catalog = GetDefaultCatalog();
            _lastLoadTime = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SymbolCatalogService] Error loading catalog: {ex.Message}");
            _catalog = GetDefaultCatalog();
            _lastLoadTime = DateTime.UtcNow;
        }
    }

    private static async Task<StorageFile?> TryGetFileAsync(StorageFolder folder, string name)
    {
        try
        {
            return await folder.GetFileAsync(name);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private static async Task<StorageFolder?> TryGetFolderAsync(StorageFolder folder, string name)
    {
        try
        {
            return await folder.GetFolderAsync(name);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private static int CalculateMatchScore(SymbolInfo symbol, string query)
    {
        var score = 0;

        // Exact ticker match - highest priority
        if (symbol.Ticker.Equals(query, StringComparison.OrdinalIgnoreCase))
            return 1000;

        // Ticker starts with query
        if (symbol.Ticker.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            score += 500;
        // Ticker contains query
        else if (symbol.Ticker.Contains(query, StringComparison.OrdinalIgnoreCase))
            score += 200;

        // Name matches (if provided)
        if (!string.IsNullOrEmpty(symbol.Name))
        {
            if (symbol.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                score += 100;
        }

        return score;
    }

    /// <summary>
    /// Returns a minimal default catalog when no configuration is found.
    /// This is intentionally small - the real catalog should come from the JSON file.
    /// </summary>
    private static SymbolCatalog GetDefaultCatalog()
    {
        return new SymbolCatalog
        {
            Version = "1.0",
            LastUpdated = DateTime.UtcNow.ToString("O"),
            Categories =
            [
                new SymbolCategory
                {
                    Name = "Major Indices ETFs",
                    Icon = "\uE9D9",
                    Symbols =
                    [
                        new SymbolInfo { Ticker = "SPY", Name = "SPDR S&P 500 ETF" },
                        new SymbolInfo { Ticker = "QQQ", Name = "Invesco QQQ Trust" },
                        new SymbolInfo { Ticker = "DIA", Name = "SPDR Dow Jones Industrial Average" },
                        new SymbolInfo { Ticker = "IWM", Name = "iShares Russell 2000 ETF" }
                    ]
                }
            ]
        };
    }
}

/// <summary>
/// Represents the symbol catalog structure.
/// </summary>
public sealed class SymbolCatalog
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("lastUpdated")]
    public string? LastUpdated { get; set; }

    [JsonPropertyName("categories")]
    public SymbolCategory[] Categories { get; set; } = [];
}

/// <summary>
/// Represents a category of symbols in the catalog.
/// </summary>
public sealed class SymbolCategory
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "\uE9D9";

    [JsonPropertyName("symbols")]
    public SymbolInfo[] Symbols { get; set; } = [];
}

/// <summary>
/// Represents a symbol in the catalog.
/// </summary>
public sealed class SymbolInfo
{
    [JsonPropertyName("ticker")]
    public string Ticker { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("exchange")]
    public string? Exchange { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

/// <summary>
/// Represents a symbol with its category information.
/// </summary>
public sealed class CatalogSymbol
{
    public string Ticker { get; }
    public string? Name { get; }
    public string Category { get; }
    public string Icon { get; }

    public CatalogSymbol(SymbolInfo info, string category, string icon)
    {
        Ticker = info.Ticker;
        Name = info.Name;
        Category = category;
        Icon = icon;
    }

    public override string ToString() => Ticker;
}
