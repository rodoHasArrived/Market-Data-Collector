/// Meridian Lending CLI — standalone entry point.
/// Demonstrates and exercises the direct-lending domain without any Meridian infrastructure.
/// Run with:  dotnet run --project src/Meridian.Lending.Cli
///
/// The demo walks through a complete loan lifecycle:
///   Create → Commit → Drawdown → Accrue interest → Repay → Close
/// followed by a forward-looking payment schedule.
module Meridian.Lending.Cli.Program

open System
open Meridian.FSharp.Domain.Lending

// ── Formatting helpers ────────────────────────────────────────────────────────

let private hr () = printfn "%s" (String.replicate 70 "─")
let private section title = hr (); printfn "  %s" title; hr ()

let private printState (state: LoanState) =
    printfn "  %-30s %s" "Status:" (state.Status.ToString())
    printfn "  %-30s %M %s" "Outstanding principal:" state.OutstandingPrincipal (state.Header.BaseCurrency.ToString())
    printfn "  %-30s %M" "Accrued interest (unpaid):" state.AccruedInterestUnpaid
    printfn "  %-30s %M" "Accrued fee (unpaid):" state.AccruedCommitmentFeeUnpaid
    if state.UnamortizedDiscount > 0m then
        printfn "  %-30s %M" "Unamortized discount:" state.UnamortizedDiscount
    if state.UnamortizedPremium > 0m then
        printfn "  %-30s %M" "Unamortized premium:" state.UnamortizedPremium
    printfn "  %-30s %M" "Carrying value:" (LoanService.carryingValue state)
    printfn "  %-30s %s" "Economically active:" (if LoanService.isEconomicallyActive state then "yes" else "no")
    printfn "  %-30s %d" "Event version:" state.Version

let private applyCmd label (state: LoanState option) (command: LoanCommand) : LoanState option =
    match LoanAggregate.handle state command with
    | Error msg ->
        printfn "  [ERROR] %s → %s" label msg
        state
    | Ok events ->
        let newState = events |> List.fold (fun s e -> Some (LoanAggregate.evolve s e)) state
        printfn "  [OK]    %s" label
        newState

let private printSchedule (state: LoanState) (fromDate: DateOnly) =
    let schedule = PaymentSchedule.generate state fromDate
    if schedule.IsEmpty then
        printfn "  (no future payments)"
    else
        printfn "  %-4s  %-12s  %-14s  %-14s  %s" "#" "Due date" "Principal" "Interest" "Remaining"
        printfn "  %s" (String.replicate 62 "·")
        for p in schedule do
            let round (v: decimal) = Math.Round(v, 2)
            printfn "  %-4d  %-12s  %14M  %14M  %M"
                p.PaymentNumber
                (p.DueDate.ToString("yyyy-MM-dd"))
                (round p.PrincipalDue)
                (round p.EstimatedInterest)
                (round p.RemainingPrincipalAfter)

// ── Demo scenarios ─────────────────────────────────────────────────────────────

/// Scenario 1 — Bullet loan full lifecycle
let private runBulletDemo () =
    section "Scenario 1 — Bullet loan (SOFR + 350 bps, Actual/360)"

    let header : LoanHeader =
        { SecurityId      = Guid.NewGuid()
          Name            = "Acme Corp Term Loan A"
          BaseCurrency    = Currency.USD
          EffectiveDate   = DateOnly(2025, 1, 15) }

    let terms : DirectLendingTerms =
        { OriginationDate          = DateOnly(2025, 1, 15)
          MaturityDate             = DateOnly(2028, 1, 15)
          CommitmentAmount         = 10_000_000m
          CommitmentFeeRate        = Some 0.005m
          InterestRate             = None
          InterestIndex            = Some "SOFR"
          SpreadBps                = Some 350m
          PaymentFrequencyMonths   = 3
          AmortizationType         = AmortizationType.BulletMaturity
          DayCountConvention       = DayCountConvention.Actual360
          PurchasePrice            = Some 0.97m       // 3 % discount
          CovenantsJson            = None }

    let mutable state : LoanState option = None

    state <- applyCmd "CreateLoan"     state (LoanCommand.CreateLoan(header, terms))
    state <- applyCmd "CommitLoan"     state (LoanCommand.CommitLoan(10_000_000m, Currency.USD))
    state <- applyCmd "RecordDrawdown" state (LoanCommand.RecordDrawdown(10_000_000m, Currency.USD, DateOnly(2025, 1, 15)))
    state <- applyCmd "AccrueInterest (Q1)" state (LoanCommand.AccrueInterest(87_500m, DateOnly(2025, 4, 15)))
    state <- applyCmd "RecordInterestPayment" state (LoanCommand.RecordInterestPayment(87_500m, DateOnly(2025, 4, 15)))
    state <- applyCmd "AmortizeDiscount" state (LoanCommand.AmortizeDiscount(25_000m, DateOnly(2025, 4, 15)))

    printfn ""
    printfn "  State after Q1:"
    state |> Option.iter printState

    printfn ""
    section "  Payment schedule from Q1"
    state |> Option.iter (fun s -> printSchedule s (DateOnly(2025, 4, 15)))

    // Fast forward: repay at maturity
    state <- applyCmd "RepayPrincipal (full)" state (LoanCommand.RepayPrincipal(10_000_000m, DateOnly(2028, 1, 15)))
    state <- applyCmd "CloseLoan"            state (LoanCommand.CloseLoan(DateOnly(2028, 1, 15)))

    printfn ""
    printfn "  Final state:"
    state |> Option.iter printState

/// Scenario 2 — StraightLine amortization with collateral and covenant
let private runStraightLineDemo () =
    section "Scenario 2 — StraightLine amortization with collateral + covenant"

    let header : LoanHeader =
        { SecurityId      = Guid.NewGuid()
          Name            = "Beta Industries Term Loan B"
          BaseCurrency    = Currency.EUR
          EffectiveDate   = DateOnly(2025, 3, 1) }

    let terms : DirectLendingTerms =
        { OriginationDate          = DateOnly(2025, 3, 1)
          MaturityDate             = DateOnly(2026, 3, 1)
          CommitmentAmount         = 2_000_000m
          CommitmentFeeRate        = None
          InterestRate             = Some 0.075m
          InterestIndex            = None
          SpreadBps                = None
          PaymentFrequencyMonths   = 3
          AmortizationType         = AmortizationType.StraightLine
          DayCountConvention       = DayCountConvention.Actual365Fixed
          PurchasePrice            = None
          CovenantsJson            = None }

    let collateral : Collateral =
        { CollateralId    = Guid.NewGuid()
          CollateralType  = CollateralType.RealEstate
          Description     = "Industrial warehouse, Hamburg"
          EstimatedValue  = 3_000_000m
          Currency        = Currency.EUR
          AppraisalDate   = DateOnly(2025, 2, 28) }

    let covenant : Covenant =
        { CovenantId      = Guid.NewGuid()
          CovenantType    = CovenantType.LeverageRatio
          Description     = "Net leverage ≤ 4.0×"
          ThresholdValue  = 4.0m
          Frequency       = CovenantFrequency.Quarterly
          Status          = CovenantStatus.Active
          LastTestDate    = None }

    let mutable state : LoanState option = None

    state <- applyCmd "CreateLoan"     state (LoanCommand.CreateLoan(header, terms))
    state <- applyCmd "CommitLoan"     state (LoanCommand.CommitLoan(2_000_000m, Currency.EUR))
    state <- applyCmd "RecordDrawdown" state (LoanCommand.RecordDrawdown(2_000_000m, Currency.EUR, DateOnly(2025, 3, 1)))
    state <- applyCmd "AddCollateral"  state (LoanCommand.AddCollateral(collateral, DateOnly(2025, 3, 1)))
    state <- applyCmd "AddCovenant"    state (LoanCommand.AddCovenant(covenant, DateOnly(2025, 3, 1)))

    printfn ""
    printfn "  LTV ratio: %s" (match state |> Option.bind LoanService.loanToValue with Some v -> sprintf "%.2f%%" (v * 100m) | None -> "n/a")
    printfn "  Coverage:  %s" (match state |> Option.bind LoanService.collateralCoverageRatio with Some v -> sprintf "%.2fx" v | None -> "n/a")

    printfn ""
    section "  Payment schedule (StraightLine)"
    state |> Option.iter (fun s -> printSchedule s (DateOnly(2025, 3, 1)))

/// Scenario 3 — Annuity schedule
let private runAnnuityDemo () =
    section "Scenario 3 — Annuity amortization (PMT formula)"

    let terms : DirectLendingTerms =
        { OriginationDate          = DateOnly(2025, 1, 1)
          MaturityDate             = DateOnly(2026, 1, 1)
          CommitmentAmount         = 1_200_000m
          CommitmentFeeRate        = None
          InterestRate             = Some 0.06m
          InterestIndex            = None
          SpreadBps                = None
          PaymentFrequencyMonths   = 3
          AmortizationType         = AmortizationType.Annuity
          DayCountConvention       = DayCountConvention.Thirty360
          PurchasePrice            = None
          CovenantsJson            = None }

    let header : LoanHeader =
        { SecurityId      = Guid.NewGuid()
          Name            = "Gamma Capital Acquisition Loan"
          BaseCurrency    = Currency.GBP
          EffectiveDate   = DateOnly(2025, 1, 1) }

    let mutable state : LoanState option = None
    state <- applyCmd "CreateLoan"     state (LoanCommand.CreateLoan(header, terms))
    state <- applyCmd "CommitLoan"     state (LoanCommand.CommitLoan(1_200_000m, Currency.GBP))
    state <- applyCmd "RecordDrawdown" state (LoanCommand.RecordDrawdown(1_200_000m, Currency.GBP, DateOnly(2025, 1, 1)))

    printfn ""
    section "  Payment schedule (Annuity — equal total payment)"
    state |> Option.iter (fun s -> printSchedule s (DateOnly(2025, 1, 1)))

// ── Entry point ──────────────────────────────────────────────────────────────

[<EntryPoint>]
let main _ =
    printfn ""
    printfn "  ╔══════════════════════════════════════════════════╗"
    printfn "  ║    Meridian Direct Lending Domain — CLI Demo     ║"
    printfn "  ╚══════════════════════════════════════════════════╝"
    printfn ""
    printfn "  This tool exercises the pure F# lending aggregate."
    printfn "  No Meridian infrastructure is required to run it."
    printfn ""

    try
        runBulletDemo ()
        printfn ""
        runStraightLineDemo ()
        printfn ""
        runAnnuityDemo ()
        hr ()
        printfn "  Demo complete."
        0
    with ex ->
        eprintfn "  [FATAL] %s" ex.Message
        1
