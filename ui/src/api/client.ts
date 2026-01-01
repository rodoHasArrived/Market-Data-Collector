import type {
  AppConfig,
  SystemStatus,
  BackfillProvider,
  BackfillStatus,
  ProviderHealth,
  SymbolResolution,
  SymbolConfig,
  StorageConfig,
  BackfillConfig,
} from '../types';

const API_BASE = import.meta.env.VITE_API_URL || '/api';

class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
    this.name = 'ApiError';
  }
}

async function fetchApi<T>(endpoint: string, options?: RequestInit): Promise<T> {
  const url = `${API_BASE}${endpoint}`;

  const response = await fetch(url, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...options?.headers,
    },
  });

  if (!response.ok) {
    const errorText = await response.text().catch(() => 'Unknown error');
    throw new ApiError(response.status, errorText || `HTTP ${response.status}`);
  }

  // Handle empty responses
  const text = await response.text();
  if (!text) return {} as T;

  return JSON.parse(text);
}

// Configuration API
export const configApi = {
  getConfig: () => fetchApi<AppConfig>('/config'),

  updateDataSource: (dataSource: string) =>
    fetchApi<void>('/config/datasource', {
      method: 'POST',
      body: JSON.stringify({ dataSource }),
    }),

  updateAlpaca: (config: Partial<AppConfig['alpaca']>) =>
    fetchApi<void>('/config/alpaca', {
      method: 'POST',
      body: JSON.stringify(config),
    }),

  updateStorage: (config: StorageConfig) =>
    fetchApi<void>('/config/storage', {
      method: 'POST',
      body: JSON.stringify(config),
    }),

  addSymbol: (symbol: SymbolConfig) =>
    fetchApi<void>('/config/symbols', {
      method: 'POST',
      body: JSON.stringify(symbol),
    }),

  deleteSymbol: (symbol: string) =>
    fetchApi<void>(`/config/symbols/${encodeURIComponent(symbol)}`, {
      method: 'DELETE',
    }),
};

// Status API
export const statusApi = {
  getStatus: () => fetchApi<SystemStatus>('/status'),
};

// Backfill API
export const backfillApi = {
  getProviders: () => fetchApi<BackfillProvider[]>('/backfill/providers'),

  getStatus: () => fetchApi<BackfillStatus>('/backfill/status'),

  getHealth: () => fetchApi<ProviderHealth[]>('/backfill/health'),

  resolveSymbol: (symbol: string) =>
    fetchApi<SymbolResolution[]>(`/backfill/resolve/${encodeURIComponent(symbol)}`),

  run: (config: Partial<BackfillConfig>) =>
    fetchApi<{ jobId: string }>('/backfill/run', {
      method: 'POST',
      body: JSON.stringify(config),
    }),
};

// Symbols API
export const symbolsApi = {
  getTemplates: () => fetchApi<SymbolConfig[]>('/symbols/templates'),

  bulkImport: (csv: string) =>
    fetchApi<{ imported: number; errors: string[] }>('/symbols/bulk-import', {
      method: 'POST',
      body: JSON.stringify({ csv }),
    }),

  bulkExport: () => fetchApi<string>('/symbols/bulk-export'),
};

export { ApiError };
