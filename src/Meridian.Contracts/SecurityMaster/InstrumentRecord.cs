namespace Meridian.Contracts.SecurityMaster;

/// <summary>
/// The full record for an instrument in the Security Master.
/// Shared fields are on this record; kind-specific fields are nullable (use <see cref="Kind"/> as the discriminator).
/// </summary>
public sealed record InstrumentRecord
{
    /// <summary>Stable, permanent identifier — never changes.</summary>
    public InstrumentId Id { get; init; }

    /// <summary>Instrument taxonomy. Use as the discriminator when reading kind-specific fields.</summary>
    public InstrumentKind Kind { get; init; }

    /// <summary>Settlement currency (ISO 4217), e.g. "USD".</summary>
    public string Currency { get; init; } = "";

    /// <summary>Current human-readable display symbol. This is metadata — it changes on corporate actions.</summary>
    public string DisplaySymbol { get; init; } = "";

    /// <summary>
    /// Contract multiplier: number of underlying units per contract.
    /// 100 for standard equity/index options; 50 for SPX mini; 1 for equities; varies for futures.
    /// </summary>
    public decimal ContractMultiplier { get; init; } = 1m;

    /// <summary>How the contract settles. Relevant for derivatives.</summary>
    public SettlementType SettlementType { get; init; } = SettlementType.PhysicalDelivery;

    /// <summary>American or European exercise. Relevant for options (<see cref="InstrumentKind.EquityOption"/>, etc.).</summary>
    public ExerciseStyle? ExerciseStyle { get; init; }

    /// <summary>Call or Put. Relevant for options.</summary>
    public OptionSide? OptionSide { get; init; }

    /// <summary>Option strike price. Relevant for options.</summary>
    public decimal? Strike { get; init; }

    /// <summary>Option or futures expiry date. Relevant for all derivatives.</summary>
    public DateOnly? Expiry { get; init; }

    /// <summary>
    /// Underlying instrument. Relevant for all derivatives.
    /// EquityOption → Equity; IndexOption → Index; Future → Index or Equity; FutureOption → Future.
    /// </summary>
    public InstrumentId? UnderlyingId { get; init; }

    /// <summary>ISO 10383 primary exchange MIC (e.g. "XNAS", "XCBF"). Relevant for Equity, Future.</summary>
    public string? PrimaryExchangeMic { get; init; }

    /// <summary>Issuer or company name. Relevant for Equity, FixedIncome.</summary>
    public string? IssuerName { get; init; }

    /// <summary>Minimum price fluctuation. Relevant for futures.</summary>
    public decimal? TickSize { get; init; }

    /// <summary>Dollar value of one tick move. Relevant for futures.</summary>
    public decimal? TickValue { get; init; }

    // ── Fixed income specific ─────────────────────────────────────────────
    public decimal? CouponRate { get; init; }
    public DateOnly? MaturityDate { get; init; }
    public decimal? FaceValue { get; init; }
    public string? DayCountConvention { get; init; }

    // ── FX pair specific ─────────────────────────────────────────────────
    /// <summary>Base currency for an FX pair. Relevant for FxPair (e.g. "EUR" in EUR/USD).</summary>
    public string? BaseCurrency { get; init; }
    /// <summary>Quote currency for an FX pair. Relevant for FxPair (e.g. "USD" in EUR/USD).</summary>
    public string? QuoteCurrency { get; init; }
    /// <summary>Forward settlement date. Null = spot (T+2). Relevant for FxPair.</summary>
    public DateOnly? FxSettlementDate { get; init; }

    // ── Lifecycle ─────────────────────────────────────────────────────────
    public bool IsActive { get; init; } = true;
    public DateTimeOffset RegisteredAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Input to <see cref="ISecurityMasterService.RegisterAsync"/>.
/// Supply at minimum: Kind, DisplaySymbol, Currency, and at least one ExternalId.
/// </summary>
public sealed record InstrumentRegistration
{
    public InstrumentKind Kind { get; init; }
    public string DisplaySymbol { get; init; } = "";
    public string Currency { get; init; } = "";
    public IReadOnlyList<ExternalId> ExternalIds { get; init; } = [];

    // Kind-specific — supply only the fields relevant to the Kind
    public decimal ContractMultiplier { get; init; } = 1m;
    public SettlementType SettlementType { get; init; } = SettlementType.PhysicalDelivery;
    public ExerciseStyle? ExerciseStyle { get; init; }
    public OptionSide? OptionSide { get; init; }
    public decimal? Strike { get; init; }
    public DateOnly? Expiry { get; init; }
    public InstrumentId? UnderlyingId { get; init; }
    public string? PrimaryExchangeMic { get; init; }
    public string? IssuerName { get; init; }
    public decimal? TickSize { get; init; }
    public decimal? TickValue { get; init; }
    public decimal? CouponRate { get; init; }
    public DateOnly? MaturityDate { get; init; }
    public decimal? FaceValue { get; init; }
    public string? BaseCurrency { get; init; }
    public string? QuoteCurrency { get; init; }
    public DateOnly? FxSettlementDate { get; init; }
}
