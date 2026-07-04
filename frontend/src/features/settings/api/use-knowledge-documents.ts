import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import type {
  KnowledgeDocumentPagedResult,
  IngestionStatus,
  SourceType,
} from '../model/knowledge-document.types';

export interface KnowledgeDocumentsParams {
  page?: number;
  pageSize?: number;
  status?: IngestionStatus;
  sourceType?: SourceType;
}

/**
 * Paginated knowledge document list (S7 — React Query owns this server state).
 * Automatically polls every 3 s while any document is pending or processing (S15).
 */
export function useKnowledgeDocuments(params?: KnowledgeDocumentsParams) {
  return useQuery({
    queryKey: ['settings', 'knowledge', params] as const,
    queryFn: async (): Promise<KnowledgeDocumentPagedResult> => {
      const { data } = await apiClient.get<KnowledgeDocumentPagedResult>(
        '/settings/knowledge',
        { params },
      );
      return data;
    },
    refetchInterval: (query) => {
      const items = query.state.data?.items ?? [];
      const hasInFlight = items.some(
        (d) => d.status === 'pending' || d.status === 'processing',
      );
      return hasInFlight ? 3_000 : false;
    },
  });
}
