namespace MarketDataCollector.Contracts.Domain.Enums;

/// <summary>
/// Provider-agnostic canonical trade condition codes.
/// Maps raw provider-specific condition codes (CTA plan, SEC numeric, IB text)
/// to a unified enumeration for cross-provider comparison.
/// </summary>
public enum CanonicalTradeCondition
{
    /// <summary>Regular trade.</summary>
    Regular = 0,

    /// <summary>Form T extended hours trade (pre-market or after-hours).</summary>
    FormT_ExtendedHours = 1,

    /// <summary>Odd lot trade (less than 100 shares).</summary>
    OddLot = 2,

    /// <summary>Average price trade.</summary>
    AveragePrice = 3,

    /// <summary>Intermarket sweep order.</summary>
    Intermarket_Sweep = 4,

    /// <summary>Opening print.</summary>
    OpeningPrint = 5,

    /// <summary>Closing print.</summary>
    ClosingPrint = 6,

    /// <summary>Derivatively priced trade.</summary>
    DerivativelyPriced = 7,

    /// <summary>Cross trade.</summary>
    CrossTrade = 8,

    /// <summary>Stock option trade.</summary>
    StockOption = 9,

    /// <summary>Trading halted.</summary>
    Halted = 10,

    /// <summary>Corrected consolidated trade.</summary>
    CorrectedConsolidated = 11,

    /// <summary>Seller-initiated trade.</summary>
    SellerInitiated = 12,

    /// <summary>Seller down exempt.</summary>
    SellerDownExempt = 13,

    /// <summary>Prior reference price trade.</summary>
    PriorReferencePrice = 14,

    /// <summary>Contingent trade.</summary>
    Contingent = 15,

    /// <summary>Qualified contingent trade.</summary>
    QualifiedContingent = 16,

    /// <summary>Unknown or unmapped condition code.</summary>
    Unknown = 255
}
