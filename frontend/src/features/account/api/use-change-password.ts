'use client';

import axios from 'axios';
import { useMutation } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import { classifyAuthError } from '@/features/auth/api/auth-error';
import type { ChangePasswordRequest } from '../model/password.schema';

/** Discriminated error so the form maps the wrong-current-password case to a localized message (S15). */
export type PasswordChangeErrorCode = 'invalidCurrent' | 'network' | 'server' | 'generic';

export class PasswordChangeError extends Error {
  constructor(readonly code: PasswordChangeErrorCode) {
    super(code);
    this.name = 'PasswordChangeError';
  }
}

/**
 * Change-password mutation (S4). The BFF reports a wrong current password as `422 {code:
 * 'invalidCurrent'}` (NOT 401) so the browser client doesn't treat it as a dead session
 * and bounce to /login. A successful change rotates the user's *other* sessions on the
 * server; this session keeps working.
 */
export function useChangePassword() {
  return useMutation<void, PasswordChangeError, ChangePasswordRequest>({
    mutationFn: async (values) => {
      try {
        await apiClient.put('/account/password', values);
      } catch (error) {
        if (axios.isAxiosError(error) && error.response?.status === 422) {
          const code = (error.response.data as { code?: string } | undefined)?.code;
          if (code === 'invalidCurrent') throw new PasswordChangeError('invalidCurrent');
        }
        throw new PasswordChangeError(classifyAuthError(error).code);
      }
    },
  });
}
