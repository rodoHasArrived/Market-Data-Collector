namespace Meridian.Contracts.SecurityMaster;

/// <summary>
/// Identifies what external naming scheme an identifier belongs to.
/// External IDs are time-valid — they can change on corporate action, relist, or data vendor update.
/// The <see cref="InstrumentId"/> never changes; these are aliases.
/// </summary>
public enum ExternalIdType
{
    /// <summary>ISIN (ISO 6166) — e.g. "US0231351067".</summary>
    Isin = 1,
    /// <summary>CUSIP (North American) — e.g. "023135106".</summary>
    Cusip = 2,
    /// <summary>SEDOL (London Stock Exchange) — e.g. "B7TL820".</summary>
    Sedol = 3,
    /// <summary>FIGI (Financial Instrument Global Identifier) — e.g. "BBG000B9XRY4".</summary>
    Figi = 4,
    /// <summary>OCC option symbol — e.g. "SPX   240119C04500000".</summary>
    OccSymbol = 5,
    /// <summary>Bloomberg ticker string — e.g. "SPX 1/19/24 C4500 Index".</summary>
    BloombergTicker = 6,
    /// <summary>Interactive Brokers contract ID (numeric string) — e.g. "416904".</summary>
    IbConId = 7,
    /// <summary>Polygon.io ticker — e.g. "O:SPX240119C04500000".</summary>
    PolygonTicker = 8,
    /// <summary>Alpaca Markets symbol.</summary>
    AlpacaSymbol = 9,
    /// <summary>Exchange-local ticker (e.g. NYSE, NASDAQ native symbol).</summary>
    ExchangeTicker = 10,
}

/// <summary>
/// An external identifier cross-referencing an instrument to a specific data provider or depository.
/// Time-valid: <see cref="ValidTo"/> is null while the identifier is current.
/// </summary>
public sealed record ExternalId
{
    /// <summary>The instrument this identifier belongs to.</summary>
    public InstrumentId InstrumentId { get; init; }

    /// <summary>The naming scheme this identifier belongs to.</summary>
    public ExternalIdType IdType { get; init; }

    /// <summary>The identifier value, e.g. "US0231351067" for an ISIN.</summary>
    public string IdValue { get; init; } = "";

    /// <summary>Date from which this identifier is valid (inclusive).</summary>
    public DateOnly ValidFrom { get; init; }

    /// <summary>Date until which this identifier is valid (exclusive). Null means currently active.</summary>
    public DateOnly? ValidTo { get; init; }

    /// <summary>Data provider or depository that assigned this identifier.</summary>
    public string Source { get; init; } = "";

    /// <summary>True if this identifier is currently active (ValidTo is null or in the future).</summary>
    public bool IsActive(DateOnly asOf) =>
        ValidFrom <= asOf && (ValidTo is null || ValidTo > asOf);
}

/// <summary>
/// Historical record of a display ticker or symbol for an instrument on a specific exchange.
/// Tickers change frequently (corporate actions, exchange moves); the <see cref="InstrumentId"/> never does.
/// </summary>
public sealed record SymbolEntry
{
    public InstrumentId InstrumentId { get; init; }
    public string Symbol { get; init; } = "";
    /// <summary>ISO 10383 MIC exchange code (e.g. "XNAS", "XCBF").</summary>
    public string ExchangeMic { get; init; } = "";
    public DateOnly ValidFrom { get; init; }
    public DateOnly? ValidTo { get; init; }
    public string Source { get; init; } = "";
}
