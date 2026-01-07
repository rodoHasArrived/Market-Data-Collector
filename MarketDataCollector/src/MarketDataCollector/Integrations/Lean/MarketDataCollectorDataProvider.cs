using QuantConnect.Logging;
using QuantConnect.Util;
using System.IO.Compression;

namespace MarketDataCollector.Integrations.Lean;

/// <summary>
/// Custom ILeanDataProvider implementation that reads market data from MarketDataCollector's JSONL files.
/// Supports both compressed (.jsonl.gz) and uncompressed (.jsonl) files.
/// </summary>
public class MarketDataCollectorDataProvider : ILeanDataProvider
{
    private readonly string _dataRoot;

    /// <summary>
    /// Creates a new instance of the MarketDataCollector data provider.
    /// </summary>
    /// <param name="dataRoot">Root directory where MarketDataCollector stores JSONL files (defaults to ./data)</param>
    public MarketDataCollectorDataProvider(string? dataRoot = null)
    {
        _dataRoot = dataRoot ?? Path.Combine(Directory.GetCurrentDirectory(), "data");
        Log.Trace($"MarketDataCollectorDataProvider initialized with data root: {_dataRoot}");
    }

    /// <summary>
    /// Fetches data from the MarketDataCollector JSONL storage.
    /// </summary>
    /// <param name="key">The file path to fetch</param>
    /// <returns>Stream containing the file data, or null if not found</returns>
    public Stream Fetch(string key)
    {
        try
        {
            // Check if the file exists directly
            if (File.Exists(key))
            {
                return OpenFile(key);
            }

            // Try with .gz extension for compressed files
            var gzPath = key + ".gz";
            if (File.Exists(gzPath))
            {
                return OpenFile(gzPath);
            }

            // Try alternative path construction
            var relativePath = key.Replace(Globals.DataFolder, "").TrimStart(Path.DirectorySeparatorChar);
            var alternativePath = Path.Combine(_dataRoot, relativePath);

            if (File.Exists(alternativePath))
            {
                return OpenFile(alternativePath);
            }

            // Try compressed alternative path
            var alternativeGzPath = alternativePath + ".gz";
            if (File.Exists(alternativeGzPath))
            {
                return OpenFile(alternativeGzPath);
            }

            Log.Trace($"MarketDataCollectorDataProvider: File not found: {key}");
            return Stream.Null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"MarketDataCollectorDataProvider: Error fetching {key}");
            return Stream.Null;
        }
    }

    /// <summary>
    /// Opens a file and returns a stream, automatically decompressing if it's a .gz file.
    /// </summary>
    private Stream OpenFile(string filePath)
    {
        var fileStream = File.OpenRead(filePath);

        // If it's a gzip file, wrap in GZipStream
        if (filePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            return new GZipStream(fileStream, CompressionMode.Decompress);
        }

        return fileStream;
    }

    /// <summary>
    /// Disposes of the data provider resources.
    /// </summary>
    public void Dispose()
    {
        // No resources to dispose
    }
}
