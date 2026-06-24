'use client';

import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import type { PermissionItem, Role } from '../model/roles.types';

/** React Query owns this server state (S7); talks only to the same-origin BFF (S4). */
export const rolesQueryKey = ['settings', 'roles'] as const;
export const permissionCatalogQueryKey = ['settings', 'roles', 'catalog'] as const;

export function useRoles() {
  return useQuery({
    queryKey: rolesQueryKey,
    queryFn: async (): Promise<Role[]> => (await apiClient.get<Role[]>('/roles')).data,
  });
}

export function usePermissionCatalog() {
  return useQuery({
    queryKey: permissionCatalogQueryKey,
    // The catalog is effectively static for a release — cache it for the session.
    staleTime: Infinity,
    queryFn: async (): Promise<PermissionItem[]> => (await apiClient.get<PermissionItem[]>('/roles/permissions')).data,
  });
}
