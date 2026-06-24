'use client';

import axios from 'axios';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';

export type UserMutationErrorCode = 'conflict' | 'unverified' | 'twoFactorRequired' | 'forbidden' | 'generic';

export class UserMutationError extends Error {
  constructor(readonly code: UserMutationErrorCode) {
    super(code);
    this.name = 'UserMutationError';
  }
}

/** The BFF normalizes 409 (last-Owner/self) and 403 (unverified | 2FA-required) to a `code`. */
function mapError(error: unknown): UserMutationError {
  if (axios.isAxiosError(error)) {
    const code = (error.response?.data as { code?: string } | undefined)?.code;
    if (code === 'conflict' || code === 'unverified' || code === 'twoFactorRequired' || code === 'forbidden') {
      return new UserMutationError(code);
    }
  }
  return new UserMutationError('generic');
}

function useUsersInvalidator() {
  const qc = useQueryClient();
  return () => qc.invalidateQueries({ queryKey: ['settings', 'users'] });
}

export function useChangeUserRole() {
  const invalidate = useUsersInvalidator();
  return useMutation<void, UserMutationError, { id: string; roleId: string }>({
    mutationFn: async ({ id, roleId }) => {
      try {
        await apiClient.put(`/users/${id}/role`, { roleId });
      } catch (error) {
        throw mapError(error);
      }
    },
    onSuccess: invalidate,
  });
}

export function useDeactivateUser() {
  const invalidate = useUsersInvalidator();
  return useMutation<void, UserMutationError, string>({
    mutationFn: async (id) => {
      try {
        await apiClient.post(`/users/${id}/deactivate`);
      } catch (error) {
        throw mapError(error);
      }
    },
    onSuccess: invalidate,
  });
}

export function useReactivateUser() {
  const invalidate = useUsersInvalidator();
  return useMutation<void, UserMutationError, string>({
    mutationFn: async (id) => {
      try {
        await apiClient.post(`/users/${id}/reactivate`);
      } catch (error) {
        throw mapError(error);
      }
    },
    onSuccess: invalidate,
  });
}
