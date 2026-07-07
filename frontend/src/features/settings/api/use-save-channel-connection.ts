import { useMutation, useQueryClient } from '@tanstack/react-query';
import axios from 'axios';
import { apiClient } from '@/shared/api/client/api-client';
import type { SaveChannelConnectionValues } from '../model/channel-connection.schema';
import type { ChannelConnectionDto } from '../model/channel-connection.types';
import { channelConnectionsQueryKey } from './use-channel-connections';

export type ChannelConnectionErrorCode = 'conflict' | 'forbidden' | 'test-failed' | 'generic';

/** Typed mutation error so the form can surface a 409 conflict or 403 forbidden distinctly (S17). */
export class ChannelConnectionError extends Error {
  constructor(readonly code: ChannelConnectionErrorCode) {
    super(code);
    this.name = 'ChannelConnectionError';
  }
}

function mapError(error: unknown): ChannelConnectionError {
  if (axios.isAxiosError(error)) {
    const status = error.response?.status;
    const code = (error.response?.data as { code?: string } | undefined)?.code;
    if (status === 409 || code === 'conflict' || code === 'concurrency-conflict') {
      return new ChannelConnectionError('conflict');
    }
    if (status === 403 || code === 'forbidden') {
      return new ChannelConnectionError('forbidden');
    }
    // Server re-tested credentials on save and they failed — distinct from a zod-validation 422.
    if (status === 422 && code === 'test-failed') {
      return new ChannelConnectionError('test-failed');
    }
  }
  return new ChannelConnectionError('generic');
}

/** Saves a new channel connection through the BFF, then refreshes the cached list (S7). */
export function useSaveChannelConnection() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (values: SaveChannelConnectionValues): Promise<ChannelConnectionDto> => {
      try {
        const { data } = await apiClient.post<ChannelConnectionDto>('/settings/channels', values);
        return data;
      } catch (error) {
        throw mapError(error);
      }
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: channelConnectionsQueryKey }),
  });
}
