namespace MarketDataCollector.Contracts.Domain.Enums;

/// <summary>
/// Provider-agnostic canonical trade condition codes.
/// Maps raw provider-specific condition codes (CTA plan, SEC numeric, IB text)
/// to a unified set of canonical conditions for cross-provider comparison.
/// </summary>
public enum CanonicalTradeCondition
{
    /// <summary>
    /// Regular trade (normal market conditions).
    /// </summary>
    Regular = 0,

    /// <summary>
    /// Form T / extended hours trade (pre-market or after-hours).
    /// </summary>
    FormT_ExtendedHours = 1,

    /// <summary>
    /// Odd lot trade (fewer than 100 shares for equities).
    /// </summary>
    OddLot = 2,

    /// <summary>
    /// Average price trade.
    /// </summary>
    AveragePrice = 3,

    /// <summary>
    /// Intermarket sweep order (ISO).
    /// </summary>
    Intermarket_Sweep = 4,

    /// <summary>
    /// Opening print.
    /// </summary>
    OpeningPrint = 5,

    /// <summary>
    /// Closing print.
    /// </summary>
    ClosingPrint = 6,

    /// <summary>
    /// Derivatively priced trade.
    /// </summary>
    DerivativelyPriced = 7,

    /// <summary>
    /// Cross trade.
    /// </summary>
    CrossTrade = 8,

    /// <summary>
    /// Stock option trade.
    /// </summary>
    StockOption = 9,

    /// <summary>
    /// Trading halt indicator.
    /// </summary>
    Halted = 10,

    /// <summary>
    /// Corrected consolidated trade.
    /// </summary>
    CorrectedConsolidated = 11,

    /// <summary>
    /// Sold out of sequence (late report).
    /// </summary>
    SoldOutOfSequence = 12,

    /// <summary>
    /// Contingent trade.
    /// </summary>
    Contingent = 13,

    /// <summary>
    /// Acquisition trade.
    /// </summary>
    Acquisition = 14,

    /// <summary>
    /// Bunched trade.
    /// </summary>
    Bunched = 15,

    /// <summary>
    /// Cash settlement.
    /// </summary>
    Cash = 16,

    /// <summary>
    /// Next day settlement.
    /// </summary>
    NextDay = 17,

    /// <summary>
    /// Seller-initiated trade (definitive aggressor inference).
    /// </summary>
    SellerInitiated = 18,

    /// <summary>
    /// Prior reference price.
    /// </summary>
    PriorReferencePrice = 19,

    /// <summary>
    /// Unknown / unmapped condition code.
    /// </summary>
    Unknown = 255
}
