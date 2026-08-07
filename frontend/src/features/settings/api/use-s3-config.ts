import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import type { S3ConfigDto } from '../model/s3-config.types';

export const s3ConfigQueryKey = ['settings', 's3-config'] as const;

export function useS3Config() {
  return useQuery({
    queryKey: s3ConfigQueryKey,
    queryFn: async (): Promise<S3ConfigDto | null> => {
      const { data } = await apiClient.get<S3ConfigDto | null>('/settings/s3-config');
      return data;
    },
  });
}
