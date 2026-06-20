'use client';

import { useMutation } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import { useSessionStore } from '../model/session.store';

/**
 * Logout mutation. The BFF revokes the refresh token and clears the cookies; we then
 * clear the session store and hard-navigate to /login so no stale authed UI lingers.
 */
export function useLogout() {
  const setUser = useSessionStore((s) => s.setUser);

  return useMutation<void, Error, void>({
    mutationFn: async () => {
      await apiClient.post('/auth/logout');
    },
    onSettled: () => {
      // Clear locally and leave the authed area regardless of the network outcome.
      setUser(null);
      if (typeof window !== 'undefined') {
        window.location.assign('/login');
      }
    },
  });
}
