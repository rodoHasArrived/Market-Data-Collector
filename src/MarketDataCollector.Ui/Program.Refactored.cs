using System.Text.Json;
using MarketDataCollector.Ui.Endpoints;
using MarketDataCollector.Ui.Services;
using MarketDataCollector.Ui.Templates;

// ============================================================
// Refactored Program.cs - Clean architecture with separation of concerns
// ============================================================
//
// This file demonstrates the refactored structure:
// - Services: ConfigStore, BackfillCoordinator (in Services/)
// - DTOs: All request/response records (in Models/DashboardDtos.cs)
// - Endpoints: API routes organized by domain (in Endpoints/)
// - Templates: HTML/CSS/JS templates (in Templates/)
//
// To use this refactored version, rename this file to Program.cs
// ============================================================

// Cached JSON serializer options to avoid repeated allocations
var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = false
};

var jsonOptionsIndented = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
};

var builder = WebApplication.CreateBuilder(args);

// Register services
builder.Services.AddSingleton<ConfigStore>();
builder.Services.AddSingleton<BackfillCoordinator>();

var app = builder.Build();

// ============================================================
// Dashboard Views
// ============================================================

app.MapGet("/", (ConfigStore store) =>
{
    var html = DashboardTemplate.Index(store.ConfigPath, store.GetStatusPath(), store.GetBackfillStatusPath());
    return Results.Content(html, "text/html");
});

app.MapGet("/providers", (ConfigStore store) =>
{
    var html = DashboardTemplate.ProvidersView(store.ConfigPath);
    return Results.Content(html, "text/html");
});

// ============================================================
// API Endpoints - Organized by Domain
// ============================================================

// Configuration endpoints (/api/config/*)
app.MapConfigEndpoints(jsonOptions);

// Provider and data source endpoints (/api/providers/*, /api/config/datasources/*)
app.MapProviderEndpoints(jsonOptions);

// Backfill endpoints (/api/backfill/*)
app.MapBackfillEndpoints(jsonOptions, jsonOptionsIndented);

// Failover endpoints (/api/failover/*)
app.MapFailoverEndpoints(jsonOptions);

// Symbol mapping endpoints (/api/symbols/*)
app.MapSymbolMappingEndpoints(jsonOptions);

app.Run();
