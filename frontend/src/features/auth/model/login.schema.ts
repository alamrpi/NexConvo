import { z } from 'zod';

/**
 * Single validation source for the login form (frontend standard S10).
 * Error messages are i18n KEYS (under `login.errors`), resolved at render time so
 * validation stays locale-aware (S12). When this form is wired to the Identity
 * service later, the same schema is shared with the BFF route handler.
 */
export const loginSchema = z.object({
  tenantSlug: z
    .string()
    .min(1, 'tenantRequired')
    .regex(/^[a-z0-9]+(?:-[a-z0-9]+)*$/, 'tenantFormat'),
  email: z.string().min(1, 'emailRequired').email('emailInvalid'),
  password: z.string().min(1, 'passwordRequired').min(8, 'passwordMin'),
});

export type LoginValues = z.infer<typeof loginSchema>;
