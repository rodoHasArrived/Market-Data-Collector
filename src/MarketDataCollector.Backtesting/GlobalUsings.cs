global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Runtime.CompilerServices;
global using System.Threading;
global using System.Threading.Tasks;
global using MarketDataCollector.Backtesting.Sdk;
global using MarketDataCollector.Contracts.Domain.Models;
global using MarketDataCollector.Ledger;
global using Microsoft.Extensions.Logging;
// Preserve the BacktestLedger name used throughout the Engine project.
global using BacktestLedger = MarketDataCollector.Ledger.Ledger;
[assembly: InternalsVisibleTo("MarketDataCollector.Backtesting.Tests")]
