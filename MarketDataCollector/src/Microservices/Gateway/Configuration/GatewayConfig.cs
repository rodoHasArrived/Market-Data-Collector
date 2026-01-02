using DataIngestion.Contracts.Configuration;

namespace DataIngestion.Gateway.Configuration;

/// <summary>
/// Configuration for the Data Ingestion Gateway.
/// </summary>
public class GatewayConfig : MicroserviceConfig
{
    public GatewayConfig()
    {
        ServiceName = "DataIngestion.Gateway";
    }

    /// <summary>API authentication configuration.</summary>
    public AuthConfig? Auth { get; set; }

    /// <summary>Downstream service URLs.</summary>
    public DownstreamServices? Services { get; set; }

    /// <summary>Provider connection settings.</summary>
    public Dictionary<string, ProviderConfig>? Providers { get; set; }

    /// <summary>Routing rules.</summary>
    public List<RoutingRule>? RoutingRules { get; set; }

    /// <summary>Circuit breaker settings.</summary>
    public CircuitBreakerConfig? CircuitBreaker { get; set; }
}

/// <summary>
/// API authentication configuration.
/// </summary>
public class AuthConfig
{
    public bool Enabled { get; set; } = false;
    public string Type { get; set; } = "ApiKey";
    public string? ApiKeyHeader { get; set; } = "X-Api-Key";
    public List<string>? AllowedApiKeys { get; set; }
    public JwtConfig? Jwt { get; set; }
}

/// <summary>
/// JWT authentication configuration.
/// </summary>
public class JwtConfig
{
    public string? Authority { get; set; }
    public string? Audience { get; set; }
    public bool ValidateIssuer { get; set; } = true;
    public bool ValidateAudience { get; set; } = true;
}

/// <summary>
/// Downstream service URLs.
/// </summary>
public class DownstreamServices
{
    public string TradeIngestionUrl { get; set; } = "http://trade-ingestion:5001";
    public string OrderBookIngestionUrl { get; set; } = "http://orderbook-ingestion:5002";
    public string QuoteIngestionUrl { get; set; } = "http://quote-ingestion:5003";
    public string HistoricalIngestionUrl { get; set; } = "http://historical-ingestion:5004";
    public string ValidationServiceUrl { get; set; } = "http://validation-service:5005";
}

/// <summary>
/// Provider-specific configuration.
/// </summary>
public class ProviderConfig
{
    public bool Enabled { get; set; } = true;
    public string Type { get; set; } = "WebSocket";
    public string? Endpoint { get; set; }
    public Dictionary<string, string>? Credentials { get; set; }
    public int MaxConnectionsPerSymbol { get; set; } = 1;
    public int ReconnectDelayMs { get; set; } = 5000;
    public int MaxReconnectAttempts { get; set; } = 10;
}

/// <summary>
/// Routing rule for directing data to appropriate services.
/// </summary>
public class RoutingRule
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string? SymbolPattern { get; set; }
    public string? Provider { get; set; }
    public string TargetService { get; set; } = string.Empty;
    public int Priority { get; set; } = 0;
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Circuit breaker configuration.
/// </summary>
public class CircuitBreakerConfig
{
    public int FailureThreshold { get; set; } = 5;
    public int SuccessThreshold { get; set; } = 2;
    public int DurationOfBreakSeconds { get; set; } = 30;
    public int SamplingDurationSeconds { get; set; } = 10;
}

/// <summary>
/// IP rate limiting options.
/// </summary>
public class IpRateLimitOptions
{
    public bool EnableEndpointRateLimiting { get; set; } = true;
    public bool StackBlockedRequests { get; set; } = false;
    public string RealIpHeader { get; set; } = "X-Real-IP";
    public string ClientIdHeader { get; set; } = "X-ClientId";
    public int HttpStatusCode { get; set; } = 429;
    public List<RateLimitRule>? GeneralRules { get; set; }
}

/// <summary>
/// Rate limiting rule.
/// </summary>
public class RateLimitRule
{
    public string Endpoint { get; set; } = "*";
    public string Period { get; set; } = "1s";
    public int Limit { get; set; } = 100;
}
