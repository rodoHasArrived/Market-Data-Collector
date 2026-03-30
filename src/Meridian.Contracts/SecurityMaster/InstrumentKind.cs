namespace Meridian.Contracts.SecurityMaster;

/// <summary>
/// Taxonomy of all tradeable and reference instrument types.
/// Used as the discriminator on <see cref="InstrumentRecord"/> and to drive margin calculations,
/// ledger account resolution, and options Greeks computation.
/// </summary>
public enum InstrumentKind
{
    /// <summary>Physical or digital currency held as cash (USD, EUR, BTC).</summary>
    CashCurrency = 1,

    /// <summary>Common or preferred stock, ETF, or ADR listed on an exchange.</summary>
    Equity = 2,

    /// <summary>Listed option on an individual equity or ETF.</summary>
    EquityOption = 3,

    /// <summary>Market index used as a reference or option underlying (S&amp;P 500, Russell 2000).</summary>
    Index = 4,

    /// <summary>Cash-settled option on a market index (SPX, NDX, RUT).</summary>
    IndexOption = 5,

    /// <summary>Exchange-traded futures contract.</summary>
    Future = 6,

    /// <summary>Option on a futures contract.</summary>
    FutureOption = 7,

    /// <summary>Bond, note, bill, or structured credit instrument.</summary>
    FixedIncome = 8,

    /// <summary>Foreign exchange spot or forward pair.</summary>
    FxPair = 9,
}

/// <summary>Whether an option can be exercised before expiry.</summary>
public enum ExerciseStyle
{
    /// <summary>Can only be exercised at expiry (most index options: SPX, NDX).</summary>
    European = 1,

    /// <summary>Can be exercised at any time before expiry (most equity options).</summary>
    American = 2,

    /// <summary>Can be exercised on specific dates before expiry.</summary>
    Bermudan = 3,
}

/// <summary>How the contract settles at expiry or exercise.</summary>
public enum SettlementType
{
    /// <summary>Settled for cash — no physical delivery of the underlying.</summary>
    CashSettled = 1,

    /// <summary>Settled by delivering or receiving the underlying asset.</summary>
    PhysicalDelivery = 2,
}

/// <summary>Call or put option side.</summary>
public enum OptionSide
{
    Call = 1,
    Put = 2,
}
