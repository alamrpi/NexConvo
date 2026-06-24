import { z } from 'zod';

/**
 * Password-change validation. Mirrors the backend rule (`ChangePasswordCommand`:
 * current non-empty, new ≥ 8). The zod messages are i18n keys under
 * `settings.password.errors` (S10/S12) — client validation is UX; the server re-validates.
 */

/** The wire payload sent to the BFF — only the two fields the backend accepts. */
export const changePasswordRequestSchema = z.object({
  currentPassword: z.string().min(1, 'currentPasswordRequired'),
  newPassword: z.string().min(8, 'passwordMin').max(200, 'passwordMax'),
});

export type ChangePasswordRequest = z.infer<typeof changePasswordRequestSchema>;

/** The form schema adds a client-only confirm field with a cross-field match check. */
export const passwordChangeSchema = changePasswordRequestSchema
  .extend({
    confirmPassword: z.string().min(1, 'confirmRequired'),
  })
  .superRefine((value, ctx) => {
    if (value.newPassword !== value.confirmPassword) {
      ctx.addIssue({
        path: ['confirmPassword'],
        code: z.ZodIssueCode.custom,
        message: 'passwordMismatch',
      });
    }
  });

export type PasswordChangeValues = z.infer<typeof passwordChangeSchema>;
