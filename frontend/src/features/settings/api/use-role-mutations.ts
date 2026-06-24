'use client';

import axios from 'axios';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import { rolesQueryKey } from './use-roles';
import type { RoleFormValues } from '../model/role.schema';

export type RoleMutationErrorCode =
  | 'nameTaken'
  | 'unverified'
  | 'twoFactorRequired'
  | 'forbidden'
  | 'membersAssigned'
  | 'generic';

export class RoleMutationError extends Error {
  constructor(readonly code: RoleMutationErrorCode) {
    super(code);
    this.name = 'RoleMutationError';
  }
}

/** Map the BFF's `{code}` business errors (409/403) to a typed, localizable code. */
function mapError(error: unknown): RoleMutationError {
  if (axios.isAxiosError(error)) {
    const code = (error.response?.data as { code?: string } | undefined)?.code;
    if (
      code === 'nameTaken' ||
      code === 'unverified' ||
      code === 'twoFactorRequired' ||
      code === 'forbidden' ||
      code === 'membersAssigned'
    ) {
      return new RoleMutationError(code);
    }
  }
  return new RoleMutationError('generic');
}

export function useCreateRole() {
  const qc = useQueryClient();
  return useMutation<void, RoleMutationError, RoleFormValues>({
    mutationFn: async (values) => {
      try {
        await apiClient.post('/roles', values);
      } catch (error) {
        throw mapError(error);
      }
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: rolesQueryKey }),
  });
}

export function useUpdateRole() {
  const qc = useQueryClient();
  return useMutation<void, RoleMutationError, { id: string } & RoleFormValues>({
    mutationFn: async ({ id, ...values }) => {
      try {
        await apiClient.put(`/roles/${id}`, values);
      } catch (error) {
        throw mapError(error);
      }
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: rolesQueryKey }),
  });
}

export function useDeleteRole() {
  const qc = useQueryClient();
  return useMutation<void, RoleMutationError, string>({
    mutationFn: async (id) => {
      try {
        await apiClient.delete(`/roles/${id}`);
      } catch (error) {
        throw mapError(error);
      }
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: rolesQueryKey }),
  });
}
