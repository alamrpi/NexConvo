'use client';

import { useMutation } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';

/** Re-send the email-verification link (S4). Used by the dashboard verification banner. */
export function useResendVerification() {
  return useMutation<void, Error, void>({
    mutationFn: async () => {
      await apiClient.post('/auth/resend-verification');
    },
  });
}
