namespace Meridian.Contracts.SecurityMaster;

/// <summary>
/// The canonical source of instrument reference data.
/// <para>
/// Every piece of market data, every ledger entry, every risk calculation, and every
/// options Greek computation flows through this service to resolve the stable
/// <see cref="InstrumentId"/> from whatever external symbol a data provider sends.
/// </para>
/// <para>
/// Rules:
/// <list type="bullet">
///   <item>No other code constructs account references or positions from raw ticker strings.</item>
///   <item>Registration is idempotent — registering the same instrument twice returns the existing ID.</item>
///   <item>Resolution returns null for unknown instruments; callers must handle the unknown-instrument case explicitly.</item>
/// </list>
/// </para>
/// </summary>
public interface ISecurityMasterService
{
    /// <summary>
    /// Register a new instrument and return its stable <see cref="InstrumentId"/>.
    /// Idempotent: if any supplied <see cref="ExternalId"/> matches an existing instrument,
    /// the existing <see cref="InstrumentId"/> is returned without creating a duplicate.
    /// </summary>
    ValueTask<InstrumentId> RegisterAsync(InstrumentRegistration registration, CancellationToken ct = default);

    /// <summary>
    /// Retrieve an instrument record by its stable ID.
    /// Returns null if the ID is not registered.
    /// </summary>
    ValueTask<InstrumentRecord?> GetByIdAsync(InstrumentId id, CancellationToken ct = default);

    /// <summary>
    /// Resolve an external symbol to an <see cref="InstrumentId"/>.
    /// </summary>
    /// <param name="externalSymbol">The symbol string, e.g. "SPX   240119C04500000".</param>
    /// <param name="idType">Which naming scheme the symbol belongs to.</param>
    /// <param name="source">The data provider or system supplying the symbol.</param>
    /// <param name="asOf">Point-in-time for historical resolution. Null = today.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The stable <see cref="InstrumentId"/>, or null if no match is registered.</returns>
    ValueTask<InstrumentId?> ResolveAsync(
        string externalSymbol,
        ExternalIdType idType,
        string source,
        DateOnly? asOf = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get the underlying instrument for a derivative (option or future).
    /// Returns null for non-derivatives (Equity, CashCurrency, Index, FixedIncome, FxPair).
    /// </summary>
    ValueTask<InstrumentRecord?> GetUnderlyingAsync(InstrumentId derivativeId, CancellationToken ct = default);

    /// <summary>
    /// Apply a corporate action to this instrument, updating symbol history, external IDs,
    /// and any affected instrument relationships.
    /// </summary>
    ValueTask ApplyCorporateActionAsync(CorporateAction action, CancellationToken ct = default);

    /// <summary>
    /// Search instruments by display symbol or issuer name (prefix / fuzzy match).
    /// Intended for UI search boxes — not for high-frequency resolution paths.
    /// </summary>
    IAsyncEnumerable<InstrumentRecord> SearchAsync(string query, CancellationToken ct = default);

    /// <summary>
    /// Get all external IDs currently registered for an instrument.
    /// </summary>
    ValueTask<IReadOnlyList<ExternalId>> GetExternalIdsAsync(InstrumentId id, CancellationToken ct = default);

    /// <summary>
    /// Get the full symbol history for an instrument (all tickers it has traded under).
    /// </summary>
    ValueTask<IReadOnlyList<SymbolEntry>> GetSymbolHistoryAsync(InstrumentId id, CancellationToken ct = default);
}
