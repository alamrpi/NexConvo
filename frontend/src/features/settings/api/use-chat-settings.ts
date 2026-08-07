import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import type { WorkspaceChatSettingsDto } from '../model/chat-settings.types';

/**
 * React Query owns this server state (S7). Talks only to the same-origin BFF (S4); the tenant is
 * resolved server-side from the session/JWT, never sent from the client (S8).
 */
export const chatSettingsQueryKey = ['settings', 'chat-settings'] as const;

export function useChatSettings() {
  return useQuery({
    queryKey: chatSettingsQueryKey,
    queryFn: async (): Promise<WorkspaceChatSettingsDto> => {
      const { data } = await apiClient.get<WorkspaceChatSettingsDto>('/settings/chat-settings');
      return data;
    },
  });
}
