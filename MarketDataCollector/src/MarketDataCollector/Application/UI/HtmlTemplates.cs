namespace MarketDataCollector.Application.UI;

public static class HtmlTemplates
{
    public static string Index(string configPath, string statusPath, string backfillPath) => $@"
<!doctype html>
<html lang=""en"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width,initial-scale=1"" />
  <title>Market Data Collector</title>
  <style>
    * {{ box-sizing: border-box; }}

    body {{
      font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, ""Helvetica Neue"", Arial, sans-serif;
      margin: 0;
      padding: 0;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      min-height: 100vh;
    }}

    .header {{
      background: white;
      padding: 16px 24px;
      box-shadow: 0 2px 4px rgba(0,0,0,0.1);
      display: flex;
      justify-content: space-between;
      align-items: center;
    }}

    .header h1 {{
      margin: 0;
      font-size: 24px;
      color: #333;
    }}

    .header-actions {{
      display: flex;
      gap: 12px;
    }}

    .btn-help {{
      background: #667eea;
      color: white;
      border: none;
      padding: 8px 16px;
      border-radius: 6px;
      cursor: pointer;
      font-size: 14px;
      display: flex;
      align-items: center;
      gap: 6px;
    }}

    .btn-help:hover {{
      background: #5a67d8;
    }}

    .container {{
      max-width: 1400px;
      margin: 0 auto;
      padding: 24px;
    }}

    .row {{
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
      gap: 24px;
      margin-bottom: 24px;
    }}

    .card {{
      background: white;
      border-radius: 12px;
      padding: 24px;
      box-shadow: 0 4px 6px rgba(0,0,0,0.1);
    }}

    .card h2 {{
      margin: 0 0 16px 0;
      color: #333;
      font-size: 20px;
      display: flex;
      align-items: center;
      gap: 8px;
    }}

    .card h3 {{
      margin: 0 0 12px 0;
      color: #555;
      font-size: 16px;
    }}

    table {{
      border-collapse: collapse;
      width: 100%;
      margin-top: 12px;
    }}

    th, td {{
      border-bottom: 1px solid #eee;
      padding: 12px 8px;
      text-align: left;
      font-size: 14px;
    }}

    th {{
      font-weight: 600;
      background: #f8f9fa;
      color: #555;
    }}

    .form-group {{
      margin-bottom: 16px;
    }}

    .form-group label {{
      display: flex;
      align-items: center;
      gap: 6px;
      margin-bottom: 6px;
      font-weight: 500;
      color: #555;
      font-size: 14px;
    }}

    .tooltip {{
      position: relative;
      display: inline-block;
      cursor: help;
      color: #667eea;
      font-size: 14px;
    }}

    .tooltip:hover::after {{
      content: attr(data-tip);
      position: absolute;
      left: 100%;
      top: 50%;
      transform: translateY(-50%);
      margin-left: 8px;
      background: #333;
      color: white;
      padding: 8px 12px;
      border-radius: 6px;
      font-size: 12px;
      white-space: nowrap;
      z-index: 1000;
      box-shadow: 0 2px 8px rgba(0,0,0,0.2);
    }}

    input, select, textarea {{
      padding: 10px 12px;
      font-size: 14px;
      width: 100%;
      border: 2px solid #e2e8f0;
      border-radius: 6px;
      transition: all 0.2s;
    }}

    input:focus, select:focus, textarea:focus {{
      outline: none;
      border-color: #667eea;
      box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.1);
    }}

    button {{
      padding: 10px 20px;
      font-size: 14px;
      font-weight: 500;
      cursor: pointer;
      border-radius: 6px;
      border: none;
      transition: all 0.2s;
    }}

    .btn-primary {{
      background: #667eea;
      color: white;
    }}

    .btn-primary:hover {{
      background: #5a67d8;
      transform: translateY(-1px);
      box-shadow: 0 4px 12px rgba(102, 126, 234, 0.4);
    }}

    .btn-danger {{
      background: #f56565;
      color: white;
      padding: 6px 12px;
      font-size: 12px;
    }}

    .btn-danger:hover {{
      background: #e53e3e;
    }}

    .btn-secondary {{
      background: #e2e8f0;
      color: #4a5568;
    }}

    .btn-secondary:hover {{
      background: #cbd5e0;
    }}

    .status-badge {{
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 4px 12px;
      border-radius: 12px;
      font-size: 12px;
      font-weight: 600;
    }}

    .status-connected {{
      background: #c6f6d5;
      color: #22543d;
    }}

    .status-disconnected {{
      background: #fed7d7;
      color: #742a2a;
    }}

    .status-dot {{
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: currentColor;
    }}

    .tag {{
      display: inline-block;
      padding: 4px 10px;
      border-radius: 4px;
      font-size: 11px;
      font-weight: 600;
    }}

    .tag-ib {{
      background: #bee3f8;
      color: #2c5282;
    }}

    .tag-alpaca {{
      background: #feebc8;
      color: #7c2d12;
    }}

    .hidden {{ display: none !important; }}

    .form-row {{
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 16px;
    }}

    .metric-grid {{
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(120px, 1fr));
      gap: 16px;
      margin-top: 16px;
    }}

    .metric {{
      text-align: center;
      padding: 12px;
      background: #f7fafc;
      border-radius: 8px;
    }}

    .metric-value {{
      font-size: 24px;
      font-weight: 700;
      color: #667eea;
    }}

    .metric-label {{
      font-size: 12px;
      color: #718096;
      margin-top: 4px;
    }}

    .toast-container {{
      position: fixed;
      top: 20px;
      right: 20px;
      z-index: 10000;
      display: flex;
      flex-direction: column;
      gap: 12px;
    }}

    .toast {{
      background: white;
      padding: 16px 20px;
      border-radius: 8px;
      box-shadow: 0 10px 40px rgba(0,0,0,0.2);
      min-width: 300px;
      max-width: 400px;
      display: flex;
      align-items: start;
      gap: 12px;
      animation: slideIn 0.3s ease;
    }}

    @keyframes slideIn {{
      from {{ transform: translateX(400px); opacity: 0; }}
      to {{ transform: translateX(0); opacity: 1; }}
    }}

    .toast-success {{
      border-left: 4px solid #48bb78;
    }}

    .toast-error {{
      border-left: 4px solid #f56565;
    }}

    .toast-info {{
      border-left: 4px solid #4299e1;
    }}

    .toast-icon {{
      font-size: 20px;
      flex-shrink: 0;
    }}

    .toast-content {{
      flex: 1;
    }}

    .toast-title {{
      font-weight: 600;
      margin-bottom: 4px;
      color: #2d3748;
    }}

    .toast-message {{
      font-size: 14px;
      color: #4a5568;
    }}

    .modal {{
      display: none;
      position: fixed;
      z-index: 9999;
      left: 0;
      top: 0;
      width: 100%;
      height: 100%;
      background: rgba(0,0,0,0.5);
      animation: fadeIn 0.2s;
    }}

    @keyframes fadeIn {{
      from {{ opacity: 0; }}
      to {{ opacity: 1; }}
    }}

    .modal-content {{
      background: white;
      margin: 80px auto;
      padding: 32px;
      border-radius: 12px;
      max-width: 800px;
      max-height: 80vh;
      overflow-y: auto;
      animation: slideDown 0.3s;
    }}

    @keyframes slideDown {{
      from {{ transform: translateY(-50px); opacity: 0; }}
      to {{ transform: translateY(0); opacity: 1; }}
    }}

    .modal-header {{
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 24px;
    }}

    .modal-header h2 {{
      margin: 0;
      color: #2d3748;
    }}

    .close {{
      font-size: 28px;
      font-weight: bold;
      color: #a0aec0;
      cursor: pointer;
      background: none;
      border: none;
      padding: 0;
      width: 32px;
      height: 32px;
    }}

    .close:hover {{
      color: #2d3748;
    }}

    .help-section {{
      margin-bottom: 24px;
    }}

    .help-section h3 {{
      color: #2d3748;
      margin-bottom: 12px;
      display: flex;
      align-items: center;
      gap: 8px;
    }}

    .help-section p {{
      color: #4a5568;
      line-height: 1.6;
      margin: 8px 0;
    }}

    .help-section code {{
      background: #f7fafc;
      padding: 2px 6px;
      border-radius: 3px;
      font-size: 13px;
      color: #e53e3e;
    }}

    .help-section ul {{
      margin: 8px 0;
      padding-left: 24px;
    }}

    .help-section li {{
      color: #4a5568;
      margin: 4px 0;
    }}

    .loading {{
      display: inline-block;
      width: 16px;
      height: 16px;
      border: 2px solid #f3f3f3;
      border-top: 2px solid #667eea;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }}

    @keyframes spin {{
      0% {{ transform: rotate(0deg); }}
      100% {{ transform: rotate(360deg); }}
    }}

    .preview-box {{
      background: #f7fafc;
      padding: 16px;
      border-radius: 8px;
      border: 2px dashed #cbd5e0;
      margin: 12px 0;
    }}

    .preview-box code {{
      color: #667eea;
      font-weight: 600;
    }}

    @media (max-width: 768px) {{
      .container {{
        padding: 16px;
      }}

      .header {{
        padding: 12px 16px;
      }}

      .header h1 {{
        font-size: 18px;
      }}

      .form-row {{
        grid-template-columns: 1fr;
      }}

      .metric-grid {{
        grid-template-columns: repeat(2, 1fr);
      }}

      .modal-content {{
        margin: 20px;
        padding: 20px;
        max-height: calc(100vh - 40px);
      }}

      .toast {{
        min-width: 250px;
        max-width: calc(100vw - 40px);
      }}
    }}
  </style>
</head>
<body>
  <div class=""header"">
    <h1>📊 Market Data Collector</h1>
    <div class=""header-actions"">
      <button class=""btn-help"" onclick=""openHelp()"">
        <span>❓</span> Help
      </button>
    </div>
  </div>

  <div class=""container"">
    <div class=""row"">
      <!-- Status Panel -->
      <div class=""card"">
        <h2>📡 System Status</h2>
        <div id=""statusBox"">
          <div class=""status-badge status-disconnected"">
            <span class=""status-dot""></span>
            Loading...
          </div>
        </div>
        <div class=""metric-grid"" id=""metricsGrid"">
          <div class=""metric"">
            <div class=""metric-value"" id=""metricPublished"">0</div>
            <div class=""metric-label"">Published</div>
          </div>
          <div class=""metric"">
            <div class=""metric-value"" id=""metricDropped"">0</div>
            <div class=""metric-label"">Dropped</div>
          </div>
          <div class=""metric"">
            <div class=""metric-value"" id=""metricIntegrity"">0</div>
            <div class=""metric-label"">Integrity</div>
          </div>
          <div class=""metric"">
            <div class=""metric-value"" id=""metricBars"">0</div>
            <div class=""metric-label"">Historical Bars</div>
          </div>
        </div>
      </div>

      <!-- Data Provider Panel -->
      <div class=""card"">
        <h2>🔌 Data Provider</h2>
        <div class=""form-group"">
          <label>
            Active Provider
            <span class=""tooltip"" data-tip=""Select your market data provider (IB or Alpaca)"">ℹ️</span>
          </label>
          <select id=""dataSource"" onchange=""updateDataSource()"">
            <option value=""IB"">Interactive Brokers (IB)</option>
            <option value=""Alpaca"">Alpaca</option>
          </select>
        </div>
        <div id=""providerStatus"" style=""margin-top: 12px;""></div>

        <!-- Alpaca Settings -->
        <div id=""alpacaSettings"" class=""hidden"" style=""margin-top: 20px; padding-top: 20px; border-top: 2px solid #f0f0f0;"">
          <h3>Alpaca Configuration</h3>
          <div class=""form-group"">
            <label>
              API Key ID
              <span class=""tooltip"" data-tip=""Your Alpaca API Key ID from dashboard"">ℹ️</span>
            </label>
            <input id=""alpacaKeyId"" type=""text"" placeholder=""PK..."" />
          </div>
          <div class=""form-group"">
            <label>
              Secret Key
              <span class=""tooltip"" data-tip=""Your Alpaca Secret Key (kept secure)"">ℹ️</span>
            </label>
            <input id=""alpacaSecretKey"" type=""password"" placeholder=""••••••"" />
          </div>
          <div class=""form-row"">
            <div class=""form-group"">
              <label>
                Feed Type
                <span class=""tooltip"" data-tip=""IEX (free), SIP (paid subscription required)"">ℹ️</span>
              </label>
              <select id=""alpacaFeed"">
                <option value=""iex"">IEX (Free)</option>
                <option value=""sip"">SIP (Paid)</option>
                <option value=""delayed_sip"">Delayed SIP</option>
              </select>
            </div>
            <div class=""form-group"">
              <label>Environment</label>
              <select id=""alpacaSandbox"">
                <option value=""false"">Production</option>
                <option value=""true"">Sandbox (Paper Trading)</option>
              </select>
            </div>
          </div>
          <div class=""form-group"">
            <label style=""display: flex; align-items: center; gap: 8px;"">
              <input type=""checkbox"" id=""alpacaSubscribeQuotes"" />
              Subscribe to Quotes (BBO)
            </label>
          </div>
          <button class=""btn-primary"" onclick=""saveAlpacaSettings()"">
            💾 Save Alpaca Settings
          </button>
        </div>
      </div>
    </div>

    <!-- Storage Settings -->
    <div class=""card"">
      <h2>💾 Storage Settings</h2>
      <div class=""form-row"">
        <div class=""form-group"" style=""grid-column: span 2;"">
          <label>
            Data Root Path
            <span class=""tooltip"" data-tip=""Directory where market data will be stored"">ℹ️</span>
          </label>
          <input id=""dataRoot"" type=""text"" placeholder=""data"" />
        </div>
        <div class=""form-group"">
          <label>
            Compression
            <span class=""tooltip"" data-tip=""Enable gzip compression to save disk space"">ℹ️</span>
          </label>
          <select id=""compress"">
            <option value=""false"">Disabled</option>
            <option value=""true"">Enabled (gzip)</option>
          </select>
        </div>
      </div>
      <div class=""form-row"">
        <div class=""form-group"">
          <label>
            Naming Convention
            <span class=""tooltip"" data-tip=""How files are organized in directories"">ℹ️</span>
          </label>
          <select id=""namingConvention"">
            <option value=""Flat"">Flat</option>
            <option value=""BySymbol"" selected>By Symbol</option>
            <option value=""ByDate"">By Date</option>
            <option value=""ByType"">By Type</option>
            <option value=""BySource"">By Source (alpaca/ib/...)</option>
            <option value=""ByAssetClass"">By Asset Class (equity/options/...)</option>
            <option value=""Hierarchical"">Hierarchical (source/asset/symbol/type)</option>
            <option value=""Canonical"">Canonical (year/month/day/source/symbol)</option>
          </select>
        </div>
        <div class=""form-group"">
          <label>
            Date Partitioning
            <span class=""tooltip"" data-tip=""How data is split across time periods"">ℹ️</span>
          </label>
          <select id=""datePartition"">
            <option value=""None"">None</option>
            <option value=""Daily"" selected>Daily</option>
            <option value=""Hourly"">Hourly</option>
            <option value=""Monthly"">Monthly</option>
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
      <div class=""preview-box"">
        <div style=""font-size: 12px; color: #718096; margin-bottom: 4px;"">Example file path:</div>
        <code id=""previewPath"">data/AAPL/Trade/2024-01-15.jsonl</code>
      </div>
      <button class=""btn-primary"" onclick=""saveStorageSettings()"">
        💾 Save Storage Settings
      </button>
    </div>

    <!-- Historical Backfill -->
    <div class=""card"">
      <h2>📅 Historical Backfill</h2>
      <p style=""color: #718096; font-size: 14px; margin: 0 0 16px 0;"">
        Download free end-of-day historical data from multiple providers with automatic failover.
      </p>

      <!-- Provider Health Status -->
      <div id=""providerHealthPanel"" style=""margin-bottom: 20px; padding: 16px; background: #f7fafc; border-radius: 8px;"">
        <h3 style=""margin: 0 0 12px 0; font-size: 14px; color: #4a5568;"">📊 Provider Status</h3>
        <div id=""providerHealthGrid"" style=""display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 12px;"">
          <div class=""provider-health-item"" style=""padding: 10px; background: white; border-radius: 6px; border-left: 3px solid #a0aec0;"">
            <span style=""font-weight: 600;"">Loading...</span>
          </div>
        </div>
      </div>

      <div class=""form-row"">
        <div class=""form-group"">
          <label>
            Provider
            <span class=""tooltip"" data-tip=""Select data source. 'Multi-Source' automatically tries multiple providers."">ℹ️</span>
          </label>
          <select id=""backfillProvider"">
            <option value=""composite"">Multi-Source (Auto-Failover)</option>
            <option value=""yahoo"">Yahoo Finance (Free)</option>
            <option value=""stooq"">Stooq (Free EOD)</option>
            <option value=""nasdaq"">Nasdaq Data Link (Quandl)</option>
          </select>
        </div>
        <div class=""form-group"">
          <label>
            Symbols
            <span class=""tooltip"" data-tip=""Comma-separated list (e.g., AAPL,MSFT,TSLA)"">ℹ️</span>
          </label>
          <input id=""backfillSymbols"" placeholder=""AAPL,MSFT,TSLA"" />
        </div>
      </div>
      <div class=""form-row"">
        <div class=""form-group"">
          <label>From Date (UTC)</label>
          <input id=""backfillFrom"" type=""date"" />
        </div>
        <div class=""form-group"">
          <label>To Date (UTC)</label>
          <input id=""backfillTo"" type=""date"" />
        </div>
      </div>

      <!-- Advanced Options -->
      <details style=""margin: 16px 0;"">
        <summary style=""cursor: pointer; font-weight: 500; color: #4a5568;"">⚙️ Advanced Options</summary>
        <div style=""padding: 16px; margin-top: 8px; background: #f7fafc; border-radius: 6px;"">
          <div class=""form-row"">
            <div class=""form-group"">
              <label style=""display: flex; align-items: center; gap: 8px;"">
                <input type=""checkbox"" id=""backfillEnableSymbolResolution"" checked />
                Enable Symbol Resolution (OpenFIGI)
              </label>
              <small style=""color: #718096;"">Automatically normalize symbols across providers</small>
            </div>
            <div class=""form-group"">
              <label style=""display: flex; align-items: center; gap: 8px;"">
                <input type=""checkbox"" id=""backfillPreferAdjusted"" checked />
                Prefer Adjusted Prices
              </label>
              <small style=""color: #718096;"">Use split/dividend adjusted prices when available</small>
            </div>
          </div>
          <div class=""form-row"">
            <div class=""form-group"">
              <label>Nasdaq Data Link API Key (optional)</label>
              <input id=""nasdaqApiKey"" type=""password"" placeholder=""Your Quandl/Nasdaq API key"" />
            </div>
            <div class=""form-group"">
              <label>OpenFIGI API Key (optional)</label>
              <input id=""openFigiApiKey"" type=""password"" placeholder=""Higher rate limits"" />
            </div>
          </div>
        </div>
      </details>

      <div style=""display: flex; gap: 12px; flex-wrap: wrap;"">
        <button class=""btn-primary"" onclick=""runBackfill()"" id=""btnStartBackfill"">
          ▶️ Start Backfill
        </button>
        <button class=""btn-secondary"" onclick=""checkProviderHealth()"">
          🔄 Check Provider Health
        </button>
        <button class=""btn-secondary"" onclick=""resolveSymbol()"">
          🔍 Resolve Symbol
        </button>
      </div>

      <!-- Progress Bar -->
      <div id=""backfillProgress"" class=""hidden"" style=""margin-top: 16px;"">
        <div style=""display: flex; justify-content: space-between; margin-bottom: 4px;"">
          <span id=""progressLabel"">Processing...</span>
          <span id=""progressPercent"">0%</span>
        </div>
        <div style=""height: 8px; background: #e2e8f0; border-radius: 4px; overflow: hidden;"">
          <div id=""progressBar"" style=""height: 100%; width: 0%; background: linear-gradient(90deg, #667eea, #764ba2); transition: width 0.3s;""></div>
        </div>
      </div>

      <!-- Status Display -->
      <div id=""backfillStatus"" style=""margin-top: 16px; padding: 12px; background: #f7fafc; border-radius: 6px; font-size: 14px;"">
        No backfill runs yet. Select a provider and symbols to get started.
      </div>
    </div>

    <!-- Symbol Resolution Modal -->
    <div id=""symbolResolveModal"" class=""modal"">
      <div class=""modal-content"" style=""max-width: 600px;"">
        <div class=""modal-header"">
          <h2>🔍 Symbol Resolution</h2>
          <button class=""close"" onclick=""closeSymbolResolveModal()"">&times;</button>
        </div>
        <div class=""form-group"">
          <label>Enter Symbol to Resolve</label>
          <input id=""symbolToResolve"" placeholder=""AAPL"" />
        </div>
        <button class=""btn-primary"" onclick=""doResolveSymbol()"">Resolve</button>
        <div id=""symbolResolutionResult"" style=""margin-top: 16px;""></div>
      </div>
    </div>

    <!-- Multi-Provider Configuration -->
    <div class=""card"">
      <h2>🔗 Multi-Provider Connections</h2>
      <p style=""color: #718096; font-size: 14px; margin: 0 0 16px 0;"">
        Connect to multiple data providers simultaneously for data comparison and failover.
      </p>

      <!-- Active Connections -->
      <div id=""activeConnectionsPanel"" style=""margin-bottom: 20px;"">
        <h3 style=""margin: 0 0 12px 0; font-size: 14px; color: #4a5568;"">Active Connections</h3>
        <div id=""activeConnectionsGrid"" style=""display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 12px;"">
          <div style=""padding: 16px; background: #f7fafc; border-radius: 8px; text-align: center; color: #718096;"">
            No providers connected
          </div>
        </div>
      </div>

      <!-- Add Provider -->
      <details style=""margin: 16px 0;"">
        <summary style=""cursor: pointer; font-weight: 500; color: #4a5568;"">➕ Add Provider Connection</summary>
        <div style=""padding: 16px; margin-top: 8px; background: #f7fafc; border-radius: 6px;"">
          <div class=""form-row"">
            <div class=""form-group"">
              <label>Provider ID</label>
              <input id=""newProviderId"" placeholder=""alpaca_primary"" />
            </div>
            <div class=""form-group"">
              <label>Provider Type</label>
              <select id=""newProviderType"">
                <option value=""Alpaca"">Alpaca</option>
                <option value=""IB"">Interactive Brokers</option>
                <option value=""Polygon"">Polygon</option>
              </select>
            </div>
            <div class=""form-group"">
              <label>Priority</label>
              <input id=""newProviderPriority"" type=""number"" value=""1"" min=""1"" max=""10"" />
            </div>
          </div>
          <div id=""newProviderAlpacaFields"">
            <div class=""form-row"">
              <div class=""form-group"">
                <label>API Key ID</label>
                <input id=""newProviderAlpacaKey"" type=""text"" placeholder=""PK..."" />
              </div>
              <div class=""form-group"">
                <label>Secret Key</label>
                <input id=""newProviderAlpacaSecret"" type=""password"" placeholder=""••••••"" />
              </div>
            </div>
          </div>
          <button class=""btn-primary"" onclick=""addProviderConnection()"">
            ➕ Connect Provider
          </button>
        </div>
      </details>
    </div>

    <!-- Provider Comparison View -->
    <div class=""card"">
      <h2>📊 Provider Comparison</h2>
      <p style=""color: #718096; font-size: 14px; margin: 0 0 16px 0;"">
        Compare data quality metrics side-by-side across connected providers.
      </p>

      <div id=""comparisonPanel"" style=""overflow-x: auto;"">
        <table id=""comparisonTable"" style=""width: 100%; min-width: 600px;"">
          <thead>
            <tr>
              <th>Metric</th>
              <th id=""comparisonHeaders"">No providers connected</th>
            </tr>
          </thead>
          <tbody id=""comparisonBody"">
            <tr>
              <td colspan=""2"" style=""text-align: center; color: #718096; padding: 24px;"">
                Connect providers to see comparison metrics
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div style=""margin-top: 16px; display: flex; gap: 12px;"">
        <button class=""btn-secondary"" onclick=""refreshComparison()"">
          🔄 Refresh Metrics
        </button>
        <button class=""btn-secondary"" onclick=""exportComparison()"">
          📥 Export Report
        </button>
      </div>
    </div>

    <!-- Automatic Failover Configuration -->
    <div class=""card"">
      <h2>🔄 Automatic Failover</h2>
      <p style=""color: #718096; font-size: 14px; margin: 0 0 16px 0;"">
        Configure automatic failover rules between providers.
      </p>

      <div id=""failoverRulesPanel"">
        <h3 style=""margin: 0 0 12px 0; font-size: 14px; color: #4a5568;"">Active Failover Rules</h3>
        <div id=""failoverRulesList"" style=""margin-bottom: 16px;"">
          <div style=""padding: 16px; background: #f7fafc; border-radius: 8px; text-align: center; color: #718096;"">
            No failover rules configured
          </div>
        </div>
      </div>

      <details style=""margin: 16px 0;"">
        <summary style=""cursor: pointer; font-weight: 500; color: #4a5568;"">➕ Add Failover Rule</summary>
        <div style=""padding: 16px; margin-top: 8px; background: #f7fafc; border-radius: 6px;"">
          <div class=""form-row"">
            <div class=""form-group"">
              <label>Rule ID</label>
              <input id=""failoverRuleId"" placeholder=""primary_failover"" />
            </div>
            <div class=""form-group"">
              <label>Primary Provider</label>
              <select id=""failoverPrimaryProvider"">
                <option value="""">Select primary...</option>
              </select>
            </div>
          </div>
          <div class=""form-row"">
            <div class=""form-group"">
              <label>Backup Providers (comma-separated)</label>
              <input id=""failoverBackupProviders"" placeholder=""backup1, backup2"" />
            </div>
            <div class=""form-group"">
              <label>Failure Threshold</label>
              <input id=""failoverThreshold"" type=""number"" value=""3"" min=""1"" max=""10"" />
              <small style=""color: #718096;"">Consecutive failures before failover</small>
            </div>
          </div>
          <div class=""form-row"">
            <div class=""form-group"">
              <label>Data Quality Threshold</label>
              <input id=""failoverQualityThreshold"" type=""number"" value=""70"" min=""0"" max=""100"" />
              <small style=""color: #718096;"">Minimum quality score (0-100, 0=disabled)</small>
            </div>
            <div class=""form-group"">
              <label>Max Latency (ms)</label>
              <input id=""failoverMaxLatency"" type=""number"" value=""0"" min=""0"" />
              <small style=""color: #718096;"">Maximum acceptable latency (0=disabled)</small>
            </div>
          </div>
          <div class=""form-group"">
            <label style=""display: flex; align-items: center; gap: 8px;"">
              <input type=""checkbox"" id=""failoverAutoRecover"" checked />
              Auto-recover when primary becomes healthy
            </label>
          </div>
          <button class=""btn-primary"" onclick=""addFailoverRule()"">
            ➕ Add Rule
          </button>
        </div>
      </details>
    </div>

    <!-- Symbol Mapping Interface -->
    <div class=""card"">
      <h2>🗺️ Provider Symbol Mapping</h2>
      <p style=""color: #718096; font-size: 14px; margin: 0 0 16px 0;"">
        Configure how symbols are mapped across different providers.
      </p>

      <div style=""overflow-x: auto;"">
        <table id=""symbolMappingTable"">
          <thead>
            <tr>
              <th>Canonical Symbol</th>
              <th>IB Symbol</th>
              <th>Alpaca Symbol</th>
              <th>Polygon Symbol</th>
              <th>FIGI</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody id=""symbolMappingBody"">
            <tr>
              <td colspan=""6"" style=""text-align: center; color: #718096; padding: 16px;"">
                No symbol mappings configured
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <details style=""margin: 16px 0;"">
        <summary style=""cursor: pointer; font-weight: 500; color: #4a5568;"">➕ Add Symbol Mapping</summary>
        <div style=""padding: 16px; margin-top: 8px; background: #f7fafc; border-radius: 6px;"">
          <div class=""form-row"">
            <div class=""form-group"">
              <label>Canonical Symbol *</label>
              <input id=""mappingCanonical"" placeholder=""AAPL"" />
            </div>
            <div class=""form-group"">
              <label>IB Symbol</label>
              <input id=""mappingIB"" placeholder=""AAPL"" />
            </div>
            <div class=""form-group"">
              <label>Alpaca Symbol</label>
              <input id=""mappingAlpaca"" placeholder=""AAPL"" />
            </div>
          </div>
          <div class=""form-row"">
            <div class=""form-group"">
              <label>Polygon Symbol</label>
              <input id=""mappingPolygon"" placeholder=""AAPL"" />
            </div>
            <div class=""form-group"">
              <label>FIGI (optional)</label>
              <input id=""mappingFigi"" placeholder=""BBG000B9XRY4"" />
            </div>
            <div class=""form-group"">
              <label>Name (optional)</label>
              <input id=""mappingName"" placeholder=""Apple Inc."" />
            </div>
          </div>
          <button class=""btn-primary"" onclick=""addSymbolMapping()"">
            ➕ Add Mapping
          </button>
        </div>
      </details>

      <div style=""margin-top: 16px; display: flex; gap: 12px; flex-wrap: wrap;"">
        <button class=""btn-secondary"" onclick=""importSymbolMappings()"">
          📤 Import CSV
        </button>
        <button class=""btn-secondary"" onclick=""exportSymbolMappings()"">
          📥 Export CSV
        </button>
        <button class=""btn-secondary"" onclick=""autoDetectMappings()"">
          🔍 Auto-Detect Mappings
        </button>
      </div>
    </div>

    <!-- Symbols Panel -->
    <div class=""card"">
      <h2>📈 Subscribed Symbols</h2>
      <div style=""overflow-x: auto;"">
        <table id=""symbolsTable"">
          <thead>
            <tr>
              <th>Symbol</th>
              <th>Trades</th>
              <th>Depth</th>
              <th>Levels</th>
              <th>LocalSymbol <span class=""tag tag-ib"">IB</span></th>
              <th>Exchange <span class=""tag tag-ib"">IB</span></th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody></tbody>
        </table>
      </div>
      <p style=""margin-top: 12px; color: #4a5568; font-size: 13px;"">
        Tweak trades/depth routing per symbol and save — when the collector runs with <code>--watch-config</code> it will
        resubscribe live without restarting.
      </p>

      <h3 style=""margin-top: 24px;"">➕ Add Symbol</h3>
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
        <h3>Interactive Brokers Options <span class=""tag tag-ib"">IB Only</span></h3>
        <div class=""form-row"">
          <div class=""form-group"">
            <label>
              LocalSymbol
              <span class=""tooltip"" data-tip=""IB local symbol (e.g., for preferred stocks)"">ℹ️</span>
            </label>
            <input id=""localsym"" placeholder=""PCG PRA"" />
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
        <button class=""btn-primary"" onclick=""addSymbol()"">
          ➕ Add Symbol
        </button>
      </div>
    </div>

    <div style=""text-align: center; padding: 24px; color: #a0aec0; font-size: 12px;"">
      <p>Market Data Collector Dashboard</p>
      <p style=""margin: 8px 0;"">Config: <code style=""background: rgba(255,255,255,0.1); padding: 2px 6px; border-radius: 3px;"">{Escape(configPath)}</code></p>
    </div>
  </div>

  <!-- Toast Container -->
  <div class=""toast-container"" id=""toastContainer""></div>

  <!-- Help Modal -->
  <div id=""helpModal"" class=""modal"">
    <div class=""modal-content"">
      <div class=""modal-header"">
        <h2>📚 Help & Documentation</h2>
        <button class=""close"" onclick=""closeHelp()"">&times;</button>
      </div>

      <div class=""help-section"">
        <h3>🚀 Quick Start</h3>
        <p>The Market Data Collector captures real-time and historical market data from multiple providers.</p>
        <ol>
          <li>Select your data provider (Interactive Brokers or Alpaca)</li>
          <li>Configure provider credentials if using Alpaca</li>
          <li>Configure storage settings for where data will be saved</li>
          <li>Add symbols you want to track</li>
          <li>Start the collector service</li>
        </ol>
      </div>

      <div class=""help-section"">
        <h3>🔌 Data Providers</h3>
        <p><strong>Interactive Brokers (IB):</strong> Provides Level 2 market depth and tick-by-tick trade data. Requires TWS or IB Gateway running.</p>
        <p><strong>Alpaca:</strong> Provides real-time trade and quote data via WebSocket. Free tier available with IEX feed.</p>
      </div>

      <div class=""help-section"">
        <h3>💾 Storage Options</h3>
        <p><strong>Naming Conventions:</strong></p>
        <ul>
          <li><strong>Flat:</strong> All files in one directory</li>
          <li><strong>By Symbol:</strong> Organized by symbol, then data type (recommended)</li>
          <li><strong>By Date:</strong> Organized by date, then symbol</li>
          <li><strong>By Type:</strong> Organized by data type, then symbol</li>
        </ul>
        <p><strong>Date Partitioning:</strong></p>
        <ul>
          <li><strong>None:</strong> Single file per symbol/type combination</li>
          <li><strong>Daily:</strong> New file each day (recommended)</li>
          <li><strong>Hourly:</strong> New file each hour (for high-frequency data)</li>
          <li><strong>Monthly:</strong> New file each month (for archival)</li>
        </ul>
      </div>

      <div class=""help-section"">
        <h3>📅 Historical Backfill</h3>
        <p>Use backfill to download historical end-of-day bar data for the symbols you're tracking. This is useful for:</p>
        <ul>
          <li>Filling gaps in your dataset</li>
          <li>Getting historical context before starting real-time collection</li>
          <li>Backtesting with consistent data formats</li>
        </ul>
      </div>

      <div class=""help-section"">
        <h3>📊 Symbol Configuration</h3>
        <p><strong>Trades:</strong> Subscribe to trade/tick data (price, volume, aggressor side)</p>
        <p><strong>Depth:</strong> Subscribe to Level 2 order book updates (requires IB)</p>
        <p><strong>Depth Levels:</strong> How many price levels to track (typically 5-20)</p>
      </div>

      <div class=""help-section"">
        <h3>🔧 Command Line Options</h3>
        <p>Start the collector with various modes:</p>
        <ul>
          <li><code>--ui</code> - Start web dashboard (this interface)</li>
          <li><code>--serve-status</code> - Enable status endpoint</li>
          <li><code>--watch-config</code> - Hot-reload configuration changes</li>
          <li><code>--backfill</code> - Run historical backfill</li>
          <li><code>--selftest</code> - Run system self-tests</li>
        </ul>
      </div>

      <div class=""help-section"">
        <h3>❓ Troubleshooting</h3>
        <p>If you're experiencing issues:</p>
        <ul>
          <li>Check that your data provider is running and accessible</li>
          <li>Verify credentials are correct (especially for Alpaca)</li>
          <li>Ensure data directory has write permissions</li>
          <li>Check logs in the data directory for detailed error messages</li>
          <li>For IB: Ensure TWS/Gateway is running and API connections are enabled</li>
        </ul>
      </div>

      <div class=""help-section"">
        <h3>📖 More Information</h3>
        <p>For detailed documentation, see the following files in your installation directory:</p>
        <ul>
          <li><code>HELP.md</code> - Complete user guide</li>
          <li><code>README.md</code> - Project overview</li>
          <li><code>docs/CONFIGURATION.md</code> - Full configuration reference</li>
          <li><code>docs/GETTING_STARTED.md</code> - Setup guide</li>
          <li><code>docs/TROUBLESHOOTING.md</code> - Common issues and solutions</li>
        </ul>
      </div>
    </div>
  </div>

  <script>
    let currentDataSource = 'IB';
    let cachedSymbols = [];
    let backfillProviders = [];

    // Toast Notification System
    function showToast(type, title, message) {{
      const container = document.getElementById('toastContainer');
      const toast = document.createElement('div');
      toast.className = 'toast toast-' + type;

      const icons = {{
        success: '✅',
        error: '❌',
        info: 'ℹ️'
      }};

      toast.innerHTML = 
        '<div class=""toast-icon"">' + (icons[type] || 'ℹ️') + '</div>' +
        '<div class=""toast-content"">' +
          '<div class=""toast-title"">' + title + '</div>' +
          '<div class=""toast-message"">' + message + '</div>' +
        '</div>';

      container.appendChild(toast);

      setTimeout(() => {{
        toast.style.animation = 'slideIn 0.3s ease reverse';
        setTimeout(() => toast.remove(), 300);
      }}, 5000);
    }}

    // Help Modal
    function openHelp() {{
      document.getElementById('helpModal').style.display = 'block';
    }}

    function closeHelp() {{
      document.getElementById('helpModal').style.display = 'none';
    }}

    window.onclick = function(event) {{
      const modal = document.getElementById('helpModal');
      if (event.target === modal) {{
        closeHelp();
      }}
    }};

    // API Functions with Error Handling
    async function apiCall(url, options = {{}}) {{
      try {{
        const response = await fetch(url, options);
        if (!response.ok) {{
          const text = await response.text();
          throw new Error(text || 'HTTP ' + response.status);
        }}
        return response;
      }} catch (error) {{
        console.error('API Error:', error);
        throw error;
      }}
    }}

    async function loadBackfillProviders(selectedProvider) {{
      try {{
        const r = await apiCall('/api/backfill/providers');
        backfillProviders = await r.json();
        const select = document.getElementById('backfillProvider');
        if (!select) return;
        select.innerHTML = '';
        for (const p of backfillProviders) {{
          const opt = document.createElement('option');
          opt.value = p.name;
          opt.textContent = p.displayName || p.name;
          select.appendChild(opt);
        }}
        if (selectedProvider) {{
          select.value = selectedProvider;
        }}
      }} catch (e) {{
        console.warn('Unable to load backfill providers', e);
      }}
    }}

    async function loadConfig() {{
      try {{
        const r = await apiCall('/api/config');
        const cfg = await r.json();

        cachedSymbols = cfg.symbols || [];

        currentDataSource = cfg.dataSource || 'IB';
        document.getElementById('dataSource').value = currentDataSource;
        updateProviderUI();

        if (cfg.alpaca) {{
          document.getElementById('alpacaKeyId').value = cfg.alpaca.keyId || '';
          document.getElementById('alpacaSecretKey').value = cfg.alpaca.secretKey || '';
          document.getElementById('alpacaFeed').value = cfg.alpaca.feed || 'iex';
          document.getElementById('alpacaSandbox').value = cfg.alpaca.useSandbox ? 'true' : 'false';
          document.getElementById('alpacaSubscribeQuotes').checked = cfg.alpaca.subscribeQuotes || false;
        }}

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

        const tbody = document.querySelector('#symbolsTable tbody');
        tbody.innerHTML = '';
        for (const s of (cfg.symbols || [])) {{
          const tr = document.createElement('tr');

          // Symbol cell
          const symbolTd = document.createElement('td');
          const symbolStrong = document.createElement('strong');
          symbolStrong.textContent = s.symbol || '';
          symbolTd.appendChild(symbolStrong);
          tr.appendChild(symbolTd);

          // Subscribe trades cell
          const tradesTd = document.createElement('td');
          tradesTd.textContent = s.subscribeTrades ? '✓ Yes' : '✗ No';
          tr.appendChild(tradesTd);

          // Subscribe depth cell
          const depthSubTd = document.createElement('td');
          depthSubTd.textContent = s.subscribeDepth ? '✓ Yes' : '✗ No';
          tr.appendChild(depthSubTd);

          // Depth levels cell
          const depthLevelsTd = document.createElement('td');
          depthLevelsTd.textContent = (s.depthLevels || 10).toString();
          tr.appendChild(depthLevelsTd);

          // Local symbol cell
          const localSymbolTd = document.createElement('td');
          localSymbolTd.textContent = s.localSymbol || '-';
          tr.appendChild(localSymbolTd);

          // Exchange cell
          const exchangeTd = document.createElement('td');
          exchangeTd.textContent = s.exchange || '-';
          tr.appendChild(exchangeTd);

          // Actions cell
          const actionsTd = document.createElement('td');
          const actionsDiv = document.createElement('div');
          actionsDiv.style.display = 'flex';
          actionsDiv.style.gap = '8px';
          actionsDiv.style.flexWrap = 'wrap';

          const editButton = document.createElement('button');
          editButton.className = 'btn-secondary';
          editButton.textContent = '✏️ Edit';
          editButton.addEventListener('click', () => editSymbol(s.symbol));

          const deleteButton = document.createElement('button');
          deleteButton.className = 'btn-danger';
          deleteButton.textContent = '🗑️ Delete';
          deleteButton.addEventListener('click', () => deleteSymbol(s.symbol));

          actionsDiv.appendChild(editButton);
          actionsDiv.appendChild(deleteButton);
          actionsTd.appendChild(actionsDiv);
          tr.appendChild(actionsTd);
          tbody.appendChild(tr);
        }}

        showToast('success', 'Configuration Loaded', 'Settings loaded successfully');
      }} catch (error) {{
        showToast('error', 'Load Failed', 'Could not load configuration: ' + error.message);
      }}
    }}

    function updateProviderUI() {{
      const isAlpaca = currentDataSource === 'Alpaca';
      document.getElementById('alpacaSettings').classList.toggle('hidden', !isAlpaca);
      document.getElementById('ibFields').classList.toggle('hidden', isAlpaca);

      const statusDiv = document.getElementById('providerStatus');
      if (isAlpaca) {{
        statusDiv.innerHTML = '<span class=""tag tag-alpaca"">Alpaca</span> WebSocket streaming for trades and quotes';
      }} else {{
        statusDiv.innerHTML = '<span class=""tag tag-ib"">Interactive Brokers</span> TWS/Gateway connection for L2 depth and trades';
      }}
    }}

    async function updateDataSource() {{
      try {{
        const ds = document.getElementById('dataSource').value;
        currentDataSource = ds;
        updateProviderUI();

        await apiCall('/api/config/datasource', {{
          method: 'POST',
          headers: {{'Content-Type': 'application/json'}},
          body: JSON.stringify({{ dataSource: ds }})
        }});

        showToast('success', 'Provider Updated', 'Data source changed. Restart collector to apply.');
      }} catch (error) {{
        showToast('error', 'Update Failed', error.message);
      }}
    }}

    async function saveAlpacaSettings() {{
      try {{
        const payload = {{
          keyId: document.getElementById('alpacaKeyId').value,
          secretKey: document.getElementById('alpacaSecretKey').value,
          feed: document.getElementById('alpacaFeed').value,
          useSandbox: document.getElementById('alpacaSandbox').value === 'true',
          subscribeQuotes: document.getElementById('alpacaSubscribeQuotes').checked
        }};

        await apiCall('/api/config/alpaca', {{
          method: 'POST',
          headers: {{'Content-Type': 'application/json'}},
          body: JSON.stringify(payload)
        }});

        showToast('success', 'Settings Saved', 'Alpaca settings saved. Restart collector to apply.');
      }} catch (error) {{
        showToast('error', 'Save Failed', error.message);
      }}
    }}

    async function saveStorageSettings() {{
      try {{
        const payload = {{
          dataRoot: document.getElementById('dataRoot').value,
          compress: document.getElementById('compress').value === 'true',
          namingConvention: document.getElementById('namingConvention').value,
          datePartition: document.getElementById('datePartition').value,
          includeProvider: document.getElementById('includeProvider').value === 'true',
          filePrefix: document.getElementById('filePrefix').value
        }};

        await apiCall('/api/config/storage', {{
          method: 'POST',
          headers: {{'Content-Type': 'application/json'}},
          body: JSON.stringify(payload)
        }});

        showToast('success', 'Settings Saved', 'Storage settings saved. Restart collector to apply.');
      }} catch (error) {{
        showToast('error', 'Save Failed', error.message);
      }}
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
        path = dateStr ? root + '/' + pfx + 'AAPL_Trade_' + dateStr + ext : root + '/' + pfx + 'AAPL_Trade' + ext;
      }} else if (naming === 'BySymbol') {{
        path = dateStr ? root + '/AAPL/Trade/' + pfx + dateStr + ext : root + '/AAPL/Trade/' + pfx + 'data' + ext;
      }} else if (naming === 'ByDate') {{
        path = dateStr ? root + '/' + dateStr + '/AAPL/' + pfx + 'Trade' + ext : root + '/AAPL/' + pfx + 'Trade' + ext;
      }} else if (naming === 'ByType') {{
        path = dateStr ? root + '/Trade/AAPL/' + pfx + dateStr + ext : root + '/Trade/AAPL/' + pfx + 'data' + ext;
      }} else if (naming === 'BySource') {{
        path = dateStr ? root + '/alpaca/AAPL/Trade/' + pfx + dateStr + ext : root + '/alpaca/AAPL/Trade/' + pfx + 'data' + ext;
      }} else if (naming === 'ByAssetClass') {{
        path = dateStr ? root + '/equity/AAPL/Trade/' + pfx + dateStr + ext : root + '/equity/AAPL/Trade/' + pfx + 'data' + ext;
      }} else if (naming === 'Hierarchical') {{
        path = dateStr ? root + '/alpaca/equity/AAPL/Trade/' + pfx + dateStr + ext : root + '/alpaca/equity/AAPL/Trade/' + pfx + 'data' + ext;
      }} else if (naming === 'Canonical') {{
        path = root + '/2024/01/15/alpaca/AAPL/' + pfx + 'Trade' + ext;
      }}

      document.getElementById('previewPath').textContent = path;
    }}

    ['dataRoot', 'compress', 'namingConvention', 'datePartition', 'filePrefix'].forEach(id => {{
      document.getElementById(id).addEventListener('change', updateStoragePreview);
      document.getElementById(id).addEventListener('input', updateStoragePreview);
    }});

    async function loadStatus() {{
      const box = document.getElementById('statusBox');
      try {{
        const r = await apiCall('/api/status');
        const s = await r.json();
        const isConnected = s.isConnected !== false;

        box.innerHTML =
          '<div class=""status-badge ' + (isConnected ? 'status-connected' : 'status-disconnected') + '"">' +
            '<span class=""status-dot""></span>' +
            (isConnected ? 'Connected' : 'Disconnected') +
          '</div>' +
          '<div style=""margin-top: 8px; font-size: 12px; color: #718096;"">' +
            'Last update: ' + (s.timestampUtc || 'n/a') +
          '</div>';

        document.getElementById('metricPublished').textContent = (s.metrics && s.metrics.published) || 0;
        document.getElementById('metricDropped').textContent = (s.metrics && s.metrics.dropped) || 0;
        document.getElementById('metricIntegrity').textContent = (s.metrics && s.metrics.integrity) || 0;
        document.getElementById('metricBars').textContent = (s.metrics && s.metrics.historicalBars) || 0;
      }} catch (e) {{
        box.innerHTML =
          '<div class=""status-badge status-disconnected"">' +
            '<span class=""status-dot""></span>' +
            'No Status' +
          '</div>' +
          '<div style=""margin-top: 8px; font-size: 12px; color: #718096;"">' +
            'Start collector with --serve-status' +
          '</div>';
      }}
    }}

    async function loadBackfillStatus() {{
      const box = document.getElementById('backfillStatus');
      try {{
        const r = await apiCall('/api/backfill/status');
        const status = await r.json();
        box.innerHTML = formatBackfillStatus(status);
      }} catch (e) {{
        box.textContent = 'No backfill runs yet.';
      }}
    }}

    function formatBackfillStatus(status) {{
      if (!status) return 'No backfill runs yet.';
      const started = status.startedUtc ? new Date(status.startedUtc).toLocaleString() : 'n/a';
      const completed = status.completedUtc ? new Date(status.completedUtc).toLocaleString() : 'n/a';
      const badge = status.success
        ? '<span style=""color: #48bb78; font-weight: 600;"">✅ Success</span>'
        : '<span style=""color: #f56565; font-weight: 600;"">❌ Failed</span>';
      const symbols = (status.symbols || []).join(', ');
      const error = status.error ? '<div style=""color: #f56565; margin-top: 8px;"">' + status.error + '</div>' : '';
      return '<div><strong>' + badge + '</strong></div>' +
        '<div style=""margin-top: 8px;"">' +
          '<strong>Provider:</strong> ' + status.provider + '<br/>' +
          '<strong>Bars Written:</strong> ' + (status.barsWritten || 0) + '<br/>' +
          '<strong>Symbols:</strong> ' + (symbols || 'n/a') + '<br/>' +
          '<strong>Started:</strong> ' + started + '<br/>' +
          '<strong>Completed:</strong> ' + completed +
        '</div>' +
        error;
    }}

    function editSymbol(symbol) {
      const match = (cachedSymbols || []).find(s => (s.symbol || '').toLowerCase() === symbol.toLowerCase());
      if (!match) {
        showToast('error', 'Not Found', 'Cannot find ' + symbol + ' in current configuration');
        return;
      }

      document.getElementById('sym').value = match.symbol || '';
      document.getElementById('trades').value = match.subscribeTrades ? 'true' : 'false';
      document.getElementById('depth').value = match.subscribeDepth ? 'true' : 'false';
      document.getElementById('levels').value = match.depthLevels || 10;
      document.getElementById('localsym').value = match.localSymbol || '';
      document.getElementById('exch').value = match.exchange || 'SMART';
      document.getElementById('pexch').value = match.primaryExchange || '';

      showToast('info', 'Editing symbol', 'Loaded ' + symbol + ' into the form. Update fields and click Add Symbol to save.');
    }

    async function addSymbol() {{
      try {{
        const symbol = document.getElementById('sym').value.trim();
        if (!symbol) {{
          showToast('error', 'Validation Error', 'Symbol is required');
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

        await apiCall('/api/config/symbols', {{
          method: 'POST',
          headers: {{'Content-Type': 'application/json'}},
          body: JSON.stringify(payload)
        }});

        showToast('success', 'Symbol Added', symbol + ' added successfully');

        document.getElementById('sym').value = '';
        document.getElementById('localsym').value = '';
        document.getElementById('pexch').value = '';
        await loadConfig();
      }} catch (error) {{
        showToast('error', 'Add Failed', error.message);
      }}
    }}

    async function deleteSymbol(symbol) {{
      if (!confirm('Delete symbol ' + symbol + '?')) return;

      try {{
        await apiCall('/api/config/symbols/' + encodeURIComponent(symbol), {{
          method: 'DELETE'
        }});

        showToast('success', 'Symbol Deleted', symbol + ' removed successfully');
        await loadConfig();
      }} catch (error) {{
        showToast('error', 'Delete Failed', error.message);
      }}
    }}

    // Provider health check
    async function checkProviderHealth() {{
      const grid = document.getElementById('providerHealthGrid');
      grid.innerHTML = '<div style=""text-align: center; padding: 20px;""><div class=""loading""></div> Checking providers...</div>';

      try {{
        const r = await apiCall('/api/backfill/health');
        const health = await r.json();
        renderProviderHealth(health);
        showToast('success', 'Health Check', 'Provider health check complete');
      }} catch (error) {{
        grid.innerHTML = '<div style=""color: #f56565;"">Failed to check provider health</div>';
        showToast('error', 'Health Check Failed', error.message);
      }}
    }}

    function renderProviderHealth(health) {{
      const grid = document.getElementById('providerHealthGrid');
      if (!health || Object.keys(health).length === 0) {{
        grid.innerHTML = '<div>No providers available</div>';
        return;
      }}

      grid.innerHTML = Object.entries(health).map(([name, status]) => {{
        const isAvailable = status.isAvailable;
        const color = isAvailable ? '#48bb78' : '#f56565';
        const icon = isAvailable ? '✓' : '✗';
        const responseTime = status.responseTime ? Math.round(status.responseTime * 1000) + 'ms' : '';

        return '<div style=""padding: 10px; background: white; border-radius: 6px; border-left: 3px solid ' + color + ';"">'+
            '<div style=""display: flex; align-items: center; gap: 6px;"">' +
              '<span style=""color: ' + color + '; font-weight: bold;"">' + icon + '</span>' +
              '<span style=""font-weight: 600; text-transform: capitalize;"">' + name + '</span>' +
            '</div>' +
            '<div style=""font-size: 11px; color: #718096; margin-top: 4px;"">' +
              (status.message || (isAvailable ? 'Available' : 'Unavailable')) +
              (responseTime ? ' (' + responseTime + ')' : '') +
            '</div>' +
          '</div>';
      }}).join('');
    }}

    // Symbol resolution
    function resolveSymbol() {{
      document.getElementById('symbolResolveModal').style.display = 'block';
      document.getElementById('symbolToResolve').value = '';
      document.getElementById('symbolResolutionResult').innerHTML = '';
    }}

    function closeSymbolResolveModal() {{
      document.getElementById('symbolResolveModal').style.display = 'none';
    }}

    async function doResolveSymbol() {{
      const symbol = document.getElementById('symbolToResolve').value.trim();
      if (!symbol) {{
        showToast('error', 'Error', 'Please enter a symbol');
        return;
      }}

      const resultDiv = document.getElementById('symbolResolutionResult');
      resultDiv.innerHTML = '<div class=""loading""></div> Resolving...';

      try {{
        const r = await apiCall('/api/backfill/resolve/' + encodeURIComponent(symbol));
        const resolution = await r.json();

        if (!resolution) {{
          resultDiv.innerHTML = '<div style=""color: #f56565;"">Symbol not found</div>';
          return;
        }}

        let providerMappingsHtml = '';
        if (resolution.providerSymbols) {{
          providerMappingsHtml = '<h4 style=""margin: 16px 0 8px 0;"">Provider Mappings</h4>' +
            '<table style=""width: 100%; font-size: 13px;"">' +
              Object.entries(resolution.providerSymbols).map(function([provider, sym]) {{
                return '<tr><td style=""padding: 4px 8px; font-weight: 500; text-transform: capitalize;"">' + provider + '</td><td style=""font-family: monospace;"">' + sym + '</td></tr>';
              }}).join('') +
            '</table>';
        }}

        resultDiv.innerHTML =
          '<div style=""background: #f7fafc; padding: 16px; border-radius: 8px;"">' +
            '<h4 style=""margin: 0 0 12px 0;"">Resolution for ' + symbol + '</h4>' +
            '<table style=""width: 100%; font-size: 14px;"">' +
              '<tr><td style=""padding: 4px 8px; font-weight: 500;"">Ticker</td><td>' + (resolution.ticker || 'N/A') + '</td></tr>' +
              '<tr><td style=""padding: 4px 8px; font-weight: 500;"">Name</td><td>' + (resolution.name || 'N/A') + '</td></tr>' +
              '<tr><td style=""padding: 4px 8px; font-weight: 500;"">FIGI</td><td style=""font-family: monospace;"">' + (resolution.figi || 'N/A') + '</td></tr>' +
              '<tr><td style=""padding: 4px 8px; font-weight: 500;"">Exchange</td><td>' + (resolution.exchange || 'N/A') + '</td></tr>' +
              '<tr><td style=""padding: 4px 8px; font-weight: 500;"">Security Type</td><td>' + (resolution.securityType || 'N/A') + '</td></tr>' +
            '</table>' +
            providerMappingsHtml +
          '</div>';
      }} catch (error) {{
        resultDiv.innerHTML = '<div style=""color: #f56565;"">Error: ' + error.message + '</div>';
      }}
    }}

    // Enhanced backfill with progress
    async function runBackfill() {{
      try {{
        const provider = document.getElementById('backfillProvider').value || 'composite';
        const symbols = (document.getElementById('backfillSymbols').value || '')
          .split(',')
          .map(s => s.trim())
          .filter(s => s);
        const from = document.getElementById('backfillFrom').value || null;
        const to = document.getElementById('backfillTo').value || null;
        const enableSymbolResolution = document.getElementById('backfillEnableSymbolResolution')?.checked ?? true;
        const preferAdjusted = document.getElementById('backfillPreferAdjusted')?.checked ?? true;

        if (!symbols.length) {{
          showToast('error', 'Validation Error', 'Please enter at least one symbol');
          return;
        }}

        // Show progress
        document.getElementById('backfillProgress').classList.remove('hidden');
        document.getElementById('progressBar').style.width = '0%';
        document.getElementById('progressPercent').textContent = '0%';
        document.getElementById('progressLabel').textContent = 'Starting backfill...';
        document.getElementById('btnStartBackfill').disabled = true;

        showToast('info', 'Backfill Started', 'Downloading data for ' + symbols.length + ' symbol(s)...');

        const payload = {{
          provider,
          symbols,
          from,
          to,
          enableSymbolResolution,
          preferAdjusted
        }};

        const r = await apiCall('/api/backfill/run', {{
          method: 'POST',
          headers: {{ 'Content-Type': 'application/json' }},
          body: JSON.stringify(payload)
        }});

        const result = await r.json();

        // Complete progress
        document.getElementById('progressBar').style.width = '100%';
        document.getElementById('progressPercent').textContent = '100%';
        document.getElementById('progressLabel').textContent = 'Complete';

        document.getElementById('backfillStatus').innerHTML = formatBackfillStatus(result);

        if (result.success) {{
          showToast('success', 'Backfill Complete', 'Downloaded ' + result.barsWritten + ' bars from ' + result.provider);
        }} else {{
          showToast('error', 'Backfill Failed', result.error || 'Unknown error');
        }}
      }} catch (error) {{
        showToast('error', 'Backfill Failed', error.message);
        document.getElementById('backfillStatus').innerHTML = '<span style=""color: #f56565;"">' + error.message + '</span>';
      }} finally {{
        document.getElementById('btnStartBackfill').disabled = false;
        setTimeout(() => {{
          document.getElementById('backfillProgress').classList.add('hidden');
        }}, 3000);
      }}
    }}

    // Load provider health on page load
    async function loadProviderHealth() {{
      try {{
        const r = await apiCall('/api/backfill/providers');
        const providers = await r.json();
        const grid = document.getElementById('providerHealthGrid');

        grid.innerHTML = providers.map(function(p) {{
          return '<div style=""padding: 10px; background: white; border-radius: 6px; border-left: 3px solid #a0aec0;"">' +
            '<div style=""font-weight: 600;"">' + (p.displayName || p.name) + '</div>' +
            '<div style=""font-size: 11px; color: #718096; margin-top: 4px;"">' + (p.description || '') + '</div>' +
          '</div>';
        }}).join('');
      }} catch (e) {{
        console.log('Could not load providers:', e);
      }}
    }}

    // ==========================================
    // Multi-Provider Connection Functions
    // ==========================================

    let connectedProviders = [];
    let failoverRules = [];
    let symbolMappings = [];

    async function loadMultiProviderStatus() {{
      try {{
        const r = await apiCall('/api/multiprovider/status');
        const status = await r.json();
        connectedProviders = status.providers || [];
        renderActiveConnections(connectedProviders);
        updateProviderSelects();
      }} catch (e) {{
        console.log('Multi-provider status not available:', e);
      }}
    }}

    function renderActiveConnections(providers) {{
      const grid = document.getElementById('activeConnectionsGrid');
      if (!providers || providers.length === 0) {{
        grid.innerHTML = '<div style=""padding: 16px; background: #f7fafc; border-radius: 8px; text-align: center; color: #718096;"">No providers connected</div>';
        return;
      }}

      grid.innerHTML = providers.map(function(p) {{
        const statusColor = p.isConnected ? '#48bb78' : '#f56565';
        const statusText = p.isConnected ? 'Connected' : 'Disconnected';
        return '<div style=""padding: 16px; background: white; border-radius: 8px; border-left: 4px solid ' + statusColor + '; box-shadow: 0 1px 3px rgba(0,0,0,0.1);"">' +
            '<div style=""display: flex; justify-content: space-between; align-items: center;"">' +
              '<strong>' + p.providerId + '</strong>' +
              '<span class=""tag tag-' + p.providerType.toLowerCase() + '"" style=""font-size: 10px;"">' + p.providerType + '</span>' +
            '</div>' +
            '<div style=""font-size: 12px; color: ' + statusColor + '; margin-top: 4px;"">' + statusText + '</div>' +
            '<div style=""font-size: 11px; color: #718096; margin-top: 4px;"">' +
              'Subscriptions: ' + (p.activeSubscriptions || 0) +
            '</div>' +
            '<div style=""margin-top: 8px;"">' +
              '<button class=""btn-danger"" style=""font-size: 11px; padding: 4px 8px;"" onclick=""disconnectProvider(\'' + p.providerId + '\')"">'+
                'Disconnect' +
              '</button>' +
            '</div>' +
          '</div>';
      }}).join('');
    }}

    async function addProviderConnection() {{
      try {{
        const providerId = document.getElementById('newProviderId').value.trim();
        const providerType = document.getElementById('newProviderType').value;
        const priority = parseInt(document.getElementById('newProviderPriority').value || '1');

        if (!providerId) {{
          showToast('error', 'Validation Error', 'Provider ID is required');
          return;
        }}

        const payload = {{
          id: providerId,
          name: providerId,
          provider: providerType,
          priority: priority,
          enabled: true
        }};

        if (providerType === 'Alpaca') {{
          payload.alpaca = {{
            keyId: document.getElementById('newProviderAlpacaKey').value,
            secretKey: document.getElementById('newProviderAlpacaSecret').value,
            feed: 'iex',
            useSandbox: false
          }};
        }}

        await apiCall('/api/multiprovider/connect', {{
          method: 'POST',
          headers: {{ 'Content-Type': 'application/json' }},
          body: JSON.stringify(payload)
        }});

        showToast('success', 'Provider Connected', 'Successfully connected to ' + providerId);
        await loadMultiProviderStatus();

        // Clear form
        document.getElementById('newProviderId').value = '';
        document.getElementById('newProviderAlpacaKey').value = '';
        document.getElementById('newProviderAlpacaSecret').value = '';
      }} catch (error) {{
        showToast('error', 'Connection Failed', error.message);
      }}
    }}

    async function disconnectProvider(providerId) {{
      if (!confirm('Disconnect provider ' + providerId + '?')) return;

      try {{
        await apiCall('/api/multiprovider/disconnect/' + encodeURIComponent(providerId), {{
          method: 'POST'
        }});
        showToast('success', 'Provider Disconnected', 'Disconnected from ' + providerId);
        await loadMultiProviderStatus();
      }} catch (error) {{
        showToast('error', 'Disconnect Failed', error.message);
      }}
    }}

    // ==========================================
    // Provider Comparison Functions
    // ==========================================

    async function refreshComparison() {{
      try {{
        const r = await apiCall('/api/multiprovider/comparison');
        const comparison = await r.json();
        renderComparison(comparison);
        showToast('success', 'Metrics Refreshed', 'Provider comparison updated');
      }} catch (error) {{
        showToast('error', 'Refresh Failed', error.message);
      }}
    }}

    function renderComparison(comparison) {{
      const headerRow = document.getElementById('comparisonHeaders').parentElement;
      const tbody = document.getElementById('comparisonBody');

      if (!comparison.providers || comparison.providers.length === 0) {{
        headerRow.innerHTML = '<th>Metric</th><th>No providers connected</th>';
        tbody.innerHTML = '<tr><td colspan=""2"" style=""text-align: center; color: #718096; padding: 24px;"">Connect providers to see comparison metrics</td></tr>';
        return;
      }}

      // Build header row
      headerRow.innerHTML = '<th>Metric</th>' + comparison.providers.map(p =>
        '<th style=""text-align: center;"">' + p.providerId + '<br/><small style=""font-weight: normal; color: #718096;"">' + p.providerType + '</small></th>'
      ).join('');

      // Metrics to compare
      const metrics = [
        {{ key: 'dataQualityScore', label: 'Data Quality Score', format: v => '<span style=""color: ' + (v >= 80 ? '#48bb78' : v >= 60 ? '#ecc94b' : '#f56565') + '; font-weight: 600;"">' + v.toFixed(1) + '%</span>' }},
        {{ key: 'tradesReceived', label: 'Trades Received', format: v => v.toLocaleString() }},
        {{ key: 'depthUpdatesReceived', label: 'Depth Updates', format: v => v.toLocaleString() }},
        {{ key: 'quotesReceived', label: 'Quotes Received', format: v => v.toLocaleString() }},
        {{ key: 'averageLatencyMs', label: 'Avg Latency', format: v => v.toFixed(2) + 'ms' }},
        {{ key: 'minLatencyMs', label: 'Min Latency', format: v => v.toFixed(2) + 'ms' }},
        {{ key: 'maxLatencyMs', label: 'Max Latency', format: v => v.toFixed(2) + 'ms' }},
        {{ key: 'connectionSuccessRate', label: 'Connection Success', format: v => v.toFixed(1) + '%' }},
        {{ key: 'messagesDropped', label: 'Messages Dropped', format: v => '<span style=""color: ' + (v > 0 ? '#f56565' : '#48bb78') + '"">' + v.toLocaleString() + '</span>' }},
        {{ key: 'activeSubscriptions', label: 'Active Subscriptions', format: v => v.toString() }}
      ];

      tbody.innerHTML = metrics.map(m => {{
        const cells = comparison.providers.map(p => {{
          const value = p[m.key] ?? 0;
          return '<td style=""text-align: center;"">' + m.format(value) + '</td>';
        }}).join('');
        return '<tr><td><strong>' + m.label + '</strong></td>' + cells + '</tr>';
      }}).join('');
    }}

    async function exportComparison() {{
      try {{
        const r = await apiCall('/api/multiprovider/comparison');
        const comparison = await r.json();

        const headers = ['Metric', ...comparison.providers.map(p => p.providerId)];
        const rows = [
          ['Data Quality Score', ...comparison.providers.map(p => p.dataQualityScore?.toFixed(1) || '0')],
          ['Trades Received', ...comparison.providers.map(p => p.tradesReceived || 0)],
          ['Depth Updates', ...comparison.providers.map(p => p.depthUpdatesReceived || 0)],
          ['Avg Latency (ms)', ...comparison.providers.map(p => p.averageLatencyMs?.toFixed(2) || '0')],
          ['Messages Dropped', ...comparison.providers.map(p => p.messagesDropped || 0)]
        ];

        const csv = [headers.join(','), ...rows.map(r => r.join(','))].join('\n');
        const blob = new Blob([csv], {{ type: 'text/csv' }});
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'provider-comparison-' + new Date().toISOString().split('T')[0] + '.csv';
        a.click();
        URL.revokeObjectURL(url);

        showToast('success', 'Export Complete', 'Comparison report exported');
      }} catch (error) {{
        showToast('error', 'Export Failed', error.message);
      }}
    }}

    // ==========================================
    // Failover Rule Functions
    // ==========================================

    function updateProviderSelects() {{
      const primarySelect = document.getElementById('failoverPrimaryProvider');
      primarySelect.innerHTML = '<option value="""">Select primary...</option>' +
        connectedProviders.map(p => '<option value=""' + p.providerId + '"">' + p.providerId + '</option>').join('');
    }}

    async function loadFailoverRules() {{
      try {{
        const r = await apiCall('/api/multiprovider/failover/rules');
        failoverRules = await r.json();
        renderFailoverRules(failoverRules);
      }} catch (e) {{
        console.log('Failover rules not available:', e);
      }}
    }}

    function renderFailoverRules(rules) {{
      const container = document.getElementById('failoverRulesList');
      if (!rules || rules.length === 0) {{
        container.innerHTML = '<div style=""padding: 16px; background: #f7fafc; border-radius: 8px; text-align: center; color: #718096;"">No failover rules configured</div>';
        return;
      }}

      container.innerHTML = rules.map(function(rule) {{
        const statusColor = rule.isInFailoverState ? '#ecc94b' : '#48bb78';
        const statusText = rule.isInFailoverState ? 'In Failover' : 'Normal';
        return '<div style=""padding: 16px; background: white; border-radius: 8px; border-left: 4px solid ' + statusColor + '; margin-bottom: 12px; box-shadow: 0 1px 3px rgba(0,0,0,0.1);"">' +
            '<div style=""display: flex; justify-content: space-between; align-items: center;"">' +
              '<strong>' + rule.id + '</strong>' +
              '<span style=""color: ' + statusColor + '; font-size: 12px; font-weight: 600;"">' + statusText + '</span>' +
            '</div>' +
            '<div style=""font-size: 13px; color: #4a5568; margin-top: 8px;"">' +
              '<div>Primary: <strong>' + rule.primaryProviderId + '</strong></div>' +
              '<div>Backups: ' + (rule.backupProviderIds.join(', ') || 'None') + '</div>' +
              '<div style=""margin-top: 4px; color: #718096; font-size: 12px;"">' +
                'Threshold: ' + rule.failoverThreshold + ' failures | Recovery: ' + rule.recoveryThreshold + ' successes' +
              '</div>' +
            '</div>' +
            '<div style=""margin-top: 8px; display: flex; gap: 8px;"">' +
              '<button class=""btn-secondary"" style=""font-size: 11px; padding: 4px 8px;"" onclick=""testFailover(\'' + rule.id + '\')"">'+
                'Test Failover' +
              '</button>' +
              '<button class=""btn-danger"" style=""font-size: 11px; padding: 4px 8px;"" onclick=""removeFailoverRule(\'' + rule.id + '\')"">'+
                'Remove' +
              '</button>' +
            '</div>' +
          '</div>';
      }}).join('');
    }}

    async function addFailoverRule() {{
      try {{
        const ruleId = document.getElementById('failoverRuleId').value.trim();
        const primaryProvider = document.getElementById('failoverPrimaryProvider').value;
        const backupProviders = document.getElementById('failoverBackupProviders').value.split(',').map(s => s.trim()).filter(s => s);
        const threshold = parseInt(document.getElementById('failoverThreshold').value || '3');
        const qualityThreshold = parseInt(document.getElementById('failoverQualityThreshold').value || '0');
        const maxLatency = parseInt(document.getElementById('failoverMaxLatency').value || '0');
        const autoRecover = document.getElementById('failoverAutoRecover').checked;

        if (!ruleId || !primaryProvider) {{
          showToast('error', 'Validation Error', 'Rule ID and Primary Provider are required');
          return;
        }}

        const payload = {{
          id: ruleId,
          primaryProviderId: primaryProvider,
          backupProviderIds: backupProviders,
          failoverThreshold: threshold,
          recoveryThreshold: 5,
          dataQualityThreshold: qualityThreshold,
          maxLatencyMs: maxLatency,
          autoRecover: autoRecover
        }};

        await apiCall('/api/multiprovider/failover/rules', {{
          method: 'POST',
          headers: {{ 'Content-Type': 'application/json' }},
          body: JSON.stringify(payload)
        }});

        showToast('success', 'Rule Added', 'Failover rule ' + ruleId + ' created');
        await loadFailoverRules();

        // Clear form
        document.getElementById('failoverRuleId').value = '';
        document.getElementById('failoverBackupProviders').value = '';
      }} catch (error) {{
        showToast('error', 'Add Rule Failed', error.message);
      }}
    }}

    async function removeFailoverRule(ruleId) {{
      if (!confirm('Remove failover rule ' + ruleId + '?')) return;

      try {{
        await apiCall('/api/multiprovider/failover/rules/' + encodeURIComponent(ruleId), {{
          method: 'DELETE'
        }});
        showToast('success', 'Rule Removed', 'Failover rule ' + ruleId + ' removed');
        await loadFailoverRules();
      }} catch (error) {{
        showToast('error', 'Remove Failed', error.message);
      }}
    }}

    async function testFailover(ruleId) {{
      try {{
        await apiCall('/api/multiprovider/failover/test/' + encodeURIComponent(ruleId), {{
          method: 'POST'
        }});
        showToast('info', 'Failover Test', 'Testing failover for rule ' + ruleId);
        await loadFailoverRules();
      }} catch (error) {{
        showToast('error', 'Test Failed', error.message);
      }}
    }}

    // ==========================================
    // Symbol Mapping Functions
    // ==========================================

    async function loadSymbolMappings() {{
      try {{
        const r = await apiCall('/api/multiprovider/symbolmappings');
        symbolMappings = await r.json();
        renderSymbolMappings(symbolMappings);
      }} catch (e) {{
        console.log('Symbol mappings not available:', e);
      }}
    }}

    function renderSymbolMappings(mappings) {{
      const tbody = document.getElementById('symbolMappingBody');
      if (!mappings || mappings.length === 0) {{
        tbody.innerHTML = '<tr><td colspan=""6"" style=""text-align: center; color: #718096; padding: 16px;"">No symbol mappings configured</td></tr>';
        return;
      }}

      tbody.innerHTML = mappings.map(function(m) {{
        return '<tr>' +
          '<td><strong>' + m.canonicalSymbol + '</strong></td>' +
          '<td>' + (m.ibSymbol || '-') + '</td>' +
          '<td>' + (m.alpacaSymbol || '-') + '</td>' +
          '<td>' + (m.polygonSymbol || '-') + '</td>' +
          '<td style=""font-family: monospace; font-size: 11px;"">' + (m.figi || '-') + '</td>' +
          '<td>' +
            '<button class=""btn-danger"" style=""font-size: 11px; padding: 4px 8px;"" onclick=""removeSymbolMapping(\'' + m.canonicalSymbol + '\')"">'+
              'Remove' +
            '</button>' +
          '</td>' +
        '</tr>';
      }}).join('');
    }}

    async function addSymbolMapping() {{
      try {{
        const canonical = document.getElementById('mappingCanonical').value.trim().toUpperCase();
        if (!canonical) {{
          showToast('error', 'Validation Error', 'Canonical symbol is required');
          return;
        }}

        const payload = {{
          canonicalSymbol: canonical,
          ibSymbol: document.getElementById('mappingIB').value.trim() || null,
          alpacaSymbol: document.getElementById('mappingAlpaca').value.trim() || null,
          polygonSymbol: document.getElementById('mappingPolygon').value.trim() || null,
          figi: document.getElementById('mappingFigi').value.trim() || null,
          name: document.getElementById('mappingName').value.trim() || null
        }};

        await apiCall('/api/multiprovider/symbolmappings', {{
          method: 'POST',
          headers: {{ 'Content-Type': 'application/json' }},
          body: JSON.stringify(payload)
        }});

        showToast('success', 'Mapping Added', 'Symbol mapping for ' + canonical + ' created');
        await loadSymbolMappings();

        // Clear form
        ['mappingCanonical', 'mappingIB', 'mappingAlpaca', 'mappingPolygon', 'mappingFigi', 'mappingName'].forEach(id => {{
          document.getElementById(id).value = '';
        }});
      }} catch (error) {{
        showToast('error', 'Add Mapping Failed', error.message);
      }}
    }}

    async function removeSymbolMapping(symbol) {{
      if (!confirm('Remove symbol mapping for ' + symbol + '?')) return;

      try {{
        await apiCall('/api/multiprovider/symbolmappings/' + encodeURIComponent(symbol), {{
          method: 'DELETE'
        }});
        showToast('success', 'Mapping Removed', 'Symbol mapping for ' + symbol + ' removed');
        await loadSymbolMappings();
      }} catch (error) {{
        showToast('error', 'Remove Failed', error.message);
      }}
    }}

    async function importSymbolMappings() {{
      const input = document.createElement('input');
      input.type = 'file';
      input.accept = '.csv';
      input.onchange = async (e) => {{
        const file = e.target.files[0];
        if (!file) return;

        const formData = new FormData();
        formData.append('file', file);

        try {{
          await apiCall('/api/multiprovider/symbolmappings/import', {{
            method: 'POST',
            body: formData
          }});
          showToast('success', 'Import Complete', 'Symbol mappings imported');
          await loadSymbolMappings();
        }} catch (error) {{
          showToast('error', 'Import Failed', error.message);
        }}
      }};
      input.click();
    }}

    async function exportSymbolMappings() {{
      try {{
        const r = await apiCall('/api/multiprovider/symbolmappings/export');
        const blob = await r.blob();
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'symbol-mappings-' + new Date().toISOString().split('T')[0] + '.csv';
        a.click();
        URL.revokeObjectURL(url);
        showToast('success', 'Export Complete', 'Symbol mappings exported');
      }} catch (error) {{
        showToast('error', 'Export Failed', error.message);
      }}
    }}

    async function autoDetectMappings() {{
      try {{
        showToast('info', 'Auto-Detection', 'Detecting symbol mappings from subscribed symbols...');
        await apiCall('/api/multiprovider/symbolmappings/autodetect', {{
          method: 'POST'
        }});
        showToast('success', 'Detection Complete', 'Symbol mappings auto-detected');
        await loadSymbolMappings();
      }} catch (error) {{
        showToast('error', 'Detection Failed', error.message);
      }}
    }}

    // Show/hide provider-specific fields
    document.getElementById('newProviderType').addEventListener('change', function() {{
      const alpacaFields = document.getElementById('newProviderAlpacaFields');
      alpacaFields.style.display = this.value === 'Alpaca' ? 'block' : 'none';
    }});

    // Initial load
    loadConfig();
    loadStatus();
    loadBackfillStatus();
    loadProviderHealth();
    loadMultiProviderStatus();
    loadFailoverRules();
    loadSymbolMappings();
    setInterval(loadStatus, 2000);
    setInterval(loadBackfillStatus, 5000);
    setInterval(loadMultiProviderStatus, 5000);
  </script>
</body>
</html>";

    private static string Escape(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
