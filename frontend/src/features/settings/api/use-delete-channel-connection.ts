import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import type { ChannelConnectionDto } from '../model/channel-connection.types';
import { channelConnectionsQueryKey } from './use-channel-connections';

/**
 * Deletes a channel connection with an optimistic update (S15/S7):
 * - onMutate: remove the item from the cache immediately for instant UI feedback.
 * - onError: restore the previous snapshot so no data is lost on failure.
 * - onSettled: invalidate to re-sync with the server regardless of outcome.
 */
export function useDeleteChannelConnection() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: string): Promise<void> => {
      await apiClient.delete(`/settings/channels/${id}`);
    },

    onMutate: async (id: string) => {
      // Cancel any in-flight refetches so they don't stomp the optimistic update.
      await queryClient.cancelQueries({ queryKey: channelConnectionsQueryKey });

      // Snapshot the current list before optimistically removing the item.
      const snapshot = queryClient.getQueryData<ChannelConnectionDto[]>(channelConnectionsQueryKey);

      queryClient.setQueryData<ChannelConnectionDto[]>(
        channelConnectionsQueryKey,
        (current) => current?.filter((c) => c.id !== id) ?? [],
      );

      return { snapshot };
    },

    onError: (_error, _id, context) => {
      // Restore the pre-mutation snapshot so the user sees the correct state.
      if (context?.snapshot !== undefined) {
        queryClient.setQueryData(channelConnectionsQueryKey, context.snapshot);
      }
    },

    onSettled: () => {
      // Always re-sync from the server once the mutation settles (S7).
      queryClient.invalidateQueries({ queryKey: channelConnectionsQueryKey });
    },
  });
}
