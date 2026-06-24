'use client';

import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import type { PagedUsers } from '../model/users.types';

export const usersQueryKey = (page: number, pageSize: number) =>
  ['settings', 'users', page, pageSize] as const;

/** Paginated workspace users (S16 — mirrors the backend page cap). Keeps prior page while fetching. */
export function useUsers(page = 1, pageSize = 20) {
  return useQuery({
    queryKey: usersQueryKey(page, pageSize),
    placeholderData: keepPreviousData,
    queryFn: async (): Promise<PagedUsers> =>
      (await apiClient.get<PagedUsers>(`/users?page=${page}&pageSize=${pageSize}`)).data,
  });
}
