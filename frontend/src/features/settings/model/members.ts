import { z } from 'zod';

/** Mirrors the Identity `InvitationDto`. */
export interface PendingInvitation {
  id: string;
  email: string;
  roleName: string;
  expiresAt: string;
}

/** Invite form schema, shared with the BFF (S10). Owner is not invitable. */
export const inviteMemberSchema = z.object({
  email: z.string().min(1, 'emailRequired').email('emailInvalid'),
  roleName: z.enum(['Admin', 'Agent']),
});
export type InviteMemberValues = z.infer<typeof inviteMemberSchema>;
