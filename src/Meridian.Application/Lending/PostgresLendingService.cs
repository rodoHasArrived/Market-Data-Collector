using Microsoft.Extensions.Logging;
using Microsoft.FSharp.Collections;
using FSharpLending = Meridian.FSharp.Domain.Lending;
using LendingRepo = Meridian.Lending.LoanContractRepository;
using LendingStore = Meridian.Lending.EventStore.ILoanEventStore;

namespace Meridian.Application.Lending;

/// <summary>
/// PostgreSQL-backed implementation of <see cref="ILendingService"/>.
/// Persists loan events using the Sharpino-pattern event store in
/// <c>Meridian.Lending</c>.  Each command is handled with optimistic
/// concurrency; on a conflict the command is retried up to three times.
/// </summary>
public sealed class PostgresLendingService : ILendingService
{
    private readonly LendingStore.ILoanEventStore _store;
    private readonly ILogger<PostgresLendingService> _logger;

    /// <summary>Maximum number of optimistic-concurrency retries per command.</summary>
    private const int MaxRetries = 3;

    public PostgresLendingService(
        LendingStore.ILoanEventStore store,
        ILogger<PostgresLendingService> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <inheritdoc/>
    public LoanCommandResult Handle(Guid loanId, FSharpLending.LoanCommand command)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            var result = LendingRepo.execute(_store, loanId, command);

            if (result.IsSuccess)
            {
                var success = (LendingRepo.CommandResult.Success)result;
                _logger.LogInformation(
                    "Lending command applied to loan {LoanId}: {EventCount} event(s) appended (seq {Seq})",
                    loanId, success.events.Length, success.newSequence);
                return new LoanCommandResult(true, null);
            }

            if (result.IsDomainError)
            {
                var err = (LendingRepo.CommandResult.DomainError)result;
                _logger.LogWarning(
                    "Lending command rejected for loan {LoanId}: {Error}", loanId, err.message);
                return new LoanCommandResult(false, err.message);
            }

            // ConflictError — retry after brief back-off.
            var conflict = (LendingRepo.CommandResult.ConflictError)result;
            var conflictInfo = conflict.Item;
            _logger.LogWarning(
                "Optimistic concurrency conflict on loan {LoanId} (attempt {Attempt}/{Max}): " +
                "expected seq {Expected}, found {Actual}",
                loanId, attempt, MaxRetries,
                conflictInfo.expected, conflictInfo.actual);

            if (attempt < MaxRetries)
                Thread.Sleep(attempt * 10);
        }

        return new LoanCommandResult(false, "Concurrency conflict: too many retries.");
    }

    /// <inheritdoc/>
    public FSharpLending.LoanState? GetState(Guid loanId)
    {
        var result = LendingRepo.getState(_store, loanId);
        return Microsoft.FSharp.Core.FSharpOption<FSharpLending.LoanState>.get_IsSome(result)
            ? result.Value
            : null;
    }

    /// <inheritdoc/>
    public IReadOnlyList<FSharpLending.LoanEvent> GetEvents(Guid loanId) =>
        LendingRepo.getEvents(_store, loanId).ToArray();

    /// <inheritdoc/>
    public IReadOnlyList<FSharpLending.ScheduledPayment> GenerateSchedule(Guid loanId, DateOnly fromDate)
    {
        var state = GetState(loanId);
        if (state is null)
            return Array.Empty<FSharpLending.ScheduledPayment>();

        return ListModule.ToArray(FSharpLending.PaymentSchedule.Generate(state, fromDate));
    }

    /// <inheritdoc/>
    public IReadOnlyList<Guid> GetAllLoanIds() =>
        Array.Empty<Guid>(); // Full scan is supported; omitted for now to avoid table-scan queries.
}
