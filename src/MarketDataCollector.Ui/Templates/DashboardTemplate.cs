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

  <!-- Toast Container for Notifications -->
  <div id="toastContainer" class="toast-container" role="alert" aria-live="polite"></div>

  <!-- Keyboard Shortcuts Help Modal -->
  <div id="shortcutsModal" class="modal hidden" role="dialog" aria-modal="true" aria-labelledby="shortcutsTitle">
    <div class="modal-content" style="max-width: 400px;">
      <div style="padding: 16px; border-bottom: 1px solid var(--border-color);">
        <h3 id="shortcutsTitle" style="margin: 0;">Keyboard Shortcuts</h3>
      </div>
      <div class="shortcuts-help">
        <span class="shortcut-key">Ctrl+K</span><span>Open command palette</span>
        <span class="shortcut-key">Ctrl+S</span><span>Start collector</span>
        <span class="shortcut-key">Ctrl+Shift+S</span><span>Stop collector</span>
        <span class="shortcut-key">F5</span><span>Refresh data</span>
        <span class="shortcut-key">?</span><span>Show this help</span>
        <span class="shortcut-key">Esc</span><span>Close dialogs</span>
      </div>
      <div style="padding: 16px; text-align: right;">
        <button class="btn" onclick="closeShortcutsHelp()">Close</button>
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

    function openCommandPalette() {
      const modal = document.getElementById('commandPalette');
      modal.classList.remove('hidden');
      modal.setAttribute('aria-hidden', 'false');
      document.getElementById('commandSearch').focus();

      // Trap focus in modal
      document.body.style.overflow = 'hidden';
    }

    function closeCommandPalette() {
      const modal = document.getElementById('commandPalette');
      modal.classList.add('hidden');
      modal.setAttribute('aria-hidden', 'true');
      document.body.style.overflow = '';
    }

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

    // Keyboard shortcuts
    document.addEventListener('keydown', (e) => {
      // Command palette
      if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
        e.preventDefault();
        openCommandPalette();
      }

      // Refresh
      if (e.key === 'F5') {
        e.preventDefault();
        loadDashboard();
        Toast.info('Refreshing', 'Dashboard data is being refreshed...');
      }

      // Help
      if (e.key === '?' && !e.ctrlKey && !e.metaKey) {
        const activeElement = document.activeElement;
        if (activeElement?.tagName !== 'INPUT' && activeElement?.tagName !== 'TEXTAREA') {
          e.preventDefault();
          openShortcutsHelp();
        }
      }

      // Escape - close modals
      if (e.key === 'Escape') {
        closeCommandPalette();
        closeShortcutsHelp();
      }
    });

    // Close modal when clicking outside
    document.getElementById('commandPalette')?.addEventListener('click', (e) => {
      if (e.target.id === 'commandPalette') {
        closeCommandPalette();
      }
    });

    document.getElementById('shortcutsModal')?.addEventListener('click', (e) => {
      if (e.target.id === 'shortcutsModal') {
        closeShortcutsHelp();
      }
    });

    // Navigation active state
    function updateActiveNav() {
      const hash = window.location.hash || '#dashboard';
      document.querySelectorAll('.nav-item').forEach(item => {
        item.classList.toggle('active', item.getAttribute('href') === hash);
        item.setAttribute('aria-current', item.getAttribute('href') === hash ? 'page' : 'false');
      });
    }

    window.addEventListener('hashchange', updateActiveNav);

    // Initialize
    document.addEventListener('DOMContentLoaded', () => {
      Toast.init();
      updateActiveNav();
      loadDashboard();

      // Auto-refresh every 5 seconds
      setInterval(loadDashboard, 5000);

      // Show welcome toast on first load
      if (!sessionStorage.getItem('welcomed')) {
        sessionStorage.setItem('welcomed', 'true');
        Toast.info('Welcome', 'Press ? for keyboard shortcuts');
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
