// Global using directives for consolidated domain models
// Models and enums are defined once in Contracts project and imported here

global using MarketDataCollector.Contracts.Domain.Models;
global using MarketDataCollector.Contracts.Domain.Enums;
global using MarketDataCollector.Contracts.Domain.Events;

// Type aliases for backwards compatibility during migration
// These allow existing code using Domain.Models to continue working
global using ContractsTrade = MarketDataCollector.Contracts.Domain.Models.Trade;
global using ContractsHistoricalBar = MarketDataCollector.Contracts.Domain.Models.HistoricalBar;
global using ContractsLOBSnapshot = MarketDataCollector.Contracts.Domain.Models.LOBSnapshot;
global using ContractsOrderBookLevel = MarketDataCollector.Contracts.Domain.Models.OrderBookLevel;
global using ContractsOrderFlowStatistics = MarketDataCollector.Contracts.Domain.Models.OrderFlowStatistics;
global using ContractsBboQuotePayload = MarketDataCollector.Contracts.Domain.Models.BboQuotePayload;
global using ContractsIntegrityEvent = MarketDataCollector.Contracts.Domain.Models.IntegrityEvent;
