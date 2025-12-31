using System.Text.Json;
using MarketDataCollector.Application.Config;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ConfigStore>();

var app = builder.Build();

app.MapGet("/", (ConfigStore store) =>
{
    var html = HtmlTemplates.Index(store.ConfigPath, store.StatusPath);
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
        symbols = cfg.Symbols ?? Array.Empty<SymbolConfig>()
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

app.Run();

public record DataSourceRequest(string DataSource);

public static class HtmlTemplates
{
    public static string Index(string configPath, string statusPath) => $@"
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
  <div class=""muted"" style=""margin-bottom: 16px;"">Config: <code>{Escape(configPath)}</code> &bull; Status: <code>{Escape(statusPath)}</code></div>

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
        Integrity: <b>${{(s.metrics && s.metrics.integrity) || 0}}</b>
      </div>`;
  }} catch (e) {{
    box.innerHTML = '<span class=""bad"">No status</span>';
  }}
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
setInterval(loadStatus, 2000);
</script>
</body>
</html>";

    private static string Escape(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}

public sealed class ConfigStore
{
    public string ConfigPath { get; }
    public string StatusPath { get; }

    public ConfigStore()
    {
        // Config lives at solution root by convention.
        ConfigPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "appsettings.json"));
        StatusPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "_status", "status.json"));
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
            return File.Exists(StatusPath) ? File.ReadAllText(StatusPath) : null;
        }
        catch
        {
            return null;
        }
    }
}
