import { useMutation } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import type { AiProviderType } from '../model/ai-settings.types';

export interface TestAiConnectionParams {
  provider: AiProviderType;
  apiKey?: string;
  baseUrl?: string;
  model: string;
}

export interface TestAiConnectionResult {
  success: boolean;
  error?: string;
}

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
