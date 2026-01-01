import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { configApi, statusApi, backfillApi, symbolsApi } from '../api/client';
import type { SymbolConfig, StorageConfig, BackfillConfig } from '../types';
import toast from 'react-hot-toast';

// Query Keys
export const queryKeys = {
  config: ['config'] as const,
  status: ['status'] as const,
  backfillProviders: ['backfill', 'providers'] as const,
  backfillStatus: ['backfill', 'status'] as const,
  backfillHealth: ['backfill', 'health'] as const,
  symbolTemplates: ['symbols', 'templates'] as const,
};

// Config Hooks
export function useConfig() {
  return useQuery({
    queryKey: queryKeys.config,
    queryFn: configApi.getConfig,
    staleTime: 10000,
    refetchInterval: 30000,
  });
}

export function useUpdateDataSource() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (dataSource: string) => configApi.updateDataSource(dataSource),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.config });
      toast.success('Data source updated');
    },
    onError: (error: Error) => {
      toast.error(`Failed to update data source: ${error.message}`);
    },
  });
}

export function useUpdateAlpaca() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: configApi.updateAlpaca,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.config });
      toast.success('Alpaca settings saved');
    },
    onError: (error: Error) => {
      toast.error(`Failed to save Alpaca settings: ${error.message}`);
    },
  });
}

export function useUpdateStorage() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (config: StorageConfig) => configApi.updateStorage(config),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.config });
      toast.success('Storage settings saved');
    },
    onError: (error: Error) => {
      toast.error(`Failed to save storage settings: ${error.message}`);
    },
  });
}

export function useAddSymbol() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (symbol: SymbolConfig) => configApi.addSymbol(symbol),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.config });
      toast.success('Symbol added');
    },
    onError: (error: Error) => {
      toast.error(`Failed to add symbol: ${error.message}`);
    },
  });
}

export function useDeleteSymbol() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (symbol: string) => configApi.deleteSymbol(symbol),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.config });
      toast.success('Symbol removed');
    },
    onError: (error: Error) => {
      toast.error(`Failed to remove symbol: ${error.message}`);
    },
  });
}

// Status Hooks
export function useStatus() {
  return useQuery({
    queryKey: queryKeys.status,
    queryFn: statusApi.getStatus,
    refetchInterval: 2000,
  });
}

// Backfill Hooks
export function useBackfillProviders() {
  return useQuery({
    queryKey: queryKeys.backfillProviders,
    queryFn: backfillApi.getProviders,
    staleTime: 60000,
  });
}

export function useBackfillStatus() {
  return useQuery({
    queryKey: queryKeys.backfillStatus,
    queryFn: backfillApi.getStatus,
    refetchInterval: 3000,
  });
}

export function useBackfillHealth() {
  return useQuery({
    queryKey: queryKeys.backfillHealth,
    queryFn: backfillApi.getHealth,
    staleTime: 30000,
  });
}

export function useResolveSymbol() {
  return useMutation({
    mutationFn: (symbol: string) => backfillApi.resolveSymbol(symbol),
    onError: (error: Error) => {
      toast.error(`Failed to resolve symbol: ${error.message}`);
    },
  });
}

export function useRunBackfill() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (config: Partial<BackfillConfig>) => backfillApi.run(config),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.backfillStatus });
      toast.success('Backfill job started');
    },
    onError: (error: Error) => {
      toast.error(`Failed to start backfill: ${error.message}`);
    },
  });
}

// Symbols Hooks
export function useSymbolTemplates() {
  return useQuery({
    queryKey: queryKeys.symbolTemplates,
    queryFn: symbolsApi.getTemplates,
    staleTime: 60000,
  });
}

export function useBulkImport() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (csv: string) => symbolsApi.bulkImport(csv),
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.config });
      toast.success(`Imported ${result.imported} symbols`);
      if (result.errors.length > 0) {
        toast.error(`${result.errors.length} errors occurred`);
      }
    },
    onError: (error: Error) => {
      toast.error(`Failed to import: ${error.message}`);
    },
  });
}
