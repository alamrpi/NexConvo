import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import type { KnowledgeDocumentPagedResult } from '../model/knowledge-document.types';

/**
 * Soft-delete a knowledge document with optimistic removal from the list (S15/S17).
 * The document is removed immediately from cache and restored on error.
 */
export function useDeleteKnowledgeDocument() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: string): Promise<void> => {
      await apiClient.delete(`/settings/knowledge/${id}`);
    },
    onMutate: async (id) => {
      // Cancel any in-flight refetches so they don't overwrite our optimistic update.
      await queryClient.cancelQueries({ queryKey: ['settings', 'knowledge'] });

      // Snapshot all matching list queries (params vary — snapshot by prefix).
      const previousSnapshots = queryClient.getQueriesData<KnowledgeDocumentPagedResult>({
        queryKey: ['settings', 'knowledge'],
      });

      // Optimistically remove from every cached list.
      queryClient.setQueriesData<KnowledgeDocumentPagedResult>(
        { queryKey: ['settings', 'knowledge'] },
        (old) => {
          if (!old) return old;
          return {
            ...old,
            items: old.items.filter((doc) => doc.id !== id),
            totalCount: Math.max(0, old.totalCount - 1),
          };
        },
      );

      return { previousSnapshots };
    },
    onError: (_err, _id, context) => {
      // Restore snapshots on failure (S15 — mutations handle failure).
      if (context?.previousSnapshots) {
        for (const [queryKey, data] of context.previousSnapshots) {
          queryClient.setQueryData(queryKey, data);
        }
      }
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: ['settings', 'knowledge'] });
    },
  });
}
