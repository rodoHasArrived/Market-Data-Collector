/// Direct-lending domain types and pure event-sourcing logic.
/// Models loan lifecycle events from commitment through closure,
/// including loan restructuring, purchase discounts/premiums, and
/// configurable day count methodologies.
/// Architecture follows docs/plans/ledger.
module Meridian.FSharp.Domain.Lending

open System

// ── Supporting types ──────────────────────────────────────────────────────────

/// ISO 4217 currency representation.
[<RequireQualifiedAccess>]
type Currency =
    | USD
    | EUR
    | GBP
    | JPY
    | CHF
    | CAD
    | AUD
    | Other of code: string

    override this.ToString() =
        match this with
        | USD -> "USD"
        | EUR -> "EUR"
        | GBP -> "GBP"
        | JPY -> "JPY"
        | CHF -> "CHF"
        | CAD -> "CAD"
        | AUD -> "AUD"
        | Other code -> code

    static member Parse(code: string) =
        match code.Trim().ToUpperInvariant() with
        | "USD" -> USD
        | "EUR" -> EUR
        | "GBP" -> GBP
        | "JPY" -> JPY
        | "CHF" -> CHF
        | "CAD" -> CAD
        | "AUD" -> AUD
        | other -> Other other

/// Amortization schedule type for a direct lending loan.
[<RequireQualifiedAccess>]
type AmortizationType =
    /// No principal payments until maturity.
    | BulletMaturity
    /// Equal principal repayments each period.
    | StraightLine
    /// Equal total payments (blended principal + interest) each period.
    | Annuity
    /// Custom schedule negotiated with the borrower.
    | Custom of description: string

/// Day count convention used to calculate interest accrual factors.
/// The convention determines how the number of days and the day-year
/// denominator are counted for an accrual period.
[<RequireQualifiedAccess>]
type DayCountConvention =
    /// Actual days elapsed / 360. Common for US leveraged loans and floating-rate notes.
    | Actual360
    /// Actual days elapsed / 365 (fixed). Common for GBP instruments and some US money market products.
    | Actual365Fixed
    /// Simplified 30-day month / 360-day year (ISDA/SIA convention). Common for fixed-rate US bonds.
    | Thirty360
    /// Actual days in period / actual days in the year (ISDA convention). Common for government bonds.
    | ActualActualISDA

/// Pure day count calculation functions.
module DayCount =

    /// Returns true if <paramref name="year"/> is a Gregorian leap year.
    let private isLeapYear (year: int) =
        (year % 4 = 0 && year % 100 <> 0) || (year % 400 = 0)

    /// Calculates the 30/360 (ISDA / US SIA) day count numerator.
    /// Reference: ISDA 2006 Definitions §4.16(f).
    let private days30_360 (startDate: DateOnly) (endDate: DateOnly) : int =
        let d1 = if startDate.Day = 31 then 30 else startDate.Day
        let d2 =
            if endDate.Day = 31 && (startDate.Day = 30 || startDate.Day = 31)
            then 30
            else endDate.Day
        360 * (endDate.Year - startDate.Year)
        + 30 * (endDate.Month - startDate.Month)
        + (d2 - d1)

    /// Calculates the Actual/Actual (ISDA) accrual factor by splitting the
    /// period at calendar-year boundaries and dividing each portion by the
    /// actual number of days in that year (366 for leap years, 365 otherwise).
    let private accrualActualActualISDA (startDate: DateOnly) (endDate: DateOnly) : decimal =
        if startDate >= endDate then 0m
        else
            let rec loop (current: DateOnly) (acc: decimal) =
                if current >= endDate then acc
                else
                    let yearEnd = DateOnly(current.Year + 1, 1, 1)
                    let periodEnd = if yearEnd < endDate then yearEnd else endDate
                    let days = (periodEnd.ToDateTime(TimeOnly.MinValue) - current.ToDateTime(TimeOnly.MinValue)).Days
                    let daysInYear = if isLeapYear current.Year then 366m else 365m
                    loop periodEnd (acc + decimal days / daysInYear)
            loop startDate 0m

    /// Computes the accrual factor (year fraction) for the period
    /// [<paramref name="startDate"/>, <paramref name="endDate"/>)
    /// using the specified <paramref name="convention"/>.
    /// Returns 0 when <paramref name="endDate"/> ≤ <paramref name="startDate"/>.
    [<CompiledName("AccrualFactor")>]
    let accrualFactor (convention: DayCountConvention) (startDate: DateOnly) (endDate: DateOnly) : decimal =
        if endDate <= startDate then 0m
        else
            match convention with
            | DayCountConvention.Actual360 ->
                let days = (endDate.ToDateTime(TimeOnly.MinValue) - startDate.ToDateTime(TimeOnly.MinValue)).Days
                decimal days / 360m
            | DayCountConvention.Actual365Fixed ->
                let days = (endDate.ToDateTime(TimeOnly.MinValue) - startDate.ToDateTime(TimeOnly.MinValue)).Days
                decimal days / 365m
            | DayCountConvention.Thirty360 ->
                decimal (days30_360 startDate endDate) / 360m
            | DayCountConvention.ActualActualISDA ->
                accrualActualActualISDA startDate endDate

/// Lifecycle status of a loan aggregate.
[<RequireQualifiedAccess>]
type LoanStatus =
    /// Loan has been recorded but commitment has not been made.
    | Pending
    /// Credit line has been formally committed.
    | Committed
    /// Funds have been drawn; loan is active and accruing.
    | Active
    /// Borrower has missed one or more payments; formal default has not yet been declared.
    | NonPerforming
    /// Borrower has formally defaulted; recovery or enforcement proceedings may be initiated.
    | Default
    /// Loan is in active workout / recovery proceedings following default or non-performance.
    | Workout
    /// Loan fully repaid and closed.
    | Closed

/// Classifies the nature of a loan restructuring event.
/// Used to document the type of distress workout or modification.
[<RequireQualifiedAccess>]
type RestructuringType =
    /// Maturity date was extended to provide the borrower more time.
    | MaturityExtension
    /// Interest rate (or spread) was reduced as a concession.
    | RateReduction
    /// A portion of the outstanding principal was forgiven (write-down).
    | PrincipalHaircut
    /// Some or all of the outstanding debt was converted into equity.
    | DebtForEquitySwap
    /// Loan was converted to PIK (Payment-In-Kind); interest accretes to principal.
    | PIKConversion
    /// Comprehensive restructuring combining multiple modifications.
    | Full

// ── Credit rating ─────────────────────────────────────────────────────────────

/// Simplified credit rating scale aligned with major agency conventions.
/// Covers investment grade (BBB and above) through distressed (CCC/CC/D) and unrated.
[<RequireQualifiedAccess>]
type CreditRating =
    /// Highest quality, minimal credit risk.
    | AAA
    /// Very high quality, very low credit risk.
    | AA
    /// High quality, low credit risk.
    | A
    /// Medium quality, moderate credit risk (lowest investment grade).
    | BBB
    /// Speculative grade, significant credit risk.
    | BB
    /// Speculative grade, high credit risk.
    | B
    /// Substantial credit risk; vulnerable to non-payment.
    | CCC
    /// Very high credit risk; default is probable.
    | CC
    /// In default or has breached an imputed promise.
    | D
    /// No credit rating has been assigned.
    | Unrated

    /// Returns true when the rating is investment grade (BBB and above).
    member this.IsInvestmentGrade =
        match this with
        | AAA | AA | A | BBB -> true
        | _ -> false

    override this.ToString() =
        match this with
        | AAA -> "AAA" | AA -> "AA" | A -> "A" | BBB -> "BBB"
        | BB -> "BB" | B -> "B" | CCC -> "CCC" | CC -> "CC"
        | D -> "D" | Unrated -> "NR"

    static member Parse(s: string) =
        match s.Trim().ToUpperInvariant() with
        | "AAA" -> CreditRating.AAA
        | "AA"  -> CreditRating.AA
        | "A"   -> CreditRating.A
        | "BBB" -> CreditRating.BBB
        | "BB"  -> CreditRating.BB
        | "B"   -> CreditRating.B
        | "CCC" -> CreditRating.CCC
        | "CC"  -> CreditRating.CC
        | "D"   -> CreditRating.D
        | "NR" | "UNRATED" -> CreditRating.Unrated
        | other -> failwithf "Unknown credit rating: '%s'" other

// ── Covenant types ─────────────────────────────────────────────────────────────

/// Classifies the financial metric that a covenant tests against a threshold.
[<RequireQualifiedAccess>]
type CovenantType =
    /// Debt service coverage ratio: cash flow / debt service ≥ threshold.
    | DebtServiceCoverageRatio
    /// Interest coverage ratio: EBITDA / interest expense ≥ threshold.
    | InterestCoverageRatio
    /// Leverage ratio: total debt / EBITDA ≤ threshold.
    | LeverageRatio
    /// Loan-to-value: outstanding loan / collateral value ≤ threshold.
    | LoanToValue
    /// Current ratio: current assets / current liabilities ≥ threshold.
    | CurrentRatio
    /// Tangible net worth must remain above the threshold.
    | TangibleNetWorth
    /// Custom covenant with a user-defined description.
    | Custom of description: string

/// How frequently the covenant must be tested and reported.
[<RequireQualifiedAccess>]
type CovenantFrequency =
    | Monthly
    | Quarterly
    | SemiAnnual
    | Annual

/// Compliance status of a covenant as of the most recent test.
[<RequireQualifiedAccess>]
type CovenantStatus =
    /// Covenant is active and borrower is in compliance.
    | Active
    /// Borrower has failed the most recent covenant test.
    | Breached
    /// Lender has formally waived the covenant breach for a specified period.
    | Waived

/// A financial covenant that the borrower must satisfy at each test date.
[<CLIMutable>]
type Covenant = {
    /// Unique identifier for this covenant.
    CovenantId: Guid
    /// Financial metric this covenant tests.
    CovenantType: CovenantType
    /// Human-readable description of the covenant obligation.
    Description: string
    /// Threshold value the borrower must meet (interpretation depends on CovenantType).
    ThresholdValue: decimal
    /// How frequently the covenant must be tested.
    Frequency: CovenantFrequency
    /// Current compliance status.
    Status: CovenantStatus
    /// Date of the most recent covenant test, if any.
    LastTestDate: DateOnly option
}

// ── Collateral types ──────────────────────────────────────────────────────────

/// Classifies the type of asset pledged as security for a loan.
[<RequireQualifiedAccess>]
type CollateralType =
    /// Commercial or residential real estate.
    | RealEstate
    /// Machinery, vehicles, or other physical equipment.
    | Equipment
    /// Raw materials, work-in-progress, or finished goods.
    | Inventory
    /// Trade receivables or other accounts receivable.
    | AccountsReceivable
    /// Publicly or privately held securities or financial instruments.
    | FinancialInstrument
    /// Any other collateral type not covered above.
    | Other of description: string

/// A unit of collateral pledged as security against a direct lending loan.
[<CLIMutable>]
type Collateral = {
    /// Unique identifier for this collateral position.
    CollateralId: Guid
    /// Type of the collateral asset.
    CollateralType: CollateralType
    /// Human-readable description of the specific collateral item.
    Description: string
    /// Appraised / estimated market value at the most recent valuation.
    EstimatedValue: decimal
    /// Currency in which the estimated value is expressed.
    Currency: Currency
    /// Date of the most recent appraisal or valuation.
    AppraisalDate: DateOnly
}

// ── Core records ─────────────────────────────────────────────────────────────

/// Immutable header identifying a loan instrument.
[<CLIMutable>]
type LoanHeader = {
    /// Unique identifier for the loan / security.
    SecurityId: Guid
    /// Human-readable name of the borrower or facility.
    Name: string
    /// Base currency of the facility.
    BaseCurrency: Currency
    /// Date the loan was originated.
    EffectiveDate: DateOnly
}

/// Economic terms of a direct lending loan.
/// Fixed after origination except via an explicit TermsAmended or LoanRestructured event.
[<CLIMutable>]
type DirectLendingTerms = {
    /// Date the loan was formally originated.
    OriginationDate: DateOnly
    /// Scheduled maturity date.
    MaturityDate: DateOnly
    /// Maximum amount the borrower may draw.
    CommitmentAmount: decimal
    /// Annual commitment fee rate on the undrawn balance (e.g. 0.005 for 50 bps).
    CommitmentFeeRate: decimal option
    /// Fixed interest rate (None for floating-rate loans).
    InterestRate: decimal option
    /// Reference index name for floating-rate loans (e.g. "SOFR", "EURIBOR").
    InterestIndex: string option
    /// Spread above the reference index in basis points.
    SpreadBps: decimal option
    /// Number of months between scheduled payment dates.
    PaymentFrequencyMonths: int
    /// Amortization type.
    AmortizationType: AmortizationType
    /// Day count convention used to compute interest accrual factors.
    DayCountConvention: DayCountConvention
    /// Acquisition price as a fraction of face value (e.g. 0.95 = 5% discount, 1.02 = 2% premium).
    /// None means the loan was acquired at par (1.0). Drives initial UnamortizedDiscount/Premium in state.
    PurchasePrice: decimal option
    /// Covenant details serialized as JSON (optional).
    CovenantsJson: string option
}

/// Snapshot of loan aggregate state rebuilt by replaying events.
[<CLIMutable>]
type LoanState = {
    /// Loan header (immutable identification).
    Header: LoanHeader
    /// Current terms (may change via TermsAmended or LoanRestructured).
    Terms: DirectLendingTerms
    /// Current lifecycle status.
    Status: LoanStatus
    /// Total amount drawn and outstanding (principal outstanding).
    OutstandingPrincipal: decimal
    /// Cumulative interest accrued but not yet paid.
    AccruedInterestUnpaid: decimal
    /// Cumulative commitment fees accrued but not yet paid.
    AccruedCommitmentFeeUnpaid: decimal
    /// Remaining unamortized purchase discount (income to be recognised over loan life).
    /// Positive means the loan was acquired below par; decreases as DiscountAmortized events are applied.
    UnamortizedDiscount: decimal
    /// Remaining unamortized purchase premium (expense to be amortised over loan life).
    /// Positive means the loan was acquired above par; decreases as PremiumAmortized events are applied.
    UnamortizedPremium: decimal
    /// Collateral items currently pledged against the loan.
    Collateral: Collateral list
    /// Structured financial covenants currently attached to the loan.
    ActiveCovenants: Covenant list
    /// Most recently assigned credit rating and the entity that assigned it.
    CreditRating: CreditRating option
    /// Monotonically increasing version counter (one per event applied).
    Version: int64
}

// ── Event catalog ─────────────────────────────────────────────────────────────

/// All domain events that can occur on a direct lending loan.
/// Events are append-only and immutable — they describe what happened.
[<RequireQualifiedAccess>]
type LoanEvent =
    /// A new loan was created with the given header and terms.
    | LoanCreated of header: LoanHeader * terms: DirectLendingTerms
    /// The credit line was formally committed.
    | LoanCommitted of amount: decimal * currency: Currency
    /// The borrower drew funds from the facility.
    | DrawdownExecuted of amount: decimal * currency: Currency * date: DateOnly
    /// A periodic interest accrual was posted (income recognized, no cash yet).
    | InterestAccrued of amount: decimal * date: DateOnly
    /// An actual interest payment was received from the borrower.
    | InterestPaid of amount: decimal * date: DateOnly
    /// A periodic commitment fee on the undrawn balance was accrued.
    | CommitmentFeeAccrued of amount: decimal * date: DateOnly
    /// A commitment fee payment was received.
    | CommitmentFeePaid of amount: decimal * date: DateOnly
    /// The floating-rate index or spread was reset.
    | InterestRateReset of newIndex: string * newSpreadBps: decimal
    /// A principal repayment (scheduled or prepayment) was received.
    | PrincipalRepaid of amount: decimal * date: DateOnly
    /// A one-time fee was charged (origination, late, amendment, etc.).
    | FeeCharged of feeType: string * amount: decimal * date: DateOnly
    /// Loan terms were amended (e.g. maturity extension, rate change).
    | TermsAmended of newTerms: DirectLendingTerms
    /// The loan was fully paid off and closed.
    | LoanClosed of date: DateOnly
    // ── Restructuring ──────────────────────────────────────────────────────
    /// The loan was formally restructured. Captures the restructuring type, the
    /// revised terms, and the effective date of the modification.
    | LoanRestructured of restructuringType: RestructuringType * newTerms: DirectLendingTerms * date: DateOnly
    /// A portion of outstanding principal was forgiven (debt write-down).
    /// Reduces outstanding principal without a cash inflow to the borrower.
    | PrincipalForgiven of amount: decimal * date: DateOnly
    /// Accrued interest was capitalised into outstanding principal (PIK).
    /// Reduces AccruedInterestUnpaid and increases OutstandingPrincipal.
    | PikInterestCapitalized of amount: decimal * date: DateOnly
    // ── Discount / Premium amortization ───────────────────────────────────
    /// A portion of the purchase discount was amortised into income.
    /// Reduces UnamortizedDiscount.
    | DiscountAmortized of amount: decimal * date: DateOnly
    /// A portion of the purchase premium was amortised as expense.
    /// Reduces UnamortizedPremium.
    | PremiumAmortized of amount: decimal * date: DateOnly
    // ── Collateral ──────────────────────────────────────────────────────────
    /// A new collateral item was pledged against the loan.
    | CollateralAdded of collateral: Collateral * date: DateOnly
    /// A collateral item was released; the lien over that asset was discharged.
    | CollateralReleased of collateralId: Guid * date: DateOnly
    /// An existing collateral item was revalued to a new estimated market value.
    | CollateralRevalued of collateralId: Guid * newValue: decimal * date: DateOnly
    // ── Default & recovery ──────────────────────────────────────────────────
    /// The loan was classified as non-performing (missed payment(s); no formal default yet).
    | LoanMarkedNonPerforming of date: DateOnly
    /// The borrower formally defaulted; loan status moves to Default.
    | LoanDefaulted of date: DateOnly * reason: string
    /// A previously declared default was cured; loan returns to Active status.
    | DefaultCured of date: DateOnly
    /// The loan was placed into active workout / recovery proceedings.
    | LoanPlacedInWorkout of date: DateOnly
    /// The remaining outstanding balance was written off as a credit loss.
    | LoanWrittenOff of amount: decimal * date: DateOnly
    // ── Credit rating ────────────────────────────────────────────────────────
    /// A credit rating was assigned or updated by a named rater (e.g. internal credit team or rating agency).
    | LoanRiskRated of rating: CreditRating * rater: string * date: DateOnly
    // ── Covenants ────────────────────────────────────────────────────────────
    /// A new financial covenant was added to the loan.
    | CovenantAdded of covenant: Covenant * date: DateOnly
    /// A covenant test was failed; records the actual value observed.
    | CovenantBreached of covenantId: Guid * actualValue: decimal * testDate: DateOnly
    /// The lender waived a covenant breach, optionally until a specified date.
    | CovenantWaived of covenantId: Guid * waivedUntil: DateOnly option * date: DateOnly
    /// A covenant's threshold was amended (typically during a restructuring or waiver negotiation).
    | CovenantAmended of covenantId: Guid * newThreshold: decimal * date: DateOnly

// ── Command catalog ────────────────────────────────────────────────────────────

/// Commands that drive state changes on a loan aggregate.
/// A command is validated against current state; on success it produces events.
[<RequireQualifiedAccess>]
type LoanCommand =
    /// Record a new loan in the system.
    | CreateLoan of header: LoanHeader * terms: DirectLendingTerms
    /// Formally commit the credit line.
    | CommitLoan of amount: decimal * currency: Currency
    /// Record a drawdown.
    | RecordDrawdown of amount: decimal * currency: Currency * date: DateOnly
    /// Post a periodic interest accrual.
    | AccrueInterest of amount: decimal * date: DateOnly
    /// Record an interest payment receipt.
    | RecordInterestPayment of amount: decimal * date: DateOnly
    /// Post a periodic commitment-fee accrual.
    | AccrueCommitmentFee of amount: decimal * date: DateOnly
    /// Record a commitment-fee payment receipt.
    | RecordCommitmentFeePayment of amount: decimal * date: DateOnly
    /// Reset the floating-rate index.
    | ResetInterestRate of newIndex: string * newSpreadBps: decimal
    /// Record a principal repayment.
    | RepayPrincipal of amount: decimal * date: DateOnly
    /// Charge a one-time fee.
    | ChargeFee of feeType: string * amount: decimal * date: DateOnly
    /// Amend loan terms.
    | AmendTerms of newTerms: DirectLendingTerms
    /// Close the loan.
    | CloseLoan of date: DateOnly
    /// Restructure the loan (workout / modification).
    | RestructureLoan of restructuringType: RestructuringType * newTerms: DirectLendingTerms * date: DateOnly
    /// Forgive a portion of outstanding principal.
    | ForgivePrincipal of amount: decimal * date: DateOnly
    /// Capitalise accrued interest into outstanding principal (PIK conversion).
    | CapitalizePikInterest of amount: decimal * date: DateOnly
    /// Amortise a portion of the purchase discount into income.
    | AmortizeDiscount of amount: decimal * date: DateOnly
    /// Amortise a portion of the purchase premium as an expense.
    | AmortizePremium of amount: decimal * date: DateOnly
    /// Pledge a new collateral item against the loan.
    | AddCollateral of collateral: Collateral * date: DateOnly
    /// Release (discharge the lien over) a pledged collateral item.
    | ReleaseCollateral of collateralId: Guid * date: DateOnly
    /// Update the estimated market value of an existing collateral item.
    | RevalueCollateral of collateralId: Guid * newValue: decimal * date: DateOnly
    /// Mark the loan as non-performing (missed payments, no formal default yet).
    | MarkNonPerforming of date: DateOnly
    /// Declare a formal default on the loan.
    | DeclareDefault of date: DateOnly * reason: string
    /// Cure a previously declared default.
    | CureDefault of date: DateOnly
    /// Place the loan into active workout / recovery proceedings.
    | PlaceInWorkout of date: DateOnly
    /// Write off the outstanding balance (or a portion) as a credit loss.
    | WriteOffLoan of amount: decimal * date: DateOnly
    /// Assign or update the credit rating on the loan.
    | AssignCreditRating of rating: CreditRating * rater: string * date: DateOnly
    /// Add a structured financial covenant to the loan.
    | AddCovenant of covenant: Covenant * date: DateOnly
    /// Report a covenant breach with the observed value at the test date.
    | ReportCovenantBreach of covenantId: Guid * actualValue: decimal * testDate: DateOnly
    /// Grant a formal waiver for a breached covenant, optionally with an expiry date.
    | GrantCovenantWaiver of covenantId: Guid * waivedUntil: DateOnly option * date: DateOnly
    /// Amend the threshold on an existing covenant.
    | AmendCovenant of covenantId: Guid * newThreshold: decimal * date: DateOnly

// ── Aggregate: pure state-transition logic ────────────────────────────────────

/// <summary>
/// Pure functions that apply events to loan state.
/// This is the aggregate's "evolve" function — no I/O, no side effects.
/// </summary>
module LoanAggregate =

    /// Result of handling a command: either a list of events or a domain error.
    type CommandResult = Result<LoanEvent list, string>

    /// Compute the initial unamortized discount and premium from the purchase price
    /// recorded in loan terms, relative to the commitment amount at origination.
    let private initialDiscountPremium (terms: DirectLendingTerms) : decimal * decimal =
        match terms.PurchasePrice with
        | None -> (0m, 0m)
        | Some price ->
            let face = terms.CommitmentAmount
            let discount = max 0m ((1m - price) * face)
            let premium  = max 0m ((price - 1m) * face)
            (discount, premium)

    /// Apply a single event to the current state, returning the new state.
    [<CompiledName("Evolve")>]
    let evolve (state: LoanState option) (event: LoanEvent) : LoanState =
        let bumpVersion s = { s with Version = s.Version + 1L }
        match state, event with
        | None, LoanEvent.LoanCreated(header, terms) ->
            let (discount, premium) = initialDiscountPremium terms
            { Header = header
              Terms = terms
              Status = LoanStatus.Pending
              OutstandingPrincipal = 0m
              AccruedInterestUnpaid = 0m
              AccruedCommitmentFeeUnpaid = 0m
              UnamortizedDiscount = discount
              UnamortizedPremium = premium
              Collateral = []
              ActiveCovenants = []
              CreditRating = None
              Version = 1L }
        | None, _ ->
            failwith "Cannot apply event to an uninitialized loan (LoanCreated must be first)."
        | Some s, LoanEvent.LoanCreated _ ->
            failwith "LoanCreated cannot be applied to an already-initialized loan."
        | Some s, LoanEvent.LoanCommitted _ ->
            bumpVersion { s with Status = LoanStatus.Committed }
        | Some s, LoanEvent.DrawdownExecuted(amount, _, _) ->
            bumpVersion { s with
                            Status = LoanStatus.Active
                            OutstandingPrincipal = s.OutstandingPrincipal + amount }
        | Some s, LoanEvent.InterestAccrued(amount, _) ->
            bumpVersion { s with AccruedInterestUnpaid = s.AccruedInterestUnpaid + amount }
        | Some s, LoanEvent.InterestPaid(amount, _) ->
            bumpVersion { s with AccruedInterestUnpaid = max 0m (s.AccruedInterestUnpaid - amount) }
        | Some s, LoanEvent.CommitmentFeeAccrued(amount, _) ->
            bumpVersion { s with AccruedCommitmentFeeUnpaid = s.AccruedCommitmentFeeUnpaid + amount }
        | Some s, LoanEvent.CommitmentFeePaid(amount, _) ->
            bumpVersion { s with AccruedCommitmentFeeUnpaid = max 0m (s.AccruedCommitmentFeeUnpaid - amount) }
        | Some s, LoanEvent.InterestRateReset(newIndex, newSpread) ->
            let updatedTerms = { s.Terms with InterestIndex = Some newIndex; SpreadBps = Some newSpread }
            bumpVersion { s with Terms = updatedTerms }
        | Some s, LoanEvent.PrincipalRepaid(amount, _) ->
            bumpVersion { s with OutstandingPrincipal = max 0m (s.OutstandingPrincipal - amount) }
        | Some s, LoanEvent.FeeCharged _ ->
            bumpVersion s
        | Some s, LoanEvent.TermsAmended newTerms ->
            bumpVersion { s with Terms = newTerms }
        | Some s, LoanEvent.LoanClosed _ ->
            bumpVersion { s with Status = LoanStatus.Closed }
        // ── Restructuring ─────────────────────────────────────────────────
        | Some s, LoanEvent.LoanRestructured(_, newTerms, _) ->
            bumpVersion { s with Terms = newTerms }
        | Some s, LoanEvent.PrincipalForgiven(amount, _) ->
            bumpVersion { s with OutstandingPrincipal = max 0m (s.OutstandingPrincipal - amount) }
        | Some s, LoanEvent.PikInterestCapitalized(amount, _) ->
            let capitalized = min amount s.AccruedInterestUnpaid
            bumpVersion { s with
                            AccruedInterestUnpaid  = max 0m (s.AccruedInterestUnpaid  - capitalized)
                            OutstandingPrincipal   = s.OutstandingPrincipal + capitalized }
        // ── Discount / Premium ─────────────────────────────────────────────
        | Some s, LoanEvent.DiscountAmortized(amount, _) ->
            bumpVersion { s with UnamortizedDiscount = max 0m (s.UnamortizedDiscount - amount) }
        | Some s, LoanEvent.PremiumAmortized(amount, _) ->
            bumpVersion { s with UnamortizedPremium = max 0m (s.UnamortizedPremium - amount) }
        // ── Collateral ─────────────────────────────────────────────────────
        | Some s, LoanEvent.CollateralAdded(collateral, _) ->
            bumpVersion { s with Collateral = collateral :: s.Collateral }
        | Some s, LoanEvent.CollateralReleased(collateralId, _) ->
            bumpVersion { s with Collateral = s.Collateral |> List.filter (fun c -> c.CollateralId <> collateralId) }
        | Some s, LoanEvent.CollateralRevalued(collateralId, newValue, _) ->
            let updated = s.Collateral |> List.map (fun c -> if c.CollateralId = collateralId then { c with EstimatedValue = newValue } else c)
            bumpVersion { s with Collateral = updated }
        // ── Default & recovery ─────────────────────────────────────────────
        | Some s, LoanEvent.LoanMarkedNonPerforming _ ->
            bumpVersion { s with Status = LoanStatus.NonPerforming }
        | Some s, LoanEvent.LoanDefaulted _ ->
            bumpVersion { s with Status = LoanStatus.Default }
        | Some s, LoanEvent.DefaultCured _ ->
            bumpVersion { s with Status = LoanStatus.Active }
        | Some s, LoanEvent.LoanPlacedInWorkout _ ->
            bumpVersion { s with Status = LoanStatus.Workout }
        | Some s, LoanEvent.LoanWrittenOff(amount, _) ->
            bumpVersion { s with OutstandingPrincipal = max 0m (s.OutstandingPrincipal - amount) }
        // ── Credit rating ──────────────────────────────────────────────────
        | Some s, LoanEvent.LoanRiskRated(rating, _, _) ->
            bumpVersion { s with CreditRating = Some rating }
        // ── Covenants ──────────────────────────────────────────────────────
        | Some s, LoanEvent.CovenantAdded(covenant, _) ->
            bumpVersion { s with ActiveCovenants = covenant :: s.ActiveCovenants }
        | Some s, LoanEvent.CovenantBreached(covenantId, _, testDate) ->
            let updated = s.ActiveCovenants |> List.map (fun c ->
                if c.CovenantId = covenantId
                then { c with Status = CovenantStatus.Breached; LastTestDate = Some testDate }
                else c)
            bumpVersion { s with ActiveCovenants = updated }
        | Some s, LoanEvent.CovenantWaived(covenantId, _, _) ->
            let updated = s.ActiveCovenants |> List.map (fun c ->
                if c.CovenantId = covenantId then { c with Status = CovenantStatus.Waived } else c)
            bumpVersion { s with ActiveCovenants = updated }
        | Some s, LoanEvent.CovenantAmended(covenantId, newThreshold, _) ->
            let updated = s.ActiveCovenants |> List.map (fun c ->
                if c.CovenantId = covenantId
                then { c with ThresholdValue = newThreshold; Status = CovenantStatus.Active }
                else c)
            bumpVersion { s with ActiveCovenants = updated }

    /// Rebuild aggregate state from a sequence of events.
    [<CompiledName("Rebuild")>]
    let rebuild (events: LoanEvent seq) : LoanState option =
        events |> Seq.fold (fun state event -> Some (evolve state event)) None

    // ── Command handlers ──────────────────────────────────────────────────────

    /// Handle a CreateLoan command.
    [<CompiledName("HandleCreateLoan")>]
    let handleCreate (state: LoanState option) (header: LoanHeader) (terms: DirectLendingTerms) : CommandResult =
        match state with
        | Some _ -> Error "Loan already exists."
        | None ->
            if terms.CommitmentAmount <= 0m then
                Error "CommitmentAmount must be positive."
            elif terms.MaturityDate <= terms.OriginationDate then
                Error "MaturityDate must be after OriginationDate."
            else
                match terms.PurchasePrice with
                | Some p when p <= 0m -> Error "PurchasePrice must be positive."
                | _ -> Ok [ LoanEvent.LoanCreated(header, terms) ]

    /// Handle a CommitLoan command.
    [<CompiledName("HandleCommitLoan")>]
    let handleCommit (state: LoanState option) (amount: decimal) (currency: Currency) : CommandResult =
        match state with
        | None -> Error "Loan does not exist."
        | Some s when s.Status <> LoanStatus.Pending ->
            Error $"Cannot commit a loan in status '{s.Status}'."
        | Some s when amount <= 0m ->
            Error "Commitment amount must be positive."
        | Some _ ->
            Ok [ LoanEvent.LoanCommitted(amount, currency) ]

    /// Handle a RecordDrawdown command.
    [<CompiledName("HandleRecordDrawdown")>]
    let handleDrawdown (state: LoanState option) (amount: decimal) (currency: Currency) (date: DateOnly) : CommandResult =
        match state with
        | None -> Error "Loan does not exist."
        | Some s when s.Status = LoanStatus.Closed ->
            Error "Cannot record a drawdown on a closed loan."
        | Some s when s.Status = LoanStatus.Pending ->
            Error "Cannot record a drawdown on a pending (uncommitted) loan."
        | Some s when amount <= 0m ->
            Error "Drawdown amount must be positive."
        | Some s when s.OutstandingPrincipal + amount > s.Terms.CommitmentAmount ->
            Error $"Drawdown of {amount} would exceed the commitment amount of {s.Terms.CommitmentAmount}."
        | Some _ ->
            Ok [ LoanEvent.DrawdownExecuted(amount, currency, date) ]

    /// Handle a RepayPrincipal command.
    [<CompiledName("HandleRepayPrincipal")>]
    let handleRepay (state: LoanState option) (amount: decimal) (date: DateOnly) : CommandResult =
        match state with
        | None -> Error "Loan does not exist."
        | Some s when s.Status = LoanStatus.Closed ->
            Error "Cannot repay a closed loan."
        | Some s when amount <= 0m ->
            Error "Repayment amount must be positive."
        | Some s when amount > s.OutstandingPrincipal ->
            Error $"Repayment of {amount} exceeds outstanding principal of {s.OutstandingPrincipal}."
        | Some _ ->
            Ok [ LoanEvent.PrincipalRepaid(amount, date) ]

    /// Handle a CloseLoan command.
    [<CompiledName("HandleCloseLoan")>]
    let handleClose (state: LoanState option) (date: DateOnly) : CommandResult =
        match state with
        | None -> Error "Loan does not exist."
        | Some s when s.Status = LoanStatus.Closed ->
            Error "Loan is already closed."
        | Some s when s.OutstandingPrincipal > 0m ->
            Error $"Cannot close a loan with outstanding principal of {s.OutstandingPrincipal}."
        | Some _ ->
            Ok [ LoanEvent.LoanClosed date ]

    /// Handle a RestructureLoan command.
    [<CompiledName("HandleRestructureLoan")>]
    let handleRestructure (state: LoanState option) (restructuringType: RestructuringType) (newTerms: DirectLendingTerms) (date: DateOnly) : CommandResult =
        match state with
        | None -> Error "Loan does not exist."
        | Some s when s.Status = LoanStatus.Closed ->
            Error "Cannot restructure a closed loan."
        | Some s when newTerms.CommitmentAmount <= 0m ->
            Error "Restructured CommitmentAmount must be positive."
        | Some s when newTerms.MaturityDate < date ->
            Error "Restructured maturity date cannot be before the restructuring effective date."
        | Some _ ->
            Ok [ LoanEvent.LoanRestructured(restructuringType, newTerms, date) ]

    /// Handle a ForgivePrincipal command.
    [<CompiledName("HandleForgivePrincipal")>]
    let handleForgivePrincipal (state: LoanState option) (amount: decimal) (date: DateOnly) : CommandResult =
        match state with
        | None -> Error "Loan does not exist."
        | Some s when s.Status = LoanStatus.Closed -> Error "Loan is already closed."
        | Some s when amount <= 0m -> Error "Forgiveness amount must be positive."
        | Some s when amount > s.OutstandingPrincipal ->
            Error $"Forgiveness of {amount} exceeds outstanding principal of {s.OutstandingPrincipal}."
        | Some _ ->
            Ok [ LoanEvent.PrincipalForgiven(amount, date) ]

    /// Dispatch a command to the appropriate handler.
    [<CompiledName("Handle")>]
    let handle (state: LoanState option) (command: LoanCommand) : CommandResult =
        match command with
        | LoanCommand.CreateLoan(header, terms) ->
            handleCreate state header terms
        | LoanCommand.CommitLoan(amount, currency) ->
            handleCommit state amount currency
        | LoanCommand.RecordDrawdown(amount, currency, date) ->
            handleDrawdown state amount currency date
        | LoanCommand.AccrueInterest(amount, date) ->
            match state with
            | None -> Error "Loan does not exist."
            | Some s when s.Status = LoanStatus.Closed -> Error "Loan is closed."
            | Some s when amount < 0m -> Error "Accrual amount cannot be negative."
            | Some _ -> Ok [ LoanEvent.InterestAccrued(amount, date) ]
        | LoanCommand.RecordInterestPayment(amount, date) ->
            match state with
            | None -> Error "Loan does not exist."
            | Some s when amount <= 0m -> Error "Payment amount must be positive."
            | Some _ -> Ok [ LoanEvent.InterestPaid(amount, date) ]
        | LoanCommand.AccrueCommitmentFee(amount, date) ->
            match state with
            | None -> Error "Loan does not exist."
            | Some s when s.Status = LoanStatus.Closed -> Error "Loan is closed."
            | Some s when amount < 0m -> Error "Accrual amount cannot be negative."
            | Some _ -> Ok [ LoanEvent.CommitmentFeeAccrued(amount, date) ]
        | LoanCommand.RecordCommitmentFeePayment(amount, date) ->
            match state with
            | None -> Error "Loan does not exist."
            | Some s when amount <= 0m -> Error "Payment amount must be positive."
            | Some _ -> Ok [ LoanEvent.CommitmentFeePaid(amount, date) ]
        | LoanCommand.ResetInterestRate(newIndex, newSpread) ->
            match state with
            | None -> Error "Loan does not exist."
            | Some s when s.Status = LoanStatus.Closed -> Error "Loan is closed."
            | Some s when newSpread < 0m -> Error "Spread cannot be negative."
            | Some _ -> Ok [ LoanEvent.InterestRateReset(newIndex, newSpread) ]
        | LoanCommand.RepayPrincipal(amount, date) ->
            handleRepay state amount date
        | LoanCommand.ChargeFee(feeType, amount, date) ->
            match state with
            | None -> Error "Loan does not exist."
            | Some s when amount <= 0m -> Error "Fee amount must be positive."
            | Some _ -> Ok [ LoanEvent.FeeCharged(feeType, amount, date) ]
        | LoanCommand.AmendTerms newTerms ->
            match state with
            | None -> Error "Loan does not exist."
            | Some s when s.Status = LoanStatus.Closed -> Error "Cannot amend terms of a closed loan."
            | Some _ -> Ok [ LoanEvent.TermsAmended newTerms ]
        | LoanCommand.CloseLoan date ->
            handleClose state date
        | LoanCommand.RestructureLoan(restructuringType, newTerms, date) ->
            handleRestructure state restructuringType newTerms date
        | LoanCommand.ForgivePrincipal(amount, date) ->
            handleForgivePrincipal state amount date
        | LoanCommand.CapitalizePikInterest(amount, date) ->
            match state with
            | None -> Error "Loan does not exist."
            | Some s when s.Status = LoanStatus.Closed -> Error "Loan is closed."
            | Some s when amount <= 0m -> Error "PIK capitalisation amount must be positive."
            | Some s when amount > s.AccruedInterestUnpaid ->
                Error $"PIK capitalisation of {amount} exceeds accrued interest of {s.AccruedInterestUnpaid}."
            | Some _ -> Ok [ LoanEvent.PikInterestCapitalized(amount, date) ]
        | LoanCommand.AmortizeDiscount(amount, date) ->
            match state with
            | None -> Error "Loan does not exist."
            | Some s when amount <= 0m -> Error "Amortisation amount must be positive."
            | Some s when amount > s.UnamortizedDiscount ->
                Error $"Discount amortisation of {amount} exceeds unamortized discount of {s.UnamortizedDiscount}."
            | Some _ -> Ok [ LoanEvent.DiscountAmortized(amount, date) ]
        | LoanCommand.AmortizePremium(amount, date) ->
            match state with
            | None -> Error "Loan does not exist."
            | Some s when amount <= 0m -> Error "Amortisation amount must be positive."
            | Some s when amount > s.UnamortizedPremium ->
                Error $"Premium amortisation of {amount} exceeds unamortized premium of {s.UnamortizedPremium}."
            | Some _ -> Ok [ LoanEvent.PremiumAmortized(amount, date) ]
        | LoanCommand.AddCollateral(collateral, date) ->
            match state with
            | None -> Error "Loan does not exist."
            | Some s when s.Status = LoanStatus.Closed -> Error "Cannot add collateral to a closed loan."
            | Some s when collateral.EstimatedValue <= 0m -> Error "Collateral estimated value must be positive."
            | Some _ -> Ok [ LoanEvent.CollateralAdded(collateral, date) ]
        | LoanCommand.ReleaseCollateral(collateralId, date) ->
            match state with
            | None -> Error "Loan does not exist."
            | Some s when not (s.Collateral |> List.exists (fun c -> c.CollateralId = collateralId)) ->
                Error "Collateral item not found."
            | Some _ -> Ok [ LoanEvent.CollateralReleased(collateralId, date) ]
        | LoanCommand.RevalueCollateral(collateralId, newValue, date) ->
            match state with
            | None -> Error "Loan does not exist."
            | Some s when newValue <= 0m -> Error "Revalued collateral value must be positive."
            | Some s when not (s.Collateral |> List.exists (fun c -> c.CollateralId = collateralId)) ->
                Error "Collateral item not found."
            | Some _ -> Ok [ LoanEvent.CollateralRevalued(collateralId, newValue, date) ]
        | LoanCommand.MarkNonPerforming date ->
            match state with
            | None -> Error "Loan does not exist."
            | Some s when s.Status = LoanStatus.Closed -> Error "Loan is already closed."
            | Some s when s.Status = LoanStatus.Default -> Error "Loan is already in default."
            | Some s when s.Status = LoanStatus.NonPerforming -> Error "Loan is already non-performing."
            | Some _ -> Ok [ LoanEvent.LoanMarkedNonPerforming date ]
        | LoanCommand.DeclareDefault(date, reason) ->
            match state with
            | None -> Error "Loan does not exist."
            | Some s when s.Status = LoanStatus.Closed -> Error "Loan is already closed."
            | Some s when s.Status = LoanStatus.Default -> Error "Loan is already in default."
            | Some s when String.IsNullOrWhiteSpace(reason) -> Error "Default reason must be provided."
            | Some _ -> Ok [ LoanEvent.LoanDefaulted(date, reason) ]
        | LoanCommand.CureDefault date ->
            match state with
            | None -> Error "Loan does not exist."
            | Some s when s.Status <> LoanStatus.Default -> Error "Loan is not in default."
            | Some _ -> Ok [ LoanEvent.DefaultCured date ]
        | LoanCommand.PlaceInWorkout date ->
            match state with
            | None -> Error "Loan does not exist."
            | Some s when s.Status <> LoanStatus.Default && s.Status <> LoanStatus.NonPerforming ->
                Error "Loan must be in Default or NonPerforming status to be placed in Workout."
            | Some _ -> Ok [ LoanEvent.LoanPlacedInWorkout date ]
        | LoanCommand.WriteOffLoan(amount, date) ->
            match state with
            | None -> Error "Loan does not exist."
            | Some s when amount <= 0m -> Error "Write-off amount must be positive."
            | Some s when amount > s.OutstandingPrincipal ->
                Error $"Write-off of {amount} exceeds outstanding principal of {s.OutstandingPrincipal}."
            | Some _ -> Ok [ LoanEvent.LoanWrittenOff(amount, date) ]
        | LoanCommand.AssignCreditRating(rating, rater, date) ->
            match state with
            | None -> Error "Loan does not exist."
            | Some s when s.Status = LoanStatus.Closed -> Error "Cannot rate a closed loan."
            | Some s when String.IsNullOrWhiteSpace(rater) -> Error "Rater must be provided."
            | Some _ -> Ok [ LoanEvent.LoanRiskRated(rating, rater, date) ]
        | LoanCommand.AddCovenant(covenant, date) ->
            match state with
            | None -> Error "Loan does not exist."
            | Some s when s.Status = LoanStatus.Closed -> Error "Cannot add a covenant to a closed loan."
            | Some s when covenant.ThresholdValue <= 0m -> Error "Covenant threshold must be positive."
            | Some s when s.ActiveCovenants |> List.exists (fun c -> c.CovenantId = covenant.CovenantId) ->
                Error "A covenant with this ID already exists."
            | Some _ -> Ok [ LoanEvent.CovenantAdded(covenant, date) ]
        | LoanCommand.ReportCovenantBreach(covenantId, actualValue, testDate) ->
            match state with
            | None -> Error "Loan does not exist."
            | Some s when not (s.ActiveCovenants |> List.exists (fun c -> c.CovenantId = covenantId)) ->
                Error "Covenant not found."
            | Some _ -> Ok [ LoanEvent.CovenantBreached(covenantId, actualValue, testDate) ]
        | LoanCommand.GrantCovenantWaiver(covenantId, waivedUntil, date) ->
            match state with
            | None -> Error "Loan does not exist."
            | Some s when not (s.ActiveCovenants |> List.exists (fun c -> c.CovenantId = covenantId)) ->
                Error "Covenant not found."
            | Some _ -> Ok [ LoanEvent.CovenantWaived(covenantId, waivedUntil, date) ]
        | LoanCommand.AmendCovenant(covenantId, newThreshold, date) ->
            match state with
            | None -> Error "Loan does not exist."
            | Some s when newThreshold <= 0m -> Error "Covenant threshold must be positive."
            | Some s when not (s.ActiveCovenants |> List.exists (fun c -> c.CovenantId = covenantId)) ->
                Error "Covenant not found."
            | Some _ -> Ok [ LoanEvent.CovenantAmended(covenantId, newThreshold, date) ]

// ── Domain service helpers (pure, no I/O) ─────────────────────────────────────

/// Pure helper functions for computing loan-level metrics and status queries.
/// All functions are deterministic given a <see cref="LoanState"/> — no I/O or side effects.
module LoanService =

    /// Sums the estimated values of all collateral items currently pledged.
    [<CompiledName("TotalCollateralValue")>]
    let totalCollateralValue (state: LoanState) : decimal =
        state.Collateral |> List.sumBy (fun c -> c.EstimatedValue)

    /// Loan-to-value ratio: OutstandingPrincipal / TotalCollateralValue.
    /// Returns None when there is no pledged collateral or total collateral value is zero.
    [<CompiledName("LoanToValue")>]
    let loanToValue (state: LoanState) : decimal option =
        let cv = totalCollateralValue state
        if cv = 0m then None
        else Some (state.OutstandingPrincipal / cv)

    /// Collateral coverage ratio: TotalCollateralValue / OutstandingPrincipal.
    /// Returns None when outstanding principal is zero (loan fully repaid).
    [<CompiledName("CollateralCoverageRatio")>]
    let collateralCoverageRatio (state: LoanState) : decimal option =
        if state.OutstandingPrincipal = 0m then None
        else Some (totalCollateralValue state / state.OutstandingPrincipal)

    /// Undrawn balance: CommitmentAmount − OutstandingPrincipal (floored at zero).
    [<CompiledName("UndrawnBalance")>]
    let undrawnBalance (state: LoanState) : decimal =
        max 0m (state.Terms.CommitmentAmount - state.OutstandingPrincipal)

    /// Net carrying value of the loan on the books:
    /// OutstandingPrincipal + UnamortizedPremium − UnamortizedDiscount.
    [<CompiledName("CarryingValue")>]
    let carryingValue (state: LoanState) : decimal =
        state.OutstandingPrincipal + state.UnamortizedPremium - state.UnamortizedDiscount

    /// Returns true if the loan is in a distressed status (NonPerforming, Default, or Workout).
    [<CompiledName("IsDistressed")>]
    let isDistressed (state: LoanState) : bool =
        match state.Status with
        | LoanStatus.NonPerforming | LoanStatus.Default | LoanStatus.Workout -> true
        | _ -> false

    /// Returns true if the loan has funds drawn and has not been closed.
    [<CompiledName("IsEconomicallyActive")>]
    let isEconomicallyActive (state: LoanState) : bool =
        state.Status <> LoanStatus.Closed && state.OutstandingPrincipal > 0m

// ── Accrual period ────────────────────────────────────────────────────────────

/// A single interest or fee accrual period with pre-computed day count metrics.
[<CLIMutable>]
type AccrualPeriod = {
    /// Inclusive start of the accrual period.
    PeriodStart: DateOnly
    /// Exclusive end of the accrual period.
    PeriodEnd: DateOnly
    /// Actual number of calendar days between PeriodStart and PeriodEnd.
    DaysInPeriod: int
    /// Year fraction computed using the loan's day count convention.
    AccrualFactor: decimal
}

// ── Interest calculator ───────────────────────────────────────────────────────

/// Pure functions for estimating interest and fee accruals on a loan aggregate.
/// All computations are deterministic and produce estimates — actual cash amounts
/// are recorded via <see cref="LoanEvent.InterestAccrued"/> and
/// <see cref="LoanEvent.CommitmentFeeAccrued"/> events.
module InterestCalculator =

    /// Constructs an <see cref="AccrualPeriod"/> for the given date range,
    /// using the supplied day count convention.
    [<CompiledName("MakeAccrualPeriod")>]
    let makeAccrualPeriod (convention: DayCountConvention) (periodStart: DateOnly) (periodEnd: DateOnly) : AccrualPeriod =
        let days =
            (periodEnd.ToDateTime(TimeOnly.MinValue) - periodStart.ToDateTime(TimeOnly.MinValue)).Days
        { PeriodStart  = periodStart
          PeriodEnd    = periodEnd
          DaysInPeriod = days
          AccrualFactor = DayCount.accrualFactor convention periodStart periodEnd }

    /// Estimates interest accrued on the current outstanding principal for [periodStart, periodEnd).
    /// Uses the fixed <c>InterestRate</c> when present; falls back to <c>SpreadBps / 10 000</c>
    /// for floating-rate loans. Returns 0 when no rate is configured or principal is zero.
    [<CompiledName("EstimateInterest")>]
    let estimateInterest (state: LoanState) (periodStart: DateOnly) (periodEnd: DateOnly) : decimal =
        if state.OutstandingPrincipal = 0m then 0m
        else
            let factor = DayCount.accrualFactor state.Terms.DayCountConvention periodStart periodEnd
            let annualRate =
                match state.Terms.InterestRate with
                | Some r -> r
                | None ->
                    match state.Terms.SpreadBps with
                    | Some bps -> bps / 10_000m
                    | None -> 0m
            state.OutstandingPrincipal * annualRate * factor

    /// Estimates the commitment fee on the undrawn balance for [periodStart, periodEnd).
    /// Returns 0 when no <c>CommitmentFeeRate</c> is set or the facility is fully drawn.
    [<CompiledName("EstimateCommitmentFee")>]
    let estimateCommitmentFee (state: LoanState) (periodStart: DateOnly) (periodEnd: DateOnly) : decimal =
        match state.Terms.CommitmentFeeRate with
        | None -> 0m
        | Some rate ->
            let undrawn = max 0m (state.Terms.CommitmentAmount - state.OutstandingPrincipal)
            if undrawn = 0m then 0m
            else
                let factor = DayCount.accrualFactor state.Terms.DayCountConvention periodStart periodEnd
                undrawn * rate * factor

    /// Estimates total all-in yield on the outstanding principal for [periodStart, periodEnd)
    /// as interest accrual + prorated discount amortisation − prorated premium amortisation.
    /// Discount/premium are amortised straight-line over the remaining loan term.
    [<CompiledName("EstimateAllInYield")>]
    let estimateAllInYield (state: LoanState) (periodStart: DateOnly) (periodEnd: DateOnly) : decimal =
        let interest = estimateInterest state periodStart periodEnd
        let factor = DayCount.accrualFactor state.Terms.DayCountConvention periodStart periodEnd
        let totalTerm =
            DayCount.accrualFactor
                state.Terms.DayCountConvention
                state.Terms.OriginationDate
                state.Terms.MaturityDate
        let discountIncome =
            if totalTerm = 0m then 0m
            else state.UnamortizedDiscount * factor / totalTerm
        let premiumExpense =
            if totalTerm = 0m then 0m
            else state.UnamortizedPremium * factor / totalTerm
        interest + discountIncome - premiumExpense

// ── Payment schedule ──────────────────────────────────────────────────────────

/// A single entry in a forward-looking payment schedule.
[<CLIMutable>]
type ScheduledPayment = {
    /// Ordinal payment number, 1-based.
    PaymentNumber: int
    /// Expected cash payment due date.
    DueDate: DateOnly
    /// Scheduled principal repayment for this period.
    PrincipalDue: decimal
    /// Estimated interest for this period (based on scheduled outstanding balance).
    EstimatedInterest: decimal
    /// Total estimated cash due: PrincipalDue + EstimatedInterest.
    TotalDue: decimal
    /// Expected outstanding principal immediately after this payment.
    RemainingPrincipalAfter: decimal
}

/// Generates a forward-looking payment schedule for a direct lending loan.
/// Amounts are estimates; actual accruals depend on realised rates and drawdowns.
module PaymentSchedule =

    // Raise a decimal to a decimal power via double arithmetic.
    // Precision loss is acceptable here: the schedule is an estimate used for
    // planning purposes, not a legally binding cash flow.
    let private dpow (b: decimal) (e: decimal) : decimal =
        decimal (Math.Pow(float b, float e))

    /// Returns the number of minor-unit decimal places for a given currency.
    let private currencyDecimalPlaces (currency: Currency) : int =
        match currency with
        | Currency.JPY -> 0  // JPY has no minor units
        | _ -> 2

    /// Builds the sequence of due dates starting after <paramref name="fromDate"/>
    /// and up to (and including) the maturity date.
    let private dueDates (terms: DirectLendingTerms) (fromDate: DateOnly) : (int * DateOnly) list =
        Seq.initInfinite (fun i -> i + 1)
        |> Seq.map (fun n -> (n, terms.OriginationDate.AddMonths(n * terms.PaymentFrequencyMonths)))
        |> Seq.skipWhile (fun (_, d) -> d <= fromDate)
        |> Seq.takeWhile (fun (_, d) -> d <= terms.MaturityDate)
        |> Seq.toList

    /// Returns the annual interest rate from the loan's terms.
    /// Fixed rate takes priority; floating-rate falls back to spread / 10 000.
    /// Returns 0 when neither is configured.
    let private annualRate (terms: DirectLendingTerms) : decimal =
        match terms.InterestRate with
        | Some r -> r
        | None ->
            match terms.SpreadBps with
            | Some bps -> bps / 10_000m
            | None -> 0m

    /// Generates a payment schedule from <paramref name="fromDate"/> to loan maturity
    /// using the loan's amortisation type and day count convention.
    ///
    /// Returns an empty list when:
    /// - the loan is closed or <paramref name="fromDate"/> is at or past maturity;
    /// - no future payment dates exist; or
    /// - <c>AmortizationType.Custom</c> is used (schedule is externally negotiated).
    [<CompiledName("Generate")>]
    let generate (state: LoanState) (fromDate: DateOnly) : ScheduledPayment list =
        let terms = state.Terms
        let isCustom =
            match terms.AmortizationType with
            | AmortizationType.Custom _ -> true
            | _ -> false
        if state.Status = LoanStatus.Closed
           || fromDate >= terms.MaturityDate
           || isCustom then []
        else
            let periods = dueDates terms fromDate
            let n = periods.Length
            if n = 0 then []
            else
                let rate = annualRate terms
                let decimals = currencyDecimalPlaces state.Header.BaseCurrency
                let indexedPeriods = periods |> List.mapi (fun i x -> (i, x))
                let prevDate (i: int) =
                    if i = 0 then fromDate else snd periods.[i - 1]
                match terms.AmortizationType with
                | AmortizationType.BulletMaturity ->
                    let principal = state.OutstandingPrincipal
                    periods |> List.mapi (fun i (_, dueDate) ->
                        let pd = prevDate i
                        let isLast = i = n - 1
                        let principalDue = if isLast then principal else 0m
                        let factor = DayCount.accrualFactor terms.DayCountConvention pd dueDate
                        let interest = principal * rate * factor
                        let remaining = principal - principalDue
                        { PaymentNumber = i + 1; DueDate = dueDate
                          PrincipalDue = principalDue; EstimatedInterest = interest
                          TotalDue = principalDue + interest; RemainingPrincipalAfter = remaining })

                | AmortizationType.StraightLine ->
                    let principalPerPeriod = state.OutstandingPrincipal / decimal n
                    let (payments, _) =
                        indexedPeriods
                        |> List.mapFold (fun remaining (i, (_, dueDate)) ->
                            let pd = prevDate i
                            // Last payment clears residual to absorb accumulated rounding error
                            let principalDue =
                                if i = n - 1 then remaining
                                else Math.Round(principalPerPeriod, decimals)
                            let factor = DayCount.accrualFactor terms.DayCountConvention pd dueDate
                            let interest = remaining * rate * factor
                            let newRemaining = max 0m (remaining - principalDue)
                            let payment =
                                { PaymentNumber = i + 1; DueDate = dueDate
                                  PrincipalDue = principalDue; EstimatedInterest = interest
                                  TotalDue = principalDue + interest; RemainingPrincipalAfter = newRemaining }
                            (payment, newRemaining)
                        ) state.OutstandingPrincipal
                    payments

                | AmortizationType.Annuity ->
                    // PMT formula: P × r / (1 − (1 + r)^−n)
                    // Per-period rate is approximated as annual rate / (periods per year).
                    // Double-precision conversion is intentional: the schedule is an estimate.
                    let periodsPerYear = 12m / decimal terms.PaymentFrequencyMonths
                    let perPeriodRate = rate / periodsPerYear
                    let pv = state.OutstandingPrincipal
                    let nd = decimal n
                    let constantPayment =
                        if perPeriodRate = 0m then pv / nd
                        else pv * perPeriodRate / (1m - dpow (1m + perPeriodRate) (-nd))
                    let (payments, _) =
                        indexedPeriods
                        |> List.mapFold (fun remaining (i, (_, dueDate)) ->
                            let pd = prevDate i
                            let factor = DayCount.accrualFactor terms.DayCountConvention pd dueDate
                            let interest = remaining * rate * factor
                            let principalDue =
                                if i = n - 1 then remaining
                                else max 0m (min remaining (constantPayment - interest))
                            let newRemaining = max 0m (remaining - principalDue)
                            let payment =
                                { PaymentNumber = i + 1; DueDate = dueDate
                                  PrincipalDue = principalDue; EstimatedInterest = interest
                                  TotalDue = principalDue + interest; RemainingPrincipalAfter = newRemaining }
                            (payment, newRemaining)
                        ) state.OutstandingPrincipal
                    payments

                | AmortizationType.Custom _ -> []
