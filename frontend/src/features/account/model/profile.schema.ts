import { z } from 'zod';

/**
 * Profile-edit validation. The zod messages are i18n keys resolved under
 * `settings.account.errors` (S10/S12) — client validation is UX only; the Account
 * service validates `UpdateProfileCommand` on the server too.
 */
export const profileSchema = z.object({
  fullName: z.string().trim().min(1, 'fullNameRequired').max(200, 'fullNameMax'),
});

export type ProfileValues = z.infer<typeof profileSchema>;
