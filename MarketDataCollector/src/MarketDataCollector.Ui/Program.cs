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
            symbols = cfg.Symbols ?? Array.Empty<SymbolConfig>()
    }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
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

app.MapGet("/api/status", (ConfigStore store) =>
{
    var status = store.TryLoadStatusJson();
    return status is null ? Results.NotFound() : Results.Content(status, "application/json");
});

app.Run();

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
    body {{ font-family: system-ui, -apple-system, Segoe UI, Roboto, Arial, sans-serif; margin: 24px; }}
    .row {{ display:flex; gap:24px; flex-wrap: wrap; }}
    .card {{ border:1px solid #ddd; border-radius:10px; padding:16px; min-width: 320px; }}
    table {{ border-collapse: collapse; width: 100%; }}
    th, td {{ border-bottom:1px solid #eee; padding:8px; text-align:left; font-size: 14px; }}
    th {{ font-weight: 600; }}
    input, select {{ padding:8px; font-size:14px; width: 100%; box-sizing:border-box; }}
    button {{ padding:10px 12px; font-size:14px; cursor:pointer; }}
    .muted {{ color:#666; font-size: 12px; }}
    .bad {{ color:#c00; font-weight: 600; }}
  </style>
</head>
<body>
  <h2>Market Data Collector Dashboard</h2>
  <div class=""muted"">Config file: <code>{Escape(configPath)}</code> &bull; Status file: <code>{Escape(statusPath)}</code></div>

  <div class=""row"">
    <div class=""card"" style=""flex:1"">
      <h3>Status</h3>
      <div id=""statusBox"" class=""muted"">Loading...</div>
    </div>

    <div class=""card"" style=""flex:2"">
      <h3>Symbols</h3>
      <table id=""symbolsTable"">
        <thead>
          <tr>
            <th>Symbol</th><th>Trades</th><th>Depth</th><th>Levels</th><th>LocalSymbol</th><th>Exchange</th>
          </tr>
        </thead>
        <tbody></tbody>
      </table>

      <h4 style=""margin-top:16px"">Add symbol</h4>
      <div class=""row"" style=""gap:12px"">
        <div style=""flex:1"">
          <label class=""muted"">Symbol</label>
          <input id=""sym"" placeholder=""PCG-PA"" />
        </div>
        <div style=""flex:1"">
          <label class=""muted"">LocalSymbol (preferreds)</label>
          <input id=""localsym"" placeholder=""PCG PRA"" />
        </div>
      </div>
      <div class=""row"" style=""gap:12px; margin-top: 8px;"">
        <div style=""flex:1"">
          <label class=""muted"">Exchange</label>
          <input id=""exch"" value=""SMART"" />
        </div>
        <div style=""flex:1"">
          <label class=""muted"">PrimaryExch</label>
          <input id=""pexch"" placeholder=""NYSE"" />
        </div>
        <div style=""flex:1"">
          <label class=""muted"">DepthLevels</label>
          <input id=""levels"" value=""10"" />
        </div>
      </div>
      <div class=""row"" style=""gap:12px; margin-top: 8px;"">
        <div style=""flex:1"">
          <label class=""muted"">SubscribeTrades</label>
          <select id=""trades"">
            <option value=""true"" selected>true</option>
            <option value=""false"">false</option>
          </select>
        </div>
        <div style=""flex:1"">
          <label class=""muted"">SubscribeDepth</label>
          <select id=""depth"">
            <option value=""true"" selected>true</option>
            <option value=""false"">false</option>
          </select>
        </div>
        <div style=""flex:1; align-self:end"">
          <button onclick=""addSymbol()"">Add + Save</button>
        </div>
      </div>

      <div id=""msg"" class=""muted"" style=""margin-top:10px""></div>
    </div>
  </div>

<script>
async function loadConfig(){{
  const r = await fetch('/api/config');
  const cfg = await r.json();
  const tbody = document.querySelector('#symbolsTable tbody');
  tbody.innerHTML = '';
  for(const s of (cfg.symbols || [])){{
    const tr = document.createElement('tr');
    tr.innerHTML = `
      <td><b>${{s.symbol}}</b></td>
      <td>${{s.subscribeTrades}}</td>
      <td>${{s.subscribeDepth}}</td>
      <td>${{s.depthLevels}}</td>
      <td>${{s.localSymbol || ''}}</td>
      <td>${{s.exchange || ''}}</td>
    `;
    tbody.appendChild(tr);
  }}
}}

async function loadStatus(){{
  const box = document.getElementById('statusBox');
  try {{
    const r = await fetch('/api/status');
    if(!r.ok){{ box.innerHTML = '<span class=\"bad\">No status</span> (start collector with --serve-status)'; return; }}
    const s = await r.json();
    box.innerHTML =
      `<div>Last update (UTC): <b>${{s.timestampUtc || 'n/a'}}</b></div>
       <div>Published: <b>${{(s.metrics && s.metrics.published) || 0}}</b>
       &bull; Dropped: <b>${{(s.metrics && s.metrics.dropped) || 0}}</b>
       &bull; Integrity: <b>${{(s.metrics && s.metrics.integrity) || 0}}</b></div>`;
  }} catch(e) {{
    box.innerHTML = '<span class=\"bad\">No status</span>';
  }}
}}

async function addSymbol(){{
  const payload = {{
    symbol: document.getElementById('sym').value,
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
  const text = await r.text();
  msg.textContent = r.ok ? 'Saved. Restart collector to apply (hot-reload comes next).' : ('Error: ' + text);

  await loadConfig();
}}

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
