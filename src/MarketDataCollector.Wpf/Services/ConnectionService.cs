using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Wpf.Services;

public sealed class ConnectionService : IConnectionService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILoggingService _logger;
    private bool _isConnected;

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (_isConnected != value)
            {
                _isConnected = value;
                ConnectionStatusChanged?.Invoke(this, value);
            }
        }
    }

    public string BaseUrl { get; }

    public event EventHandler<bool>? ConnectionStatusChanged;

    public ConnectionService(ILoggingService logger, string? baseUrl = null)
    {
        _logger = logger;
        BaseUrl = baseUrl ?? "http://localhost:8080/api";
        
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };
        
        _logger.Log($"ConnectionService initialized with base URL: {BaseUrl}");
    }

    public async Task<bool> CheckConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Log("Checking connection to backend...");
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            IsConnected = response.IsSuccessStatusCode;
            
            _logger.Log($"Connection check: {(IsConnected ? "Success" : "Failed")}");
            return IsConnected;
        }
        catch (Exception ex)
        {
            _logger.Log($"Connection check failed: {ex.Message}");
            IsConnected = false;
            return false;
        }
    }

    public async Task<HttpResponseMessage> GetAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Log($"GET {endpoint}");
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            response.EnsureSuccessStatusCode();
            return response;
        }
        catch (Exception ex)
        {
            _logger.Log($"GET {endpoint} failed: {ex.Message}");
            throw;
        }
    }

    public async Task<HttpResponseMessage> PostAsync(string endpoint, HttpContent? content = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Log($"POST {endpoint}");
            var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
            response.EnsureSuccessStatusCode();
            return response;
        }
        catch (Exception ex)
        {
            _logger.Log($"POST {endpoint} failed: {ex.Message}");
            throw;
        }
    }

    public async Task<T?> GetJsonAsync<T>(string endpoint, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Log($"GET JSON {endpoint}");
            return await _httpClient.GetFromJsonAsync<T>(endpoint, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Log($"GET JSON {endpoint} failed: {ex.Message}");
            throw;
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
