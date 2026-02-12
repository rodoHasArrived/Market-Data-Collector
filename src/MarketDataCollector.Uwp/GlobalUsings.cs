// =============================================================================
// GlobalUsings.cs - UWP Project Namespace Imports
// =============================================================================
// Imports shared library namespaces so services, collections, and contracts
// from MarketDataCollector.Ui.Services are available throughout this project.
// 
// NOTE: Type aliases and Contracts namespaces are NOT re-defined here because
// they are already provided by the referenced MarketDataCollector.Ui.Services
// project (via its GlobalUsings.cs). Re-defining them would cause CS0101
// duplicate type definition errors.
// =============================================================================

// Shared desktop services, collections, and contracts
global using MarketDataCollector.Ui.Services;
global using MarketDataCollector.Ui.Services.Collections;
global using MarketDataCollector.Ui.Services.Contracts;
global using MarketDataCollector.Ui.Services.Services;

// UWP-specific contracts
global using MarketDataCollector.Uwp.Contracts;
