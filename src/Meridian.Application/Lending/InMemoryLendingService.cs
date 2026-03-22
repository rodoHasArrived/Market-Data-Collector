using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using FSharpLending = Meridian.FSharp.Domain.Lending;

namespace Meridian.Application.Lending;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="ILendingService"/>.
/// Maintains a per-loan event log and rebuilds state on demand.
/// Suitable for single-process use or as a starting point for a durable implementation.
/// </summary>
public sealed class InMemoryLendingService : ILendingService
{
    private readonly ConcurrentDictionary<Guid, List<FSharpLending.LoanEvent>> _eventStore = new();
    private readonly ILogger<InMemoryLendingService> _logger;

    public InMemoryLendingService(ILogger<InMemoryLendingService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public LoanCommandResult Handle(Guid loanId, FSharpLending.LoanCommand command)
    {
        var events = _eventStore.GetOrAdd(loanId, _ => []);

        FSharpOption<FSharpLending.LoanState> currentState;
        lock (events)
        {
            currentState = FSharpLending.LoanAggregate.Rebuild(ListModule.OfSeq(events));
        }

        var result = FSharpLending.LoanAggregate.Handle(currentState, command);
        if (result.IsError)
        {
            var errMsg = result.ErrorValue;
            _logger.LogWarning("Lending command rejected for loan {LoanId}: {Error}", loanId, errMsg);
            return new LoanCommandResult(false, errMsg);
        }

        var newEvents = result.ResultValue;
        lock (events)
        {
            events.AddRange(newEvents);
        }

        _logger.LogInformation("Lending command applied to loan {LoanId}: {EventCount} event(s) appended",
            loanId, newEvents.Length);
        return new LoanCommandResult(true, null);
    }

    /// <inheritdoc/>
    public FSharpLending.LoanState? GetState(Guid loanId)
    {
        if (!_eventStore.TryGetValue(loanId, out var events))
            return null;

        FSharpOption<FSharpLending.LoanState> state;
        lock (events)
        {
            state = FSharpLending.LoanAggregate.Rebuild(ListModule.OfSeq(events));
        }

        return FSharpOption<FSharpLending.LoanState>.get_IsSome(state) ? state.Value : null;
    }

    /// <inheritdoc/>
    public IReadOnlyList<FSharpLending.LoanEvent> GetEvents(Guid loanId)
    {
        if (!_eventStore.TryGetValue(loanId, out var events))
            return Array.Empty<FSharpLending.LoanEvent>();

        lock (events)
            return [.. events];
    }

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
        [.. _eventStore.Keys];
}
