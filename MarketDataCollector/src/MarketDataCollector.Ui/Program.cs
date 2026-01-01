using System.Text.Json;
using MarketDataCollector.Application.Backfill;
using MarketDataCollector.Application.Config;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Application.Monitoring;
using MarketDataCollector.Application.Pipeline;
using MarketDataCollector.Infrastructure.Providers.Backfill;
using MarketDataCollector.Storage;
using MarketDataCollector.Storage.Policies;
using MarketDataCollector.Storage.Sinks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ConfigStore>();
builder.Services.AddSingleton<BackfillCoordinator>();

var app = builder.Build();

app.MapGet("/", (ConfigStore store) =>
{
    var html = HtmlTemplates.Index(store.ConfigPath, store.GetStatusPath(), store.GetBackfillStatusPath());
    return Results.Content(html, "text/html");
});

app.MapGet("/api/config", (ConfigStore store) =>
{
    var cfg = store.Load();
    return Results.Json(new
    {
        dataRoot = cfg.DataRoot,
        compress = cfg.Compress,
        dataSource = cfg.DataSource.ToString(),
        alpaca = cfg.Alpaca,
        storage = cfg.Storage,
        symbols = cfg.Symbols ?? Array.Empty<SymbolConfig>(),
        backfill = cfg.Backfill
    }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
});

app.MapPost("/api/config/datasource", async (ConfigStore store, DataSourceRequest req) =>
{
    var cfg = store.Load();

    if (!Enum.TryParse<DataSourceKind>(req.DataSource, ignoreCase: true, out var ds))
        return Results.BadRequest("Invalid DataSource. Use 'IB' or 'Alpaca'.");

    var next = cfg with { DataSource = ds };
    await store.SaveAsync(next);

    return Results.Ok();
});

app.MapPost("/api/config/alpaca", async (ConfigStore store, AlpacaOptions alpaca) =>
{
    var cfg = store.Load();
    var next = cfg with { Alpaca = alpaca };
    await store.SaveAsync(next);
    return Results.Ok();
});

app.MapPost("/api/config/storage", async (ConfigStore store, StorageSettingsRequest req) =>
{
    var cfg = store.Load();
    var storage = new StorageConfig(
        NamingConvention: req.NamingConvention ?? "BySymbol",
        DatePartition: req.DatePartition ?? "Daily",
        IncludeProvider: req.IncludeProvider,
        FilePrefix: string.IsNullOrWhiteSpace(req.FilePrefix) ? null : req.FilePrefix
    );
    var next = cfg with
    {
        DataRoot = string.IsNullOrWhiteSpace(req.DataRoot) ? "data" : req.DataRoot,
        Compress = req.Compress,
        Storage = storage
    };
    await store.SaveAsync(next);
    return Results.Ok();
});

app.MapPost("/api/config/symbols", async (ConfigStore store, SymbolConfig symbol) =>
{
    if (string.IsNullOrWhiteSpace(symbol.Symbol))
        return Results.BadRequest("Symbol is required.");

    var cfg = store.Load();

    var list = (cfg.Symbols ?? Array.Empty<SymbolConfig>()).ToList();
    var idx = list.FindIndex(s => string.Equals(s.Symbol, symbol.Symbol, StringComparison.OrdinalIgnoreCase));
    if (idx >= 0) list[idx] = symbol;
    else list.Add(symbol);

    var next = cfg with { Symbols = list.ToArray() };
    await store.SaveAsync(next);

    return Results.Ok();
});

app.MapDelete("/api/config/symbols/{symbol}", async (ConfigStore store, string symbol) =>
{
    var cfg = store.Load();
    var list = (cfg.Symbols ?? Array.Empty<SymbolConfig>()).ToList();
    list.RemoveAll(s => string.Equals(s.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
    var next = cfg with { Symbols = list.ToArray() };
    await store.SaveAsync(next);
    return Results.Ok();
});

app.MapGet("/api/status", (ConfigStore store) =>
{
    var status = store.TryLoadStatusJson();
    return status is null ? Results.NotFound() : Results.Content(status, "application/json");
});

app.MapGet("/api/backfill/providers", (BackfillCoordinator backfill) =>
{
    var providers = backfill.DescribeProviders();
    return Results.Json(providers, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
});

app.MapGet("/api/backfill/status", (BackfillCoordinator backfill) =>
{
    var status = backfill.TryReadLast();
    return status is null
        ? Results.NotFound()
        : Results.Json(status, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });
});

app.MapPost("/api/backfill/run", async (BackfillCoordinator backfill, BackfillRequestDto req) =>
{
    if (req.Symbols is null || req.Symbols.Length == 0)
        return Results.BadRequest("At least one symbol is required.");

    try
    {
        var request = new BackfillRequest(
            string.IsNullOrWhiteSpace(req.Provider) ? "stooq" : req.Provider!,
            req.Symbols,
            req.From,
            req.To);

        var result = await backfill.RunAsync(request);
        return Results.Json(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

app.Run();

public record DataSourceRequest(string DataSource);
public record StorageSettingsRequest(string? DataRoot, bool Compress, string? NamingConvention, string? DatePartition, bool IncludeProvider, string? FilePrefix);

public static class HtmlTemplates
{
    public static string Index(string configPath, string statusPath, string backfillPath) => $@"
<!doctype html>
<html>
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width,initial-scale=1"" />
  <title>Market Data Collector Dashboard</title>
  <style>
    body {{ font-family: system-ui, -apple-system, Segoe UI, Roboto, Arial, sans-serif; margin: 24px; background: #f8f9fa; }}
    h2 {{ color: #333; margin-bottom: 8px; }}
    .row {{ display:flex; gap:24px; flex-wrap: wrap; margin-bottom: 24px; }}
    .card {{ border:1px solid #ddd; border-radius:10px; padding:16px; min-width: 320px; background: white; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }}
    table {{ border-collapse: collapse; width: 100%; }}
    th, td {{ border-bottom:1px solid #eee; padding:8px; text-align:left; font-size: 14px; }}
    th {{ font-weight: 600; background: #f8f9fa; }}
    input, select {{ padding:8px; font-size:14px; width: 100%; box-sizing:border-box; border: 1px solid #ccc; border-radius: 4px; }}
    button {{ padding:10px 16px; font-size:14px; cursor:pointer; border-radius: 4px; border: none; }}
    .btn-primary {{ background: #0066cc; color: white; }}
    .btn-primary:hover {{ background: #0052a3; }}
    .btn-danger {{ background: #dc3545; color: white; padding: 4px 8px; font-size: 12px; }}
    .btn-danger:hover {{ background: #c82333; }}
    .muted {{ color:#666; font-size: 12px; }}
    .bad {{ color:#c00; font-weight: 600; }}
    .good {{ color:#080; font-weight: 600; }}
    .tag {{ display: inline-block; padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 600; }}
    .tag-ib {{ background: #e3f2fd; color: #1565c0; }}
    .tag-alpaca {{ background: #fff3e0; color: #e65100; }}
    .provider-section {{ border-top: 1px solid #eee; margin-top: 16px; padding-top: 16px; }}
    .hidden {{ display: none; }}
    .form-group {{ margin-bottom: 12px; }}
    .form-group label {{ display: block; margin-bottom: 4px; font-weight: 500; color: #555; }}
    .form-row {{ display: flex; gap: 12px; }}
    .form-row > div {{ flex: 1; }}
    .provider-badge {{ font-size: 11px; padding: 2px 6px; border-radius: 3px; margin-left: 8px; }}
    .ib-only {{ background: #e3f2fd; color: #1565c0; }}
    .alpaca-only {{ background: #fff3e0; color: #e65100; }}
  </style>
</head>
<body>
  <h2>Market Data Collector Dashboard</h2>
  <div class=""muted"" style=""margin-bottom: 16px;"">Config: <code>{Escape(configPath)}</code> &bull; Status: <code>{Escape(statusPath)}</code> &bull; Backfill: <code>{Escape(backfillPath)}</code></div>

  <div class=""row"">
    <!-- Status Panel -->
    <div class=""card"" style=""flex:1; min-width: 280px;"">
      <h3>Status</h3>
      <div id=""statusBox"" class=""muted"">Loading...</div>
    </div>

    <!-- Data Source Panel -->
    <div class=""card"" style=""flex:1; min-width: 280px;"">
      <h3>Data Provider</h3>
      <div class=""form-group"">
        <label>Active Provider</label>
        <select id=""dataSource"" onchange=""updateDataSource()"">
          <option value=""IB"">Interactive Brokers (IB)</option>
          <option value=""Alpaca"">Alpaca</option>
        </select>
      </div>
      <div id=""providerStatus"" class=""muted""></div>

      <!-- Alpaca Settings -->
      <div id=""alpacaSettings"" class=""provider-section hidden"">
        <h4>Alpaca Settings</h4>
        <div class=""form-group"">
          <label>API Key ID</label>
          <input id=""alpacaKeyId"" type=""text"" placeholder=""Your Alpaca Key ID"" />
        </div>
        <div class=""form-group"">
          <label>Secret Key</label>
          <input id=""alpacaSecretKey"" type=""password"" placeholder=""Your Alpaca Secret Key"" />
        </div>
        <div class=""form-row"">
          <div class=""form-group"">
            <label>Feed</label>
            <select id=""alpacaFeed"">
              <option value=""iex"">IEX (free)</option>
              <option value=""sip"">SIP (paid)</option>
              <option value=""delayed_sip"">Delayed SIP</option>
            </select>
          </div>
          <div class=""form-group"">
            <label>Environment</label>
            <select id=""alpacaSandbox"">
              <option value=""false"">Production</option>
              <option value=""true"">Sandbox</option>
            </select>
          </div>
        </div>
        <div class=""form-group"">
          <label><input type=""checkbox"" id=""alpacaSubscribeQuotes"" /> Subscribe to Quotes (BBO)</label>
        </div>
        <button class=""btn-primary"" onclick=""saveAlpacaSettings()"">Save Alpaca Settings</button>
        <div id=""alpacaMsg"" class=""muted"" style=""margin-top: 8px;""></div>
      </div>
    </div>
  </div>

  <div class=""row"">
    <!-- Storage Settings Panel -->
    <div class=""card"" style=""flex:1; min-width: 400px;"">
      <h3>Storage Settings</h3>
      <div class=""form-row"">
        <div class=""form-group"" style=""flex: 2"">
          <label>Data Root Path</label>
          <input id=""dataRoot"" type=""text"" placeholder=""data"" />
        </div>
        <div class=""form-group"" style=""flex: 1"">
          <label>Compression</label>
          <select id=""compress"">
            <option value=""false"">Disabled</option>
            <option value=""true"">Enabled (gzip)</option>
          </select>
        </div>
      </div>
      <div class=""form-row"">
        <div class=""form-group"">
          <label>Naming Convention</label>
          <select id=""namingConvention"">
            <option value=""Flat"">Flat ({'{'}root{'}'}/{'{'}symbol{'}'}_{'{'}type{'}'}_{'{'}date{'}'}.jsonl)</option>
            <option value=""BySymbol"" selected>By Symbol ({'{'}root{'}'}/{'{'}symbol{'}'}/{'{'}type{'}'}/{'{'}date{'}'}.jsonl)</option>
            <option value=""ByDate"">By Date ({'{'}root{'}'}/{'{'}date{'}'}/{'{'}symbol{'}'}/{'{'}type{'}'}.jsonl)</option>
            <option value=""ByType"">By Type ({'{'}root{'}'}/{'{'}type{'}'}/{'{'}symbol{'}'}/{'{'}date{'}'}.jsonl)</option>
          </select>
        </div>
        <div class=""form-group"">
          <label>Date Partitioning</label>
          <select id=""datePartition"">
            <option value=""None"">None (single file)</option>
            <option value=""Daily"" selected>Daily (yyyy-MM-dd)</option>
            <option value=""Hourly"">Hourly (yyyy-MM-dd_HH)</option>
            <option value=""Monthly"">Monthly (yyyy-MM)</option>
          </select>
        </div>
      </div>
      <div class=""form-row"">
        <div class=""form-group"">
          <label>File Prefix (optional)</label>
          <input id=""filePrefix"" type=""text"" placeholder=""e.g., market_"" />
        </div>
        <div class=""form-group"">
          <label>Include Provider in Path</label>
          <select id=""includeProvider"">
            <option value=""false"" selected>No</option>
            <option value=""true"">Yes</option>
          </select>
        </div>
      </div>
      <div id=""storagePreview"" class=""muted"" style=""margin: 12px 0; padding: 8px; background: #f8f9fa; border-radius: 4px;"">
        Example path: <code id=""previewPath"">data/AAPL/Trade/2024-01-15.jsonl</code>
      </div>
      <button class=""btn-primary"" onclick=""saveStorageSettings()"">Save Storage Settings</button>
      <div id=""storageMsg"" class=""muted"" style=""margin-top: 8px;""></div>
    </div>
  </div>

  <div class=""row"">
    <div class=""card"" style=""flex:1; min-width: 400px;"">
      <h3>Historical Backfill</h3>
      <div id=""backfillHelp"" class=""muted"" style=""margin-bottom: 8px;"">Download free end-of-day bars to backfill gaps.</div>
      <div class=""form-row"">
        <div class=""form-group"">
          <label>Provider</label>
          <select id=""backfillProvider""></select>
        </div>
        <div class=""form-group"">
          <label>Symbols (comma separated)</label>
          <input id=""backfillSymbols"" placeholder=""AAPL,MSFT"" />
        </div>
      </div>
      <div class=""form-row"">
        <div class=""form-group"">
          <label>From (UTC)</label>
          <input id=""backfillFrom"" type=""date"" />
        </div>
        <div class=""form-group"">
          <label>To (UTC)</label>
          <input id=""backfillTo"" type=""date"" />
        </div>
      </div>
      <button class=""btn-primary"" onclick=""runBackfill()"">Start Backfill</button>
      <div id=""backfillStatus"" class=""muted"" style=""margin-top: 8px;"">No backfill started yet.</div>
    </div>
  </div>

  <div class=""row"">
    <!-- Symbols Panel -->
    <div class=""card"" style=""flex:1"">
      <h3>Subscribed Symbols</h3>
      <table id=""symbolsTable"">
        <thead>
          <tr>
            <th>Symbol</th>
            <th>Trades</th>
            <th>Depth</th>
            <th>Levels</th>
            <th>LocalSymbol <span class=""provider-badge ib-only"">IB</span></th>
            <th>Exchange <span class=""provider-badge ib-only"">IB</span></th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody></tbody>
      </table>

      <h4 style=""margin-top:24px"">Add Symbol</h4>
      <div class=""form-row"">
        <div class=""form-group"">
          <label>Symbol *</label>
          <input id=""sym"" placeholder=""AAPL"" />
        </div>
        <div class=""form-group"">
          <label>Subscribe Trades</label>
          <select id=""trades"">
            <option value=""true"" selected>Yes</option>
            <option value=""false"">No</option>
          </select>
        </div>
        <div class=""form-group"">
          <label>Subscribe Depth</label>
          <select id=""depth"">
            <option value=""true"">Yes</option>
            <option value=""false"" selected>No</option>
          </select>
        </div>
        <div class=""form-group"">
          <label>Depth Levels</label>
          <input id=""levels"" value=""10"" type=""number"" />
        </div>
      </div>

      <!-- IB-specific fields -->
      <div id=""ibFields"">
        <div class=""muted"" style=""margin: 12px 0 8px 0;"">IB-specific options <span class=""provider-badge ib-only"">IB only</span></div>
        <div class=""form-row"">
          <div class=""form-group"">
            <label>LocalSymbol</label>
            <input id=""localsym"" placeholder=""PCG PRA (for preferreds)"" />
          </div>
          <div class=""form-group"">
            <label>Exchange</label>
            <input id=""exch"" value=""SMART"" />
          </div>
          <div class=""form-group"">
            <label>Primary Exchange</label>
            <input id=""pexch"" placeholder=""NYSE"" />
          </div>
        </div>
      </div>

      <div style=""margin-top: 16px;"">
        <button class=""btn-primary"" onclick=""addSymbol()"">Add Symbol</button>
      </div>

      <div id=""msg"" class=""muted"" style=""margin-top:10px""></div>
    </div>
  </div>

<script>
let currentDataSource = 'IB';
let backfillProviders = [];

async function loadBackfillProviders(selectedProvider) {
  try {
    const r = await fetch('/api/backfill/providers');
    if (!r.ok) return;
    backfillProviders = await r.json();
    const select = document.getElementById('backfillProvider');
    if (!select) return;
    select.innerHTML = '';
    for (const p of backfillProviders) {
      const opt = document.createElement('option');
      opt.value = p.name;
      opt.textContent = p.displayName || p.name;
      select.appendChild(opt);
    }
    if (selectedProvider) {
      select.value = selectedProvider;
    }
    const help = document.getElementById('backfillHelp');
    if (help && backfillProviders.length) {
      help.textContent = backfillProviders.map(p => `${p.displayName || p.name}: ${p.description || ''}`).join(' • ');
    }
  } catch (e) {
    console.warn('Unable to load backfill providers', e);
  }
}

async function loadConfig() {{
  const r = await fetch('/api/config');
  const cfg = await r.json();

  // Update data source selector
  currentDataSource = cfg.dataSource || 'IB';
  document.getElementById('dataSource').value = currentDataSource;
  updateProviderUI();

  // Load Alpaca settings if available
  if (cfg.alpaca) {{
    document.getElementById('alpacaKeyId').value = cfg.alpaca.keyId || '';
    document.getElementById('alpacaSecretKey').value = cfg.alpaca.secretKey || '';
    document.getElementById('alpacaFeed').value = cfg.alpaca.feed || 'iex';
    document.getElementById('alpacaSandbox').value = cfg.alpaca.useSandbox ? 'true' : 'false';
    document.getElementById('alpacaSubscribeQuotes').checked = cfg.alpaca.subscribeQuotes || false;
  }}

  // Load storage settings
  document.getElementById('dataRoot').value = cfg.dataRoot || 'data';
  document.getElementById('compress').value = cfg.compress ? 'true' : 'false';
  if (cfg.storage) {{
    document.getElementById('namingConvention').value = cfg.storage.namingConvention || 'BySymbol';
    document.getElementById('datePartition').value = cfg.storage.datePartition || 'Daily';
    document.getElementById('includeProvider').value = cfg.storage.includeProvider ? 'true' : 'false';
    document.getElementById('filePrefix').value = cfg.storage.filePrefix || '';
  }}
  updateStoragePreview();

  await loadBackfillProviders(cfg.backfill ? cfg.backfill.provider : null);
  if (cfg.backfill) {{
    if (cfg.backfill.symbols) document.getElementById('backfillSymbols').value = cfg.backfill.symbols.join(',');
    if (cfg.backfill.from) document.getElementById('backfillFrom').value = cfg.backfill.from;
    if (cfg.backfill.to) document.getElementById('backfillTo').value = cfg.backfill.to;
    if (cfg.backfill.provider) document.getElementById('backfillProvider').value = cfg.backfill.provider;
  }}

  // Update symbols table
  const tbody = document.querySelector('#symbolsTable tbody');
  tbody.innerHTML = '';
  for (const s of (cfg.symbols || [])) {{
    const tr = document.createElement('tr');
    tr.innerHTML = `
      <td><b>${{s.symbol}}</b></td>
      <td>${{s.subscribeTrades ? '<span class=""good"">Yes</span>' : 'No'}}</td>
      <td>${{s.subscribeDepth ? '<span class=""good"">Yes</span>' : 'No'}}</td>
      <td>${{s.depthLevels || 10}}</td>
      <td>${{s.localSymbol || '-'}}</td>
      <td>${{s.exchange || '-'}}</td>
      <td><button class=""btn-danger"" onclick=""deleteSymbol('${{s.symbol}}')"">Delete</button></td>
    `;
    tbody.appendChild(tr);
  }}
}}

function updateProviderUI() {{
  const isAlpaca = currentDataSource === 'Alpaca';

  // Show/hide Alpaca settings
  document.getElementById('alpacaSettings').classList.toggle('hidden', !isAlpaca);

  // Show/hide IB-specific fields
  document.getElementById('ibFields').classList.toggle('hidden', isAlpaca);

  // Update provider status message
  const statusDiv = document.getElementById('providerStatus');
  if (isAlpaca) {{
    statusDiv.innerHTML = '<span class=""tag tag-alpaca"">Alpaca</span> WebSocket streaming for trades and quotes';
  }} else {{
    statusDiv.innerHTML = '<span class=""tag tag-ib"">Interactive Brokers</span> TWS/Gateway connection for L2 depth and trades';
  }}
}}

async function updateDataSource() {{
  const ds = document.getElementById('dataSource').value;
  currentDataSource = ds;
  updateProviderUI();

  const r = await fetch('/api/config/datasource', {{
    method: 'POST',
    headers: {{'Content-Type': 'application/json'}},
    body: JSON.stringify({{ dataSource: ds }})
  }});

  if (r.ok) {{
    document.getElementById('msg').textContent = 'Data source updated. Restart collector to apply changes.';
  }}
}}

async function saveAlpacaSettings() {{
  const payload = {{
    keyId: document.getElementById('alpacaKeyId').value,
    secretKey: document.getElementById('alpacaSecretKey').value,
    feed: document.getElementById('alpacaFeed').value,
    useSandbox: document.getElementById('alpacaSandbox').value === 'true',
    subscribeQuotes: document.getElementById('alpacaSubscribeQuotes').checked
  }};

  const r = await fetch('/api/config/alpaca', {{
    method: 'POST',
    headers: {{'Content-Type': 'application/json'}},
    body: JSON.stringify(payload)
  }});

  const msgDiv = document.getElementById('alpacaMsg');
  msgDiv.textContent = r.ok ? 'Alpaca settings saved. Restart collector to apply.' : 'Error saving settings.';
}}

async function saveStorageSettings() {{
  const payload = {{
    dataRoot: document.getElementById('dataRoot').value,
    compress: document.getElementById('compress').value === 'true',
    namingConvention: document.getElementById('namingConvention').value,
    datePartition: document.getElementById('datePartition').value,
    includeProvider: document.getElementById('includeProvider').value === 'true',
    filePrefix: document.getElementById('filePrefix').value
  }};

  const r = await fetch('/api/config/storage', {{
    method: 'POST',
    headers: {{'Content-Type': 'application/json'}},
    body: JSON.stringify(payload)
  }});

  const msgDiv = document.getElementById('storageMsg');
  msgDiv.textContent = r.ok ? 'Storage settings saved. Restart collector to apply.' : 'Error saving settings.';
}}

function updateStoragePreview() {{
  const root = document.getElementById('dataRoot').value || 'data';
  const compress = document.getElementById('compress').value === 'true';
  const naming = document.getElementById('namingConvention').value;
  const partition = document.getElementById('datePartition').value;
  const prefix = document.getElementById('filePrefix').value;
  const ext = compress ? '.jsonl.gz' : '.jsonl';
  const pfx = prefix ? prefix + '_' : '';

  let dateStr = '';
  if (partition === 'Daily') dateStr = '2024-01-15';
  else if (partition === 'Hourly') dateStr = '2024-01-15_14';
  else if (partition === 'Monthly') dateStr = '2024-01';

  let path = '';
  if (naming === 'Flat') {{
    path = dateStr ? `${{root}}/${{pfx}}AAPL_Trade_${{dateStr}}${{ext}}` : `${{root}}/${{pfx}}AAPL_Trade${{ext}}`;
  }} else if (naming === 'BySymbol') {{
    path = dateStr ? `${{root}}/AAPL/Trade/${{pfx}}${{dateStr}}${{ext}}` : `${{root}}/AAPL/Trade/${{pfx}}data${{ext}}`;
  }} else if (naming === 'ByDate') {{
    path = dateStr ? `${{root}}/${{dateStr}}/AAPL/${{pfx}}Trade${{ext}}` : `${{root}}/AAPL/${{pfx}}Trade${{ext}}`;
  }} else if (naming === 'ByType') {{
    path = dateStr ? `${{root}}/Trade/AAPL/${{pfx}}${{dateStr}}${{ext}}` : `${{root}}/Trade/AAPL/${{pfx}}data${{ext}}`;
  }}

  document.getElementById('previewPath').textContent = path;
}}

// Add event listeners for live preview updates
['dataRoot', 'compress', 'namingConvention', 'datePartition', 'filePrefix'].forEach(id => {{
  document.getElementById(id).addEventListener('change', updateStoragePreview);
  document.getElementById(id).addEventListener('input', updateStoragePreview);
}});

async function loadStatus() {{
  const box = document.getElementById('statusBox');
  try {{
    const r = await fetch('/api/status');
    if (!r.ok) {{
      box.innerHTML = '<span class=""bad"">No status</span> (start collector with --serve-status)';
      return;
    }}
    const s = await r.json();
    const isConnected = s.isConnected !== false;
    box.innerHTML = `
      <div>Last update: <b>${{s.timestampUtc || 'n/a'}}</b></div>
      <div>Connection: ${{isConnected ? '<span class=""good"">Connected</span>' : '<span class=""bad"">Disconnected</span>'}}</div>
      <div style=""margin-top: 8px;"">
        Published: <b>${{(s.metrics && s.metrics.published) || 0}}</b> &bull;
        Dropped: <b>${{(s.metrics && s.metrics.dropped) || 0}}</b> &bull;
        Integrity: <b>${{(s.metrics && s.metrics.integrity) || 0}}</b> &bull;
        Historical bars: <b>${{(s.metrics && s.metrics.historicalBars) || 0}}</b>
      </div>`;
  }} catch (e) {{
    box.innerHTML = '<span class=""bad"">No status</span>';
  }}
}}

async function loadBackfillStatus() {{
  const box = document.getElementById('backfillStatus');
  try {{
    const r = await fetch('/api/backfill/status');
    if (!r.ok) {{
      box.textContent = 'No backfill runs yet.';
      return;
    }}
    const status = await r.json();
    box.innerHTML = formatBackfillStatus(status);
  }} catch (e) {{
    box.textContent = 'Unable to load backfill status.';
  }}
}}

function formatBackfillStatus(status) {{
  if (!status) return 'No backfill runs yet.';
  const started = status.startedUtc ? new Date(status.startedUtc).toLocaleString() : 'n/a';
  const completed = status.completedUtc ? new Date(status.completedUtc).toLocaleString() : 'n/a';
  const badge = status.success ? '<span class=""good"">Success</span>' : '<span class=""bad"">Failed</span>';
  const symbols = (status.symbols || []).join(', ');
  const error = status.error ? `<div class=""bad"">${{status.error}}</div>` : '';
  return `${{badge}} • Provider <b>${{status.provider}}</b> • Bars <b>${{status.barsWritten || 0}}</b><br/>Symbols: ${{symbols || 'n/a'}}<br/>Started: ${{started}}<br/>Completed: ${{completed}}${{error}}`;
}}

async function runBackfill() {{
  const statusBox = document.getElementById('backfillStatus');
  const provider = document.getElementById('backfillProvider').value || 'stooq';
  const symbols = (document.getElementById('backfillSymbols').value || '')
    .split(',')
    .map(s => s.trim())
    .filter(s => s);
  const from = document.getElementById('backfillFrom').value || null;
  const to = document.getElementById('backfillTo').value || null;

  if (!symbols.length) {{
    statusBox.textContent = 'Please enter at least one symbol to backfill.';
    return;
  }}

  statusBox.textContent = 'Starting backfill...';
  const payload = {{ provider, symbols, from, to }};
  const r = await fetch('/api/backfill/run', {{
    method: 'POST',
    headers: {{ 'Content-Type': 'application/json' }},
    body: JSON.stringify(payload)
  }});

  if (!r.ok) {{
    const msg = await r.text();
    statusBox.innerHTML = `<span class=""bad"">${{msg || 'Backfill failed'}}</span>`;
    return;
  }}

  const result = await r.json();
  statusBox.innerHTML = formatBackfillStatus(result);
  await loadBackfillStatus();
}}

async function addSymbol() {{
  const symbol = document.getElementById('sym').value.trim();
  if (!symbol) {{
    document.getElementById('msg').textContent = 'Symbol is required.';
    return;
  }}

  const payload = {{
    symbol: symbol,
    subscribeTrades: document.getElementById('trades').value === 'true',
    subscribeDepth: document.getElementById('depth').value === 'true',
    depthLevels: parseInt(document.getElementById('levels').value || '10', 10),
    securityType: 'STK',
    exchange: document.getElementById('exch').value || 'SMART',
    currency: 'USD',
    primaryExchange: document.getElementById('pexch').value || null,
    localSymbol: document.getElementById('localsym').value || null
  }};

  const r = await fetch('/api/config/symbols', {{
    method: 'POST',
    headers: {{'Content-Type': 'application/json'}},
    body: JSON.stringify(payload)
  }});

  const msg = document.getElementById('msg');
  msg.textContent = r.ok ? 'Symbol added. Changes apply on hot-reload or restart.' : 'Error adding symbol.';

  if (r.ok) {{
    document.getElementById('sym').value = '';
    document.getElementById('localsym').value = '';
    document.getElementById('pexch').value = '';
    await loadConfig();
  }}
}}

async function deleteSymbol(symbol) {{
  if (!confirm(`Delete symbol ${{symbol}}?`)) return;

  const r = await fetch(`/api/config/symbols/${{encodeURIComponent(symbol)}}`, {{
    method: 'DELETE'
  }});

  if (r.ok) {{
    await loadConfig();
  }}
}}

// Initial load
loadConfig();
loadStatus();
loadBackfillStatus();
setInterval(loadStatus, 2000);
setInterval(loadBackfillStatus, 5000);
</script>
</body>
</html>";

    private static string Escape(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}

public sealed class ConfigStore
{
    public string ConfigPath { get; }

    public ConfigStore()
    {
        // Config lives at solution root by convention.
        ConfigPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "appsettings.json"));
    }

    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return new AppConfig();
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json, AppConfigJsonOptions.Read) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public async Task SaveAsync(AppConfig cfg)
    {
        var json = JsonSerializer.Serialize(cfg, AppConfigJsonOptions.Write);
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        await File.WriteAllTextAsync(ConfigPath, json);
    }

    public string? TryLoadStatusJson()
    {
        try
        {
            var statusPath = GetStatusPath();
            return File.Exists(statusPath) ? File.ReadAllText(statusPath) : null;
        }
        catch
        {
            return null;
        }
    }

    public string GetStatusPath(AppConfig? cfg = null)
    {
        cfg ??= Load();
        var root = GetDataRoot(cfg);
        return Path.Combine(root, "_status", "status.json");
    }

    public string GetBackfillStatusPath(AppConfig? cfg = null)
    {
        cfg ??= Load();
        var root = GetDataRoot(cfg);
        return Path.Combine(root, "_status", "backfill.json");
    }

    public BackfillResult? TryLoadBackfillStatus()
    {
        var cfg = Load();
        var store = new BackfillStatusStore(GetDataRoot(cfg));
        return store.TryRead();
    }

    public string GetDataRoot(AppConfig? cfg = null)
    {
        cfg ??= Load();
        var root = string.IsNullOrWhiteSpace(cfg.DataRoot) ? "data" : cfg.DataRoot;
        var baseDir = Path.GetDirectoryName(ConfigPath)!;
        return Path.GetFullPath(Path.Combine(baseDir, root));
    }
}

public sealed class BackfillCoordinator
{
    private readonly ConfigStore _store;
    private readonly ILogger _log = LoggingSetup.ForContext<BackfillCoordinator>();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private BackfillResult? _lastRun;

    public BackfillCoordinator(ConfigStore store)
    {
        _store = store;
        _lastRun = store.TryLoadBackfillStatus();
    }

    public IEnumerable<object> DescribeProviders()
    {
        var service = CreateService();
        return service.Providers
            .Select(p => new { p.Name, p.DisplayName, p.Description });
    }

    public BackfillResult? TryReadLast() => _lastRun ?? _store.TryLoadBackfillStatus();

    public async Task<BackfillResult> RunAsync(BackfillRequest request, CancellationToken ct = default)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, ct).ConfigureAwait(false))
            throw new InvalidOperationException("A backfill is already running. Please try again after it completes.");

        try
        {
            var cfg = _store.Load();
            var storageOpt = cfg.Storage?.ToStorageOptions(cfg.DataRoot, cfg.Compress)
                ?? new StorageOptions
                {
                    RootPath = cfg.DataRoot,
                    Compress = cfg.Compress,
                    NamingConvention = FileNamingConvention.BySymbol,
                    DatePartition = DatePartition.Daily
                };

            var policy = new JsonlStoragePolicy(storageOpt);
            await using var sink = new JsonlStorageSink(storageOpt, policy);
            await using var pipeline = new EventPipeline(sink, capacity: 20_000, enablePeriodicFlush: false);

            // Keep pipeline counters scoped per run
            Metrics.Reset();

            var service = CreateService();
            var result = await service.RunAsync(request, pipeline, ct).ConfigureAwait(false);

            var statusStore = new BackfillStatusStore(_store.GetDataRoot(cfg));
            await statusStore.WriteAsync(result).ConfigureAwait(false);
            _lastRun = result;
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    private HistoricalBackfillService CreateService()
    {
        var providers = new IHistoricalDataProvider[]
        {
            new StooqHistoricalDataProvider()
        };
        return new HistoricalBackfillService(providers, _log);
    }
}

public record BackfillRequestDto(string? Provider, string[] Symbols, DateOnly? From, DateOnly? To);
