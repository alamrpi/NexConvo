import { useQuery, keepPreviousData } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import type { CursorPagedResult, MessageDto } from '../model/inbox.types';

/**
 * React Query owns this server state (S7). Mirrors GetConversationMessagesQuery: chronological
 * (oldest first), cursor-paged. Disabled until a conversationId is selected so opening the inbox
 * with no conversation open issues no request.
 */
export const conversationMessagesQueryKey = (conversationId: string | null) =>
  ['inbox', 'conversations', conversationId, 'messages'] as const;

export function useConversationMessages(conversationId: string | null, cursor?: string) {
  return useQuery({
    queryKey: [...conversationMessagesQueryKey(conversationId), cursor ?? null] as const,
    queryFn: async (): Promise<CursorPagedResult<MessageDto>> => {
      const { data } = await apiClient.get<CursorPagedResult<MessageDto>>(
        `/conversations/${conversationId}/messages`,
        { params: { cursor } },
      );
      return data;
    },
    enabled: conversationId !== null,
    placeholderData: keepPreviousData,
  });
}
