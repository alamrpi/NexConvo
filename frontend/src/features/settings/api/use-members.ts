import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import type { InviteMemberValues, PendingInvitation } from '../model/members';

const pendingInvitationsKey = ['settings', 'members'] as const;

export function usePendingInvitations() {
  return useQuery({
    queryKey: pendingInvitationsKey,
    queryFn: async (): Promise<PendingInvitation[]> => {
      const { data } = await apiClient.get<PendingInvitation[]>('/settings/members');
      return data;
    },
  });
}

export function useInviteMember() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (values: InviteMemberValues): Promise<void> => {
      await apiClient.post('/settings/members', values);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: pendingInvitationsKey }),
  });
}
