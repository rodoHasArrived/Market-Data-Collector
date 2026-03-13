using System;
using System.Collections.Generic;
using MarketDataCollector.Ui.Services;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Manages per-page filter and sort state for session persistence.
/// Stores state in-memory during a session and serializes it into
/// <see cref="SessionState.ActiveFilters"/> (a flat dictionary keyed as
/// "{pageTag}:{key}") when the session is saved.
/// </summary>
public sealed class PageStateService
{
    private static readonly Lazy<PageStateService> _instance = new(() => new PageStateService());

    /// <summary>Gets the singleton instance.</summary>
    public static PageStateService Instance => _instance.Value;

    private readonly Dictionary<string, Dictionary<string, string>> _pageFilters = new(StringComparer.OrdinalIgnoreCase);

    private PageStateService() { }

    // ── Load / Save ─────────────────────────────────────────────────

    /// <summary>
    /// Populates in-memory state from a previously persisted <see cref="SessionState"/>.
    /// Keys in <see cref="SessionState.ActiveFilters"/> must follow the
    /// "{pageTag}:{key}" convention produced by <see cref="GetAllFiltersFlat"/>.
    /// </summary>
    public void LoadFromSession(SessionState session)
    {
        _pageFilters.Clear();

        foreach (var kv in session.ActiveFilters)
        {
            var colonIndex = kv.Key.IndexOf(':', StringComparison.Ordinal);
            if (colonIndex <= 0) continue;

            var pageTag = kv.Key[..colonIndex];
            var key = kv.Key[(colonIndex + 1)..];

            if (!_pageFilters.TryGetValue(pageTag, out var dict))
                _pageFilters[pageTag] = dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            dict[key] = kv.Value;
        }
    }

    /// <summary>
    /// Returns all stored filters as a flat dictionary suitable for serialising
    /// into <see cref="SessionState.ActiveFilters"/>.
    /// </summary>
    public Dictionary<string, string> GetAllFiltersFlat()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (pageTag, filters) in _pageFilters)
            foreach (var (key, value) in filters)
                result[$"{pageTag}:{key}"] = value;

        return result;
    }

    // ── Per-Page Access ─────────────────────────────────────────────

    /// <summary>
    /// Gets a saved filter value for the specified page and key.
    /// Returns <paramref name="defaultValue"/> when no value is stored.
    /// </summary>
    public string? GetFilter(string pageTag, string key, string? defaultValue = null)
    {
        return _pageFilters.TryGetValue(pageTag, out var dict) && dict.TryGetValue(key, out var value)
            ? value
            : defaultValue;
    }

    /// <summary>
    /// Sets (or clears when <paramref name="value"/> is <see langword="null"/>)
    /// a filter value for the specified page and key.
    /// </summary>
    public void SetFilter(string pageTag, string key, string? value)
    {
        if (!_pageFilters.TryGetValue(pageTag, out var dict))
        {
            if (value == null) return;
            _pageFilters[pageTag] = dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        if (value == null)
            dict.Remove(key);
        else
            dict[key] = value;
    }

    /// <summary>
    /// Removes all stored filter state for the specified page.
    /// </summary>
    public void ClearPageFilters(string pageTag)
    {
        _pageFilters.Remove(pageTag);
    }

    /// <summary>
    /// Returns the keys stored for a given page (for testing and diagnostics).
    /// </summary>
    public IEnumerable<string> GetPageKeys(string pageTag)
    {
        return _pageFilters.TryGetValue(pageTag, out var dict)
            ? (IEnumerable<string>)dict.Keys
            : Array.Empty<string>();
    }
}
