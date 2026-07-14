import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import type { KnowledgeDocumentDetailDto } from '../model/knowledge-document.types';

/**
 * Single knowledge document detail — includes chunks and version history.
 * Polls while the document is still pending or processing (S15).
 */
export function useKnowledgeDocument(id: string) {
  return useQuery({
    queryKey: ['settings', 'knowledge', id] as const,
    queryFn: async (): Promise<KnowledgeDocumentDetailDto> => {
      const { data } = await apiClient.get<KnowledgeDocumentDetailDto>(
        `/settings/knowledge/${id}`,
      );
      return data;
    },
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      return status === 'Pending' || status === 'Processing' ? 3_000 : false;
    },
  });
}
