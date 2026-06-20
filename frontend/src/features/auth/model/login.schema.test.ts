import { describe, expect, it } from 'vitest';
import { loginSchema } from './login.schema';

/** Pull the first error-key for a field, or undefined when it passed. */
function firstError(
  result: ReturnType<typeof loginSchema.safeParse>,
  field: 'tenantSlug' | 'email' | 'password',
): string | undefined {
  if (result.success) return undefined;
  return result.error.issues.find((issue) => issue.path[0] === field)?.message;
}

const valid = { tenantSlug: 'dhaka-retail', email: 'alam@dhakaretail.co', password: 'sup3rsecret' };

describe('loginSchema', () => {
  it('accepts a valid workspace, email, and password', () => {
    expect(loginSchema.safeParse(valid).success).toBe(true);
  });

  it('flags required fields with error keys', () => {
    const result = loginSchema.safeParse({ tenantSlug: '', email: '', password: '' });
    expect(firstError(result, 'tenantSlug')).toBe('tenantRequired');
    expect(firstError(result, 'email')).toBe('emailRequired');
    expect(firstError(result, 'password')).toBe('passwordRequired');
  });

  it('rejects an invalid workspace slug format', () => {
    const result = loginSchema.safeParse({ ...valid, tenantSlug: 'Dhaka Retail!' });
    expect(firstError(result, 'tenantSlug')).toBe('tenantFormat');
  });

  it('rejects a malformed email', () => {
    const result = loginSchema.safeParse({ ...valid, email: 'not-an-email' });
    expect(firstError(result, 'email')).toBe('emailInvalid');
  });

  it('rejects a short password', () => {
    const result = loginSchema.safeParse({ ...valid, password: 'short' });
    expect(firstError(result, 'password')).toBe('passwordMin');
  });
});
