import { z } from 'zod';

/**
 * Single validation source for the signup form (frontend standard S10), mirroring the
 * Identity `SignupRequest` contract (S19). Error messages are i18n KEYS (under
 * `signup.errors`), resolved at render time so validation stays locale-aware (S12).
 * The same schema is shared with the BFF route handler.
 */
export const signupSchema = z.object({
  tenantName: z.string().min(1, 'tenantNameRequired').max(200, 'tenantNameMax'),
  tenantSlug: z
    .string()
    .min(1, 'tenantRequired')
    .min(3, 'tenantMin')
    .max(40, 'tenantMax')
    .regex(/^[a-z0-9]+(?:-[a-z0-9]+)*$/, 'tenantFormat'),
  fullName: z.string().min(1, 'fullNameRequired').max(200, 'fullNameMax'),
  email: z.string().min(1, 'emailRequired').email('emailInvalid'),
  password: z.string().min(1, 'passwordRequired').min(8, 'passwordMin').max(128, 'passwordMax'),
});

export type SignupValues = z.infer<typeof signupSchema>;
