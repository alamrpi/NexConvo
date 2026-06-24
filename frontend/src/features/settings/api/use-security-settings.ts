'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import { useSessionStore } from '@/features/auth/model/session.store';
import type { SecuritySettings } from '../model/security-settings.types';

export const securitySettingsKey = ['settings', 'security'] as const;

/** Workspace security policy (the 2FA requirement). Server state owned by React Query (S7). */
export function useSecuritySettings() {
  return useQuery({
    queryKey: securitySettingsKey,
    queryFn: async (): Promise<SecuritySettings> => (await apiClient.get<SecuritySettings>('/settings/security')).data,
  });
}

export function useUpdateSecuritySettings() {
  const qc = useQueryClient();
  return useMutation<void, Error, SecuritySettings>({
    mutationFn: async (values) => {
      await apiClient.put('/settings/security', values);
    },
    onSuccess: (_r, values) => {
      qc.invalidateQueries({ queryKey: securitySettingsKey });
      // Reflect the policy in the current session so the "enroll now" banner reacts immediately.
      const { user, setUser } = useSessionStore.getState();
      if (user) setUser({ ...user, workspaceRequiresTwoFactor: values.requireTwoFactor });
    },
  });
}
