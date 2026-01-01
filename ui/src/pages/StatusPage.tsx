import { useStatus, useConfig } from '../hooks/useApi';
import {
  Activity,
  TrendingUp,
  TrendingDown,
  AlertTriangle,
  CheckCircle,
  XCircle,
  Zap,
  BarChart3,
  Layers,
  MessageSquare,
} from 'lucide-react';

export default function StatusPage() {
  const { data: status, isLoading: statusLoading, error: statusError } = useStatus();
  const { data: config } = useConfig();

  if (statusLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary-600"></div>
      </div>
    );
  }

  if (statusError) {
    return (
      <div className="card p-6">
        <div className="flex items-center space-x-3 text-red-600">
          <XCircle className="w-6 h-6" />
          <span>Failed to load status. Is the collector running?</span>
        </div>
      </div>
    );
  }

  const metrics = status?.metrics;

  return (
    <div className="space-y-6 animate-fadeIn">
      {/* Status Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold text-gray-900">System Status</h2>
          <p className="text-sm text-gray-500 mt-1">
            Real-time monitoring and metrics
          </p>
        </div>
        <div className="flex items-center space-x-2">
          {status?.isRunning ? (
            <span className="badge badge-success flex items-center space-x-1">
              <CheckCircle className="w-3 h-3" />
              <span>Running</span>
            </span>
          ) : (
            <span className="badge badge-danger flex items-center space-x-1">
              <XCircle className="w-3 h-3" />
              <span>Stopped</span>
            </span>
          )}
          <span className="badge badge-info">
            {config?.dataSource || 'Unknown'} Provider
          </span>
        </div>
      </div>

      {/* Metrics Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <MetricCard
          title="Events Published"
          value={formatNumber(metrics?.published || 0)}
          icon={<Activity className="w-5 h-5" />}
          color="primary"
        />
        <MetricCard
          title="Events/Second"
          value={formatNumber(metrics?.eventsPerSecond || 0, 1)}
          icon={<Zap className="w-5 h-5" />}
          color="success"
          suffix="/s"
        />
        <MetricCard
          title="Events Dropped"
          value={formatNumber(metrics?.dropped || 0)}
          icon={<TrendingDown className="w-5 h-5" />}
          color={metrics?.dropped ? 'danger' : 'success'}
        />
        <MetricCard
          title="Drop Rate"
          value={formatNumber(metrics?.dropRate || 0, 2)}
          icon={<BarChart3 className="w-5 h-5" />}
          color={metrics?.dropRate && metrics.dropRate > 1 ? 'warning' : 'success'}
          suffix="%"
        />
      </div>

      {/* Event Types Grid */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <MetricCard
          title="Trades"
          value={formatNumber(metrics?.trades || 0)}
          icon={<TrendingUp className="w-5 h-5" />}
          color="info"
        />
        <MetricCard
          title="Depth Updates"
          value={formatNumber(metrics?.depthUpdates || 0)}
          icon={<Layers className="w-5 h-5" />}
          color="info"
        />
        <MetricCard
          title="Quotes"
          value={formatNumber(metrics?.quotes || 0)}
          icon={<MessageSquare className="w-5 h-5" />}
          color="info"
        />
      </div>

      {/* System Info */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {/* Connection Info */}
        <div className="card">
          <div className="card-header">
            <h3 className="text-lg font-semibold text-gray-900">Connection Info</h3>
          </div>
          <div className="card-body">
            <dl className="space-y-3">
              <InfoRow label="Data Source" value={config?.dataSource || 'Not configured'} />
              <InfoRow label="Symbols" value={`${config?.symbols?.length || 0} subscribed`} />
              <InfoRow label="Uptime" value={status?.uptime || 'N/A'} />
              <InfoRow
                label="Last Update"
                value={status?.lastUpdated ? new Date(status.lastUpdated).toLocaleTimeString() : 'N/A'}
              />
            </dl>
          </div>
        </div>

        {/* Integrity Events */}
        <div className="card">
          <div className="card-header flex items-center justify-between">
            <h3 className="text-lg font-semibold text-gray-900">Recent Integrity Events</h3>
            {status?.integrityEvents && status.integrityEvents.length > 0 && (
              <span className="badge badge-warning">
                {status.integrityEvents.length} events
              </span>
            )}
          </div>
          <div className="card-body">
            {status?.integrityEvents && status.integrityEvents.length > 0 ? (
              <div className="space-y-2 max-h-48 overflow-y-auto">
                {status.integrityEvents.slice(0, 10).map((event, idx) => (
                  <div
                    key={idx}
                    className={`p-2 rounded-lg text-sm ${
                      event.severity === 'Error'
                        ? 'bg-red-50 text-red-800'
                        : event.severity === 'Warning'
                        ? 'bg-amber-50 text-amber-800'
                        : 'bg-blue-50 text-blue-800'
                    }`}
                  >
                    <div className="flex items-center space-x-2">
                      <AlertTriangle className="w-4 h-4 flex-shrink-0" />
                      <span className="font-medium">{event.symbol}</span>
                      <span className="text-xs opacity-75">
                        {new Date(event.timestamp).toLocaleTimeString()}
                      </span>
                    </div>
                    <p className="mt-1 text-xs opacity-90">{event.message}</p>
                  </div>
                ))}
              </div>
            ) : (
              <div className="flex items-center justify-center h-32 text-gray-400">
                <div className="text-center">
                  <CheckCircle className="w-8 h-8 mx-auto mb-2" />
                  <p className="text-sm">No integrity events</p>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

interface MetricCardProps {
  title: string;
  value: string | number;
  icon: React.ReactNode;
  color: 'primary' | 'success' | 'danger' | 'warning' | 'info';
  suffix?: string;
}

function MetricCard({ title, value, icon, color, suffix }: MetricCardProps) {
  const colorClasses = {
    primary: 'bg-primary-50 text-primary-600',
    success: 'bg-emerald-50 text-emerald-600',
    danger: 'bg-red-50 text-red-600',
    warning: 'bg-amber-50 text-amber-600',
    info: 'bg-blue-50 text-blue-600',
  };

  return (
    <div className="card p-4">
      <div className="flex items-center justify-between">
        <div>
          <p className="text-sm font-medium text-gray-500">{title}</p>
          <p className="text-2xl font-bold text-gray-900 mt-1">
            {value}
            {suffix && <span className="text-sm font-normal text-gray-500 ml-1">{suffix}</span>}
          </p>
        </div>
        <div className={`p-3 rounded-lg ${colorClasses[color]}`}>{icon}</div>
      </div>
    </div>
  );
}

interface InfoRowProps {
  label: string;
  value: string;
}

function InfoRow({ label, value }: InfoRowProps) {
  return (
    <div className="flex justify-between">
      <dt className="text-sm text-gray-500">{label}</dt>
      <dd className="text-sm font-medium text-gray-900">{value}</dd>
    </div>
  );
}

function formatNumber(num: number, decimals = 0): string {
  if (num >= 1_000_000) {
    return (num / 1_000_000).toFixed(1) + 'M';
  }
  if (num >= 1_000) {
    return (num / 1_000).toFixed(1) + 'K';
  }
  return num.toFixed(decimals);
}
