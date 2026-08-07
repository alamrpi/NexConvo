import { useQuery, keepPreviousData } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import type { ConversationListFilters, ConversationSummaryDto, CursorPagedResult } from '../model/inbox.types';

/**
 * React Query owns this server state (S7). Talks only to the same-origin BFF (S4); the tenant is
 * resolved server-side from the JWT (S8), never sent from the client. Cursor-paged per
 * GetConversationsQuery/CursorPagedResult (design.md decision 1) — the query key includes the
 * filter set (minus cursor, which is passed as a page-fetch param) so each distinct filter
 * combination gets its own cache entry.
 */
export const conversationsQueryKey = (filters: Omit<ConversationListFilters, 'cursor'>) =>
  ['inbox', 'conversations', filters] as const;

export function useConversations(filters: Omit<ConversationListFilters, 'cursor'> = {}, cursor?: string) {
  return useQuery({
    queryKey: [...conversationsQueryKey(filters), cursor ?? null] as const,
    queryFn: async (): Promise<CursorPagedResult<ConversationSummaryDto>> => {
      const { data } = await apiClient.get<CursorPagedResult<ConversationSummaryDto>>('/conversations', {
        params: {
          state: filters.state,
          channel: filters.channel,
          assignedToMe: filters.assignedToMe,
          cursor,
          pageSize: filters.pageSize,
        },
      });
      return data;
    },
    // Keep showing the previous page's data while the next page loads, so the list doesn't
    // flash empty on pagination/filter changes.
    placeholderData: keepPreviousData,
  });
}
