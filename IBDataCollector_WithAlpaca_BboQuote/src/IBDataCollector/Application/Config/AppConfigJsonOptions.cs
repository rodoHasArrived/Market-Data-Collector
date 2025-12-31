using System.Text.Json;

namespace IBDataCollector.Application.Config;

/// <summary>
/// Centralized JSON serializer options for reading/writing AppConfig.
/// </summary>
public static class AppConfigJsonOptions
{
    public static JsonSerializerOptions Read { get; } = CreateBase();

    public static JsonSerializerOptions Write { get; } = CreateWrite();

    private static JsonSerializerOptions CreateBase()
    {
        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        opts.Converters.Add(new DataSourceKindConverter());
        return opts;
    }

    private static JsonSerializerOptions CreateWrite()
    {
        var opts = CreateBase();
        opts.WriteIndented = true;
        return opts;
    }
}
