import { useState } from 'react';
import {
  useConfig,
  useUpdateDataSource,
  useUpdateAlpaca,
  useUpdateStorage,
} from '../hooks/useApi';
import { Save, RefreshCw, Eye, EyeOff, Server, Database, HardDrive } from 'lucide-react';

export default function ConfigPage() {
  const { data: config, isLoading, refetch } = useConfig();

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary-600"></div>
      </div>
    );
  }

  return (
    <div className="space-y-6 animate-fadeIn">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold text-gray-900">Configuration</h2>
          <p className="text-sm text-gray-500 mt-1">
            Manage data sources, storage, and provider settings
          </p>
        </div>
        <button onClick={() => refetch()} className="btn btn-secondary">
          <RefreshCw className="w-4 h-4 mr-2" />
          Refresh
        </button>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Data Source Selection */}
        <DataSourceCard currentSource={config?.dataSource || 'IB'} />

        {/* Storage Settings */}
        <StorageCard config={config?.storage} dataRoot={config?.dataRoot} />
      </div>

      {/* Alpaca Settings */}
      {config?.dataSource === 'Alpaca' && (
        <AlpacaCard config={config?.alpaca} />
      )}
    </div>
  );
}

interface DataSourceCardProps {
  currentSource: string;
}

function DataSourceCard({ currentSource }: DataSourceCardProps) {
  const [selected, setSelected] = useState(currentSource);
  const updateDataSource = useUpdateDataSource();

  const handleSave = () => {
    updateDataSource.mutate(selected);
  };

  return (
    <div className="card">
      <div className="card-header flex items-center space-x-2">
        <Server className="w-5 h-5 text-primary-600" />
        <h3 className="text-lg font-semibold text-gray-900">Data Source</h3>
      </div>
      <div className="card-body space-y-4">
        <div>
          <label className="label">Active Provider</label>
          <select
            value={selected}
            onChange={(e) => setSelected(e.target.value)}
            className="select"
          >
            <option value="IB">Interactive Brokers</option>
            <option value="Alpaca">Alpaca</option>
            <option value="Polygon">Polygon</option>
          </select>
          <p className="text-xs text-gray-500 mt-1">
            Select the market data provider to use for live streaming
          </p>
        </div>

        {selected !== currentSource && (
          <button
            onClick={handleSave}
            disabled={updateDataSource.isPending}
            className="btn btn-primary w-full"
          >
            {updateDataSource.isPending ? (
              <RefreshCw className="w-4 h-4 mr-2 animate-spin" />
            ) : (
              <Save className="w-4 h-4 mr-2" />
            )}
            Save Changes
          </button>
        )}
      </div>
    </div>
  );
}

interface StorageCardProps {
  config?: {
    namingConvention: string;
    datePartition: string;
    includeProvider: boolean;
    filePrefix: string | null;
    retentionDays: number | null;
    maxTotalMegabytes: number | null;
  };
  dataRoot?: string;
}

function StorageCard({ config, dataRoot }: StorageCardProps) {
  const [settings, setSettings] = useState({
    namingConvention: config?.namingConvention || 'BySymbol',
    datePartition: config?.datePartition || 'Daily',
    includeProvider: config?.includeProvider || false,
    filePrefix: config?.filePrefix || '',
    retentionDays: config?.retentionDays?.toString() || '',
    maxTotalMegabytes: config?.maxTotalMegabytes?.toString() || '',
  });

  const updateStorage = useUpdateStorage();

  const handleSave = () => {
    updateStorage.mutate({
      namingConvention: settings.namingConvention as 'Flat' | 'BySymbol' | 'ByDate' | 'ByType',
      datePartition: settings.datePartition as 'None' | 'Daily' | 'Hourly' | 'Monthly',
      includeProvider: settings.includeProvider,
      filePrefix: settings.filePrefix || null,
      retentionDays: settings.retentionDays ? parseInt(settings.retentionDays) : null,
      maxTotalMegabytes: settings.maxTotalMegabytes ? parseInt(settings.maxTotalMegabytes) : null,
    });
  };

  // Generate example path
  const getExamplePath = () => {
    const symbol = 'SPY';
    const type = 'trades';
    const date = '2024-01-15';
    const prefix = settings.filePrefix ? `${settings.filePrefix}_` : '';

    switch (settings.namingConvention) {
      case 'Flat':
        return `${dataRoot}/${prefix}${symbol}_${type}_${date}.jsonl`;
      case 'ByDate':
        return `${dataRoot}/${date}/${symbol}/${prefix}${type}.jsonl`;
      case 'ByType':
        return `${dataRoot}/${type}/${symbol}/${prefix}${date}.jsonl`;
      default:
        return `${dataRoot}/${symbol}/${type}/${prefix}${date}.jsonl`;
    }
  };

  return (
    <div className="card">
      <div className="card-header flex items-center space-x-2">
        <HardDrive className="w-5 h-5 text-primary-600" />
        <h3 className="text-lg font-semibold text-gray-900">Storage Settings</h3>
      </div>
      <div className="card-body space-y-4">
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="label">Naming Convention</label>
            <select
              value={settings.namingConvention}
              onChange={(e) => setSettings({ ...settings, namingConvention: e.target.value })}
              className="select"
            >
              <option value="BySymbol">By Symbol</option>
              <option value="ByDate">By Date</option>
              <option value="ByType">By Type</option>
              <option value="Flat">Flat</option>
            </select>
          </div>
          <div>
            <label className="label">Date Partition</label>
            <select
              value={settings.datePartition}
              onChange={(e) => setSettings({ ...settings, datePartition: e.target.value })}
              className="select"
            >
              <option value="Daily">Daily</option>
              <option value="Hourly">Hourly</option>
              <option value="Monthly">Monthly</option>
              <option value="None">None</option>
            </select>
          </div>
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="label">Retention Days</label>
            <input
              type="number"
              value={settings.retentionDays}
              onChange={(e) => setSettings({ ...settings, retentionDays: e.target.value })}
              placeholder="Forever"
              className="input"
            />
          </div>
          <div>
            <label className="label">Max Storage (MB)</label>
            <input
              type="number"
              value={settings.maxTotalMegabytes}
              onChange={(e) => setSettings({ ...settings, maxTotalMegabytes: e.target.value })}
              placeholder="Unlimited"
              className="input"
            />
          </div>
        </div>

        {/* Path Preview */}
        <div className="bg-gray-50 rounded-lg p-3">
          <p className="text-xs font-medium text-gray-500 mb-1">Example Path:</p>
          <code className="text-xs text-gray-700 break-all">{getExamplePath()}</code>
        </div>

        <button
          onClick={handleSave}
          disabled={updateStorage.isPending}
          className="btn btn-primary w-full"
        >
          {updateStorage.isPending ? (
            <RefreshCw className="w-4 h-4 mr-2 animate-spin" />
          ) : (
            <Save className="w-4 h-4 mr-2" />
          )}
          Save Storage Settings
        </button>
      </div>
    </div>
  );
}

interface AlpacaCardProps {
  config?: {
    keyId: string;
    secretKey: string;
    feed: string;
    useSandbox: boolean;
    subscribeQuotes: boolean;
  };
}

function AlpacaCard({ config }: AlpacaCardProps) {
  const [showSecret, setShowSecret] = useState(false);
  const [settings, setSettings] = useState({
    keyId: config?.keyId || '',
    secretKey: config?.secretKey || '',
    feed: config?.feed || 'iex',
    useSandbox: config?.useSandbox || false,
    subscribeQuotes: config?.subscribeQuotes || false,
  });

  const updateAlpaca = useUpdateAlpaca();

  const handleSave = () => {
    updateAlpaca.mutate(settings);
  };

  return (
    <div className="card">
      <div className="card-header flex items-center space-x-2">
        <Database className="w-5 h-5 text-primary-600" />
        <h3 className="text-lg font-semibold text-gray-900">Alpaca Settings</h3>
      </div>
      <div className="card-body space-y-4">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label className="label">API Key ID</label>
            <input
              type="text"
              value={settings.keyId}
              onChange={(e) => setSettings({ ...settings, keyId: e.target.value })}
              placeholder="ALPACA_KEY_ID"
              className="input"
            />
          </div>
          <div>
            <label className="label">Secret Key</label>
            <div className="relative">
              <input
                type={showSecret ? 'text' : 'password'}
                value={settings.secretKey}
                onChange={(e) => setSettings({ ...settings, secretKey: e.target.value })}
                placeholder="ALPACA_SECRET_KEY"
                className="input pr-10"
              />
              <button
                type="button"
                onClick={() => setShowSecret(!showSecret)}
                className="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
              >
                {showSecret ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
              </button>
            </div>
          </div>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div>
            <label className="label">Data Feed</label>
            <select
              value={settings.feed}
              onChange={(e) => setSettings({ ...settings, feed: e.target.value })}
              className="select"
            >
              <option value="iex">IEX (Free)</option>
              <option value="sip">SIP (Paid)</option>
              <option value="delayed_sip">Delayed SIP</option>
            </select>
          </div>
          <div className="flex items-center space-x-2 pt-6">
            <input
              type="checkbox"
              id="useSandbox"
              checked={settings.useSandbox}
              onChange={(e) => setSettings({ ...settings, useSandbox: e.target.checked })}
              className="rounded border-gray-300 text-primary-600 focus:ring-primary-500"
            />
            <label htmlFor="useSandbox" className="text-sm text-gray-700">
              Use Sandbox
            </label>
          </div>
          <div className="flex items-center space-x-2 pt-6">
            <input
              type="checkbox"
              id="subscribeQuotes"
              checked={settings.subscribeQuotes}
              onChange={(e) => setSettings({ ...settings, subscribeQuotes: e.target.checked })}
              className="rounded border-gray-300 text-primary-600 focus:ring-primary-500"
            />
            <label htmlFor="subscribeQuotes" className="text-sm text-gray-700">
              Subscribe Quotes
            </label>
          </div>
        </div>

        <button
          onClick={handleSave}
          disabled={updateAlpaca.isPending}
          className="btn btn-primary"
        >
          {updateAlpaca.isPending ? (
            <RefreshCw className="w-4 h-4 mr-2 animate-spin" />
          ) : (
            <Save className="w-4 h-4 mr-2" />
          )}
          Save Alpaca Settings
        </button>
      </div>
    </div>
  );
}
