import { useMutation, useQueryClient } from '@tanstack/react-query';
import axios from 'axios';
import { apiClient } from '@/shared/api/client/api-client';
import type { ChatSettingsValues } from '../model/chat-settings.schema';
import { chatSettingsQueryKey } from './use-chat-settings';

export type ChatSettingsErrorCode = 'conflict' | 'generic';

/** Typed mutation error so the form can surface a 409 optimistic-concurrency conflict distinctly (S17). */
export class ChatSettingsError extends Error {
  constructor(readonly code: ChatSettingsErrorCode) {
    super(code);
    this.name = 'ChatSettingsError';
  }
}

function mapError(error: unknown): ChatSettingsError {
  if (axios.isAxiosError(error)) {
    const status = error.response?.status;
    const code = (error.response?.data as { code?: string } | undefined)?.code;
    if (status === 409 || code === 'conflict' || code === 'concurrency-conflict') {
      return new ChatSettingsError('conflict');
    }
  }
  return new ChatSettingsError('generic');
}

/** Persists workspace chat settings through the BFF, then refreshes the cached query (S7). */
export function useUpdateChatSettings() {
  const queryClient = useQueryClient();
  return useMutation<void, ChatSettingsError, ChatSettingsValues>({
    mutationFn: async (values: ChatSettingsValues): Promise<void> => {
      try {
        await apiClient.put('/settings/chat-settings', values);
      } catch (error) {
        throw mapError(error);
      }
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: chatSettingsQueryKey }),
  });
}
