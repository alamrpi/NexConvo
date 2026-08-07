import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';

/**
 * Takes ownership of a conversation pending human handoff (TakeOverConversationCommand). Backend
 * returns 204 No Content on success (Result -> NoContentResult, see ResultExtensions.cs); the
 * authoritative state change (state -> HumanHandling, assignedAgentName) arrives via the
 * "assigned" chatEvent handled by useChatHub, so this mutation only invalidates the conversation
 * list as a fallback in case the SignalR event is missed (e.g. brief disconnect).
 */
export function useTakeOver() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (conversationId: string): Promise<void> => {
      await apiClient.post(`/conversations/${conversationId}/take-over`);
    },

    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['inbox', 'conversations'], exact: false });
    },
  });
}
