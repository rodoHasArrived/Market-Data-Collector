/// Unit tests for the direct-lending F# domain module.
module Meridian.FSharp.Tests.LendingTests

open System
open Xunit
open FsUnit.Xunit
open Meridian.FSharp.Domain.Lending

// ── Helpers ───────────────────────────────────────────────────────────────────

let private sampleHeader () : LoanHeader =
    { SecurityId = Guid.NewGuid()
      Name = "Acme Corp Term Loan A"
      BaseCurrency = Currency.USD
      EffectiveDate = DateOnly(2025, 1, 15) }

let private sampleTerms () : DirectLendingTerms =
    { OriginationDate = DateOnly(2025, 1, 15)
      MaturityDate = DateOnly(2028, 1, 15)
      CommitmentAmount = 10_000_000m
      CommitmentFeeRate = Some 0.005m
      InterestRate = None
      InterestIndex = Some "SOFR"
      SpreadBps = Some 350m
      PaymentFrequencyMonths = 3
      AmortizationType = AmortizationType.BulletMaturity
      CovenantsJson = None }

let private createLoan () =
    let header = sampleHeader ()
    let terms = sampleTerms ()
    let events = [ LoanEvent.LoanCreated(header, terms) ]
    LoanAggregate.rebuild events

// ── Currency tests ────────────────────────────────────────────────────────────

[<Fact>]
let ``Currency.ToString returns ISO code`` () =
    Currency.USD.ToString() |> should equal "USD"
    Currency.EUR.ToString() |> should equal "EUR"
    (Currency.Other "SGD").ToString() |> should equal "SGD"

[<Fact>]
let ``Currency.Parse round-trips known codes`` () =
    Currency.Parse "USD" |> should equal Currency.USD
    Currency.Parse "eur" |> should equal Currency.EUR
    Currency.Parse "gbp" |> should equal Currency.GBP

[<Fact>]
let ``Currency.Parse wraps unknown code in Other`` () =
    Currency.Parse "SGD" |> should equal (Currency.Other "SGD")

// ── LoanCreated ───────────────────────────────────────────────────────────────

[<Fact>]
let ``HandleCreate produces LoanCreated event for valid input`` () =
    let header = sampleHeader ()
    let terms = sampleTerms ()
    let result = LoanAggregate.handleCreate None header terms
    match result with
    | Ok events ->
        events |> should haveLength 1
        match events.[0] with
        | LoanEvent.LoanCreated(h, t) ->
            h.Name |> should equal header.Name
            t.CommitmentAmount |> should equal terms.CommitmentAmount
        | _ -> failwith "Expected LoanCreated"
    | Error msg -> failwith $"Unexpected error: {msg}"

[<Fact>]
let ``HandleCreate rejects negative commitment amount`` () =
    let header = sampleHeader ()
    let terms = { sampleTerms () with CommitmentAmount = -1m }
    let result = LoanAggregate.handleCreate None header terms
    match result with
    | Error msg -> msg |> should equal "CommitmentAmount must be positive."
    | Ok _ -> failwith "Expected error"

[<Fact>]
let ``HandleCreate rejects maturity before origination`` () =
    let header = sampleHeader ()
    let terms = { sampleTerms () with MaturityDate = DateOnly(2024, 1, 1) }
    let result = LoanAggregate.handleCreate None header terms
    match result with
    | Error msg -> msg |> should equal "MaturityDate must be after OriginationDate."
    | Ok _ -> failwith "Expected error"

[<Fact>]
let ``HandleCreate rejects duplicate create`` () =
    let state = createLoan ()
    let result = LoanAggregate.handleCreate state (sampleHeader ()) (sampleTerms ())
    match result with
    | Error msg -> msg |> should equal "Loan already exists."
    | Ok _ -> failwith "Expected error"

// ── Evolve / Rebuild ──────────────────────────────────────────────────────────

[<Fact>]
let ``Rebuild produces correct initial state from LoanCreated`` () =
    let state = createLoan ()
    state |> should not' (equal None)
    let s = state.Value
    s.Status |> should equal LoanStatus.Pending
    s.OutstandingPrincipal |> should equal 0m
    s.Version |> should equal 1L

[<Fact>]
let ``Evolve raises on LoanCreated applied to existing state`` () =
    let state = createLoan ()
    let applyAgain () =
        LoanAggregate.evolve state (LoanEvent.LoanCreated(sampleHeader (), sampleTerms ())) |> ignore
    (fun () -> applyAgain ()) |> should throw typeof<System.Exception>

[<Fact>]
let ``Evolve raises on non-create event applied to None`` () =
    let apply () =
        LoanAggregate.evolve None (LoanEvent.LoanCommitted(1_000_000m, Currency.USD)) |> ignore
    (fun () -> apply ()) |> should throw typeof<System.Exception>

// ── Commit ────────────────────────────────────────────────────────────────────

[<Fact>]
let ``HandleCommitLoan succeeds for Pending loan`` () =
    let state = createLoan ()
    let result = LoanAggregate.handleCommit state 5_000_000m Currency.USD
    match result with
    | Ok events ->
        events |> should haveLength 1
        match events.[0] with
        | LoanEvent.LoanCommitted(amount, currency) ->
            amount |> should equal 5_000_000m
            currency |> should equal Currency.USD
        | _ -> failwith "Expected LoanCommitted"
    | Error msg -> failwith $"Unexpected error: {msg}"

[<Fact>]
let ``HandleCommitLoan rejects non-Pending loan`` () =
    let state = createLoan ()
    let events = [ LoanEvent.LoanCommitted(10_000_000m, Currency.USD) ]
    let committed = events |> List.fold (fun s e -> Some (LoanAggregate.evolve s e)) state
    let result = LoanAggregate.handleCommit committed 5_000_000m Currency.USD
    match result with
    | Error _ -> ()
    | Ok _ -> failwith "Expected an error for non-Pending loan"

[<Fact>]
let ``Evolve LoanCommitted sets status to Committed`` () =
    let state = createLoan ()
    let newState = LoanAggregate.evolve state (LoanEvent.LoanCommitted(10_000_000m, Currency.USD))
    newState.Status |> should equal LoanStatus.Committed
    newState.Version |> should equal 2L

// ── Drawdown ──────────────────────────────────────────────────────────────────

[<Fact>]
let ``HandleRecordDrawdown succeeds for Committed loan within limit`` () =
    let state =
        [ LoanEvent.LoanCreated(sampleHeader (), sampleTerms ())
          LoanEvent.LoanCommitted(10_000_000m, Currency.USD) ]
        |> LoanAggregate.rebuild
    let result = LoanAggregate.handleDrawdown state 3_000_000m Currency.USD (DateOnly(2025, 2, 1))
    match result with
    | Ok _ -> ()
    | Error msg -> failwith $"Expected success but got: {msg}"

[<Fact>]
let ``HandleRecordDrawdown rejects amount exceeding commitment`` () =
    let state =
        [ LoanEvent.LoanCreated(sampleHeader (), sampleTerms ())
          LoanEvent.LoanCommitted(10_000_000m, Currency.USD) ]
        |> LoanAggregate.rebuild
    let result = LoanAggregate.handleDrawdown state 15_000_000m Currency.USD (DateOnly(2025, 2, 1))
    match result with
    | Error msg -> msg |> should haveSubstring "commitment amount"
    | Ok _ -> failwith "Expected error"

[<Fact>]
let ``Evolve DrawdownExecuted increases outstanding principal`` () =
    let state =
        [ LoanEvent.LoanCreated(sampleHeader (), sampleTerms ())
          LoanEvent.LoanCommitted(10_000_000m, Currency.USD)
          LoanEvent.DrawdownExecuted(4_000_000m, Currency.USD, DateOnly(2025, 2, 1)) ]
        |> LoanAggregate.rebuild
    state.Value.OutstandingPrincipal |> should equal 4_000_000m
    state.Value.Status |> should equal LoanStatus.Active

// ── Accruals ──────────────────────────────────────────────────────────────────

[<Fact>]
let ``Interest accrual and payment update accrued balance`` () =
    let state =
        [ LoanEvent.LoanCreated(sampleHeader (), sampleTerms ())
          LoanEvent.LoanCommitted(10_000_000m, Currency.USD)
          LoanEvent.DrawdownExecuted(4_000_000m, Currency.USD, DateOnly(2025, 2, 1))
          LoanEvent.InterestAccrued(10_000m, DateOnly(2025, 2, 28))
          LoanEvent.InterestAccrued(10_000m, DateOnly(2025, 3, 31)) ]
        |> LoanAggregate.rebuild
    state.Value.AccruedInterestUnpaid |> should equal 20_000m

    let afterPayment =
        LoanAggregate.evolve state (LoanEvent.InterestPaid(15_000m, DateOnly(2025, 4, 1)))
    afterPayment.AccruedInterestUnpaid |> should equal 5_000m

[<Fact>]
let ``Accrued interest cannot go below zero on overpayment`` () =
    let state =
        [ LoanEvent.LoanCreated(sampleHeader (), sampleTerms ())
          LoanEvent.LoanCommitted(10_000_000m, Currency.USD)
          LoanEvent.DrawdownExecuted(4_000_000m, Currency.USD, DateOnly(2025, 2, 1))
          LoanEvent.InterestAccrued(5_000m, DateOnly(2025, 2, 28)) ]
        |> LoanAggregate.rebuild
    let afterPayment =
        LoanAggregate.evolve state (LoanEvent.InterestPaid(10_000m, DateOnly(2025, 3, 1)))
    afterPayment.AccruedInterestUnpaid |> should equal 0m

// ── Interest rate reset ───────────────────────────────────────────────────────

[<Fact>]
let ``InterestRateReset updates terms index and spread`` () =
    let state =
        [ LoanEvent.LoanCreated(sampleHeader (), sampleTerms ())
          LoanEvent.LoanCommitted(10_000_000m, Currency.USD)
          LoanEvent.DrawdownExecuted(4_000_000m, Currency.USD, DateOnly(2025, 2, 1))
          LoanEvent.InterestRateReset("EURIBOR", 400m) ]
        |> LoanAggregate.rebuild
    state.Value.Terms.InterestIndex |> should equal (Some "EURIBOR")
    state.Value.Terms.SpreadBps |> should equal (Some 400m)

[<Fact>]
let ``HandleResetInterestRate rejects negative spread`` () =
    let state =
        [ LoanEvent.LoanCreated(sampleHeader (), sampleTerms ())
          LoanEvent.LoanCommitted(10_000_000m, Currency.USD) ]
        |> LoanAggregate.rebuild
    let result = LoanAggregate.handle state (LoanCommand.ResetInterestRate("SOFR", -10m))
    match result with
    | Error msg -> msg |> should equal "Spread cannot be negative."
    | Ok _ -> failwith "Expected error"

// ── Principal repayment ───────────────────────────────────────────────────────

[<Fact>]
let ``HandleRepayPrincipal rejects amount exceeding outstanding`` () =
    let state =
        [ LoanEvent.LoanCreated(sampleHeader (), sampleTerms ())
          LoanEvent.LoanCommitted(10_000_000m, Currency.USD)
          LoanEvent.DrawdownExecuted(3_000_000m, Currency.USD, DateOnly(2025, 2, 1)) ]
        |> LoanAggregate.rebuild
    let result = LoanAggregate.handleRepay state 5_000_000m (DateOnly(2025, 3, 1))
    match result with
    | Error msg -> msg |> should haveSubstring "outstanding principal"
    | Ok _ -> failwith "Expected error"

[<Fact>]
let ``Evolve PrincipalRepaid reduces outstanding principal`` () =
    let state =
        [ LoanEvent.LoanCreated(sampleHeader (), sampleTerms ())
          LoanEvent.LoanCommitted(10_000_000m, Currency.USD)
          LoanEvent.DrawdownExecuted(6_000_000m, Currency.USD, DateOnly(2025, 2, 1))
          LoanEvent.PrincipalRepaid(2_000_000m, DateOnly(2025, 6, 30)) ]
        |> LoanAggregate.rebuild
    state.Value.OutstandingPrincipal |> should equal 4_000_000m

// ── Loan closure ──────────────────────────────────────────────────────────────

[<Fact>]
let ``HandleCloseLoan succeeds when principal is fully repaid`` () =
    let state =
        [ LoanEvent.LoanCreated(sampleHeader (), sampleTerms ())
          LoanEvent.LoanCommitted(10_000_000m, Currency.USD)
          LoanEvent.DrawdownExecuted(5_000_000m, Currency.USD, DateOnly(2025, 2, 1))
          LoanEvent.PrincipalRepaid(5_000_000m, DateOnly(2025, 12, 31)) ]
        |> LoanAggregate.rebuild
    let result = LoanAggregate.handleClose state (DateOnly(2025, 12, 31))
    match result with
    | Ok events ->
        events |> should haveLength 1
        match events.[0] with
        | LoanEvent.LoanClosed date -> date |> should equal (DateOnly(2025, 12, 31))
        | _ -> failwith "Expected LoanClosed event"
    | Error msg -> failwith $"Expected Ok but got: {msg}"

[<Fact>]
let ``HandleCloseLoan rejects if outstanding principal remains`` () =
    let state =
        [ LoanEvent.LoanCreated(sampleHeader (), sampleTerms ())
          LoanEvent.LoanCommitted(10_000_000m, Currency.USD)
          LoanEvent.DrawdownExecuted(5_000_000m, Currency.USD, DateOnly(2025, 2, 1)) ]
        |> LoanAggregate.rebuild
    let result = LoanAggregate.handleClose state (DateOnly(2025, 12, 31))
    match result with
    | Error msg -> msg |> should haveSubstring "outstanding principal"
    | Ok _ -> failwith "Expected error"

[<Fact>]
let ``Evolve LoanClosed sets status to Closed`` () =
    let state =
        [ LoanEvent.LoanCreated(sampleHeader (), sampleTerms ())
          LoanEvent.LoanClosed(DateOnly(2028, 1, 15)) ]
        |> LoanAggregate.rebuild
    state.Value.Status |> should equal LoanStatus.Closed

// ── Full lifecycle ─────────────────────────────────────────────────────────────

[<Fact>]
let ``Full loan lifecycle transitions through expected statuses`` () =
    let header = sampleHeader ()
    let terms = sampleTerms ()
    let drawDate = DateOnly(2025, 2, 1)
    let repayDate = DateOnly(2028, 1, 14)
    let closeDate = DateOnly(2028, 1, 15)

    let events =
        [ LoanEvent.LoanCreated(header, terms)
          LoanEvent.LoanCommitted(10_000_000m, Currency.USD)
          LoanEvent.DrawdownExecuted(10_000_000m, Currency.USD, drawDate)
          LoanEvent.InterestAccrued(87_500m, DateOnly(2025, 4, 30))
          LoanEvent.InterestPaid(87_500m, DateOnly(2025, 5, 1))
          LoanEvent.PrincipalRepaid(10_000_000m, repayDate)
          LoanEvent.LoanClosed closeDate ]

    let finalState = LoanAggregate.rebuild events
    finalState |> should not' (equal None)
    let s = finalState.Value
    s.Status |> should equal LoanStatus.Closed
    s.OutstandingPrincipal |> should equal 0m
    s.AccruedInterestUnpaid |> should equal 0m
    s.Version |> should equal 7L

// ── AmendTerms ────────────────────────────────────────────────────────────────

[<Fact>]
let ``AmendTerms replaces loan terms and bumps version`` () =
    let state = createLoan ()
    let newTerms = { sampleTerms () with CommitmentAmount = 20_000_000m; SpreadBps = Some 400m }
    let result = LoanAggregate.handle state (LoanCommand.AmendTerms newTerms)
    match result with
    | Ok events ->
        let newState = events |> List.fold (fun s e -> Some (LoanAggregate.evolve s e)) state
        newState.Value.Terms.CommitmentAmount |> should equal 20_000_000m
        newState.Value.Terms.SpreadBps |> should equal (Some 400m)
    | Error msg -> failwith $"Unexpected error: {msg}"
