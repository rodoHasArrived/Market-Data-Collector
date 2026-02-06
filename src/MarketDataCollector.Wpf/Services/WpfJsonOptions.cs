using System.Text.Json;
using System.Text.Json.Serialization;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Centralized JSON serialization options for the WPF application.
/// Consolidates all JsonSerializerOptions to avoid duplication across services.
///
/// This mirrors the patterns from MarketDataCollector.Application.Serialization.MarketDataJsonContext
/// but provides UWP-specific defaults without requiring a reference to the core assembly.
/// </summary>
public static class WpfJsonOptions
{
    /// <summary>
    /// Options for compact JSON output (storage, API communication).
    /// - No indentation
    /// - CamelCase property naming
    /// - Null values omitted
    /// - Case-insensitive property matching on read
    /// </summary>
    public static readonly JsonSerializerOptions Compact = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Options for pretty-printed JSON output (config files, debugging, user-visible exports).
    /// - Indented output for readability
    /// - CamelCase property naming
    /// - Null values omitted
    /// - Case-insensitive property matching on read
    /// </summary>
    public static readonly JsonSerializerOptions PrettyPrint = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Options for API communication (matches most REST APIs).
    /// - No indentation (compact wire format)
    /// - CamelCase property naming
    /// - Case-insensitive property matching on read
    /// </summary>
    public static readonly JsonSerializerOptions Api = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}
