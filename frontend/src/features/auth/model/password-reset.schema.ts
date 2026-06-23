import { z } from 'zod';

/** Shared by the forgot-password form and its BFF route (S10). Error messages are i18n keys. */
export const forgotPasswordSchema = z.object({
  tenantSlug: z.string().min(1, 'tenantRequired'),
  email: z.string().min(1, 'emailRequired').email('emailInvalid'),
});
export type ForgotPasswordValues = z.infer<typeof forgotPasswordSchema>;

export const resetPasswordSchema = z
  .object({
    password: z.string().min(8, 'passwordMin').max(128),
    confirmPassword: z.string().min(1, 'confirmRequired'),
  })
  .refine((v) => v.password === v.confirmPassword, {
    path: ['confirmPassword'],
    message: 'passwordMismatch',
  });
export type ResetPasswordValues = z.infer<typeof resetPasswordSchema>;
