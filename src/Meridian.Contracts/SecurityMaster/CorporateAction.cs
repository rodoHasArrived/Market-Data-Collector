namespace Meridian.Contracts.SecurityMaster;

/// <summary>
/// Categories of corporate events that affect instrument records, positions, and cost basis.
/// </summary>
public enum CorporateActionKind
{
    /// <summary>Forward or reverse split (e.g. 2-for-1).</summary>
    StockSplit = 1,
    /// <summary>Target acquired; positions convert to surviving entity.</summary>
    Merger = 2,
    /// <summary>Display symbol changed (no economic impact).</summary>
    SymbolChange = 3,
    /// <summary>Portion of business spun off as a separate instrument.</summary>
    SpinOff = 4,
    /// <summary>Security removed from exchange listing.</summary>
    Delisting = 5,
    /// <summary>Option strike and/or multiplier adjusted following underlying corporate action.</summary>
    OptionAdjustment = 6,
    /// <summary>Cash dividend paid to long holders.</summary>
    CashDividend = 7,
    /// <summary>Stock dividend or bonus shares issued.</summary>
    StockDividend = 8,
}

/// <summary>
/// A corporate action record. Applied via <see cref="ISecurityMasterService.ApplyCorporateActionAsync"/>
/// to update symbol history, instrument relationships, and (where applicable) create adjustment records.
/// </summary>
public sealed record CorporateAction
{
    public InstrumentId InstrumentId { get; init; }
    public CorporateActionKind Kind { get; init; }

    /// <summary>Ex-date: the first date on which shares trade without the entitlement.</summary>
    public DateOnly ExDate { get; init; }

    /// <summary>Date announced publicly. Used for audit ordering.</summary>
    public DateOnly AnnounceDate { get; init; }

    public DateOnly? RecordDate { get; init; }
    public DateOnly? PayDate { get; init; }

    /// <summary>Split ratio (e.g. 2.0 for 2-for-1). Relevant for StockSplit, StockDividend.</summary>
    public decimal? Ratio { get; init; }

    /// <summary>Cash amount per share/contract. Relevant for CashDividend.</summary>
    public decimal? CashAmount { get; init; }

    /// <summary>New display symbol. Relevant for SymbolChange, SpinOff.</summary>
    public string? NewSymbol { get; init; }

    /// <summary>Surviving or new instrument. Relevant for Merger, SpinOff.</summary>
    public InstrumentId? RelatedInstrumentId { get; init; }

    /// <summary>Adjusted option strike. Relevant for OptionAdjustment.</summary>
    public decimal? NewStrike { get; init; }

    /// <summary>Adjusted contract multiplier. Relevant for OptionAdjustment.</summary>
    public decimal? NewMultiplier { get; init; }

    /// <summary>Data source that reported this action.</summary>
    public string Source { get; init; } = "";
}
