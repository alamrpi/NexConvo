import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import type { AiConfigDto } from '../model/ai-settings.types';

/**
 * React Query owns this server state (S7). Talks only to the same-origin BFF (S4); the tenant is
 * resolved server-side from the session/JWT, never sent from the client (S8).
 */
export const aiSettingsQueryKey = ['settings', 'ai-config'] as const;

export function useAiSettings() {
  return useQuery({
    queryKey: aiSettingsQueryKey,
    queryFn: async (): Promise<AiConfigDto[]> => {
      const { data } = await apiClient.get<AiConfigDto[]>('/settings/ai-config');
      return data;
    },
  });
}
