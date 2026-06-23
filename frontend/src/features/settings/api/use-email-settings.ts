import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import type { EmailSettings } from '../model/email-settings.types';

/** React Query owns this server state (S7). Talks only to the same-origin BFF (S4). */
export const emailSettingsQueryKey = ['settings', 'email'] as const;

export function useEmailSettings() {
  return useQuery({
    queryKey: emailSettingsQueryKey,
    queryFn: async (): Promise<EmailSettings> => {
      const { data } = await apiClient.get<EmailSettings>('/settings/email');
      return data;
    },
  });
}
