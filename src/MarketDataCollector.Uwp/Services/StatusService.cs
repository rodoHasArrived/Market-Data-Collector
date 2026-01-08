using System.Text.Json;
using MarketDataCollector.Uwp.Models;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for retrieving system status from the collector.
/// Supports both /api/* and /* endpoint patterns for compatibility.
/// </summary>
public class StatusService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly string _statusUrl;

    public StatusService(string baseUrl = "http://localhost:8080")
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        // Use /api/status endpoint (core now supports both /api/* and /* patterns)
        _statusUrl = $"{baseUrl}/api/status";
    }

    public async Task<StatusResponse?> GetStatusAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(_statusUrl);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<StatusResponse>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Service for managing backfill operations.
/// </summary>
public class BackfillService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public BackfillService(string baseUrl = "http://localhost:8080")
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        _baseUrl = baseUrl;
    }

    public async Task<List<BackfillProviderInfo>> GetProvidersAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/backfill/providers");
            if (!response.IsSuccessStatusCode)
            {
                return new List<BackfillProviderInfo>();
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<BackfillProviderInfo>>(json, JsonOptions)
                ?? new List<BackfillProviderInfo>();
        }
        catch
        {
            return new List<BackfillProviderInfo>();
        }
    }

    public async Task<BackfillResult?> GetLastStatusAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/backfill/status");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<BackfillResult>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public async Task<BackfillResult?> RunBackfillAsync(string provider, string[] symbols, string? from, string? to)
    {
        try
        {
            var request = new
            {
                Provider = provider,
                Symbols = symbols,
                From = from,
                To = to
            };

            var content = new StringContent(
                JsonSerializer.Serialize(request, JsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/backfill/run", content);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new BackfillResult { Success = false, Error = json };
            }

            return JsonSerializer.Deserialize<BackfillResult>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            return new BackfillResult { Success = false, Error = ex.Message };
        }
    }
}
