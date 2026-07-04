import { useMutation } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import type { TestChannelConnectionResponse } from '../model/channel-connection.types';

/**
 * Tests the live connectivity of a saved channel connection.
 *
 * Failure is communicated via `success: false` in the response body — the BFF always returns
 * HTTP 200 so this mutation never rejects on a provider-side credential failure (S15). It only
 * rejects on a genuine network or gateway error (which the caller should surface as a toast).
 */
export function useTestChannelConnection() {
  return useMutation({
    mutationFn: async (id: string): Promise<TestChannelConnectionResponse> => {
      const { data } = await apiClient.post<TestChannelConnectionResponse>(
        `/settings/channels/${id}/test`,
      );
      return data;
    },
  });
}
