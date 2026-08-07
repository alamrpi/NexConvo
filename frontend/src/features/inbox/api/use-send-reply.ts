import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import type { CursorPagedResult, MessageDto } from '../model/inbox.types';
import { conversationMessagesQueryKey } from './use-conversation-messages';

/**
 * Sends an agent reply on a conversation (SendAgentReplyCommand). The message row is appended to
 * the open conversation's message cache immediately on success (the same event also arrives via
 * the "message" chatEvent for any other connected agent client, per design.md decision 2 — this
 * mutation's own optimistic append covers the sending agent's own tab without waiting on the
 * SignalR round-trip). The conversation list is invalidated so lastMessagePreview/lastMessageAt
 * re-sync (S7).
 */
export function useSendReply(conversationId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (text: string): Promise<MessageDto> => {
      const { data } = await apiClient.post<MessageDto>(`/conversations/${conversationId}/reply`, { text });
      return data;
    },

    onSuccess: (message) => {
      queryClient.setQueriesData<CursorPagedResult<MessageDto>>(
        { queryKey: conversationMessagesQueryKey(conversationId) },
        (current) =>
          current
            ? { ...current, items: [...current.items, message] }
            : { items: [message], nextCursor: null },
      );
      queryClient.invalidateQueries({ queryKey: ['inbox', 'conversations'], exact: false });
    },
  });
}
