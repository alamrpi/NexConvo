import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import type { EmailSettingsValues } from '../model/email-settings.schema';
import { emailSettingsQueryKey } from './use-email-settings';

/** Persists workspace email settings through the BFF, then refreshes the cached query (S7). */
export function useUpdateEmailSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (values: EmailSettingsValues): Promise<void> => {
      await apiClient.put('/settings/email', values);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: emailSettingsQueryKey }),
  });
}
