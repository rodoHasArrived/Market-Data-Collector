namespace MarketDataCollector.Tests.Infrastructure;

/// <summary>
/// xUnit test fixture that provides a temporary directory for tests.
/// Automatically creates a unique temp directory on construction and
/// cleans it up on disposal.
/// </summary>
/// <example>
/// <code>
/// public class MyTests : IClassFixture&lt;TempDirectoryFixture&gt;
/// {
///     private readonly TempDirectoryFixture _fixture;
///
///     public MyTests(TempDirectoryFixture fixture)
///     {
///         _fixture = fixture;
///     }
///
///     [Fact]
///     public void Test()
///     {
///         var testDir = _fixture.CreateSubdirectory("test");
///         // Use testDir...
///     }
/// }
/// </code>
/// </example>
public sealed class TempDirectoryFixture : IDisposable
{
    private readonly string _rootPath;
    private bool _disposed;

    /// <summary>
    /// Gets the root path of the temporary directory.
    /// </summary>
    public string RootPath => _rootPath;

    /// <summary>
    /// Creates a new temp directory fixture with a unique directory under system temp.
    /// </summary>
    /// <param name="prefix">Optional prefix for the directory name (default: "mdc_test")</param>
    public TempDirectoryFixture(string prefix = "mdc_test")
    {
        _rootPath = Path.Combine(Path.GetTempPath(), $"{prefix}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootPath);
    }

    /// <summary>
    /// Creates a subdirectory under the temp root.
    /// </summary>
    /// <param name="name">Name of the subdirectory</param>
    /// <returns>Full path to the created subdirectory</returns>
    public string CreateSubdirectory(string name)
    {
        var path = Path.Combine(_rootPath, name);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Gets a unique file path within the temp directory.
    /// </summary>
    /// <param name="filename">Filename (optionally with subdirectory path)</param>
    /// <returns>Full path to the file location</returns>
    public string GetFilePath(string filename)
    {
        var fullPath = Path.Combine(_rootPath, filename);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return fullPath;
    }

    /// <summary>
    /// Writes content to a file in the temp directory.
    /// </summary>
    public async Task WriteFileAsync(string filename, string content, CancellationToken ct = default)
    {
        var path = GetFilePath(filename);
        await File.WriteAllTextAsync(path, content, ct);
    }

    /// <summary>
    /// Writes lines to a file in the temp directory.
    /// </summary>
    public async Task WriteLinesAsync(string filename, IEnumerable<string> lines, CancellationToken ct = default)
    {
        var path = GetFilePath(filename);
        await File.WriteAllLinesAsync(path, lines, ct);
    }

    /// <summary>
    /// Cleans up the temp directory.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (Directory.Exists(_rootPath))
            {
                Directory.Delete(_rootPath, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best effort cleanup - directory may be in use
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort cleanup
        }
    }
}

/// <summary>
/// Collection definition for tests that share a temp directory.
/// Use [Collection("TempDirectory")] on test classes to share the fixture.
/// </summary>
[CollectionDefinition("TempDirectory")]
public class TempDirectoryCollection : ICollectionFixture<TempDirectoryFixture>
{
    // This class has no code - it's used to define the collection
}
