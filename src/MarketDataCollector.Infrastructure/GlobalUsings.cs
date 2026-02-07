// Global using directives for Infrastructure layer
global using MarketDataCollector.Contracts.Domain.Models;
global using MarketDataCollector.Contracts.Domain.Enums;
global using MarketDataCollector.Contracts.Configuration;

// Type aliases - Domain.Events.MarketEvent is the primary type
global using MarketEvent = MarketDataCollector.Domain.Events.MarketEvent;
global using MarketEventPayload = MarketDataCollector.Domain.Events.MarketEventPayload;

// Backwards compatibility aliases
global using ContractsTrade = MarketDataCollector.Contracts.Domain.Models.Trade;
global using ContractsHistoricalBar = MarketDataCollector.Contracts.Domain.Models.HistoricalBar;
global using ContractsLOBSnapshot = MarketDataCollector.Contracts.Domain.Models.LOBSnapshot;
global using ContractsOrderBookLevel = MarketDataCollector.Contracts.Domain.Models.OrderBookLevel;
global using ContractsOrderFlowStatistics = MarketDataCollector.Contracts.Domain.Models.OrderFlowStatistics;
global using ContractsBboQuotePayload = MarketDataCollector.Contracts.Domain.Models.BboQuotePayload;
global using ContractsIntegrityEvent = MarketDataCollector.Contracts.Domain.Models.IntegrityEvent;

// Expose internal classes to test assembly for unit testing
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("MarketDataCollector.Tests")]
