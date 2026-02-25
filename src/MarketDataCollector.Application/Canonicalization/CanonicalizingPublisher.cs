using MarketDataCollector.Application.Config;
using MarketDataCollector.Domain.Events;
using Serilog;

namespace MarketDataCollector.Application.Canonicalization;

/// <summary>
/// Decorator around <see cref="IMarketEventPublisher"/> that applies canonicalization
/// to events before forwarding them to the inner publisher.
/// <para>
/// Phase 2 behaviour: when <see cref="CanonicalizationConfig.PilotSymbols"/> is set,
/// only matching symbols are canonicalized. All others pass through unchanged.
/// When <see cref="CanonicalizationConfig.EnableDualWrite"/> is true, both the raw
/// and enriched events are published (the raw event first, then the enriched copy).
/// </para>
/// </summary>
public sealed class CanonicalizingPublisher : IMarketEventPublisher
{
    private readonly IMarketEventPublisher _inner;
    private readonly IEventCanonicalizer _canonicalizer;
    private readonly HashSet<string>? _pilotSymbols;
    private readonly bool _dualWrite;
    private readonly ILogger _log = Log.ForContext<CanonicalizingPublisher>();

    /// <summary>
    /// Initializes a new <see cref="CanonicalizingPublisher"/>.
    /// </summary>
    /// <param name="inner">The downstream publisher to forward events to.</param>
    /// <param name="canonicalizer">The canonicalization engine.</param>
    /// <param name="config">Configuration controlling pilot scope and dual-write.</param>
    public CanonicalizingPublisher(
        IMarketEventPublisher inner,
        IEventCanonicalizer canonicalizer,
        CanonicalizationConfig config)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _canonicalizer = canonicalizer ?? throw new ArgumentNullException(nameof(canonicalizer));

        ArgumentNullException.ThrowIfNull(config);

        _dualWrite = config.EnableDualWrite;

        if (config.PilotSymbols is { Length: > 0 })
        {
            _pilotSymbols = new HashSet<string>(
                config.PilotSymbols,
                StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <inheritdoc />
    public bool TryPublish(in MarketEvent evt)
    {
        if (!IsInScope(evt))
        {
            return _inner.TryPublish(evt);
        }

        if (_dualWrite)
        {
            // Publish the raw event first (unchanged)
            _inner.TryPublish(evt);
            CanonicalizationMetrics.RecordDualWrite();
        }

        // Canonicalize and publish the enriched event
        var enriched = _canonicalizer.Canonicalize(evt);
        return _inner.TryPublish(enriched);
    }

    /// <summary>
    /// Checks whether the event's symbol falls within the pilot scope.
    /// When no pilot symbols are configured, all symbols are in scope.
    /// </summary>
    private bool IsInScope(in MarketEvent evt)
    {
        if (_pilotSymbols is null)
            return true;

        return _pilotSymbols.Contains(evt.Symbol);
    }
}
