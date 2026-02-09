// Global using directives for consolidated domain models
// Models and enums are defined once in Contracts project and imported here

global using MarketDataCollector.Contracts.Domain.Models;
global using MarketDataCollector.Contracts.Domain.Enums;
global using MarketDataCollector.Contracts.Configuration;

// Type alias to resolve ambiguity between Domain.Events.MarketEvent and Contracts.Domain.Events.MarketEvent
// Domain.Events.MarketEvent is the primary type used throughout the application
global using MarketEvent = MarketDataCollector.Domain.Events.MarketEvent;
global using MarketEventPayload = MarketDataCollector.Domain.Events.MarketEventPayload;

// Expose internal classes to test assembly for unit testing
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("MarketDataCollector.Tests")]
