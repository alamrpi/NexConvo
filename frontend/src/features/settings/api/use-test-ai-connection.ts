import { useMutation } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import type { AiProviderType } from '../model/ai-settings.types';
import type { ConnectionHealthResult } from '../model/connection-health.schema';

export interface TestAiConnectionParams {
  provider: AiProviderType;
  apiKey?: string;
  baseUrl?: string;
  model: string;
}

export type TestAiConnectionResult = ConnectionHealthResult;

/**
 * Tests connectivity to the given AI provider without saving. Failure is communicated via
 * `success: false` in the response body — the BFF always returns HTTP 200, so this mutation
 * never rejects on a provider-side credential failure (S15); it only rejects on a genuine
 * network or gateway error. Mirrors `useTestS3Connection`.
 */
export function useTestAiConnection() {
  return useMutation({
    mutationFn: async (params: TestAiConnectionParams): Promise<TestAiConnectionResult> => {
      const { data } = await apiClient.post<TestAiConnectionResult>(
        '/settings/ai-config/test',
        params,
      );
      return data;
    },
  });
}
