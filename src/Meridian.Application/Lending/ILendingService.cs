using Meridian.FSharp.Domain;
using FSharpLending = Meridian.FSharp.Domain.Lending;

namespace Meridian.Application.Lending;

/// <summary>
/// A snapshot of the result of handling a loan command.
/// </summary>
public sealed record LoanCommandResult(bool IsSuccess, string? ErrorMessage);

/// <summary>
/// Service interface for the direct-lending domain.
/// Wraps the pure F# <see cref="FSharpLending.LoanAggregate"/> module with an in-process,
/// dependency-injectable façade that can be embedded into any .NET host.
/// </summary>
/// <remarks>
/// All state changes are driven by commands and stored as immutable events
/// (event-sourced aggregate pattern). The store is in-memory by default;
/// replace <see cref="InMemoryLendingService"/> with a persistent implementation
/// when durability is required.
/// </remarks>
public interface ILendingService
{
    /// <summary>Sends a command to the aggregate for the specified loan.</summary>
    /// <param name="loanId">Identifier of the loan to target.</param>
    /// <param name="command">The command to handle.</param>
    /// <returns>A result indicating success or a domain error message.</returns>
    LoanCommandResult Handle(Guid loanId, FSharpLending.LoanCommand command);

    /// <summary>Returns the current state of the loan, or <c>null</c> when the loan does not exist.</summary>
    FSharpLending.LoanState? GetState(Guid loanId);

    /// <summary>Returns all event versions stored for the specified loan.</summary>
    IReadOnlyList<FSharpLending.LoanEvent> GetEvents(Guid loanId);

    /// <summary>
    /// Generates a forward-looking payment schedule from <paramref name="fromDate"/> to maturity.
    /// Returns an empty list for closed loans, past-maturity dates, or Custom amortization.
    /// </summary>
    IReadOnlyList<FSharpLending.ScheduledPayment> GenerateSchedule(Guid loanId, DateOnly fromDate);

    /// <summary>Returns all loan IDs currently tracked by the service.</summary>
    IReadOnlyList<Guid> GetAllLoanIds();
}
