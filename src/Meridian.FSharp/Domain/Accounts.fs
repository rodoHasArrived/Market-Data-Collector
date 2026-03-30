/// F# type-safe domain model for double-entry accounting direction.
///
/// Double-entry bookkeeping records every economic event as a balanced set
/// of debits and credits. Amounts are ALWAYS positive; the direction (Debit
/// or Credit) determines whether the amount increases or decreases a given
/// account balance.
///
/// Normal balance rules (which direction increases the account):
///   Asset    → Debit increases, Credit decreases
///   Expense  → Debit increases, Credit decreases
///   Liability → Credit increases, Debit decreases
///   Equity   → Credit increases, Debit decreases
///   Revenue  → Credit increases, Debit decreases
///
/// Never use signed amounts to represent accounting direction —
/// that loses the intent and makes trial balance verification harder.
module Meridian.FSharp.Domain.Accounts

open System

// ── Entry direction ───────────────────────────────────────────────────────────

/// The direction of a single ledger entry line.
/// Amounts are always positive; this discriminator carries the sign semantics.
[<RequireQualifiedAccess>]
type EntryDirection =
    /// An entry that increases the balance of asset and expense accounts,
    /// or decreases the balance of liability, equity, and revenue accounts.
    | Debit

    /// An entry that increases the balance of liability, equity, and revenue accounts,
    /// or decreases the balance of asset and expense accounts.
    | Credit

    member this.ToInt() =
        match this with
        | Debit  -> 1
        | Credit -> 2

    static member FromInt(value: int) =
        match value with
        | 1 -> Debit
        | 2 -> Credit
        | _ -> invalidArg "value" $"Invalid EntryDirection value: {value}"

    override this.ToString() =
        match this with
        | Debit  -> "DR"
        | Credit -> "CR"

// ── Account type classification ───────────────────────────────────────────────

/// The normal-balance type of a ledger account — determines which direction
/// increases the account and how the account appears in financial statements.
[<RequireQualifiedAccess>]
type AccountNormalBalance =
    /// Asset and Expense accounts: debit-normal (debit increases).
    | DebitNormal
    /// Liability, Equity, and Revenue accounts: credit-normal (credit increases).
    | CreditNormal

    /// Returns the normal balance for a given account type string.
    static member ForAccountType(accountType: string) =
        match accountType.ToLowerInvariant() with
        | "asset" | "expense" -> DebitNormal
        | "liability" | "equity" | "revenue" -> CreditNormal
        | unknown -> invalidArg "accountType" $"Unknown account type: {unknown}"

// ── Accounting entry ──────────────────────────────────────────────────────────

/// A single line in a journal entry: an account, a direction, and a positive amount.
type AccountingEntry =
    {
        /// The account being debited or credited (account code or name).
        AccountCode  : string

        /// The direction of this entry.
        Direction    : EntryDirection

        /// The positive amount for this entry line.
        Amount       : decimal

        /// Human-readable description of this specific line.
        Memo         : string
    }

// ── Validation helpers ────────────────────────────────────────────────────────

/// Verify that a set of lines balances: ΣDebits = ΣCredits.
/// Returns Ok () on balance, Error with the imbalance amount on failure.
[<CompiledName("VerifyBalance")>]
let verifyBalance (lines: AccountingEntry list) : Result<unit, decimal> =
    let debits  = lines |> List.filter (fun l -> l.Direction = EntryDirection.Debit)  |> List.sumBy _.Amount
    let credits = lines |> List.filter (fun l -> l.Direction = EntryDirection.Credit) |> List.sumBy _.Amount
    let diff = abs (debits - credits)
    if diff <= 0.000001m then Ok ()
    else Error diff

/// True if all amounts in the list are strictly positive.
[<CompiledName("AllAmountsPositive")>]
let allAmountsPositive (lines: AccountingEntry list) : bool =
    lines |> List.forall (fun l -> l.Amount > 0m)

// ── Convenience factory ───────────────────────────────────────────────────────

[<CompiledName("Debit")>]
let debit accountCode amount memo =
    { AccountCode = accountCode; Direction = EntryDirection.Debit; Amount = amount; Memo = memo }

[<CompiledName("Credit")>]
let credit accountCode amount memo =
    { AccountCode = accountCode; Direction = EntryDirection.Credit; Amount = amount; Memo = memo }
