import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';

/** Trigger re-embedding for a knowledge document. Invalidates both list and detail. */
export function useReEmbedKnowledgeDocument() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: string): Promise<void> => {
      await apiClient.post(`/settings/knowledge/${id}/re-embed`);
    },
    onSuccess: (_data, id) => {
      void queryClient.invalidateQueries({ queryKey: ['settings', 'knowledge'] });
      void queryClient.invalidateQueries({ queryKey: ['settings', 'knowledge', id] });
    },
  });
}
