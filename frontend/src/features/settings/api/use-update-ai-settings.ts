import { useMutation, useQueryClient } from '@tanstack/react-query';
import axios from 'axios';
import { apiClient } from '@/shared/api/client/api-client';
import type { AiSettingsValues } from '../model/ai-settings.schema';
import { aiSettingsQueryKey } from './use-ai-settings';

export type AiSettingsErrorCode = 'conflict' | 'test-failed' | 'generic';

/** Typed mutation error so the form can surface a 409 optimistic-concurrency conflict, or a
 * server-side re-test failure on save, distinctly (S17). */
export class AiSettingsError extends Error {
  constructor(readonly code: AiSettingsErrorCode) {
    super(code);
    this.name = 'AiSettingsError';
  }
}

function mapError(error: unknown): AiSettingsError {
  if (axios.isAxiosError(error)) {
    const status = error.response?.status;
    const code = (error.response?.data as { code?: string } | undefined)?.code;
    if (status === 409 || code === 'conflict' || code === 'concurrency-conflict') {
      return new AiSettingsError('conflict');
    }
    // Server re-tested credentials on save and they failed — distinct from a zod-validation 422.
    if (status === 422 && code === 'test-failed') {
      return new AiSettingsError('test-failed');
    }
  }
  return new AiSettingsError('generic');
}

/** Persists workspace AI settings through the BFF, then refreshes the cached query (S7). */
export function useUpdateAiSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (values: AiSettingsValues): Promise<void> => {
      try {
        await apiClient.post('/settings/ai-config', values);
      } catch (error) {
        throw mapError(error);
      }
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: aiSettingsQueryKey }),
  });
}
