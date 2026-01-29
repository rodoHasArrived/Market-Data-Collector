using MarketDataCollector.Ui.Shared.Endpoints;

// ═══════════════════════════════════════════════════════════════════════════
// MarketDataCollector.Ui - Thin Web Dashboard Host
// ═══════════════════════════════════════════════════════════════════════════
// This is a minimal host shell that uses the shared UI module.
// All endpoint logic, services, and templates are in MarketDataCollector.Ui.Shared.
// ═══════════════════════════════════════════════════════════════════════════

var builder = WebApplication.CreateBuilder(args);

// Register shared UI services
builder.Services.AddUiSharedServices();

var app = builder.Build();

// Map all UI endpoints (dashboard + API routes)
app.MapAllUiEndpoints();

app.Run();
