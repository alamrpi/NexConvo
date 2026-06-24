import { describe, expect, it } from 'vitest';
import { roleFormSchema } from './role.schema';

function firstError(result: ReturnType<typeof roleFormSchema.safeParse>): string | undefined {
  return result.success ? undefined : result.error.issues[0]?.message;
}

describe('roleFormSchema', () => {
  it('accepts a named role with at least one permission', () => {
    expect(roleFormSchema.safeParse({ name: 'Support', permissions: ['leads:read'] }).success).toBe(true);
  });

  it('requires a name', () => {
    expect(firstError(roleFormSchema.safeParse({ name: '  ', permissions: ['leads:read'] }))).toBe('nameRequired');
  });

  it('requires at least one permission', () => {
    expect(firstError(roleFormSchema.safeParse({ name: 'Support', permissions: [] }))).toBe('permissionsRequired');
  });

  it('rejects the wildcard permission on a custom role', () => {
    const r = roleFormSchema.safeParse({ name: 'Support', permissions: ['*'] });
    expect(r.success).toBe(false);
    if (!r.success) expect(r.error.issues.some((i) => i.message === 'noWildcard')).toBe(true);
  });
});
