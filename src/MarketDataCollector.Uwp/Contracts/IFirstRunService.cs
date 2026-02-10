using System.Threading.Tasks;

namespace MarketDataCollector.Uwp.Contracts;

/// <summary>
/// Interface for first-run initialization and setup operations.
/// </summary>
public interface IFirstRunService
{
    Task<bool> IsFirstRunAsync();
    Task InitializeAsync();
    Task ResetFirstRunAsync();
}
