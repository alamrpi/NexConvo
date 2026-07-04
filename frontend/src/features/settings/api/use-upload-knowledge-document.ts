import { useRef } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import type {
  KnowledgeDocumentDto,
  UploadKnowledgeDocumentInput,
} from '../model/knowledge-document.types';

/**
 * Upload or create a knowledge document.
 *
 * File uploads use multipart/form-data with byte-level progress tracking via
 * `onUploadProgress` (non-negotiable S4 requirement). Text/URL/FAQ uploads use JSON.
 *
 * Usage:
 *   const { mutate, progressRef } = useUploadKnowledgeDocument();
 *   progressRef.current = (pct) => setProgress(pct);
 *   mutate({ file, title, sourceType: 'file' });
 */
export function useUploadKnowledgeDocument() {
  const queryClient = useQueryClient();

  /**
   * Ref rather than state — the upload progress callback must not cause re-renders
   * of the hook itself (which would reset the mutation). Callers write their own
   * setState into this ref before calling mutate.
   */
  const progressRef = useRef<((percent: number) => void) | null>(null);

  const mutation = useMutation({
    mutationFn: async (input: UploadKnowledgeDocumentInput): Promise<KnowledgeDocumentDto> => {
      if (input.file) {
        const form = new FormData();
        form.append('file', input.file);
        form.append('title', input.title);
        form.append('sourceType', input.sourceType);

        const { data } = await apiClient.post<KnowledgeDocumentDto>(
          '/settings/knowledge',
          form,
          {
            onUploadProgress: (event) => {
              if (event.total && progressRef.current) {
                progressRef.current(Math.round((event.loaded / event.total) * 100));
              }
            },
          },
        );
        return data;
      }

      // Text / URL / FAQ / past-chats: JSON body
      const body: Record<string, unknown> = {
        title: input.title,
        sourceType: input.sourceType,
      };
      if (input.content !== undefined) body['content'] = input.content;
      if (input.sourceUrl !== undefined) body['sourceUrl'] = input.sourceUrl;
      if (input.faqPairs !== undefined) body['faqPairs'] = input.faqPairs;
      if (input.dateFrom !== undefined) body['dateFrom'] = input.dateFrom;
      if (input.dateTo !== undefined) body['dateTo'] = input.dateTo;

      const { data } = await apiClient.post<KnowledgeDocumentDto>('/settings/knowledge', body);
      return data;
    },
    onSuccess: () => {
      // Invalidate the list so the new document appears (S7 — React Query owns state).
      void queryClient.invalidateQueries({ queryKey: ['settings', 'knowledge'] });
    },
  });

  return { ...mutation, progressRef };
}
