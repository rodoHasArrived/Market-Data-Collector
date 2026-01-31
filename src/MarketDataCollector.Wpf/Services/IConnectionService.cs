using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Wpf.Services;

public interface IConnectionService
{
    bool IsConnected { get; }
    string BaseUrl { get; }
    
    event EventHandler<bool>? ConnectionStatusChanged;
    
    Task<bool> CheckConnectionAsync(CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> GetAsync(string endpoint, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> PostAsync(string endpoint, HttpContent? content = null, CancellationToken cancellationToken = default);
    Task<T?> GetJsonAsync<T>(string endpoint, CancellationToken cancellationToken = default);
}
