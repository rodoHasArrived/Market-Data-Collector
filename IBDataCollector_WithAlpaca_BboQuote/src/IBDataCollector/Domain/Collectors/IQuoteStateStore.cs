using IBDataCollector.Domain.Models;

namespace IBDataCollector.Domain.Collectors;

/// <summary>
/// Provides access to the latest Best-Bid/Offer quote per symbol for downstream inference (e.g., aggressor side).
/// </summary>
public interface IQuoteStateStore
{
    /// <summary>Try get the latest BBO for a symbol.</summary>
    bool TryGet(string symbol, out BboQuotePayload quote);
}
