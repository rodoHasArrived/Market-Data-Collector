import { useState } from 'react';
import {
  useBackfillProviders,
  useBackfillStatus,
  useBackfillHealth,
  useRunBackfill,
  useConfig,
} from '../hooks/useApi';
import {
  Clock,
  Play,
  CheckCircle,
  XCircle,
  AlertTriangle,
  RefreshCw,
  Calendar,
  Database,
  Zap,
} from 'lucide-react';

export default function BackfillPage() {
  const { data: providers, isLoading: providersLoading } = useBackfillProviders();
  const { data: status } = useBackfillStatus();
  const { data: health } = useBackfillHealth();
  const { data: config } = useConfig();
  const runBackfill = useRunBackfill();

  const [form, setForm] = useState({
    provider: 'composite',
    symbols: '',
    from: getDefaultFromDate(),
    to: getDefaultToDate(),
    enableFallback: true,
    preferAdjustedPrices: true,
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const symbolList = form.symbols
      .split(/[,\s]+/)
      .map((s) => s.trim().toUpperCase())
      .filter(Boolean);

    if (symbolList.length === 0) {
      return;
    }

    runBackfill.mutate({
      provider: form.provider,
      symbols: symbolList,
      from: form.from,
      to: form.to,
      enableFallback: form.enableFallback,
      preferAdjustedPrices: form.preferAdjustedPrices,
    });
  };

  const isRunning = status?.isRunning;

  return (
    <div className="space-y-6 animate-fadeIn">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold text-gray-900">Historical Backfill</h2>
          <p className="text-sm text-gray-500 mt-1">
            Download historical market data from free providers
          </p>
        </div>
        {isRunning && (
          <span className="badge badge-warning flex items-center space-x-1">
            <RefreshCw className="w-3 h-3 animate-spin" />
            <span>Backfill Running</span>
          </span>
        )}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Backfill Form */}
        <div className="lg:col-span-2 card">
          <div className="card-header flex items-center space-x-2">
            <Clock className="w-5 h-5 text-primary-600" />
            <h3 className="text-lg font-semibold text-gray-900">Run Backfill</h3>
          </div>
          <form onSubmit={handleSubmit} className="card-body space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="col-span-2">
                <label className="label">Provider</label>
                <select
                  value={form.provider}
                  onChange={(e) => setForm({ ...form, provider: e.target.value })}
                  className="select"
                  disabled={isRunning}
                >
                  <option value="composite">Composite (Auto-Failover)</option>
                  {providers?.map((p) => (
                    <option key={p.name} value={p.name} disabled={!p.isAvailable}>
                      {p.displayName} {!p.isAvailable && '(Unavailable)'}
                    </option>
                  ))}
                </select>
                <p className="text-xs text-gray-500 mt-1">
                  Composite tries multiple providers with automatic failover
                </p>
              </div>

              <div className="col-span-2">
                <label className="label">Symbols</label>
                <input
                  type="text"
                  value={form.symbols}
                  onChange={(e) => setForm({ ...form, symbols: e.target.value.toUpperCase() })}
                  placeholder="SPY, QQQ, AAPL"
                  className="input"
                  disabled={isRunning}
                />
                <p className="text-xs text-gray-500 mt-1">
                  Comma or space-separated list of symbols
                </p>
              </div>

              <div>
                <label className="label">From Date</label>
                <div className="relative">
                  <Calendar className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                  <input
                    type="date"
                    value={form.from}
                    onChange={(e) => setForm({ ...form, from: e.target.value })}
                    className="input pl-10"
                    disabled={isRunning}
                  />
                </div>
              </div>

              <div>
                <label className="label">To Date</label>
                <div className="relative">
                  <Calendar className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                  <input
                    type="date"
                    value={form.to}
                    onChange={(e) => setForm({ ...form, to: e.target.value })}
                    className="input pl-10"
                    disabled={isRunning}
                  />
                </div>
              </div>
            </div>

            {/* Options */}
            <div className="flex flex-wrap gap-4 pt-2">
              <label className="flex items-center space-x-2">
                <input
                  type="checkbox"
                  checked={form.enableFallback}
                  onChange={(e) => setForm({ ...form, enableFallback: e.target.checked })}
                  className="rounded border-gray-300 text-primary-600 focus:ring-primary-500"
                  disabled={isRunning}
                />
                <span className="text-sm text-gray-700">Enable Fallback</span>
              </label>
              <label className="flex items-center space-x-2">
                <input
                  type="checkbox"
                  checked={form.preferAdjustedPrices}
                  onChange={(e) => setForm({ ...form, preferAdjustedPrices: e.target.checked })}
                  className="rounded border-gray-300 text-primary-600 focus:ring-primary-500"
                  disabled={isRunning}
                />
                <span className="text-sm text-gray-700">Adjusted Prices</span>
              </label>
            </div>

            <button
              type="submit"
              disabled={isRunning || runBackfill.isPending}
              className="btn btn-primary w-full"
            >
              {isRunning || runBackfill.isPending ? (
                <RefreshCw className="w-4 h-4 mr-2 animate-spin" />
              ) : (
                <Play className="w-4 h-4 mr-2" />
              )}
              {isRunning ? 'Backfill Running...' : 'Start Backfill'}
            </button>
          </form>
        </div>

        {/* Provider Health */}
        <div className="card">
          <div className="card-header flex items-center space-x-2">
            <Zap className="w-5 h-5 text-primary-600" />
            <h3 className="text-lg font-semibold text-gray-900">Provider Health</h3>
          </div>
          <div className="card-body">
            {providersLoading ? (
              <div className="flex justify-center py-8">
                <RefreshCw className="w-6 h-6 animate-spin text-gray-400" />
              </div>
            ) : (
              <div className="space-y-3">
                {health?.map((h) => (
                  <div
                    key={h.provider}
                    className={`p-3 rounded-lg ${
                      h.isHealthy ? 'bg-emerald-50' : 'bg-red-50'
                    }`}
                  >
                    <div className="flex items-center justify-between">
                      <div className="flex items-center space-x-2">
                        {h.isHealthy ? (
                          <CheckCircle className="w-4 h-4 text-emerald-600" />
                        ) : (
                          <XCircle className="w-4 h-4 text-red-600" />
                        )}
                        <span className="font-medium text-gray-900">{h.provider}</span>
                      </div>
                      {h.isHealthy && (
                        <span className="text-xs text-gray-500">
                          {h.responseTimeMs}ms
                        </span>
                      )}
                    </div>
                    {h.error && (
                      <p className="text-xs text-red-600 mt-1">{h.error}</p>
                    )}
                  </div>
                )) || (
                  <p className="text-center text-gray-500 py-4">
                    No provider health data
                  </p>
                )}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Backfill Status */}
      {status && (status.isRunning || status.completedAt) && (
        <BackfillStatusCard status={status} />
      )}

      {/* Quick Actions */}
      <div className="card">
        <div className="card-header flex items-center space-x-2">
          <Database className="w-5 h-5 text-primary-600" />
          <h3 className="text-lg font-semibold text-gray-900">Quick Actions</h3>
        </div>
        <div className="card-body">
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <QuickAction
              label="Backfill Subscribed"
              description={`${config?.symbols?.length || 0} symbols`}
              onClick={() => {
                const symbols = config?.symbols?.map((s) => s.symbol).join(', ') || '';
                setForm({ ...form, symbols });
              }}
              disabled={isRunning}
            />
            <QuickAction
              label="Last Week"
              description="7 days of data"
              onClick={() => {
                const to = new Date();
                const from = new Date();
                from.setDate(from.getDate() - 7);
                setForm({
                  ...form,
                  from: formatDate(from),
                  to: formatDate(to),
                });
              }}
              disabled={isRunning}
            />
            <QuickAction
              label="Last Month"
              description="30 days of data"
              onClick={() => {
                const to = new Date();
                const from = new Date();
                from.setDate(from.getDate() - 30);
                setForm({
                  ...form,
                  from: formatDate(from),
                  to: formatDate(to),
                });
              }}
              disabled={isRunning}
            />
            <QuickAction
              label="YTD"
              description={`From Jan 1, ${new Date().getFullYear()}`}
              onClick={() => {
                const to = new Date();
                const from = new Date(to.getFullYear(), 0, 1);
                setForm({
                  ...form,
                  from: formatDate(from),
                  to: formatDate(to),
                });
              }}
              disabled={isRunning}
            />
          </div>
        </div>
      </div>
    </div>
  );
}

interface BackfillStatusCardProps {
  status: {
    isRunning: boolean;
    provider: string;
    symbols: string[];
    from: string;
    to: string;
    progress: number;
    currentSymbol: string;
    completedSymbols: string[];
    failedSymbols: string[];
    startedAt: string;
    completedAt: string | null;
    error: string | null;
  };
}

function BackfillStatusCard({ status }: BackfillStatusCardProps) {
  const hasErrors = status.failedSymbols.length > 0 || status.error;

  return (
    <div className="card">
      <div className="card-header flex items-center justify-between">
        <div className="flex items-center space-x-2">
          {status.isRunning ? (
            <RefreshCw className="w-5 h-5 text-primary-600 animate-spin" />
          ) : hasErrors ? (
            <AlertTriangle className="w-5 h-5 text-amber-600" />
          ) : (
            <CheckCircle className="w-5 h-5 text-emerald-600" />
          )}
          <h3 className="text-lg font-semibold text-gray-900">
            {status.isRunning
              ? 'Backfill In Progress'
              : hasErrors
              ? 'Backfill Completed with Errors'
              : 'Backfill Completed'}
          </h3>
        </div>
        <span className="text-sm text-gray-500">
          {status.provider}
        </span>
      </div>
      <div className="card-body space-y-4">
        {/* Progress Bar */}
        {status.isRunning && (
          <div>
            <div className="flex justify-between text-sm mb-1">
              <span className="text-gray-600">Progress</span>
              <span className="font-medium">{Math.round(status.progress)}%</span>
            </div>
            <div className="h-2 bg-gray-200 rounded-full overflow-hidden">
              <div
                className="h-full bg-gradient-primary transition-all duration-300"
                style={{ width: `${status.progress}%` }}
              />
            </div>
            {status.currentSymbol && (
              <p className="text-xs text-gray-500 mt-1">
                Processing: {status.currentSymbol}
              </p>
            )}
          </div>
        )}

        {/* Stats */}
        <div className="grid grid-cols-3 gap-4">
          <div className="text-center p-3 bg-gray-50 rounded-lg">
            <p className="text-xl font-bold text-gray-900">{status.symbols.length}</p>
            <p className="text-xs text-gray-500">Total Symbols</p>
          </div>
          <div className="text-center p-3 bg-emerald-50 rounded-lg">
            <p className="text-xl font-bold text-emerald-600">
              {status.completedSymbols.length}
            </p>
            <p className="text-xs text-gray-500">Completed</p>
          </div>
          <div className="text-center p-3 bg-red-50 rounded-lg">
            <p className="text-xl font-bold text-red-600">
              {status.failedSymbols.length}
            </p>
            <p className="text-xs text-gray-500">Failed</p>
          </div>
        </div>

        {/* Error Message */}
        {status.error && (
          <div className="p-3 bg-red-50 rounded-lg text-sm text-red-700">
            <p className="font-medium">Error:</p>
            <p>{status.error}</p>
          </div>
        )}

        {/* Failed Symbols */}
        {status.failedSymbols.length > 0 && (
          <div className="p-3 bg-amber-50 rounded-lg">
            <p className="text-sm font-medium text-amber-800 mb-1">Failed Symbols:</p>
            <p className="text-xs text-amber-700">
              {status.failedSymbols.join(', ')}
            </p>
          </div>
        )}

        {/* Timing */}
        <div className="text-xs text-gray-500">
          <p>Started: {new Date(status.startedAt).toLocaleString()}</p>
          {status.completedAt && (
            <p>Completed: {new Date(status.completedAt).toLocaleString()}</p>
          )}
        </div>
      </div>
    </div>
  );
}

interface QuickActionProps {
  label: string;
  description: string;
  onClick: () => void;
  disabled?: boolean;
}

function QuickAction({ label, description, onClick, disabled }: QuickActionProps) {
  return (
    <button
      onClick={onClick}
      disabled={disabled}
      className="p-4 bg-gray-50 hover:bg-gray-100 rounded-lg text-left transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
    >
      <p className="font-medium text-gray-900">{label}</p>
      <p className="text-xs text-gray-500">{description}</p>
    </button>
  );
}

function getDefaultFromDate(): string {
  const date = new Date();
  date.setMonth(date.getMonth() - 1);
  return formatDate(date);
}

function getDefaultToDate(): string {
  return formatDate(new Date());
}

function formatDate(date: Date): string {
  return date.toISOString().split('T')[0];
}
