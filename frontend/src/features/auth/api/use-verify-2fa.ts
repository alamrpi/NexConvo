'use client';

import axios from 'axios';
import { useMutation } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import { useSessionStore } from '../model/session.store';
import { classifyAuthError } from './auth-error';
import type { CurrentUser } from '../model/auth.types';

export type VerifyTwoFactorErrorCode =
  | 'invalidCode'
  | 'challengeExpired'
  | 'network'
  | 'server'
  | 'generic';

export class VerifyTwoFactorError extends Error {
  constructor(readonly code: VerifyTwoFactorErrorCode) {
    super(code);
    this.name = 'VerifyTwoFactorError';
  }
}

/**
 * Complete the 2FA login challenge (S4/S7). Sends only the code; the challenge token lives
 * in an HttpOnly cookie read by the BFF. On success the session cookies are set and the
 * user is hydrated. A missing/expired challenge comes back as `401 {code:'challengeExpired'}`
 * so the form can restart login; any other 401 is a wrong code.
 */
export function useVerifyTwoFactor() {
  const setUser = useSessionStore((s) => s.setUser);

  return useMutation<CurrentUser, VerifyTwoFactorError, { code: string }>({
    mutationFn: async (body) => {
      try {
        const { data } = await apiClient.post<CurrentUser>('/auth/2fa/verify', body);
        return data;
      } catch (error) {
        if (axios.isAxiosError(error) && error.response?.status === 401) {
          const code = (error.response.data as { code?: string } | undefined)?.code;
          throw new VerifyTwoFactorError(code === 'challengeExpired' ? 'challengeExpired' : 'invalidCode');
        }
        throw new VerifyTwoFactorError(classifyAuthError(error).code);
      }
    },
    onSuccess: (user) => setUser(user),
  });
}
