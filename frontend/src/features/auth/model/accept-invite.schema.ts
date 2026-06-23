import { z } from 'zod';

/** Shared by the accept-invite form and its BFF route (S10). Error messages are i18n keys. */
export const acceptInviteSchema = z.object({
  fullName: z.string().min(1, 'fullNameRequired').max(200),
  password: z.string().min(8, 'passwordMin').max(128),
});
export type AcceptInviteValues = z.infer<typeof acceptInviteSchema>;
