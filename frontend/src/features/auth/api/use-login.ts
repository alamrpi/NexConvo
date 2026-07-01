'use client';

import { useMutation } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import { useSessionStore } from '../model/session.store';
import { classifyAuthError } from './auth-error';
import type { LoginValues } from '../model/login.schema';
import type { CurrentUser } from '../model/auth.types';

/** Discriminated error so the form can map gateway statuses to localized messages (S15). */
export type LoginErrorCode = 'invalidCredentials' | 'network' | 'server' | 'generic';

export class LoginError extends Error {
  constructor(readonly code: LoginErrorCode) {
    super(code);
    this.name = 'LoginError';
  }
}

/** Login can complete, or pause for a second factor — the form branches on this. */
export type LoginOutcome =
  | { kind: 'authenticated'; user: CurrentUser }
  | { kind: 'twoFactorRequired' };

/**
 * Login mutation (frontend standards S4, S7). Posts to the same-origin BFF, which sets
 * the HttpOnly cookies and returns the user profile — OR, when 2FA is enabled, returns
 * `{ twoFactorRequired: true }` (tokens withheld until the code is verified). Tokens never
 * touch the browser.
 */
export function useLogin() {
  const setUser = useSessionStore((s) => s.setUser);

  return useMutation<LoginOutcome, LoginError, LoginValues>({
    mutationFn: async (values) => {
      try {
        const { data } = await apiClient.post<CurrentUser | { twoFactorRequired: true }>(
          '/auth/login',
          values,
        );
        if ('twoFactorRequired' in data) return { kind: 'twoFactorRequired' };
        return { kind: 'authenticated', user: data };
      } catch (error) {
        const { status, code } = classifyAuthError(error);
        throw new LoginError(status === 401 ? 'invalidCredentials' : code);
      }
    },
    onSuccess: (outcome) => {
      if (outcome.kind === 'authenticated') setUser(outcome.user);
    },
  });
}
