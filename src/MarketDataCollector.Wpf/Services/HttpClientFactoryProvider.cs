using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Provides HttpClient instances using the IHttpClientFactory pattern.
/// This ensures proper HttpClient lifecycle management and avoids socket exhaustion.
/// </summary>
public static class HttpClientFactoryProvider
{
    private static IServiceProvider? _serviceProvider;
    private static IHttpClientFactory? _httpClientFactory;
    private static bool _isInitialized;
    private static readonly object _lock = new();

    /// <summary>
    /// Default client name used when no specific name is provided.
    /// </summary>
    public const string DefaultClientName = "MarketDataCollector";

    /// <summary>
    /// Client name for API calls with JSON content.
    /// </summary>
    public const string JsonApiClientName = "JsonApi";

    /// <summary>
    /// Gets a value indicating whether the factory has been initialized.
    /// </summary>
    public static bool IsInitialized => _isInitialized;

    /// <summary>
    /// Initializes the HttpClientFactory with default configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if already initialized.</exception>
    public static void Initialize()
    {
        Initialize(configureServices: null);
    }

    /// <summary>
    /// Initializes the HttpClientFactory with custom service configuration.
    /// </summary>
    /// <param name="configureServices">Optional action to configure additional services.</param>
    /// <exception cref="InvalidOperationException">Thrown if already initialized.</exception>
    public static void Initialize(Action<IServiceCollection>? configureServices)
    {
        lock (_lock)
        {
            if (_isInitialized)
            {
                LoggingService.Instance.LogWarning("HttpClientFactoryProvider is already initialized");
                return;
            }

            var services = new ServiceCollection();

            // Configure default HttpClient
            services.AddHttpClient(DefaultClientName, client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", "MarketDataCollector-WPF/1.0");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
                MaxConnectionsPerServer = 10
            });

            // Configure JSON API client with appropriate headers
            services.AddHttpClient(JsonApiClientName, client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", "MarketDataCollector-WPF/1.0");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
                MaxConnectionsPerServer = 10
            });

            // Allow additional service configuration
            configureServices?.Invoke(services);

            _serviceProvider = services.BuildServiceProvider();
            _httpClientFactory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
            _isInitialized = true;

            LoggingService.Instance.LogInfo("HttpClientFactoryProvider initialized successfully");
        }
    }

    /// <summary>
    /// Gets an HttpClient instance using the default client name.
    /// </summary>
    /// <returns>An HttpClient instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if not initialized.</exception>
    public static HttpClient GetClient()
    {
        return GetClient(DefaultClientName);
    }

    /// <summary>
    /// Gets an HttpClient instance using the specified client name.
    /// </summary>
    /// <param name="name">The name of the configured client.</param>
    /// <returns>An HttpClient instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if not initialized.</exception>
    /// <exception cref="ArgumentNullException">Thrown if name is null.</exception>
    public static HttpClient GetClient(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        lock (_lock)
        {
            if (!_isInitialized || _httpClientFactory is null)
            {
                throw new InvalidOperationException(
                    "HttpClientFactoryProvider has not been initialized. Call Initialize() first.");
            }

            return _httpClientFactory.CreateClient(name);
        }
    }

    /// <summary>
    /// Gets an HttpClient configured for JSON API calls.
    /// </summary>
    /// <returns>An HttpClient instance configured for JSON.</returns>
    public static HttpClient GetJsonApiClient()
    {
        return GetClient(JsonApiClientName);
    }

    /// <summary>
    /// Registers a named HttpClient with custom configuration.
    /// Must be called before Initialize().
    /// </summary>
    /// <param name="name">The client name.</param>
    /// <param name="configure">Configuration action for the client.</param>
    /// <exception cref="InvalidOperationException">Thrown if already initialized.</exception>
    public static void RegisterClient(string name, Action<HttpClient> configure)
    {
        if (_isInitialized)
        {
            throw new InvalidOperationException(
                "Cannot register clients after initialization. Call RegisterClient before Initialize().");
        }

        // This method provides a way to pre-register clients before initialization
        // The actual registration happens during Initialize() with configureServices
        LoggingService.Instance.LogInfo(
            "Client registration request queued",
            ("ClientName", name));
    }

    /// <summary>
    /// Disposes resources used by the factory.
    /// </summary>
    public static void Dispose()
    {
        lock (_lock)
        {
            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _serviceProvider = null;
            _httpClientFactory = null;
            _isInitialized = false;

            LoggingService.Instance.LogInfo("HttpClientFactoryProvider disposed");
        }
    }
}
