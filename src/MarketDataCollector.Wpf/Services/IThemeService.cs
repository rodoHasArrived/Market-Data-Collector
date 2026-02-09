// C1: Interface and AppTheme enum promoted to MarketDataCollector.Ui.Services.Contracts.
// This file re-exports the shared definitions for backwards compatibility.
// The canonical interface is in the shared Ui.Services project.

using System;
using SharedTheme = MarketDataCollector.Ui.Services.Contracts.AppTheme;
using SharedContract = MarketDataCollector.Ui.Services.Contracts.IThemeService;

namespace MarketDataCollector.Wpf.Services
{
    /// <summary>
    /// Backwards-compatible shim for the old WPF <c>AppTheme</c> type.
    /// Use <see cref="SharedTheme"/> instead from MarketDataCollector.Ui.Services.Contracts.
    /// </summary>
    [Obsolete("Use MarketDataCollector.Ui.Services.Contracts.AppTheme instead.")]
    public enum AppTheme
    {
        // Intentionally left empty. This exists only to satisfy existing references
        // in the MarketDataCollector.Wpf assembly. New code should use the shared enum.
    }

    /// <summary>
    /// Backwards-compatible wrapper for the old WPF <c>IThemeService</c> interface.
    /// The canonical contract is <see cref="SharedContract"/> in MarketDataCollector.Ui.Services.Contracts.
    /// </summary>
    [Obsolete("Use MarketDataCollector.Ui.Services.Contracts.IThemeService instead.")]
    public interface IThemeService : SharedContract
    {
        // No additional members; this interface simply forwards to the shared contract.
    }
}
