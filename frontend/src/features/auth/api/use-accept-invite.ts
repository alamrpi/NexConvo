import { useMutation } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import { useSessionStore } from '../model/session.store';
import type { CurrentUser } from '../model/auth.types';
import type { AcceptInviteValues } from '../model/accept-invite.schema';

/** Accept an invitation → provisions the account and logs in (hydrates the session store). */
export function useAcceptInvite() {
  const setUser = useSessionStore((s) => s.setUser);
  return useMutation({
    mutationFn: async (vars: AcceptInviteValues & { token: string }): Promise<CurrentUser> => {
      const { data } = await apiClient.post<CurrentUser>('/auth/accept-invite', vars);
      return data;
    },
    onSuccess: (user) => setUser(user),
  });
}
