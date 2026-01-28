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
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width,initial-scale=1" />
  <meta name="description" content="Market Data Collector - Real-time market data collection and monitoring dashboard" />
  <title>MDC Terminal</title>
  <link rel="stylesheet" href="/static/dashboard.css" />
</head>
<body>
  <!-- Skip Link for Accessibility -->
  <a href="#main-content" class="skip-link">Skip to main content</a>

  <!-- Top Navigation Bar -->
  <header class="top-bar" role="banner">
    <div class="logo">
      <button class="mobile-menu-toggle" id="mobileMenuToggle" aria-label="Toggle navigation menu" aria-expanded="false" aria-controls="sidebar">
        <span class="hamburger-icon">&#x2630;</span>
      </button>
      <div class="logo-icon" aria-hidden="true">MDC</div>
      <span class="logo-text">Market Data Collector</span>
      <span class="logo-version">v1.5.0</span>
    </div>
    <button class="cmd-palette" onclick="openCommandPalette()" aria-label="Open command palette (Ctrl+K)" tabindex="0">
      <span class="cmd-palette-icon" aria-hidden="true">&#x1F50D;</span>
      <span class="cmd-palette-text">Search commands...</span>
      <span class="cmd-palette-shortcut"><kbd>Ctrl</kbd>+<kbd>K</kbd></span>
    </button>
    <div class="top-status">
      <div class="status-indicator" id="connectionStatus" role="status" aria-live="polite" aria-label="Connection status: Disconnected">
        <div class="status-dot disconnected" aria-hidden="true"></div>
        <span>Disconnected</span>
      </div>
    </div>
  </header>

  <!-- Main Container -->
  <div class="main-container">
    <aside class="sidebar" id="sidebar" role="navigation" aria-label="Main navigation">
      <nav class="nav-section" aria-labelledby="nav-overview">
        <div class="nav-section-title" id="nav-overview">Overview</div>
        <a class="nav-item active" href="#dashboard" aria-current="page">Dashboard</a>
        <a class="nav-item" href="#providers">Providers</a>
      </nav>
      <nav class="nav-section" aria-labelledby="nav-config">
        <div class="nav-section-title" id="nav-config">Configuration</div>
        <a class="nav-item" href="#config">Config</a>
        <a class="nav-item" href="#storage">Storage</a>
        <a class="nav-item" href="#datasources">Data Sources</a>
      </nav>
      <nav class="nav-section" aria-labelledby="nav-data">
        <div class="nav-section-title" id="nav-data">Data</div>
        <a class="nav-item" href="#symbols">Symbols</a>
        <a class="nav-item" href="#backfill">Backfill</a>
      </nav>
      <div class="sidebar-footer">
        <button class="btn btn-secondary" onclick="openShortcutsHelp()" aria-label="Show keyboard shortcuts">
          <span aria-hidden="true">?</span> Shortcuts
        </button>
      </div>
    </aside>

    <main class="content" id="main-content" role="main" aria-label="Dashboard content">
      <!-- Dashboard Section -->
      <section id="dashboard-section" class="content-section" aria-labelledby="dashboard-heading">
        <h1 id="dashboard-heading" class="section-title sr-only">Dashboard</h1>

        <!-- Dashboard metrics and status -->
        <div class="metrics-grid" role="region" aria-label="Real-time metrics">
          <div class="metric-card success" tabindex="0" aria-label="Events Published">
            <div class="metric-value success" id="publishedCount" aria-live="polite">0</div>
            <div class="metric-label">Events Published</div>
            <div class="metric-trend" id="publishedTrend"></div>
          </div>
          <div class="metric-card danger" tabindex="0" aria-label="Events Dropped">
            <div class="metric-value danger" id="droppedCount" aria-live="polite">0</div>
            <div class="metric-label">Events Dropped</div>
            <div class="metric-trend" id="droppedTrend"></div>
          </div>
          <div class="metric-card warning" tabindex="0" aria-label="Integrity Events">
            <div class="metric-value warning" id="integrityCount" aria-live="polite">0</div>
            <div class="metric-label">Integrity Events</div>
            <div class="metric-trend" id="integrityTrend"></div>
          </div>
          <div class="metric-card info" tabindex="0" aria-label="Historical Bars">
            <div class="metric-value info" id="historicalCount" aria-live="polite">0</div>
            <div class="metric-label">Historical Bars</div>
            <div class="metric-trend" id="historicalTrend"></div>
          </div>
        </div>

        <!-- Quick Actions -->
        <div class="card" role="region" aria-label="Quick actions">
          <div class="card-header">
            <h2 class="card-title">Quick Actions</h2>
            <span class="uptime-badge" id="uptimeBadge" aria-label="Collector uptime">Uptime: --</span>
          </div>
          <div class="quick-actions-grid">
            <button class="btn btn-primary" id="startCollectorBtn" onclick="startCollector()" aria-describedby="start-hint">
              <span aria-hidden="true">&#x25B6;</span> Start Collector
            </button>
            <button class="btn btn-danger" id="stopCollectorBtn" onclick="stopCollector()" disabled aria-describedby="stop-hint">
              <span aria-hidden="true">&#x25A0;</span> Stop Collector
            </button>
            <button class="btn" onclick="refreshDashboard()" aria-label="Refresh dashboard data">
              <span aria-hidden="true">&#x21BB;</span> Refresh
            </button>
            <button class="btn" onclick="exportData()" aria-label="Export collected data">
              <span aria-hidden="true">&#x21E9;</span> Export Data
            </button>
          </div>
          <div class="sr-only" id="start-hint">Press to start collecting market data</div>
          <div class="sr-only" id="stop-hint">Press to stop the data collector</div>
        </div>

        <!-- Active Streams -->
        <div class="card" role="region" aria-label="Active data streams">
          <h2 class="card-title">Active Streams</h2>
          <div class="streams-grid" id="streamsGrid">
            <div class="stream-badge active" aria-label="Trades stream active">
              <span class="stream-icon" aria-hidden="true">&#x21C4;</span>
              <span class="stream-name">Trades</span>
              <span class="stream-count" id="tradesStreamCount">0</span>
            </div>
            <div class="stream-badge" aria-label="Depth stream inactive">
              <span class="stream-icon" aria-hidden="true">&#x2261;</span>
              <span class="stream-name">Depth</span>
              <span class="stream-count" id="depthStreamCount">0</span>
            </div>
            <div class="stream-badge" aria-label="Quotes stream inactive">
              <span class="stream-icon" aria-hidden="true">&#x275D;</span>
              <span class="stream-name">Quotes</span>
              <span class="stream-count" id="quotesStreamCount">0</span>
            </div>
          </div>
        </div>
      </section>

      <!-- Config Section -->
      <section id="config-section" class="content-section hidden" aria-labelledby="config-heading">
        <h1 id="config-heading" class="section-title">Configuration</h1>
        <div class="card">
          <form id="configForm" aria-label="Configuration settings">
            <div class="form-group">
              <label for="dataSource" class="form-label required">Active Data Source</label>
              <select id="dataSource" class="form-select" required aria-required="true">
                <option value="">Select a data source...</option>
                <option value="Alpaca">Alpaca Markets</option>
                <option value="InteractiveBrokers">Interactive Brokers</option>
                <option value="Polygon">Polygon.io</option>
                <option value="NYSE">NYSE</option>
              </select>
              <div class="form-hint">Select the primary data source for real-time market data</div>
            </div>
            <div class="form-actions">
              <button type="submit" class="btn btn-primary">Save Configuration</button>
              <button type="button" class="btn" onclick="resetConfigForm()">Reset</button>
            </div>
          </form>
        </div>
      </section>

      <!-- Storage Section -->
      <section id="storage-section" class="content-section hidden" aria-labelledby="storage-heading">
        <h1 id="storage-heading" class="section-title">Storage Settings</h1>
        <div class="card">
          <form id="storageForm" aria-label="Storage configuration">
            <div class="form-row">
              <div class="form-group">
                <label for="dataRoot" class="form-label required">Data Root Directory</label>
                <input type="text" id="dataRoot" class="form-input" placeholder="/data/market-data" required aria-required="true" />
              </div>
              <div class="form-group">
                <label for="namingConvention" class="form-label">Naming Convention</label>
                <select id="namingConvention" class="form-select">
                  <option value="BySymbol">By Symbol</option>
                  <option value="ByDate">By Date</option>
                  <option value="ByType">By Type</option>
                  <option value="Flat">Flat</option>
                </select>
              </div>
            </div>
            <div class="form-row">
              <div class="form-group">
                <label class="form-label">Compression</label>
                <div class="checkbox-group">
                  <label class="checkbox-label">
                    <input type="checkbox" id="enableCompression" />
                    <span>Enable Compression</span>
                  </label>
                </div>
              </div>
              <div class="form-group">
                <label for="compressionType" class="form-label">Compression Type</label>
                <select id="compressionType" class="form-select">
                  <option value="gzip">Gzip (Standard)</option>
                  <option value="lz4">LZ4 (Fast)</option>
                  <option value="zstd">ZSTD (High Compression)</option>
                </select>
              </div>
            </div>
            <div class="form-actions">
              <button type="submit" class="btn btn-primary">Save Storage Settings</button>
            </div>
          </form>
        </div>

        <!-- Storage Statistics -->
        <div class="card">
          <h2 class="card-title">Storage Statistics</h2>
          <div class="stats-grid">
            <div class="stat-item">
              <span class="stat-value" id="totalStorageUsed">--</span>
              <span class="stat-label">Total Storage Used</span>
            </div>
            <div class="stat-item">
              <span class="stat-value" id="filesCount">--</span>
              <span class="stat-label">Total Files</span>
            </div>
            <div class="stat-item">
              <span class="stat-value" id="oldestData">--</span>
              <span class="stat-label">Oldest Data</span>
            </div>
            <div class="stat-item">
              <span class="stat-value" id="newestData">--</span>
              <span class="stat-label">Newest Data</span>
            </div>
          </div>
        </div>
      </section>

      <!-- Data Sources Section -->
      <section id="datasources-section" class="content-section hidden" aria-labelledby="datasources-heading">
        <h1 id="datasources-heading" class="section-title">Data Sources</h1>
        <div class="card">
          <div class="card-header">
            <h2 class="card-title">Configured Providers</h2>
            <button class="btn btn-primary" onclick="openAddProviderModal()">Add Provider</button>
          </div>
          <div id="providersList" class="providers-list" role="list" aria-label="List of configured data providers">
            <div class="empty-state">
              <div class="empty-state-icon" aria-hidden="true">&#x1F4E1;</div>
              <div class="empty-state-title">No providers configured</div>
              <p>Add a data provider to start collecting market data</p>
            </div>
          </div>
        </div>
      </section>

      <!-- Symbols Section -->
      <section id="symbols-section" class="content-section hidden" aria-labelledby="symbols-heading">
        <h1 id="symbols-heading" class="section-title">Symbols</h1>
        <div class="card">
          <div class="card-header">
            <h2 class="card-title">Subscribed Symbols</h2>
            <div class="header-actions">
              <input type="text" id="symbolSearch" class="form-input" placeholder="Search symbols..." aria-label="Search symbols" />
              <button class="btn btn-primary" onclick="openAddSymbolModal()">Add Symbol</button>
            </div>
          </div>
          <div id="symbolsList" class="symbols-table-container">
            <table class="symbols-table" role="table" aria-label="Subscribed symbols list">
              <thead>
                <tr>
                  <th scope="col">Symbol</th>
                  <th scope="col">Streams</th>
                  <th scope="col">Status</th>
                  <th scope="col">Events</th>
                  <th scope="col">Last Update</th>
                  <th scope="col">Actions</th>
                </tr>
              </thead>
              <tbody id="symbolsTableBody">
                <tr>
                  <td colspan="6" class="text-muted">No symbols subscribed</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </section>

      <!-- Backfill Section -->
      <section id="backfill-section" class="content-section hidden" aria-labelledby="backfill-heading">
        <h1 id="backfill-heading" class="section-title">Historical Backfill</h1>
        <div class="card">
          <form id="backfillForm" aria-label="Backfill configuration">
            <div class="form-row">
              <div class="form-group">
                <label for="backfillProvider" class="form-label required">Provider</label>
                <select id="backfillProvider" class="form-select" required aria-required="true">
                  <option value="">Select provider...</option>
                  <option value="alpaca">Alpaca</option>
                  <option value="polygon">Polygon</option>
                  <option value="yahoo">Yahoo Finance</option>
                  <option value="tiingo">Tiingo</option>
                  <option value="stooq">Stooq</option>
                </select>
              </div>
              <div class="form-group">
                <label for="backfillSymbols" class="form-label required">Symbols</label>
                <input type="text" id="backfillSymbols" class="form-input" placeholder="SPY, AAPL, MSFT" required aria-required="true" />
                <div class="form-hint">Comma-separated list of symbols</div>
              </div>
            </div>
            <div class="form-row">
              <div class="form-group">
                <label for="backfillFrom" class="form-label required">From Date</label>
                <input type="date" id="backfillFrom" class="form-input" required aria-required="true" />
              </div>
              <div class="form-group">
                <label for="backfillTo" class="form-label required">To Date</label>
                <input type="date" id="backfillTo" class="form-input" required aria-required="true" />
              </div>
            </div>
            <div class="form-actions">
              <button type="submit" class="btn btn-primary">Start Backfill</button>
              <button type="button" class="btn" onclick="previewBackfill()">Preview</button>
            </div>
          </form>
        </div>

        <!-- Backfill Progress -->
        <div class="card" id="backfillProgressCard" style="display: none;">
          <h2 class="card-title">Backfill Progress</h2>
          <div class="backfill-progress">
            <div class="progress-info">
              <span id="backfillStatus">Processing...</span>
              <span id="backfillPercent">0%</span>
            </div>
            <div class="progress-bar">
              <div class="progress-fill" id="backfillProgressBar" style="width: 0%;" role="progressbar" aria-valuenow="0" aria-valuemin="0" aria-valuemax="100"></div>
            </div>
            <div class="progress-details">
              <span id="backfillProcessed">0 / 0 bars</span>
              <button class="btn btn-danger btn-sm" onclick="cancelBackfill()">Cancel</button>
            </div>
          </div>
        </div>
      </section>
    </main>
  </div>

  <!-- Command Palette Modal -->
  <div id="commandPalette" class="modal hidden" role="dialog" aria-modal="true" aria-labelledby="cmdPaletteTitle">
    <div class="modal-content command-palette-modal">
      <div class="sr-only" id="cmdPaletteTitle">Command Palette</div>
      <input type="text" id="commandSearch" placeholder="Type a command or search..." aria-label="Search commands" autocomplete="off" />
      <div id="commandResults" role="listbox" aria-label="Available commands"></div>
      <div class="command-palette-footer">
        <span><kbd>&#x2191;</kbd><kbd>&#x2193;</kbd> Navigate</span>
        <span><kbd>Enter</kbd> Select</span>
        <span><kbd>Esc</kbd> Close</span>
      </div>
    </div>
  </div>

  <!-- Toast Container for Notifications -->
  <div id="toastContainer" class="toast-container" role="log" aria-live="polite" aria-label="Notifications"></div>

  <!-- Keyboard Shortcuts Help Modal -->
  <div id="shortcutsModal" class="modal hidden" role="dialog" aria-modal="true" aria-labelledby="shortcutsTitle">
    <div class="modal-content" style="max-width: 450px;">
      <div class="modal-header">
        <h3 id="shortcutsTitle">Keyboard Shortcuts</h3>
        <button class="modal-close" onclick="closeShortcutsHelp()" aria-label="Close">&times;</button>
      </div>
      <div class="shortcuts-help">
        <div class="shortcuts-section">
          <h4>General</h4>
          <div class="shortcut-row"><span class="shortcut-key">Ctrl+K</span><span>Open command palette</span></div>
          <div class="shortcut-row"><span class="shortcut-key">F5</span><span>Refresh dashboard data</span></div>
          <div class="shortcut-row"><span class="shortcut-key">?</span><span>Show this help</span></div>
          <div class="shortcut-row"><span class="shortcut-key">Esc</span><span>Close dialogs</span></div>
        </div>
        <div class="shortcuts-section">
          <h4>Collector Control</h4>
          <div class="shortcut-row"><span class="shortcut-key">Ctrl+S</span><span>Start collector</span></div>
          <div class="shortcut-row"><span class="shortcut-key">Ctrl+Shift+S</span><span>Stop collector</span></div>
        </div>
        <div class="shortcuts-section">
          <h4>Navigation</h4>
          <div class="shortcut-row"><span class="shortcut-key">G then D</span><span>Go to Dashboard</span></div>
          <div class="shortcut-row"><span class="shortcut-key">G then C</span><span>Go to Config</span></div>
          <div class="shortcut-row"><span class="shortcut-key">G then S</span><span>Go to Symbols</span></div>
          <div class="shortcut-row"><span class="shortcut-key">G then B</span><span>Go to Backfill</span></div>
        </div>
      </div>
      <div class="modal-footer">
        <button class="btn" onclick="closeShortcutsHelp()">Close</button>
      </div>
    </div>
  </div>

  <!-- Confirmation Dialog Modal -->
  <div id="confirmModal" class="modal hidden" role="alertdialog" aria-modal="true" aria-labelledby="confirmTitle" aria-describedby="confirmMessage">
    <div class="modal-content confirm-modal">
      <div class="modal-header">
        <h3 id="confirmTitle">Confirm Action</h3>
      </div>
      <div class="modal-body">
        <p id="confirmMessage">Are you sure you want to proceed?</p>
      </div>
      <div class="modal-footer">
        <button class="btn" id="confirmCancel" onclick="closeConfirmModal()">Cancel</button>
        <button class="btn btn-danger" id="confirmOk">Confirm</button>
      </div>
    </div>
  </div>

  <!-- Add Symbol Modal -->
  <div id="addSymbolModal" class="modal hidden" role="dialog" aria-modal="true" aria-labelledby="addSymbolTitle">
    <div class="modal-content">
      <div class="modal-header">
        <h3 id="addSymbolTitle">Add Symbol</h3>
        <button class="modal-close" onclick="closeAddSymbolModal()" aria-label="Close">&times;</button>
      </div>
      <form id="addSymbolForm" class="modal-body">
        <div class="form-group">
          <label for="newSymbol" class="form-label required">Symbol</label>
          <input type="text" id="newSymbol" class="form-input" placeholder="e.g., AAPL" required pattern="^[A-Z0-9.]+$" aria-required="true" />
          <div class="form-hint">Enter a valid stock symbol (uppercase letters and numbers)</div>
        </div>
        <div class="form-group">
          <label class="form-label">Data Streams</label>
          <div class="checkbox-group">
            <label class="checkbox-label">
              <input type="checkbox" name="streams" value="trades" checked />
              <span>Trades</span>
            </label>
            <label class="checkbox-label">
              <input type="checkbox" name="streams" value="depth" />
              <span>Market Depth</span>
            </label>
            <label class="checkbox-label">
              <input type="checkbox" name="streams" value="quotes" />
              <span>Quotes</span>
            </label>
          </div>
        </div>
      </form>
      <div class="modal-footer">
        <button class="btn" onclick="closeAddSymbolModal()">Cancel</button>
        <button class="btn btn-primary" onclick="submitAddSymbol()">Add Symbol</button>
      </div>
    </div>
  </div>

  <script>
    // Configuration paths
    const CONFIG = {
      configPath: '{{Escape(configPath)}}',
      statusPath: '{{Escape(statusPath)}}',
      backfillPath: '{{Escape(backfillPath)}}'
    };

    // Command definitions for command palette
    const COMMANDS = [
      { id: 'dashboard', name: 'Go to Dashboard', shortcut: 'G D', action: () => navigateTo('dashboard'), category: 'Navigation' },
      { id: 'config', name: 'Go to Configuration', shortcut: 'G C', action: () => navigateTo('config'), category: 'Navigation' },
      { id: 'storage', name: 'Go to Storage', shortcut: '', action: () => navigateTo('storage'), category: 'Navigation' },
      { id: 'datasources', name: 'Go to Data Sources', shortcut: '', action: () => navigateTo('datasources'), category: 'Navigation' },
      { id: 'symbols', name: 'Go to Symbols', shortcut: 'G S', action: () => navigateTo('symbols'), category: 'Navigation' },
      { id: 'backfill', name: 'Go to Backfill', shortcut: 'G B', action: () => navigateTo('backfill'), category: 'Navigation' },
      { id: 'providers', name: 'View Provider Comparison', shortcut: '', action: () => window.location.href = '/providers', category: 'Navigation' },
      { id: 'start', name: 'Start Collector', shortcut: 'Ctrl+S', action: () => startCollector(), category: 'Actions' },
      { id: 'stop', name: 'Stop Collector', shortcut: 'Ctrl+Shift+S', action: () => stopCollector(), category: 'Actions' },
      { id: 'refresh', name: 'Refresh Dashboard', shortcut: 'F5', action: () => refreshDashboard(), category: 'Actions' },
      { id: 'export', name: 'Export Data', shortcut: '', action: () => exportData(), category: 'Actions' },
      { id: 'add-symbol', name: 'Add New Symbol', shortcut: '', action: () => openAddSymbolModal(), category: 'Actions' },
      { id: 'shortcuts', name: 'Show Keyboard Shortcuts', shortcut: '?', action: () => openShortcutsHelp(), category: 'Help' },
    ];

    // Toast notification system
    const Toast = {
      container: null,
      init() {
        this.container = document.getElementById('toastContainer');
      },
      show(type, title, message, duration = 5000) {
        const icons = {
          success: '&#x2713;',
          error: '&#x2717;',
          warning: '&#x26A0;',
          info: '&#x2139;'
        };

        const toast = document.createElement('div');
        toast.className = `toast ${type}`;
        toast.setAttribute('role', 'alert');
        toast.innerHTML = `
          <span class="toast-icon">${icons[type] || icons.info}</span>
          <div class="toast-content">
            <div class="toast-title">${this.escapeHtml(title)}</div>
            <div class="toast-message">${this.escapeHtml(message)}</div>
          </div>
          <button class="toast-close" onclick="Toast.dismiss(this.parentElement)" aria-label="Dismiss">&#x2715;</button>
        `;

        this.container.appendChild(toast);

        // Error messages stay longer
        const displayDuration = type === 'error' ? Math.max(duration, 8000) : duration;

        if (displayDuration > 0) {
          setTimeout(() => this.dismiss(toast), displayDuration);
        }

        return toast;
      },
      dismiss(toast) {
        if (!toast || !toast.parentElement) return;
        toast.classList.add('removing');
        setTimeout(() => toast.remove(), 300);
      },
      escapeHtml(str) {
        const div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
      },
      success(title, message) { return this.show('success', title, message); },
      error(title, message) { return this.show('error', title, message, 10000); },
      warning(title, message) { return this.show('warning', title, message, 7000); },
      info(title, message) { return this.show('info', title, message); }
    };

    // Enhanced API client with retry logic and better error handling
    const Api = {
      retryCount: 3,
      retryDelay: 1000,

      async request(endpoint, options = {}) {
        let lastError;

        for (let attempt = 1; attempt <= this.retryCount; attempt++) {
          try {
            const controller = new AbortController();
            const timeoutId = setTimeout(() => controller.abort(), options.timeout || 10000);

            const response = await fetch(endpoint, {
              headers: { 'Content-Type': 'application/json', ...options.headers },
              signal: controller.signal,
              ...options
            });

            clearTimeout(timeoutId);

            if (!response.ok) {
              const errorText = await response.text();
              const error = new Error(errorText || response.statusText);
              error.status = response.status;
              error.statusText = response.statusText;
              throw error;
            }

            const data = await response.json().catch(() => null);
            return { success: true, data };
          } catch (error) {
            lastError = error;

            // Don't retry on certain errors
            if (error.status && error.status >= 400 && error.status < 500) {
              break;
            }

            // Don't retry on abort
            if (error.name === 'AbortError') {
              lastError = new Error('Request timed out. Please check your connection.');
              break;
            }

            // Wait before retry (exponential backoff)
            if (attempt < this.retryCount) {
              await new Promise(r => setTimeout(r, this.retryDelay * attempt));
            }
          }
        }

        return {
          success: false,
          error: lastError,
          message: this.getUserFriendlyError(lastError)
        };
      },

      getUserFriendlyError(error) {
        if (!navigator.onLine) {
          return 'You appear to be offline. Please check your internet connection.';
        }
        if (error.name === 'AbortError' || error.message?.includes('timeout')) {
          return 'The request took too long. The server might be busy.';
        }
        if (error.status === 401) {
          return 'Authentication required. Please check your credentials.';
        }
        if (error.status === 403) {
          return 'Access denied. You may not have permission for this action.';
        }
        if (error.status === 404) {
          return 'The requested resource was not found.';
        }
        if (error.status === 429) {
          return 'Too many requests. Please wait a moment and try again.';
        }
        if (error.status >= 500) {
          return 'Server error. Please try again later or contact support.';
        }
        return error.message || 'An unexpected error occurred.';
      },

      async get(endpoint, options = {}) {
        return this.request(endpoint, { method: 'GET', ...options });
      },

      async post(endpoint, data, options = {}) {
        return this.request(endpoint, {
          method: 'POST',
          body: JSON.stringify(data),
          ...options
        });
      }
    };

    // Form validation
    const Validator = {
      rules: {
        required: (value) => value?.trim() ? null : 'This field is required',
        symbol: (value) => /^[A-Z0-9.]+$/i.test(value?.trim()) ? null : 'Invalid symbol format',
        dateRange: (from, to) => {
          if (!from || !to) return 'Both dates are required';
          if (new Date(from) > new Date(to)) return 'Start date must be before end date';
          return null;
        }
      },

      validateField(input, rules = []) {
        const value = input.value;
        const errorContainer = input.parentElement?.querySelector('.form-error') ||
                              this.createErrorElement(input);

        for (const rule of rules) {
          const error = typeof rule === 'function' ? rule(value) : this.rules[rule]?.(value);
          if (error) {
            input.classList.add('error');
            input.classList.remove('success');
            input.setAttribute('aria-invalid', 'true');
            errorContainer.textContent = error;
            errorContainer.style.display = 'flex';
            return false;
          }
        }

        input.classList.remove('error');
        input.classList.add('success');
        input.setAttribute('aria-invalid', 'false');
        errorContainer.style.display = 'none';
        return true;
      },

      createErrorElement(input) {
        const error = document.createElement('div');
        error.className = 'form-error';
        error.setAttribute('role', 'alert');
        error.style.display = 'none';
        input.parentElement?.appendChild(error);
        return error;
      }
    };

    // Loading state manager
    const Loading = {
      show(element, message = 'Loading...') {
        if (!element) return;
        element.classList.add('loading');
        element.setAttribute('aria-busy', 'true');

        // Add spinner if not exists
        if (!element.querySelector('.spinner-overlay')) {
          const overlay = document.createElement('div');
          overlay.className = 'spinner-overlay';
          overlay.innerHTML = `<div class="spinner"></div><span class="sr-only">${message}</span>`;
          overlay.style.cssText = 'position:absolute;inset:0;display:flex;align-items:center;justify-content:center;background:rgba(13,17,23,0.7);border-radius:inherit;';
          element.style.position = 'relative';
          element.appendChild(overlay);
        }
      },

      hide(element) {
        if (!element) return;
        element.classList.remove('loading');
        element.setAttribute('aria-busy', 'false');
        element.querySelector('.spinner-overlay')?.remove();
      }
    };

    // Dashboard state
    let dashboardState = {
      isConnected: false,
      lastUpdate: null,
      metrics: {
        published: 0,
        dropped: 0,
        integrity: 0,
        historicalBars: 0
      }
    };

    // Load dashboard data with proper error handling
    async function loadDashboard() {
      const metricsGrid = document.querySelector('.metrics-grid');

      const result = await Api.get('/api/status');

      if (result.success && result.data) {
        const status = result.data;
        dashboardState = {
          isConnected: status.isConnected,
          lastUpdate: new Date(),
          metrics: status.metrics || dashboardState.metrics
        };

        // Update metrics with animation
        updateMetricWithAnimation('publishedCount', dashboardState.metrics.published || 0);
        updateMetricWithAnimation('droppedCount', dashboardState.metrics.dropped || 0);
        updateMetricWithAnimation('integrityCount', dashboardState.metrics.integrity || 0);
        updateMetricWithAnimation('historicalCount', dashboardState.metrics.historicalBars || 0);

        // Update connection status
        updateConnectionStatus(status.isConnected);
      } else {
        // Show error only on first failure or after being connected
        if (dashboardState.isConnected || !dashboardState.lastUpdate) {
          updateConnectionStatus(false);
          Toast.warning('Connection Issue', result.message || 'Unable to fetch status');
        }
      }
    }

    function updateMetricWithAnimation(elementId, value) {
      const element = document.getElementById(elementId);
      if (!element) return;

      const formattedValue = formatNumber(value);
      if (element.textContent !== formattedValue) {
        element.style.transform = 'scale(1.1)';
        element.textContent = formattedValue;
        setTimeout(() => element.style.transform = '', 200);
      }
    }

    function updateConnectionStatus(isConnected) {
      const statusDot = document.querySelector('#connectionStatus .status-dot');
      const statusText = document.querySelector('#connectionStatus span:last-child');

      if (statusDot && statusText) {
        statusDot.className = `status-dot ${isConnected ? 'connected' : 'disconnected'}`;
        statusText.textContent = isConnected ? 'Connected' : 'Disconnected';

        // Update ARIA
        const indicator = document.querySelector('#connectionStatus');
        indicator?.setAttribute('aria-label', `Connection status: ${isConnected ? 'Connected' : 'Disconnected'}`);
      }
    }

    function formatNumber(n) {
      if (n >= 1e9) return (n / 1e9).toFixed(1) + 'B';
      if (n >= 1e6) return (n / 1e6).toFixed(1) + 'M';
      if (n >= 1e3) return (n / 1e3).toFixed(1) + 'K';
      return n.toLocaleString();
    }

    // Command palette state
    let commandPaletteIndex = 0;
    let filteredCommands = [...COMMANDS];

    function openCommandPalette() {
      const modal = document.getElementById('commandPalette');
      const input = document.getElementById('commandSearch');

      modal.classList.remove('hidden');
      modal.setAttribute('aria-hidden', 'false');
      input.value = '';
      commandPaletteIndex = 0;
      renderCommandResults('');
      input.focus();

      document.body.style.overflow = 'hidden';
    }

    function closeCommandPalette() {
      const modal = document.getElementById('commandPalette');
      modal.classList.add('hidden');
      modal.setAttribute('aria-hidden', 'true');
      document.body.style.overflow = '';
    }

    function renderCommandResults(query) {
      const container = document.getElementById('commandResults');
      const q = query.toLowerCase().trim();

      filteredCommands = q
        ? COMMANDS.filter(cmd =>
            cmd.name.toLowerCase().includes(q) ||
            cmd.category.toLowerCase().includes(q)
          )
        : [...COMMANDS];

      if (filteredCommands.length === 0) {
        container.innerHTML = '<div class="command-empty">No matching commands found</div>';
        return;
      }

      // Group by category
      const grouped = filteredCommands.reduce((acc, cmd) => {
        (acc[cmd.category] = acc[cmd.category] || []).push(cmd);
        return acc;
      }, {});

      let html = '';
      let index = 0;
      for (const [category, commands] of Object.entries(grouped)) {
        html += `<div class="command-category">${category}</div>`;
        for (const cmd of commands) {
          const selected = index === commandPaletteIndex ? 'selected' : '';
          html += `
            <div class="command-item ${selected}" role="option" data-index="${index}" onclick="executeCommand(${index})" aria-selected="${index === commandPaletteIndex}">
              <span class="command-name">${cmd.name}</span>
              ${cmd.shortcut ? `<span class="command-shortcut">${cmd.shortcut}</span>` : ''}
            </div>`;
          index++;
        }
      }
      container.innerHTML = html;
    }

    function executeCommand(index) {
      if (index >= 0 && index < filteredCommands.length) {
        const cmd = filteredCommands[index];
        closeCommandPalette();
        cmd.action();
      }
    }

    // Command palette input handling
    document.getElementById('commandSearch')?.addEventListener('input', (e) => {
      commandPaletteIndex = 0;
      renderCommandResults(e.target.value);
    });

    document.getElementById('commandSearch')?.addEventListener('keydown', (e) => {
      if (e.key === 'ArrowDown') {
        e.preventDefault();
        commandPaletteIndex = Math.min(commandPaletteIndex + 1, filteredCommands.length - 1);
        renderCommandResults(e.target.value);
      } else if (e.key === 'ArrowUp') {
        e.preventDefault();
        commandPaletteIndex = Math.max(commandPaletteIndex - 1, 0);
        renderCommandResults(e.target.value);
      } else if (e.key === 'Enter') {
        e.preventDefault();
        executeCommand(commandPaletteIndex);
      }
    });

    function openShortcutsHelp() {
      const modal = document.getElementById('shortcutsModal');
      modal.classList.remove('hidden');
      modal.setAttribute('aria-hidden', 'false');
      document.body.style.overflow = 'hidden';
    }

    function closeShortcutsHelp() {
      const modal = document.getElementById('shortcutsModal');
      modal.classList.add('hidden');
      modal.setAttribute('aria-hidden', 'true');
      document.body.style.overflow = '';
    }

    // Confirmation dialog
    let confirmCallback = null;

    function showConfirm(title, message, onConfirm) {
      document.getElementById('confirmTitle').textContent = title;
      document.getElementById('confirmMessage').textContent = message;
      confirmCallback = onConfirm;

      const modal = document.getElementById('confirmModal');
      modal.classList.remove('hidden');
      modal.setAttribute('aria-hidden', 'false');
      document.getElementById('confirmCancel').focus();
      document.body.style.overflow = 'hidden';

      document.getElementById('confirmOk').onclick = () => {
        closeConfirmModal();
        if (confirmCallback) confirmCallback();
      };
    }

    function closeConfirmModal() {
      const modal = document.getElementById('confirmModal');
      modal.classList.add('hidden');
      modal.setAttribute('aria-hidden', 'true');
      document.body.style.overflow = '';
      confirmCallback = null;
    }

    // Navigation
    function navigateTo(section) {
      window.location.hash = section;
      updateActiveSection();
    }

    function updateActiveSection() {
      const hash = window.location.hash.replace('#', '') || 'dashboard';

      // Hide all sections
      document.querySelectorAll('.content-section').forEach(section => {
        section.classList.add('hidden');
      });

      // Show target section
      const targetSection = document.getElementById(`${hash}-section`);
      if (targetSection) {
        targetSection.classList.remove('hidden');
      } else if (hash === 'providers') {
        window.location.href = '/providers';
        return;
      } else {
        // Default to dashboard
        document.getElementById('dashboard-section')?.classList.remove('hidden');
      }

      // Update nav active state
      document.querySelectorAll('.nav-item').forEach(item => {
        const href = item.getAttribute('href');
        const isActive = href === `#${hash}` || (hash === 'dashboard' && href === '#dashboard');
        item.classList.toggle('active', isActive);
        item.setAttribute('aria-current', isActive ? 'page' : 'false');
      });

      // Close mobile menu if open
      document.getElementById('sidebar')?.classList.remove('open');
      document.getElementById('mobileMenuToggle')?.setAttribute('aria-expanded', 'false');
    }

    // Mobile menu toggle
    document.getElementById('mobileMenuToggle')?.addEventListener('click', () => {
      const sidebar = document.getElementById('sidebar');
      const toggle = document.getElementById('mobileMenuToggle');
      const isOpen = sidebar.classList.toggle('open');
      toggle.setAttribute('aria-expanded', isOpen);
    });

    // Collector control
    let isCollectorRunning = false;
    let collectorStartTime = null;
    let uptimeInterval = null;

    async function startCollector() {
      if (isCollectorRunning) {
        Toast.warning('Already Running', 'The collector is already running.');
        return;
      }

      const result = await Api.post('/api/collector/start');
      if (result.success) {
        isCollectorRunning = true;
        collectorStartTime = new Date();
        updateCollectorButtons();
        startUptimeTimer();
        Toast.success('Started', 'Market data collector is now running.');
      } else {
        Toast.error('Failed to Start', result.message);
      }
    }

    async function stopCollector() {
      if (!isCollectorRunning) {
        Toast.warning('Not Running', 'The collector is not currently running.');
        return;
      }

      showConfirm(
        'Stop Collector?',
        'Are you sure you want to stop the market data collector? Any active subscriptions will be disconnected.',
        async () => {
          const result = await Api.post('/api/collector/stop');
          if (result.success) {
            isCollectorRunning = false;
            collectorStartTime = null;
            updateCollectorButtons();
            stopUptimeTimer();
            Toast.success('Stopped', 'Market data collector has been stopped.');
          } else {
            Toast.error('Failed to Stop', result.message);
          }
        }
      );
    }

    function updateCollectorButtons() {
      const startBtn = document.getElementById('startCollectorBtn');
      const stopBtn = document.getElementById('stopCollectorBtn');

      if (startBtn) startBtn.disabled = isCollectorRunning;
      if (stopBtn) stopBtn.disabled = !isCollectorRunning;
    }

    function startUptimeTimer() {
      updateUptime();
      uptimeInterval = setInterval(updateUptime, 1000);
    }

    function stopUptimeTimer() {
      if (uptimeInterval) {
        clearInterval(uptimeInterval);
        uptimeInterval = null;
      }
      const badge = document.getElementById('uptimeBadge');
      if (badge) badge.textContent = 'Uptime: --';
    }

    function updateUptime() {
      if (!collectorStartTime) return;
      const diff = Date.now() - collectorStartTime.getTime();
      const hours = Math.floor(diff / 3600000);
      const minutes = Math.floor((diff % 3600000) / 60000);
      const seconds = Math.floor((diff % 60000) / 1000);

      const badge = document.getElementById('uptimeBadge');
      if (badge) badge.textContent = `Uptime: ${hours}h ${minutes}m ${seconds}s`;
    }

    function refreshDashboard() {
      loadDashboard();
      Toast.info('Refreshing', 'Dashboard data is being refreshed...');
    }

    async function exportData() {
      Toast.info('Exporting', 'Preparing data export...');
      const result = await Api.get('/api/data/export');
      if (result.success) {
        Toast.success('Export Ready', 'Your data export is ready for download.');
      } else {
        Toast.error('Export Failed', result.message);
      }
    }

    // Add Symbol Modal
    function openAddSymbolModal() {
      const modal = document.getElementById('addSymbolModal');
      modal.classList.remove('hidden');
      modal.setAttribute('aria-hidden', 'false');
      document.getElementById('newSymbol').focus();
      document.body.style.overflow = 'hidden';
    }

    function closeAddSymbolModal() {
      const modal = document.getElementById('addSymbolModal');
      modal.classList.add('hidden');
      modal.setAttribute('aria-hidden', 'true');
      document.getElementById('addSymbolForm').reset();
      document.body.style.overflow = '';
    }

    async function submitAddSymbol() {
      const symbolInput = document.getElementById('newSymbol');
      const symbol = symbolInput.value.trim().toUpperCase();

      if (!Validator.validateField(symbolInput, ['required', 'symbol'])) {
        return;
      }

      const streams = Array.from(document.querySelectorAll('input[name="streams"]:checked'))
        .map(cb => cb.value);

      if (streams.length === 0) {
        Toast.warning('Select Streams', 'Please select at least one data stream.');
        return;
      }

      const result = await Api.post('/api/config/symbols', { symbol, streams });
      if (result.success) {
        closeAddSymbolModal();
        Toast.success('Symbol Added', `${symbol} has been added with ${streams.join(', ')} streams.`);
        loadDashboard();
      } else {
        Toast.error('Failed to Add', result.message);
      }
    }

    // Form submissions
    document.getElementById('configForm')?.addEventListener('submit', async (e) => {
      e.preventDefault();
      const dataSource = document.getElementById('dataSource').value;

      if (!dataSource) {
        Toast.error('Validation Error', 'Please select a data source.');
        return;
      }

      const result = await Api.post('/api/config/datasource', { dataSource });
      if (result.success) {
        Toast.success('Saved', 'Configuration has been updated.');
      } else {
        Toast.error('Save Failed', result.message);
      }
    });

    document.getElementById('storageForm')?.addEventListener('submit', async (e) => {
      e.preventDefault();
      const data = {
        dataRoot: document.getElementById('dataRoot').value,
        namingConvention: document.getElementById('namingConvention').value,
        enableCompression: document.getElementById('enableCompression').checked,
        compressionType: document.getElementById('compressionType').value
      };

      const result = await Api.post('/api/config/storage', data);
      if (result.success) {
        Toast.success('Saved', 'Storage settings have been updated.');
      } else {
        Toast.error('Save Failed', result.message);
      }
    });

    document.getElementById('backfillForm')?.addEventListener('submit', async (e) => {
      e.preventDefault();
      const fromDate = document.getElementById('backfillFrom').value;
      const toDate = document.getElementById('backfillTo').value;

      const dateError = Validator.rules.dateRange(fromDate, toDate);
      if (dateError) {
        Toast.error('Validation Error', dateError);
        return;
      }

      const data = {
        provider: document.getElementById('backfillProvider').value,
        symbols: document.getElementById('backfillSymbols').value.split(',').map(s => s.trim()),
        fromDate,
        toDate
      };

      document.getElementById('backfillProgressCard').style.display = 'block';

      const result = await Api.post('/api/backfill/start', data);
      if (result.success) {
        Toast.success('Backfill Started', 'Historical data backfill has been initiated.');
        pollBackfillStatus();
      } else {
        document.getElementById('backfillProgressCard').style.display = 'none';
        Toast.error('Backfill Failed', result.message);
      }
    });

    async function pollBackfillStatus() {
      const result = await Api.get('/api/backfill/status');
      if (result.success && result.data) {
        const { progress, status, processed, total } = result.data;

        document.getElementById('backfillStatus').textContent = status;
        document.getElementById('backfillPercent').textContent = `${progress}%`;
        document.getElementById('backfillProgressBar').style.width = `${progress}%`;
        document.getElementById('backfillProgressBar').setAttribute('aria-valuenow', progress);
        document.getElementById('backfillProcessed').textContent = `${processed} / ${total} bars`;

        if (status === 'completed') {
          Toast.success('Backfill Complete', 'Historical data has been downloaded.');
        } else if (status === 'failed') {
          Toast.error('Backfill Failed', 'An error occurred during backfill.');
        } else {
          setTimeout(pollBackfillStatus, 2000);
        }
      }
    }

    async function cancelBackfill() {
      showConfirm(
        'Cancel Backfill?',
        'Are you sure you want to cancel the backfill operation?',
        async () => {
          const result = await Api.post('/api/backfill/cancel');
          if (result.success) {
            document.getElementById('backfillProgressCard').style.display = 'none';
            Toast.info('Cancelled', 'Backfill operation has been cancelled.');
          }
        }
      );
    }

    async function previewBackfill() {
      const symbolsInput = document.getElementById('backfillSymbols')?.value;
      const fromDate = document.getElementById('backfillFrom')?.value;
      const toDate = document.getElementById('backfillTo')?.value;
      const provider = document.getElementById('backfillProvider')?.value || 'stooq';

      if (!symbolsInput || !symbolsInput.trim()) {
        Toast.warning('Missing Input', 'Please enter at least one symbol to preview.');
        return;
      }

      const symbols = symbolsInput.split(',').map(s => s.trim()).filter(s => s.length > 0);
      if (symbols.length === 0) {
        Toast.warning('Missing Input', 'Please enter at least one valid symbol.');
        return;
      }

      Toast.info('Loading...', 'Generating backfill preview...');

      const requestData = {
        provider: provider,
        symbols: symbols
      };
      if (fromDate) requestData.from = fromDate;
      if (toDate) requestData.to = toDate;

      const result = await Api.post('/api/backfill/preview', requestData);

      if (result.success && result.data) {
        const preview = result.data;
        let html = `
          <div style=""text-align: left; font-size: 14px;"">
            <p><strong>Provider:</strong> ${preview.providerDisplayName || preview.provider}</p>
            <p><strong>Date Range:</strong> ${preview.from} to ${preview.to}</p>
            <p><strong>Total Days:</strong> ${preview.totalDays} (${preview.estimatedTradingDays} trading days)</p>
            <p><strong>Estimated Duration:</strong> ${formatDuration(preview.estimatedDurationSeconds)}</p>
            <hr style=""margin: 10px 0;"">
            <p><strong>Symbols (${preview.symbols.length}):</strong></p>
            <ul style=""margin: 5px 0; padding-left: 20px;"">
        `;

        for (const sym of preview.symbols) {
          let status = '';
          if (sym.existingData?.hasData) {
            if (sym.existingData.isComplete) {
              status = ' <span style=""color: #48bb78;"">(complete)</span>';
            } else {
              status = ' <span style=""color: #ed8936;"">(partial: ' + (sym.existingData.existingFrom || '?') + ' to ' + (sym.existingData.existingTo || '?') + ')</span>';
            }
          } else {
            status = ' <span style=""color: #4299e1;"">(new)</span>';
          }
          html += `<li>${sym.symbol} - ~${sym.estimatedBars} bars${status}</li>`;
        }

        html += '</ul>';

        if (preview.notes && preview.notes.length > 0) {
          html += '<hr style=""margin: 10px 0;""><p><strong>Notes:</strong></p><ul style=""margin: 5px 0; padding-left: 20px;"">';
          for (const note of preview.notes) {
            html += `<li>${note}</li>`;
          }
          html += '</ul>';
        }

        html += '</div>';

        showModal('Backfill Preview', html, [
          { text: 'Close', action: 'close', primary: false },
          { text: 'Run Backfill', action: 'run', primary: true }
        ], (action) => {
          if (action === 'run') {
            document.getElementById('backfillForm')?.dispatchEvent(new Event('submit'));
          }
        });
      } else {
        Toast.error('Preview Failed', result.message || 'Failed to generate backfill preview.');
      }
    }

    function formatDuration(seconds) {
      if (seconds < 60) return seconds + ' seconds';
      if (seconds < 3600) return Math.round(seconds / 60) + ' minutes';
      return Math.round(seconds / 3600) + ' hours';
    }

    function showModal(title, content, buttons, callback) {
      // Create modal if it doesn't exist
      let modal = document.getElementById('previewModal');
      if (!modal) {
        modal = document.createElement('div');
        modal.id = 'previewModal';
        modal.className = 'modal hidden';
        modal.innerHTML = `
          <div class=""modal-content"" style=""max-width: 500px;"">
            <h3 id=""previewModalTitle""></h3>
            <div id=""previewModalBody"" style=""max-height: 400px; overflow-y: auto;""></div>
            <div id=""previewModalButtons"" style=""margin-top: 15px; text-align: right;""></div>
          </div>
        `;
        document.body.appendChild(modal);

        modal.addEventListener('click', (e) => {
          if (e.target === modal) {
            modal.classList.add('hidden');
          }
        });
      }

      document.getElementById('previewModalTitle').textContent = title;
      document.getElementById('previewModalBody').innerHTML = content;

      const buttonsContainer = document.getElementById('previewModalButtons');
      buttonsContainer.innerHTML = '';
      for (const btn of buttons) {
        const button = document.createElement('button');
        button.textContent = btn.text;
        button.className = btn.primary ? 'btn btn-primary' : 'btn btn-secondary';
        button.style.marginLeft = '10px';
        button.onclick = () => {
          modal.classList.add('hidden');
          if (callback) callback(btn.action);
        };
        buttonsContainer.appendChild(button);
      }

      modal.classList.remove('hidden');
    }

    // Keyboard shortcuts
    let pendingKey = null;

    document.addEventListener('keydown', (e) => {
      const isInput = ['INPUT', 'TEXTAREA', 'SELECT'].includes(document.activeElement?.tagName);

      // Handle 'G' key sequence for navigation
      if (pendingKey === 'g' && !isInput) {
        e.preventDefault();
        switch (e.key.toLowerCase()) {
          case 'd': navigateTo('dashboard'); break;
          case 'c': navigateTo('config'); break;
          case 's': navigateTo('symbols'); break;
          case 'b': navigateTo('backfill'); break;
        }
        pendingKey = null;
        return;
      }

      if (e.key === 'g' && !isInput && !e.ctrlKey && !e.metaKey) {
        pendingKey = 'g';
        setTimeout(() => { pendingKey = null; }, 1000);
        return;
      }

      // Command palette
      if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
        e.preventDefault();
        openCommandPalette();
        return;
      }

      // Start collector
      if ((e.ctrlKey || e.metaKey) && e.key === 's' && !e.shiftKey) {
        e.preventDefault();
        startCollector();
        return;
      }

      // Stop collector
      if ((e.ctrlKey || e.metaKey) && e.shiftKey && e.key === 'S') {
        e.preventDefault();
        stopCollector();
        return;
      }

      // Refresh
      if (e.key === 'F5') {
        e.preventDefault();
        refreshDashboard();
        return;
      }

      // Help
      if (e.key === '?' && !e.ctrlKey && !e.metaKey && !isInput) {
        e.preventDefault();
        openShortcutsHelp();
        return;
      }

      // Escape - close modals
      if (e.key === 'Escape') {
        closeCommandPalette();
        closeShortcutsHelp();
        closeConfirmModal();
        closeAddSymbolModal();
      }
    });

    // Close modals when clicking outside
    document.querySelectorAll('.modal').forEach(modal => {
      modal.addEventListener('click', (e) => {
        if (e.target === modal) {
          modal.classList.add('hidden');
          modal.setAttribute('aria-hidden', 'true');
          document.body.style.overflow = '';
        }
      });
    });

    window.addEventListener('hashchange', updateActiveSection);

    // Initialize
    document.addEventListener('DOMContentLoaded', () => {
      Toast.init();
      updateActiveSection();
      loadDashboard();

      // Auto-refresh every 5 seconds
      setInterval(loadDashboard, 5000);

      // Show welcome toast on first load
      if (!sessionStorage.getItem('welcomed')) {
        sessionStorage.setItem('welcomed', 'true');
        Toast.info('Welcome', 'Press Ctrl+K for commands or ? for keyboard shortcuts');
      }
    });

    // Online/offline handling
    window.addEventListener('online', () => {
      Toast.success('Back Online', 'Connection restored. Refreshing data...');
      loadDashboard();
    });

    window.addEventListener('offline', () => {
      Toast.warning('Offline', 'You appear to be offline. Data may be stale.');
      updateConnectionStatus(false);
    });
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
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width,initial-scale=1" />
  <meta name="description" content="Market Data Collector - Compare data provider performance and metrics" />
  <title>MDC - Provider Comparison</title>
  <link rel="stylesheet" href="/static/dashboard.css" />
</head>
<body>
  <!-- Skip Link for Accessibility -->
  <a href="#main-content" class="skip-link">Skip to main content</a>

  <header class="top-bar" role="banner">
    <div class="logo">
      <a href="/" class="logo-link" aria-label="Back to dashboard">
        <div class="logo-icon" aria-hidden="true">MDC</div>
      </a>
      <span class="logo-text">Provider Comparison</span>
    </div>
    <div class="top-status">
      <span class="last-update" id="lastUpdate" aria-live="polite">Last updated: --</span>
      <button class="btn" onclick="refreshProviders()" aria-label="Refresh provider data">
        <span aria-hidden="true">&#x21BB;</span> Refresh
      </button>
      <a href="/" class="btn btn-secondary">Back to Dashboard</a>
    </div>
  </header>

  <main class="main-container" id="main-content" role="main">
    <div class="content" style="max-width: 1400px; margin: 0 auto;">
      <div class="section-header">
        <h1 class="section-title">Provider Comparison</h1>
        <p class="section-description">Compare performance metrics across all configured data providers</p>
      </div>

      <!-- Error Banner -->
      <div id="errorBanner" class="error-banner hidden" role="alert" aria-live="assertive">
        <span class="error-banner-icon" aria-hidden="true">&#x26A0;</span>
        <div class="error-banner-content">
          <div class="error-banner-title">Failed to load providers</div>
          <div class="error-banner-message" id="errorMessage">Unable to fetch provider data.</div>
        </div>
        <div class="error-banner-actions">
          <button class="btn" onclick="refreshProviders()">Retry</button>
        </div>
      </div>

      <!-- Loading State -->
      <div id="loadingState" class="loading-state" aria-busy="true" aria-label="Loading provider data">
        <div class="spinner spinner-lg"></div>
        <span>Loading providers...</span>
      </div>

      <!-- Provider Cards -->
      <div class="metrics-grid" id="providerMetrics" role="region" aria-label="Provider summary cards">
        <!-- Provider cards will be loaded dynamically -->
      </div>

      <!-- Empty State -->
      <div id="emptyState" class="empty-state hidden">
        <div class="empty-state-icon" aria-hidden="true">&#x1F4E1;</div>
        <div class="empty-state-title">No providers configured</div>
        <p>Configure data providers in the dashboard to see comparison metrics.</p>
        <a href="/#datasources" class="btn btn-primary">Configure Providers</a>
      </div>

      <!-- Provider Details Table -->
      <div class="card" id="providerTableCard" style="display: none;">
        <div class="card-header">
          <h2 class="card-title">Detailed Metrics</h2>
          <div class="header-actions">
            <label for="sortSelect" class="sr-only">Sort by</label>
            <select id="sortSelect" class="form-select" onchange="sortProviders(this.value)" aria-label="Sort providers">
              <option value="quality">Sort by Quality</option>
              <option value="trades">Sort by Trades</option>
              <option value="latency">Sort by Latency</option>
              <option value="name">Sort by Name</option>
            </select>
          </div>
        </div>
        <div class="symbols-table-container">
          <table id="providerTable" role="table" aria-label="Provider metrics comparison">
            <thead>
              <tr>
                <th scope="col" aria-sort="none">Provider</th>
                <th scope="col" aria-sort="none">Type</th>
                <th scope="col" aria-sort="none">Trades</th>
                <th scope="col" aria-sort="none">Quotes</th>
                <th scope="col" aria-sort="none">Depth</th>
                <th scope="col" aria-sort="none">Latency (ms)</th>
                <th scope="col" aria-sort="none">Quality</th>
                <th scope="col" aria-sort="none">Status</th>
                <th scope="col">Actions</th>
              </tr>
            </thead>
            <tbody id="providerTableBody"></tbody>
          </table>
        </div>
      </div>

      <!-- Provider Detail Modal -->
      <div id="providerDetailModal" class="modal hidden" role="dialog" aria-modal="true" aria-labelledby="providerDetailTitle">
        <div class="modal-content" style="max-width: 600px;">
          <div class="modal-header">
            <h3 id="providerDetailTitle">Provider Details</h3>
            <button class="modal-close" onclick="closeProviderDetail()" aria-label="Close">&times;</button>
          </div>
          <div class="modal-body" id="providerDetailBody">
            <!-- Populated dynamically -->
          </div>
          <div class="modal-footer">
            <button class="btn" onclick="closeProviderDetail()">Close</button>
          </div>
        </div>
      </div>
    </div>
  </main>

  <!-- Toast Container -->
  <div id="toastContainer" class="toast-container" role="log" aria-live="polite" aria-label="Notifications"></div>

  <script>
    let providers = [];
    let currentSort = 'quality';
    let isLoading = true;

    // Toast notification system (simplified)
    const Toast = {
      container: null,
      init() { this.container = document.getElementById('toastContainer'); },
      show(type, title, message) {
        const icons = { success: '&#x2713;', error: '&#x2717;', warning: '&#x26A0;', info: '&#x2139;' };
        const toast = document.createElement('div');
        toast.className = `toast ${type}`;
        toast.setAttribute('role', 'alert');
        toast.innerHTML = `
          <span class="toast-icon">${icons[type]}</span>
          <div class="toast-content">
            <div class="toast-title">${title}</div>
            <div class="toast-message">${message}</div>
          </div>
          <button class="toast-close" onclick="this.parentElement.remove()" aria-label="Dismiss">&times;</button>
        `;
        this.container.appendChild(toast);
        setTimeout(() => toast.remove(), 5000);
      },
      success(t, m) { this.show('success', t, m); },
      error(t, m) { this.show('error', t, m); },
      warning(t, m) { this.show('warning', t, m); },
      info(t, m) { this.show('info', t, m); }
    };

    async function loadProviders() {
      try {
        showLoading(true);
        hideError();

        const response = await fetch('/api/providers/comparison');
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const data = await response.json();
        providers = data.providers || [];

        if (providers.length === 0) {
          showEmptyState();
        } else {
          hideEmptyState();
          renderProviderMetrics(providers);
          sortProviders(currentSort);
        }

        updateLastUpdateTime();
      } catch (e) {
        console.error('Failed to load providers:', e);
        showError(e.message);
      } finally {
        showLoading(false);
      }
    }

    function refreshProviders() {
      Toast.info('Refreshing', 'Updating provider metrics...');
      loadProviders();
    }

    function showLoading(show) {
      isLoading = show;
      document.getElementById('loadingState').style.display = show ? 'flex' : 'none';
      document.getElementById('loadingState').setAttribute('aria-busy', show);
    }

    function showError(message) {
      document.getElementById('errorBanner').classList.remove('hidden');
      document.getElementById('errorMessage').textContent = message;
      document.getElementById('providerTableCard').style.display = 'none';
    }

    function hideError() {
      document.getElementById('errorBanner').classList.add('hidden');
    }

    function showEmptyState() {
      document.getElementById('emptyState').classList.remove('hidden');
      document.getElementById('providerTableCard').style.display = 'none';
    }

    function hideEmptyState() {
      document.getElementById('emptyState').classList.add('hidden');
      document.getElementById('providerTableCard').style.display = 'block';
    }

    function updateLastUpdateTime() {
      const time = new Date().toLocaleTimeString();
      document.getElementById('lastUpdate').textContent = `Last updated: ${time}`;
    }

    function renderProviderMetrics(providerList) {
      const container = document.getElementById('providerMetrics');
      if (providerList.length === 0) {
        container.innerHTML = '';
        return;
      }

      container.innerHTML = providerList.map(p => {
        const qualityClass = p.dataQualityScore >= 90 ? 'success' : p.dataQualityScore >= 70 ? 'warning' : 'danger';
        return `
          <div class="metric-card ${qualityClass}" tabindex="0" role="button"
               aria-label="${p.providerType}: ${formatNumber(p.tradesReceived)} trades, ${p.dataQualityScore.toFixed(1)}% quality"
               onclick="showProviderDetail('${p.providerId}')">
            <div class="metric-value">${escapeHtml(p.providerType)}</div>
            <div class="metric-label">${formatNumber(p.tradesReceived)} trades</div>
            <div class="metric-trend">${p.dataQualityScore.toFixed(1)}% quality</div>
          </div>
        `;
      }).join('');
    }

    function renderProviderTable(providerList) {
      const tbody = document.getElementById('providerTableBody');

      if (providerList.length === 0) {
        tbody.innerHTML = '<tr><td colspan="9" class="text-muted">No providers available</td></tr>';
        return;
      }

      tbody.innerHTML = providerList.map(p => {
        const statusClass = p.connectionSuccessRate >= 90 ? 'success' : p.connectionSuccessRate >= 70 ? 'warning' : 'danger';
        const statusText = p.connectionSuccessRate >= 90 ? 'Healthy' : p.connectionSuccessRate >= 70 ? 'Degraded' : 'Unhealthy';

        return `
          <tr>
            <td><strong>${escapeHtml(p.providerId)}</strong></td>
            <td>${escapeHtml(p.providerType)}</td>
            <td>${formatNumber(p.tradesReceived)}</td>
            <td>${formatNumber(p.quotesReceived)}</td>
            <td>${formatNumber(p.depthUpdatesReceived)}</td>
            <td class="${p.averageLatencyMs > 100 ? 'text-warning' : ''}">${p.averageLatencyMs.toFixed(2)}</td>
            <td>
              <div class="quality-bar" role="progressbar" aria-valuenow="${p.dataQualityScore}" aria-valuemin="0" aria-valuemax="100">
                <div class="progress-bar">
                  <div class="progress-fill ${statusClass}" style="width: ${p.dataQualityScore}%;"></div>
                </div>
                <span>${p.dataQualityScore.toFixed(1)}%</span>
              </div>
            </td>
            <td>
              <span class="status-badge ${statusClass}" aria-label="Status: ${statusText}">${statusText}</span>
            </td>
            <td>
              <button class="btn btn-sm" onclick="showProviderDetail('${p.providerId}')" aria-label="View details for ${p.providerId}">
                Details
              </button>
            </td>
          </tr>
        `;
      }).join('');
    }

    function sortProviders(sortBy) {
      currentSort = sortBy;
      const sorted = [...providers].sort((a, b) => {
        switch (sortBy) {
          case 'quality': return b.dataQualityScore - a.dataQualityScore;
          case 'trades': return b.tradesReceived - a.tradesReceived;
          case 'latency': return a.averageLatencyMs - b.averageLatencyMs;
          case 'name': return a.providerId.localeCompare(b.providerId);
          default: return 0;
        }
      });
      renderProviderTable(sorted);
    }

    function showProviderDetail(providerId) {
      const provider = providers.find(p => p.providerId === providerId);
      if (!provider) return;

      const body = document.getElementById('providerDetailBody');
      const qualityClass = provider.dataQualityScore >= 90 ? 'success' : provider.dataQualityScore >= 70 ? 'warning' : 'danger';

      body.innerHTML = `
        <div class="provider-detail">
          <div class="detail-header">
            <h4>${escapeHtml(provider.providerId)}</h4>
            <span class="status-badge ${qualityClass}">${provider.dataQualityScore.toFixed(1)}% Quality</span>
          </div>
          <dl class="detail-list">
            <dt>Provider Type</dt>
            <dd>${escapeHtml(provider.providerType)}</dd>
            <dt>Trades Received</dt>
            <dd>${formatNumber(provider.tradesReceived)}</dd>
            <dt>Quotes Received</dt>
            <dd>${formatNumber(provider.quotesReceived)}</dd>
            <dt>Depth Updates</dt>
            <dd>${formatNumber(provider.depthUpdatesReceived)}</dd>
            <dt>Average Latency</dt>
            <dd>${provider.averageLatencyMs.toFixed(2)} ms</dd>
            <dt>Connection Success Rate</dt>
            <dd>${provider.connectionSuccessRate.toFixed(1)}%</dd>
          </dl>
        </div>
      `;

      document.getElementById('providerDetailTitle').textContent = `${provider.providerId} Details`;
      document.getElementById('providerDetailModal').classList.remove('hidden');
      document.getElementById('providerDetailModal').setAttribute('aria-hidden', 'false');
      document.body.style.overflow = 'hidden';
    }

    function closeProviderDetail() {
      document.getElementById('providerDetailModal').classList.add('hidden');
      document.getElementById('providerDetailModal').setAttribute('aria-hidden', 'true');
      document.body.style.overflow = '';
    }

    function formatNumber(n) {
      if (n >= 1e9) return (n / 1e9).toFixed(1) + 'B';
      if (n >= 1e6) return (n / 1e6).toFixed(1) + 'M';
      if (n >= 1e3) return (n / 1e3).toFixed(1) + 'K';
      return n.toLocaleString();
    }

    function escapeHtml(str) {
      const div = document.createElement('div');
      div.textContent = str;
      return div.innerHTML;
    }

    // Keyboard shortcuts
    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') {
        closeProviderDetail();
      }
      if (e.key === 'F5') {
        e.preventDefault();
        refreshProviders();
      }
    });

    // Close modal on backdrop click
    document.getElementById('providerDetailModal')?.addEventListener('click', (e) => {
      if (e.target.id === 'providerDetailModal') {
        closeProviderDetail();
      }
    });

    // Initialize
    document.addEventListener('DOMContentLoaded', () => {
      Toast.init();
      loadProviders();
      // Auto-refresh every 10 seconds
      setInterval(loadProviders, 10000);
    });
  </script>

  <style>
    .loading-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 48px;
      gap: 16px;
      color: var(--text-muted);
    }

    .section-description {
      color: var(--text-secondary);
      margin-top: 4px;
    }

    .last-update {
      font-size: 12px;
      color: var(--text-muted);
      margin-right: 16px;
    }

    .logo-link {
      text-decoration: none;
      display: flex;
    }

    .quality-bar {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .quality-bar .progress-bar {
      flex: 1;
      min-width: 60px;
    }

    .provider-detail {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }

    .detail-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }

    .detail-header h4 {
      margin: 0;
    }

    .detail-list {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 8px 16px;
      margin: 0;
    }

    .detail-list dt {
      color: var(--text-muted);
      font-size: 12px;
    }

    .detail-list dd {
      margin: 0;
      font-weight: 600;
    }
  </style>
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
