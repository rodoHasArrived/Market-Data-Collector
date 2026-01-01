import { useState } from 'react';
import { useConfig, useAddSymbol, useDeleteSymbol } from '../hooks/useApi';
import type { SymbolConfig } from '../types';
import {
  Plus,
  Trash2,
  Edit2,
  X,
  Check,
  Database,
  TrendingUp,
  Layers,
  Search,
} from 'lucide-react';
import toast from 'react-hot-toast';

export default function SymbolsPage() {
  const { data: config, isLoading } = useConfig();
  const [searchQuery, setSearchQuery] = useState('');
  const [showAddModal, setShowAddModal] = useState(false);
  const [editingSymbol, setEditingSymbol] = useState<SymbolConfig | null>(null);

  const deleteSymbol = useDeleteSymbol();

  const filteredSymbols = config?.symbols?.filter((s) =>
    s.symbol.toLowerCase().includes(searchQuery.toLowerCase())
  );

  const handleDelete = (symbol: string) => {
    if (confirm(`Remove ${symbol} from subscriptions?`)) {
      deleteSymbol.mutate(symbol);
    }
  };

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
          <h2 className="text-2xl font-bold text-gray-900">Symbol Subscriptions</h2>
          <p className="text-sm text-gray-500 mt-1">
            Manage symbols for market data collection
          </p>
        </div>
        <button onClick={() => setShowAddModal(true)} className="btn btn-primary">
          <Plus className="w-4 h-4 mr-2" />
          Add Symbol
        </button>
      </div>

      {/* Search */}
      <div className="relative">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
        <input
          type="text"
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          placeholder="Search symbols..."
          className="input pl-10"
        />
      </div>

      {/* Symbols Table */}
      <div className="card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="table">
            <thead>
              <tr>
                <th>Symbol</th>
                <th>Type</th>
                <th>Exchange</th>
                <th>Subscriptions</th>
                <th>Depth Levels</th>
                <th className="text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-200">
              {filteredSymbols?.length === 0 ? (
                <tr>
                  <td colSpan={6} className="text-center py-8 text-gray-500">
                    <Database className="w-8 h-8 mx-auto mb-2 opacity-50" />
                    <p>No symbols configured</p>
                    <button
                      onClick={() => setShowAddModal(true)}
                      className="text-primary-600 hover:text-primary-700 mt-2"
                    >
                      Add your first symbol
                    </button>
                  </td>
                </tr>
              ) : (
                filteredSymbols?.map((symbol) => (
                  <tr key={symbol.symbol}>
                    <td className="font-medium">{symbol.symbol}</td>
                    <td>
                      <span className="badge badge-info">{symbol.securityType}</span>
                    </td>
                    <td>{symbol.exchange}</td>
                    <td>
                      <div className="flex items-center space-x-2">
                        {symbol.subscribeTrades && (
                          <span className="badge badge-success flex items-center">
                            <TrendingUp className="w-3 h-3 mr-1" />
                            Trades
                          </span>
                        )}
                        {symbol.subscribeDepth && (
                          <span className="badge badge-info flex items-center">
                            <Layers className="w-3 h-3 mr-1" />
                            Depth
                          </span>
                        )}
                      </div>
                    </td>
                    <td>{symbol.subscribeDepth ? symbol.depthLevels : '-'}</td>
                    <td className="text-right">
                      <div className="flex items-center justify-end space-x-2">
                        <button
                          onClick={() => setEditingSymbol(symbol)}
                          className="p-1 text-gray-400 hover:text-primary-600"
                          title="Edit"
                        >
                          <Edit2 className="w-4 h-4" />
                        </button>
                        <button
                          onClick={() => handleDelete(symbol.symbol)}
                          disabled={deleteSymbol.isPending}
                          className="p-1 text-gray-400 hover:text-red-600"
                          title="Delete"
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Summary */}
      <div className="grid grid-cols-3 gap-4">
        <div className="card p-4 text-center">
          <p className="text-2xl font-bold text-gray-900">{config?.symbols?.length || 0}</p>
          <p className="text-sm text-gray-500">Total Symbols</p>
        </div>
        <div className="card p-4 text-center">
          <p className="text-2xl font-bold text-emerald-600">
            {config?.symbols?.filter((s) => s.subscribeTrades).length || 0}
          </p>
          <p className="text-sm text-gray-500">Trade Subscriptions</p>
        </div>
        <div className="card p-4 text-center">
          <p className="text-2xl font-bold text-blue-600">
            {config?.symbols?.filter((s) => s.subscribeDepth).length || 0}
          </p>
          <p className="text-sm text-gray-500">Depth Subscriptions</p>
        </div>
      </div>

      {/* Add/Edit Modal */}
      {(showAddModal || editingSymbol) && (
        <SymbolModal
          symbol={editingSymbol}
          onClose={() => {
            setShowAddModal(false);
            setEditingSymbol(null);
          }}
        />
      )}
    </div>
  );
}

interface SymbolModalProps {
  symbol: SymbolConfig | null;
  onClose: () => void;
}

function SymbolModal({ symbol, onClose }: SymbolModalProps) {
  const isEdit = !!symbol;
  const addSymbol = useAddSymbol();

  const [form, setForm] = useState<SymbolConfig>(
    symbol || {
      symbol: '',
      subscribeTrades: true,
      subscribeDepth: true,
      depthLevels: 10,
      securityType: 'STK',
      exchange: 'SMART',
      currency: 'USD',
      primaryExchange: '',
      localSymbol: '',
    }
  );

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.symbol.trim()) {
      toast.error('Symbol is required');
      return;
    }
    addSymbol.mutate(form, {
      onSuccess: () => onClose(),
    });
  };

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-xl shadow-xl max-w-lg w-full max-h-[90vh] overflow-y-auto animate-fadeIn">
        <div className="flex items-center justify-between p-4 border-b">
          <h3 className="text-lg font-semibold text-gray-900">
            {isEdit ? 'Edit Symbol' : 'Add Symbol'}
          </h3>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            <X className="w-5 h-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-4 space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div className="col-span-2">
              <label className="label">Symbol</label>
              <input
                type="text"
                value={form.symbol}
                onChange={(e) => setForm({ ...form, symbol: e.target.value.toUpperCase() })}
                placeholder="e.g., SPY"
                className="input"
                disabled={isEdit}
              />
            </div>

            <div>
              <label className="label">Security Type</label>
              <select
                value={form.securityType}
                onChange={(e) => setForm({ ...form, securityType: e.target.value })}
                className="select"
              >
                <option value="STK">Stock</option>
                <option value="FUT">Future</option>
                <option value="OPT">Option</option>
                <option value="CASH">Forex</option>
                <option value="IND">Index</option>
              </select>
            </div>

            <div>
              <label className="label">Exchange</label>
              <input
                type="text"
                value={form.exchange}
                onChange={(e) => setForm({ ...form, exchange: e.target.value.toUpperCase() })}
                placeholder="SMART"
                className="input"
              />
            </div>

            <div>
              <label className="label">Currency</label>
              <input
                type="text"
                value={form.currency}
                onChange={(e) => setForm({ ...form, currency: e.target.value.toUpperCase() })}
                placeholder="USD"
                className="input"
              />
            </div>

            <div>
              <label className="label">Primary Exchange</label>
              <input
                type="text"
                value={form.primaryExchange}
                onChange={(e) => setForm({ ...form, primaryExchange: e.target.value.toUpperCase() })}
                placeholder="e.g., NASDAQ"
                className="input"
              />
            </div>

            <div className="col-span-2">
              <label className="label">Local Symbol (Optional)</label>
              <input
                type="text"
                value={form.localSymbol || ''}
                onChange={(e) => setForm({ ...form, localSymbol: e.target.value })}
                placeholder="For futures/preferred shares"
                className="input"
              />
            </div>
          </div>

          {/* Subscriptions */}
          <div className="border-t pt-4">
            <p className="text-sm font-medium text-gray-700 mb-3">Subscriptions</p>
            <div className="space-y-3">
              <label className="flex items-center space-x-3">
                <input
                  type="checkbox"
                  checked={form.subscribeTrades}
                  onChange={(e) => setForm({ ...form, subscribeTrades: e.target.checked })}
                  className="rounded border-gray-300 text-primary-600 focus:ring-primary-500"
                />
                <span className="text-sm text-gray-700">Subscribe to Trades</span>
              </label>
              <label className="flex items-center space-x-3">
                <input
                  type="checkbox"
                  checked={form.subscribeDepth}
                  onChange={(e) => setForm({ ...form, subscribeDepth: e.target.checked })}
                  className="rounded border-gray-300 text-primary-600 focus:ring-primary-500"
                />
                <span className="text-sm text-gray-700">Subscribe to Market Depth</span>
              </label>
              {form.subscribeDepth && (
                <div className="ml-6">
                  <label className="label">Depth Levels</label>
                  <input
                    type="number"
                    value={form.depthLevels}
                    onChange={(e) => setForm({ ...form, depthLevels: parseInt(e.target.value) || 10 })}
                    min={1}
                    max={20}
                    className="input w-24"
                  />
                </div>
              )}
            </div>
          </div>

          {/* Actions */}
          <div className="flex justify-end space-x-3 pt-4 border-t">
            <button type="button" onClick={onClose} className="btn btn-secondary">
              Cancel
            </button>
            <button type="submit" disabled={addSymbol.isPending} className="btn btn-primary">
              {addSymbol.isPending ? (
                <div className="w-4 h-4 mr-2 animate-spin rounded-full border-2 border-white border-t-transparent" />
              ) : (
                <Check className="w-4 h-4 mr-2" />
              )}
              {isEdit ? 'Update Symbol' : 'Add Symbol'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
