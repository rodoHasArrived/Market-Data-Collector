/// Direct-lending domain types and pure event-sourcing logic.
/// Models loan lifecycle events from commitment through closure
/// following the architecture described in docs/plans/ledger.
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
/// Fixed after origination except via an explicit TermsAmended event.
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
    /// Covenant details serialized as JSON (optional).
    CovenantsJson: string option
}

/// Snapshot of loan aggregate state rebuilt by replaying events.
[<CLIMutable>]
type LoanState = {
    /// Loan header (immutable identification).
    Header: LoanHeader
    /// Current terms (may change via TermsAmended).
    Terms: DirectLendingTerms
    /// Current lifecycle status.
    Status: LoanStatus
    /// Total amount drawn and outstanding (principal outstanding).
    OutstandingPrincipal: decimal
    /// Cumulative interest accrued but not yet paid.
    AccruedInterestUnpaid: decimal
    /// Cumulative commitment fees accrued but not yet paid.
    AccruedCommitmentFeeUnpaid: decimal
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

// ── Aggregate: pure state-transition logic ────────────────────────────────────

/// <summary>
/// Pure functions that apply events to loan state.
/// This is the aggregate's "evolve" function — no I/O, no side effects.
/// </summary>
module LoanAggregate =

    /// Result of handling a command: either a list of events or a domain error.
    type CommandResult = Result<LoanEvent list, string>

    /// Initial (empty) state before any events have been applied.
    /// Used internally; real state is always rebuilt from a LoanCreated event.
    let private initialState : LoanState option = None

    /// Apply a single event to the current state, returning the new state.
    [<CompiledName("Evolve")>]
    let evolve (state: LoanState option) (event: LoanEvent) : LoanState =
        let bumpVersion s = { s with Version = s.Version + 1L }
        match state, event with
        | None, LoanEvent.LoanCreated(header, terms) ->
            { Header = header
              Terms = terms
              Status = LoanStatus.Pending
              OutstandingPrincipal = 0m
              AccruedInterestUnpaid = 0m
              AccruedCommitmentFeeUnpaid = 0m
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
                Ok [ LoanEvent.LoanCreated(header, terms) ]

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
