import { useMutation } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import type { ForgotPasswordValues } from '../model/password-reset.schema';

export function useForgotPassword() {
  return useMutation({
    mutationFn: async (values: ForgotPasswordValues): Promise<void> => {
      await apiClient.post('/auth/forgot-password', values);
    },
  });
}

export function useResetPassword() {
  return useMutation({
    mutationFn: async (vars: { token: string; newPassword: string }): Promise<void> => {
      await apiClient.post('/auth/reset-password', vars);
    },
  });
}

export function useVerifyEmail() {
  return useMutation({
    mutationFn: async (token: string): Promise<void> => {
      await apiClient.post('/auth/verify-email', { token });
    },
  });
}
