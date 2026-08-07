import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import type { ChannelConnectionDto } from '../model/channel-connection.types';

/**
 * React Query owns this server state (S7). Talks only to the same-origin BFF (S4); the tenant is
 * resolved server-side from the session/JWT, never sent from the client (S8).
 */
export const channelConnectionsQueryKey = ['settings', 'channels'] as const;

export function useChannelConnections() {
  return useQuery({
    queryKey: channelConnectionsQueryKey,
    queryFn: async (): Promise<ChannelConnectionDto[]> => {
      const { data } = await apiClient.get<ChannelConnectionDto[]>('/settings/channels');
      return data;
    },
  });
}
