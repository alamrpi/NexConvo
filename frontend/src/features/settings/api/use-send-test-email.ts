import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import { emailSettingsQueryKey } from './use-email-settings';

/** Sends a test email via the workspace's resolved sender; refreshes settings to show the result. */
export function useSendTestEmail() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (): Promise<void> => {
      await apiClient.post('/settings/email/test');
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: emailSettingsQueryKey }),
  });
}
