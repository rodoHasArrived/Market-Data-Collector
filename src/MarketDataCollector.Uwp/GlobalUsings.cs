// =============================================================================
// GlobalUsings.cs - UWP Project Namespace Imports
// =============================================================================
// Imports shared library namespaces so services, collections, and contracts
// from MarketDataCollector.Ui.Services are available throughout this project.
// Also provides type aliases for Contracts types (backwards compatibility).
// =============================================================================

// Shared desktop services, collections, and contracts
global using MarketDataCollector.Ui.Services;
global using MarketDataCollector.Ui.Services.Collections;
global using MarketDataCollector.Ui.Services.Contracts;

// Import all Contracts namespaces globally so types are available throughout UWP project
global using MarketDataCollector.Contracts.Api;
global using MarketDataCollector.Contracts.Archive;
global using MarketDataCollector.Contracts.Backfill;
global using MarketDataCollector.Contracts.Credentials;
global using MarketDataCollector.Contracts.Export;
global using MarketDataCollector.Contracts.Manifest;
global using MarketDataCollector.Contracts.Pipeline;
global using MarketDataCollector.Contracts.Schema;
global using MarketDataCollector.Contracts.Session;

// Domain namespaces (Models, Events, Enums)
global using MarketDataCollector.Contracts.Domain;
global using MarketDataCollector.Contracts.Domain.Models;
global using MarketDataCollector.Contracts.Domain.Events;
global using MarketDataCollector.Contracts.Domain.Enums;

// Configuration type aliases (Dto suffix -> non-Dto names for backwards compatibility)
global using AppConfig = MarketDataCollector.Contracts.Configuration.AppConfigDto;
global using AlpacaOptions = MarketDataCollector.Contracts.Configuration.AlpacaOptionsDto;
global using StorageConfig = MarketDataCollector.Contracts.Configuration.StorageConfigDto;
global using SymbolConfig = MarketDataCollector.Contracts.Configuration.SymbolConfigDto;
global using BackfillConfig = MarketDataCollector.Contracts.Configuration.BackfillConfigDto;
global using DataSourcesConfig = MarketDataCollector.Contracts.Configuration.DataSourcesConfigDto;
global using DataSourceConfig = MarketDataCollector.Contracts.Configuration.DataSourceConfigDto;
global using PolygonOptions = MarketDataCollector.Contracts.Configuration.PolygonOptionsDto;
global using IBOptions = MarketDataCollector.Contracts.Configuration.IBOptionsDto;
global using SymbolGroupsConfig = MarketDataCollector.Contracts.Configuration.SymbolGroupsConfigDto;
global using SymbolGroup = MarketDataCollector.Contracts.Configuration.SymbolGroupDto;
global using SmartGroupCriteria = MarketDataCollector.Contracts.Configuration.SmartGroupCriteriaDto;
global using ExtendedSymbolConfig = MarketDataCollector.Contracts.Configuration.ExtendedSymbolConfigDto;
global using AppSettings = MarketDataCollector.Contracts.Configuration.AppSettingsDto;
global using DerivativesConfig = MarketDataCollector.Contracts.Configuration.DerivativesConfigDto;
global using IndexOptionsConfig = MarketDataCollector.Contracts.Configuration.IndexOptionsConfigDto;
