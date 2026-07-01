'use client';

import { useMutation } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import { useSessionStore } from '@/features/auth/model/session.store';
import type { ProfileValues } from '../model/profile.schema';

/**
 * Update the signed-in user's profile (S4). On success we patch the session store's
 * `fullName` so the topbar/user-menu reflect the change immediately — this is client
 * session state we already own (S7), updated on a known mutation, not a server cache.
 */
export function useUpdateProfile() {
  return useMutation<void, Error, ProfileValues>({
    mutationFn: async (values) => {
      await apiClient.put('/account/profile', values);
    },
    onSuccess: (_result, values) => {
      const { user, setUser } = useSessionStore.getState();
      if (user) setUser({ ...user, fullName: values.fullName });
    },
  });
}
