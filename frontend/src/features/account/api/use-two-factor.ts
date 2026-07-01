'use client';

import axios from 'axios';
import { useMutation } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import { classifyAuthError } from '@/features/auth/api/auth-error';
import { useSessionStore } from '@/features/auth/model/session.store';
import type { BackupCodesResult, TotpEnrollment } from '../model/account.types';

/** Begin enrollment: returns the secret + otpauth:// URI to render as a QR. */
export function useStartTwoFactorEnrollment() {
  return useMutation<TotpEnrollment, Error, void>({
    mutationFn: async () => {
      const { data } = await apiClient.post<TotpEnrollment>('/account/2fa/start');
      return data;
    },
  });
}

export type ConfirmTwoFactorErrorCode = 'codeInvalid' | 'network' | 'server' | 'generic';

export class ConfirmTwoFactorError extends Error {
  constructor(readonly code: ConfirmTwoFactorErrorCode) {
    super(code);
    this.name = 'ConfirmTwoFactorError';
  }
}

/**
 * Confirm enrollment with a TOTP code → returns the one-time backup codes and flips the
 * session's `twoFactorEnabled` on. A wrong code arrives as `422 {code:'codeInvalid'}`.
 */
export function useConfirmTwoFactorEnrollment() {
  return useMutation<BackupCodesResult, ConfirmTwoFactorError, { code: string }>({
    mutationFn: async (body) => {
      try {
        const { data } = await apiClient.post<BackupCodesResult>('/account/2fa/confirm', body);
        return data;
      } catch (error) {
        if (axios.isAxiosError(error) && error.response?.status === 422) {
          const code = (error.response.data as { code?: string } | undefined)?.code;
          if (code === 'codeInvalid') throw new ConfirmTwoFactorError('codeInvalid');
        }
        throw new ConfirmTwoFactorError(classifyAuthError(error).code);
      }
    },
    onSuccess: () => {
      const { user, setUser } = useSessionStore.getState();
      if (user) setUser({ ...user, twoFactorEnabled: true });
    },
  });
}

export type DisableTwoFactorErrorCode = 'invalidPassword' | 'network' | 'server' | 'generic';

export class DisableTwoFactorError extends Error {
  constructor(readonly code: DisableTwoFactorErrorCode) {
    super(code);
    this.name = 'DisableTwoFactorError';
  }
}

/** Disable 2FA after a password re-check; flips the session's `twoFactorEnabled` off. */
export function useDisableTwoFactor() {
  return useMutation<void, DisableTwoFactorError, { password: string }>({
    mutationFn: async (body) => {
      try {
        await apiClient.post('/account/2fa/disable', body);
      } catch (error) {
        if (axios.isAxiosError(error) && error.response?.status === 422) {
          const code = (error.response.data as { code?: string } | undefined)?.code;
          if (code === 'invalidPassword') throw new DisableTwoFactorError('invalidPassword');
        }
        throw new DisableTwoFactorError(classifyAuthError(error).code);
      }
    },
    onSuccess: () => {
      const { user, setUser } = useSessionStore.getState();
      if (user) setUser({ ...user, twoFactorEnabled: false });
    },
  });
}
