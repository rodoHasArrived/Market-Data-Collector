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
