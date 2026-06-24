import { z } from 'zod';

/**
 * Custom-role validation. Mirrors the backend rule (`CreateRoleCommandValidator`: name 1–100,
 * ≥1 permission, every key assignable) plus the domain invariant that the `*` wildcard can
 * never be granted to a custom role. Messages are i18n keys under `settings.roles.errors`.
 */
export const roleFormSchema = z.object({
  name: z.string().trim().min(1, 'nameRequired').max(100, 'nameMax'),
  permissions: z
    .array(z.string())
    .min(1, 'permissionsRequired')
    .refine((keys) => !keys.includes('*'), { message: 'noWildcard' }),
});

export type RoleFormValues = z.infer<typeof roleFormSchema>;
