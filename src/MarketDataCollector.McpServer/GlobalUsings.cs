// Global using directives for the MCP server project
global using System.ComponentModel;
global using System.Text.Json;
global using System.Text.Json.Serialization;
global using MarketDataCollector.Application.Backfill;
global using MarketDataCollector.Application.Config;
global using MarketDataCollector.Application.Logging;
global using MarketDataCollector.Application.UI;
global using MarketDataCollector.Contracts.Domain.Models;
global using MarketDataCollector.Infrastructure.Adapters.Core;
global using MarketDataCollector.Infrastructure.Contracts;
global using MarketDataCollector.Storage.Interfaces;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;
global using ModelContextProtocol.Server;
global using Serilog;
