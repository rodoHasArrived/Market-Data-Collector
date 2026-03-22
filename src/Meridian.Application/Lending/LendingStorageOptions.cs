namespace Meridian.Application.Lending;

/// <summary>
/// Configuration options for the direct-lending event store backend.
/// </summary>
public sealed class LendingStorageOptions
{
    /// <summary>Section name in appsettings.json.</summary>
    public const string Section = "Lending:Storage";

    /// <summary>
    /// When <c>true</c>, events are persisted to PostgreSQL using the connection string
    /// in <see cref="ConnectionString"/>.  When <c>false</c> (default), an in-memory
    /// store is used — suitable for development, tests, and single-process deployments.
    /// </summary>
    public bool UsePostgres { get; set; }

    /// <summary>
    /// Npgsql connection string for the PostgreSQL event store.
    /// Required when <see cref="UsePostgres"/> is <c>true</c>.
    /// </summary>
    /// <example>Host=localhost;Database=meridian;Username=meridian;Password=secret</example>
    public string? ConnectionString { get; set; }
}
