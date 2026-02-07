// Global using directives for Domain layer
global using MarketDataCollector.Contracts.Domain.Models;
global using MarketDataCollector.Contracts.Domain.Enums;
global using MarketDataCollector.Contracts.Configuration;

// Expose internal classes to test assembly for unit testing
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("MarketDataCollector.Tests")]
