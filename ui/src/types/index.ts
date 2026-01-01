// Configuration Types
export interface AppConfig {
  dataRoot: string;
  compress: boolean;
  dataSource: 'IB' | 'Alpaca' | 'Polygon';
  alpaca: AlpacaConfig;
  storage: StorageConfig;
  symbols: SymbolConfig[];
  backfill: BackfillConfig;
}

export interface AlpacaConfig {
  keyId: string;
  secretKey: string;
  feed: 'iex' | 'sip' | 'delayed_sip';
  useSandbox: boolean;
  subscribeQuotes: boolean;
  maxConnectionRetries: number;
  initialRetryDelaySeconds: number;
  maxRetryDelaySeconds: number;
  connectionTimeoutSeconds: number;
  enableAutoReconnect: boolean;
  enableHeartbeat: boolean;
  heartbeatIntervalSeconds: number;
  heartbeatTimeoutSeconds: number;
  consecutiveFailuresBeforeReconnect: number;
}

export interface StorageConfig {
  namingConvention: 'Flat' | 'BySymbol' | 'ByDate' | 'ByType';
  datePartition: 'None' | 'Daily' | 'Hourly' | 'Monthly';
  includeProvider: boolean;
  filePrefix: string | null;
  retentionDays: number | null;
  maxTotalMegabytes: number | null;
}

export interface SymbolConfig {
  symbol: string;
  subscribeTrades: boolean;
  subscribeDepth: boolean;
  depthLevels: number;
  securityType: string;
  exchange: string;
  currency: string;
  primaryExchange: string;
  localSymbol?: string;
}

export interface BackfillConfig {
  enabled: boolean;
  provider: string;
  symbols: string[];
  from: string;
  to: string;
  enableFallback: boolean;
  preferAdjustedPrices: boolean;
  enableSymbolResolution: boolean;
}

// Status Types
export interface SystemStatus {
  isRunning: boolean;
  uptime: string;
  dataSource: string;
  symbolCount: number;
  metrics: MetricsData;
  integrityEvents: IntegrityEvent[];
  lastUpdated: string;
}

export interface MetricsData {
  published: number;
  dropped: number;
  trades: number;
  depthUpdates: number;
  quotes: number;
  eventsPerSecond: number;
  dropRate: number;
}

export interface IntegrityEvent {
  timestamp: string;
  symbol: string;
  eventType: string;
  message: string;
  severity: 'Info' | 'Warning' | 'Error';
}

// Backfill Types
export interface BackfillProvider {
  name: string;
  displayName: string;
  description: string;
  isAvailable: boolean;
  supportsAdjustedPrices: boolean;
  rateLimit: string;
}

export interface BackfillStatus {
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
}

export interface ProviderHealth {
  provider: string;
  isHealthy: boolean;
  lastCheck: string;
  responseTimeMs: number;
  error: string | null;
}

export interface SymbolResolution {
  originalSymbol: string;
  figi: string;
  name: string;
  ticker: string;
  exchange: string;
  securityType: string;
}

// API Response Types
export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  error?: string;
}
