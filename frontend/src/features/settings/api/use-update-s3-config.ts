import { useMutation, useQueryClient } from '@tanstack/react-query';
import axios from 'axios';
import { apiClient } from '@/shared/api/client/api-client';
import type { S3ConfigValues } from '../model/s3-config.schema';
import { s3ConfigQueryKey } from './use-s3-config';

export type S3ConfigErrorCode = 'conflict' | 'generic';

export class S3ConfigError extends Error {
  constructor(readonly code: S3ConfigErrorCode) {
    super(code);
    this.name = 'S3ConfigError';
  }
}

function mapError(error: unknown): S3ConfigError {
  if (axios.isAxiosError(error)) {
    const status = error.response?.status;
    const code = (error.response?.data as { code?: string } | undefined)?.code;
    if (status === 409 || code === 'conflict' || code === 'concurrency-conflict') {
      return new S3ConfigError('conflict');
    }
  }
  return new S3ConfigError('generic');
}

export function useUpdateS3Config() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (values: S3ConfigValues): Promise<void> => {
      try {
        await apiClient.put('/settings/s3-config', values);
      } catch (error) {
        throw mapError(error);
      }
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: s3ConfigQueryKey }),
  });
}
