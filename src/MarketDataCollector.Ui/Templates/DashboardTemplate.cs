namespace MarketDataCollector.Ui.Templates;

/// <summary>
/// Provides HTML templates for the dashboard views.
/// For production use, consider moving the full templates to static HTML files
/// and using a templating engine like Razor or serving static files.
/// </summary>
public static class DashboardTemplate
{
    /// <summary>
    /// Generates the main dashboard HTML page.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    /// <param name="statusPath">Path to the status file.</param>
    /// <param name="backfillPath">Path to the backfill status file.</param>
    /// <returns>Complete HTML document string.</returns>
    public static string Index(string configPath, string statusPath, string backfillPath)
    {
        // Note: The full template (~3,400 lines) should be loaded from a static file
        // or embedded resource for better maintainability.
        // This is a simplified version showing the structure.

        return $$"""
<!doctype html>
<html>
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width,initial-scale=1" />
  <title>MDC Terminal</title>
  <link rel="stylesheet" href="/static/dashboard.css" />
</head>
<body>
  <!-- Top Navigation Bar -->
  <div class="top-bar">
    <div class="logo">
      <div class="logo-icon">MDC</div>
      <span class="logo-text">Market Data Collector</span>
      <span class="logo-version">v1.5.0</span>
    </div>
    <div class="cmd-palette" onclick="openCommandPalette()">
      <span class="cmd-palette-icon">&#x1F50D;</span>
      <span class="cmd-palette-text">Search commands...</span>
      <span class="cmd-palette-shortcut"><kbd>Ctrl</kbd>+<kbd>K</kbd></span>
    </div>
    <div class="top-status">
      <div class="status-indicator" id="connectionStatus">
        <div class="status-dot disconnected"></div>
        <span>Disconnected</span>
      </div>
    </div>
  </div>

  <!-- Main Container -->
  <div class="main-container">
    <aside class="sidebar">
      <nav class="nav-section">
        <div class="nav-section-title">Overview</div>
        <a class="nav-item active" href="#dashboard">Dashboard</a>
        <a class="nav-item" href="#providers">Providers</a>
      </nav>
      <nav class="nav-section">
        <div class="nav-section-title">Configuration</div>
        <a class="nav-item" href="#config">Config</a>
        <a class="nav-item" href="#storage">Storage</a>
        <a class="nav-item" href="#datasources">Data Sources</a>
      </nav>
      <nav class="nav-section">
        <div class="nav-section-title">Data</div>
        <a class="nav-item" href="#symbols">Symbols</a>
        <a class="nav-item" href="#backfill">Backfill</a>
      </nav>
    </aside>

    <main class="content">
      <div id="dashboard-content">
        <!-- Dashboard metrics and status -->
        <div class="metrics-grid">
          <div class="metric-card success">
            <div class="metric-value success" id="publishedCount">0</div>
            <div class="metric-label">Events Published</div>
          </div>
          <div class="metric-card danger">
            <div class="metric-value danger" id="droppedCount">0</div>
            <div class="metric-label">Events Dropped</div>
          </div>
          <div class="metric-card warning">
            <div class="metric-value warning" id="integrityCount">0</div>
            <div class="metric-label">Integrity Events</div>
          </div>
          <div class="metric-card info">
            <div class="metric-value info" id="historicalCount">0</div>
            <div class="metric-label">Historical Bars</div>
          </div>
        </div>

        <!-- Configuration sections will be loaded dynamically -->
        <div id="config-section"></div>
        <div id="storage-section"></div>
        <div id="datasources-section"></div>
        <div id="symbols-section"></div>
        <div id="backfill-section"></div>
      </div>
    </main>
  </div>

  <!-- Command Palette Modal -->
  <div id="commandPalette" class="modal hidden">
    <div class="modal-content">
      <input type="text" id="commandSearch" placeholder="Type a command..." />
      <div id="commandResults"></div>
    </div>
  </div>

  <script>
    // Configuration paths
    const CONFIG = {
      configPath: '{{Escape(configPath)}}',
      statusPath: '{{Escape(statusPath)}}',
      backfillPath: '{{Escape(backfillPath)}}'
    };

    // API client
    async function api(endpoint, options = {}) {
      const response = await fetch(endpoint, {
        headers: { 'Content-Type': 'application/json', ...options.headers },
        ...options
      });
      if (!response.ok) {
        const error = await response.text();
        throw new Error(error || response.statusText);
      }
      return response.json().catch(() => null);
    }

    // Load initial data
    async function loadDashboard() {
      try {
        const status = await api('/api/status');
        if (status) {
          document.getElementById('publishedCount').textContent = formatNumber(status.metrics?.published || 0);
          document.getElementById('droppedCount').textContent = formatNumber(status.metrics?.dropped || 0);
          document.getElementById('integrityCount').textContent = formatNumber(status.metrics?.integrity || 0);
          document.getElementById('historicalCount').textContent = formatNumber(status.metrics?.historicalBars || 0);

          const statusDot = document.querySelector('#connectionStatus .status-dot');
          const statusText = document.querySelector('#connectionStatus span:last-child');
          if (status.isConnected) {
            statusDot.className = 'status-dot connected';
            statusText.textContent = 'Connected';
          }
        }
      } catch (e) {
        console.error('Failed to load dashboard:', e);
      }
    }

    function formatNumber(n) {
      if (n >= 1e9) return (n / 1e9).toFixed(1) + 'B';
      if (n >= 1e6) return (n / 1e6).toFixed(1) + 'M';
      if (n >= 1e3) return (n / 1e3).toFixed(1) + 'K';
      return n.toString();
    }

    function openCommandPalette() {
      document.getElementById('commandPalette').classList.remove('hidden');
      document.getElementById('commandSearch').focus();
    }

    // Keyboard shortcuts
    document.addEventListener('keydown', (e) => {
      if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
        e.preventDefault();
        openCommandPalette();
      }
      if (e.key === 'Escape') {
        document.getElementById('commandPalette').classList.add('hidden');
      }
    });

    // Initialize
    loadDashboard();
    setInterval(loadDashboard, 5000);
  </script>
</body>
</html>
""";
    }

    /// <summary>
    /// Generates the providers comparison view HTML page.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    /// <returns>Complete HTML document string.</returns>
    public static string ProvidersView(string configPath)
    {
        return $$"""
<!doctype html>
<html>
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width,initial-scale=1" />
  <title>MDC - Provider Comparison</title>
  <link rel="stylesheet" href="/static/dashboard.css" />
</head>
<body>
  <div class="top-bar">
    <div class="logo">
      <div class="logo-icon">MDC</div>
      <span class="logo-text">Provider Comparison</span>
    </div>
    <a href="/" class="btn-secondary">Back to Dashboard</a>
  </div>

  <div class="main-container">
    <main class="content" style="max-width: 1400px; margin: 0 auto;">
      <div class="section-header">
        <h2 class="section-title">Provider Comparison</h2>
      </div>

      <div class="metrics-grid" id="providerMetrics">
        <!-- Provider cards will be loaded dynamically -->
      </div>

      <div class="card">
        <h3>Detailed Metrics</h3>
        <table id="providerTable">
          <thead>
            <tr>
              <th>Provider</th>
              <th>Type</th>
              <th>Trades</th>
              <th>Quotes</th>
              <th>Depth</th>
              <th>Latency (ms)</th>
              <th>Quality</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody></tbody>
        </table>
      </div>
    </main>
  </div>

  <script>
    async function loadProviders() {
      try {
        const response = await fetch('/api/providers/comparison');
        const data = await response.json();

        renderProviderMetrics(data.providers);
        renderProviderTable(data.providers);
      } catch (e) {
        console.error('Failed to load providers:', e);
      }
    }

    function renderProviderMetrics(providers) {
      const container = document.getElementById('providerMetrics');
      container.innerHTML = providers.map(p => `
        <div class="metric-card ${p.dataQualityScore >= 90 ? 'success' : p.dataQualityScore >= 70 ? 'warning' : 'danger'}">
          <div class="metric-value">${p.providerType}</div>
          <div class="metric-label">${formatNumber(p.tradesReceived)} trades</div>
          <div class="metric-trend">${p.dataQualityScore.toFixed(1)}% quality</div>
        </div>
      `).join('');
    }

    function renderProviderTable(providers) {
      const tbody = document.querySelector('#providerTable tbody');
      tbody.innerHTML = providers.map(p => `
        <tr>
          <td>${p.providerId}</td>
          <td>${p.providerType}</td>
          <td>${formatNumber(p.tradesReceived)}</td>
          <td>${formatNumber(p.quotesReceived)}</td>
          <td>${formatNumber(p.depthUpdatesReceived)}</td>
          <td>${p.averageLatencyMs.toFixed(2)}</td>
          <td>${p.dataQualityScore.toFixed(1)}%</td>
          <td><span class="status-badge ${p.connectionSuccessRate >= 90 ? 'success' : 'warning'}">
            ${p.connectionSuccessRate >= 90 ? 'Healthy' : 'Degraded'}
          </span></td>
        </tr>
      `).join('');
    }

    function formatNumber(n) {
      if (n >= 1e6) return (n / 1e6).toFixed(1) + 'M';
      if (n >= 1e3) return (n / 1e3).toFixed(1) + 'K';
      return n.toString();
    }

    loadProviders();
    setInterval(loadProviders, 5000);
  </script>
</body>
</html>
""";
    }

    private static string Escape(string s) => s
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("'", "\\'");
}
