using Meridian.Contracts.SecurityMaster;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Meridian.Infrastructure.SecurityMaster;

/// <summary>
/// DI registration and startup helpers for the Security Master service.
/// </summary>
public static class SecurityMasterServiceExtensions
{
    /// <summary>
    /// Register <see cref="SecurityMasterService"/> as a singleton <see cref="ISecurityMasterService"/>.
    /// The service is NOT pre-loaded — call <see cref="InitializeSecurityMasterAsync"/> after
    /// the host is built to hydrate it from disk.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="dataFilePath">
    /// Path to the JSON persistence file. Defaults to <c>data/security-master.json</c>
    /// relative to the current directory.
    /// </param>
    public static IServiceCollection AddSecurityMaster(
        this IServiceCollection services,
        string? dataFilePath = null)
    {
        var path = dataFilePath ?? Path.Combine("data", "security-master.json");

        services.AddSingleton<SecurityMasterService>(sp =>
            new SecurityMasterService(
                path,
                sp.GetRequiredService<ILogger<SecurityMasterService>>()));

        services.AddSingleton<ISecurityMasterService>(sp =>
            sp.GetRequiredService<SecurityMasterService>());

        return services;
    }

    /// <summary>
    /// Load the Security Master from disk. Call this once after the host is started,
    /// before serving any requests that require instrument resolution.
    /// </summary>
    public static async Task InitializeSecurityMasterAsync(
        this IServiceProvider serviceProvider,
        CancellationToken ct = default)
    {
        var service = serviceProvider.GetRequiredService<SecurityMasterService>();
        await service.LoadAsync(ct);
    }
}
