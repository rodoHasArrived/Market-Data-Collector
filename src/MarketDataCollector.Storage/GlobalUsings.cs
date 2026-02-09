// Global using directives for Storage layer
global using MarketDataCollector.Contracts.Domain.Models;
global using MarketDataCollector.Contracts.Domain.Enums;
global using MarketDataCollector.Contracts.Configuration;

// Type aliases - Domain.Events.MarketEvent is the primary type
global using MarketEvent = MarketDataCollector.Domain.Events.MarketEvent;
global using MarketEventPayload = MarketDataCollector.Domain.Events.MarketEventPayload;

// Expose internal classes to test assembly for unit testing
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("MarketDataCollector.Tests")]
