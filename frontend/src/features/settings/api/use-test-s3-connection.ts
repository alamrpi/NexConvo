import { useMutation } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import type { S3TestResult } from '../model/s3-config.schema';

export interface TestS3ConnectionParams {
  bucketName: string;
  region: string;
  accessKeyId?: string;
  secretAccessKey?: string;
  customEndpoint?: string;
}

/**
 * Tests connectivity to the configured (or in-progress) S3 bucket without saving it.
 *
 * Blank credential fields are omitted so the server falls back to the already-stored keys
 * (matches the backend contract). Failure is communicated via `success: false` in the response
 * body — the BFF always returns HTTP 200, so this mutation never rejects on a provider-side
 * credential failure (S15); it only rejects on a genuine network or gateway error.
 */
export function useTestS3Connection() {
  return useMutation({
    mutationFn: async (params: TestS3ConnectionParams): Promise<S3TestResult> => {
      const { data } = await apiClient.post<S3TestResult>('/settings/s3-config/test', {
        bucketName: params.bucketName,
        region: params.region,
        accessKeyId: params.accessKeyId || undefined,
        secretAccessKey: params.secretAccessKey || undefined,
        customEndpoint: params.customEndpoint || undefined,
      });
      return data;
    },
  });
}
