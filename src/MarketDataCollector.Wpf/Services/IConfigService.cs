using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Wpf.Services;

public interface IConfigService
{
    T? GetValue<T>(string key);
    string? GetString(string key);
    int GetInt(string key, int defaultValue = 0);
    bool GetBool(string key, bool defaultValue = false);

    Task LoadConfigurationAsync(CancellationToken cancellationToken = default);
    Task SaveConfigurationAsync(string key, object value, CancellationToken cancellationToken = default);
}
