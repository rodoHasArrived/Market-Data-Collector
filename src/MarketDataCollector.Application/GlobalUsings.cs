// Global using directives for Application layer
global using MarketDataCollector.Contracts.Configuration;
global using MarketDataCollector.Contracts.Domain.Enums;
global using MarketDataCollector.Contracts.Domain.Models;
global using ContractsBboQuotePayload = MarketDataCollector.Contracts.Domain.Models.BboQuotePayload;
global using ContractsHistoricalBar = MarketDataCollector.Contracts.Domain.Models.HistoricalBar;
global using ContractsIntegrityEvent = MarketDataCollector.Contracts.Domain.Models.IntegrityEvent;
global using ContractsLOBSnapshot = MarketDataCollector.Contracts.Domain.Models.LOBSnapshot;
global using ContractsOrderBookLevel = MarketDataCollector.Contracts.Domain.Models.OrderBookLevel;
global using ContractsOrderFlowStatistics = MarketDataCollector.Contracts.Domain.Models.OrderFlowStatistics;
// Backwards compatibility aliases
global using ContractsTrade = MarketDataCollector.Contracts.Domain.Models.Trade;
// Type aliases - Domain.Events.MarketEvent is the primary type
global using MarketEvent = MarketDataCollector.Domain.Events.MarketEvent;
global using MarketEventPayload = MarketDataCollector.Domain.Events.MarketEventPayload;
// Expose internal classes to test assembly and main entry point for unit testing
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("MarketDataCollector.Tests")]
[assembly: InternalsVisibleTo("MarketDataCollector")]
