namespace MarketDataCollector.Storage;

public sealed class StorageOptions
{
    public string RootPath { get; init; } = "data";
    public bool Compress { get; init; } = true;
}
