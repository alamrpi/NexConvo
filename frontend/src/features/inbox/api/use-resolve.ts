import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';

/**
 * Marks an actively-handled conversation as resolved (ResolveConversationCommand). Backend returns
 * 204 No Content on success; the authoritative state change arrives via the "resolved" chatEvent
 * handled by useChatHub — this mutation's invalidation is the fallback path (see use-take-over.ts).
 */
export function useResolve() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (conversationId: string): Promise<void> => {
      await apiClient.post(`/conversations/${conversationId}/resolve`);
    },

    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['inbox', 'conversations'], exact: false });
    },
  });
}
