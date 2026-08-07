import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';

/**
 * Reopens a resolved/closed conversation back to AI handling (ReopenConversationCommand). Backend
 * returns 204 No Content on success; the authoritative state change arrives via the "reopened"
 * chatEvent handled by useChatHub — this mutation's invalidation is the fallback path (see
 * use-take-over.ts).
 */
export function useReopen() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (conversationId: string): Promise<void> => {
      await apiClient.post(`/conversations/${conversationId}/reopen`);
    },

    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['inbox', 'conversations'], exact: false });
    },
  });
}
