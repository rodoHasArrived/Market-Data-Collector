using FluentAssertions;
using Meridian.Application.Lending;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.FSharp.Core;
using Xunit;
using FSharpLending = Meridian.FSharp.Domain.Lending;

namespace Meridian.Tests.Application.Lending;

/// <summary>
/// Tests for <see cref="InMemoryLendingService"/>.
/// Covers the full loan lifecycle, schedule generation, concurrent access isolation,
/// and error propagation from the underlying F# aggregate.
/// </summary>
public sealed class InMemoryLendingServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static InMemoryLendingService CreateService() =>
        new(NullLogger<InMemoryLendingService>.Instance);

    private static FSharpLending.LoanHeader MakeHeader(Guid id) =>
        new(id, "Test Loan", FSharpLending.Currency.USD, new DateOnly(2025, 1, 15));

    private static FSharpLending.DirectLendingTerms MakeTerms() => new(
        new DateOnly(2025, 1, 15),             // OriginationDate
        new DateOnly(2028, 1, 15),             // MaturityDate
        1_000_000m,                            // CommitmentAmount
        FSharpOption<decimal>.None,            // CommitmentFeeRate
        FSharpOption<decimal>.Some(0.08m),     // InterestRate
        FSharpOption<string>.None,             // InterestIndex
        FSharpOption<decimal>.None,            // SpreadBps
        3,                                     // PaymentFrequencyMonths
        FSharpLending.AmortizationType.BulletMaturity,
        FSharpLending.DayCountConvention.Actual360,
        FSharpOption<decimal>.None,            // PurchasePrice
        FSharpOption<string>.None);            // CovenantsJson

    // ── GetState on unknown loan ───────────────────────────────────────────────

    [Fact]
    public void GetState_ReturnsNull_WhenLoanDoesNotExist()
    {
        var svc = CreateService();
        svc.GetState(Guid.NewGuid()).Should().BeNull();
    }

    [Fact]
    public void GetEvents_ReturnsEmpty_WhenLoanDoesNotExist()
    {
        var svc = CreateService();
        svc.GetEvents(Guid.NewGuid()).Should().BeEmpty();
    }

    [Fact]
    public void GetAllLoanIds_ReturnsEmpty_WhenNoLoans()
    {
        var svc = CreateService();
        svc.GetAllLoanIds().Should().BeEmpty();
    }

    // ── CreateLoan ────────────────────────────────────────────────────────────

    [Fact]
    public void Handle_CreateLoan_Succeeds_AndStateIsPending()
    {
        var svc = CreateService();
        var id = Guid.NewGuid();

        var result = svc.Handle(id, FSharpLending.LoanCommand.NewCreateLoan(MakeHeader(id), MakeTerms()));

        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        var state = svc.GetState(id);
        state.Should().NotBeNull();
        state!.Status.Should().Be(FSharpLending.LoanStatus.Pending);
    }

    [Fact]
    public void Handle_CreateLoan_IsTracked_InGetAllLoanIds()
    {
        var svc = CreateService();
        var id = Guid.NewGuid();
        svc.Handle(id, FSharpLending.LoanCommand.NewCreateLoan(MakeHeader(id), MakeTerms()));

        svc.GetAllLoanIds().Should().Contain(id);
    }

    [Fact]
    public void Handle_DuplicateCreate_ReturnsError()
    {
        var svc = CreateService();
        var id = Guid.NewGuid();
        svc.Handle(id, FSharpLending.LoanCommand.NewCreateLoan(MakeHeader(id), MakeTerms()));

        var result = svc.Handle(id, FSharpLending.LoanCommand.NewCreateLoan(MakeHeader(id), MakeTerms()));

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    // ── Full lifecycle ─────────────────────────────────────────────────────────

    [Fact]
    public void Handle_FullLifecycle_SetsClosedStatus()
    {
        var svc = CreateService();
        var id = Guid.NewGuid();

        svc.Handle(id, FSharpLending.LoanCommand.NewCreateLoan(MakeHeader(id), MakeTerms()))
           .IsSuccess.Should().BeTrue();
        svc.Handle(id, FSharpLending.LoanCommand.NewCommitLoan(1_000_000m, FSharpLending.Currency.USD))
           .IsSuccess.Should().BeTrue();
        svc.Handle(id, FSharpLending.LoanCommand.NewRecordDrawdown(1_000_000m, FSharpLending.Currency.USD, new DateOnly(2025, 1, 15)))
           .IsSuccess.Should().BeTrue();
        svc.Handle(id, FSharpLending.LoanCommand.NewRepayPrincipal(1_000_000m, new DateOnly(2028, 1, 15)))
           .IsSuccess.Should().BeTrue();
        svc.Handle(id, FSharpLending.LoanCommand.NewCloseLoan(new DateOnly(2028, 1, 15)))
           .IsSuccess.Should().BeTrue();

        var state = svc.GetState(id);
        state!.Status.Should().Be(FSharpLending.LoanStatus.Closed);
        state.OutstandingPrincipal.Should().Be(0m);
    }

    // ── GetEvents ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetEvents_ReturnsOneEvent_AfterCreate()
    {
        var svc = CreateService();
        var id = Guid.NewGuid();
        svc.Handle(id, FSharpLending.LoanCommand.NewCreateLoan(MakeHeader(id), MakeTerms()));

        svc.GetEvents(id).Should().HaveCount(1);
    }

    [Fact]
    public void GetEvents_GrowsWithEachCommand()
    {
        var svc = CreateService();
        var id = Guid.NewGuid();
        svc.Handle(id, FSharpLending.LoanCommand.NewCreateLoan(MakeHeader(id), MakeTerms()));
        svc.Handle(id, FSharpLending.LoanCommand.NewCommitLoan(1_000_000m, FSharpLending.Currency.USD));
        svc.Handle(id, FSharpLending.LoanCommand.NewRecordDrawdown(1_000_000m, FSharpLending.Currency.USD, new DateOnly(2025, 1, 15)));

        svc.GetEvents(id).Should().HaveCount(3);
    }

    // ── GenerateSchedule ──────────────────────────────────────────────────────

    [Fact]
    public void GenerateSchedule_ReturnsEmpty_WhenLoanDoesNotExist()
    {
        var svc = CreateService();
        svc.GenerateSchedule(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today))
           .Should().BeEmpty();
    }

    [Fact]
    public void GenerateSchedule_ReturnsFuturePayments_ForActiveDrawnLoan()
    {
        var svc = CreateService();
        var id = Guid.NewGuid();
        svc.Handle(id, FSharpLending.LoanCommand.NewCreateLoan(MakeHeader(id), MakeTerms()));
        svc.Handle(id, FSharpLending.LoanCommand.NewCommitLoan(1_000_000m, FSharpLending.Currency.USD));
        svc.Handle(id, FSharpLending.LoanCommand.NewRecordDrawdown(1_000_000m, FSharpLending.Currency.USD, new DateOnly(2025, 1, 15)));

        var schedule = svc.GenerateSchedule(id, new DateOnly(2025, 1, 15));

        schedule.Should().NotBeEmpty();
        schedule[^1].RemainingPrincipalAfter.Should().Be(0m);
    }

    [Fact]
    public void GenerateSchedule_ReturnsEmpty_ForClosedLoan()
    {
        var svc = CreateService();
        var id = Guid.NewGuid();
        svc.Handle(id, FSharpLending.LoanCommand.NewCreateLoan(MakeHeader(id), MakeTerms()));
        svc.Handle(id, FSharpLending.LoanCommand.NewCommitLoan(1_000_000m, FSharpLending.Currency.USD));
        svc.Handle(id, FSharpLending.LoanCommand.NewRecordDrawdown(1_000_000m, FSharpLending.Currency.USD, new DateOnly(2025, 1, 15)));
        svc.Handle(id, FSharpLending.LoanCommand.NewRepayPrincipal(1_000_000m, new DateOnly(2028, 1, 15)));
        svc.Handle(id, FSharpLending.LoanCommand.NewCloseLoan(new DateOnly(2028, 1, 15)));

        svc.GenerateSchedule(id, new DateOnly(2028, 1, 16)).Should().BeEmpty();
    }

    // ── Isolation between loans ───────────────────────────────────────────────

    [Fact]
    public void Handle_TwoLoans_DoNotShareState()
    {
        var svc = CreateService();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        svc.Handle(id1, FSharpLending.LoanCommand.NewCreateLoan(MakeHeader(id1), MakeTerms()));
        svc.Handle(id2, FSharpLending.LoanCommand.NewCreateLoan(MakeHeader(id2), MakeTerms()));
        svc.Handle(id1, FSharpLending.LoanCommand.NewCommitLoan(1_000_000m, FSharpLending.Currency.USD));

        // id2 was not committed — it remains Pending
        var state2 = svc.GetState(id2)!;
        state2.Status.Should().Be(FSharpLending.LoanStatus.Pending);
        svc.GetEvents(id2).Should().HaveCount(1);
    }

    // ── Validation errors are surfaced ────────────────────────────────────────

    [Fact]
    public void Handle_InvalidAmendTerms_ReturnsDomainError()
    {
        var svc = CreateService();
        var id = Guid.NewGuid();
        svc.Handle(id, FSharpLending.LoanCommand.NewCreateLoan(MakeHeader(id), MakeTerms()));

        // Amend to zero CommitmentAmount — rejected by validateTermsFields
        var badTerms = new FSharpLending.DirectLendingTerms(
            new DateOnly(2025, 1, 15),
            new DateOnly(2028, 1, 15),
            0m,                                // CommitmentAmount = 0 → invalid
            FSharpOption<decimal>.None,
            FSharpOption<decimal>.Some(0.08m),
            FSharpOption<string>.None,
            FSharpOption<decimal>.None,
            3,
            FSharpLending.AmortizationType.BulletMaturity,
            FSharpLending.DayCountConvention.Actual360,
            FSharpOption<decimal>.None,
            FSharpOption<string>.None);

        var result = svc.Handle(id, FSharpLending.LoanCommand.NewAmendTerms(badTerms));

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("CommitmentAmount");
    }
}
