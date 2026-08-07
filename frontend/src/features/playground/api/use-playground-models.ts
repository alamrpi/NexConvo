import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import type { ProviderModelDto } from '../model/playground.types';

/**
 * React Query owns this server state (S7). One fetch per session — models don't change mid-session,
 * so staleTime is Infinity rather than the default (re-opening the model picker never refetches).
 */
export const playgroundModelsQueryKey = ['playground', 'models'] as const;

export function usePlaygroundModels() {
  return useQuery({
    queryKey: playgroundModelsQueryKey,
    queryFn: async (): Promise<ProviderModelDto[]> => {
      const { data } = await apiClient.get<ProviderModelDto[]>('/playground/models');
      return data;
    },
    staleTime: Infinity,
  });
}
