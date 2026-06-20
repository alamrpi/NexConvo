'use client';

import axios from 'axios';
import { useMutation } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import { useSessionStore } from '../model/session.store';
import type { SignupValues } from '../model/signup.schema';
import type { CurrentUser } from '../model/auth.types';

/** Discriminated error so the form maps gateway statuses to localized messages (S15). */
export type SignupErrorCode = 'slugTaken' | 'generic';

export class SignupError extends Error {
  constructor(readonly code: SignupErrorCode) {
    super(code);
    this.name = 'SignupError';
  }
}

/**
 * Signup mutation (frontend standards S4, S7). Posts to the same-origin BFF, which
 * provisions the tenant, sets the HttpOnly cookies, and returns the user profile; on
 * success we hydrate the session store. Tokens never touch the browser.
 */
export function useSignup() {
  const setUser = useSessionStore((s) => s.setUser);

  return useMutation<CurrentUser, SignupError, SignupValues>({
    mutationFn: async (values) => {
      try {
        const { data } = await apiClient.post<CurrentUser>('/auth/signup', values);
        return data;
      } catch (error) {
        if (axios.isAxiosError(error) && error.response?.status === 409) {
          throw new SignupError('slugTaken');
        }
        throw new SignupError('generic');
      }
    },
    onSuccess: (user) => setUser(user),
  });
}
