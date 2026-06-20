import { describe, expect, it } from 'vitest';
import { signupSchema } from './signup.schema';

function firstError(
  result: ReturnType<typeof signupSchema.safeParse>,
  field: keyof ReturnType<typeof signupSchema.parse>,
): string | undefined {
  if (result.success) return undefined;
  return result.error.issues.find((issue) => issue.path[0] === field)?.message;
}

const valid = {
  tenantName: 'Dhaka Retail Co.',
  tenantSlug: 'dhaka-retail',
  fullName: 'Md. Alam Hossain',
  email: 'alam@dhakaretail.co',
  password: 'sup3rsecret',
};

describe('signupSchema', () => {
  it('accepts a complete valid payload', () => {
    expect(signupSchema.safeParse(valid).success).toBe(true);
  });

  it('flags required fields with error keys', () => {
    const result = signupSchema.safeParse({
      tenantName: '',
      tenantSlug: '',
      fullName: '',
      email: '',
      password: '',
    });
    expect(firstError(result, 'tenantName')).toBe('tenantNameRequired');
    expect(firstError(result, 'fullName')).toBe('fullNameRequired');
    expect(firstError(result, 'email')).toBe('emailRequired');
  });

  it('enforces slug format and length', () => {
    expect(firstError(signupSchema.safeParse({ ...valid, tenantSlug: 'Ab' }), 'tenantSlug')).toBe(
      'tenantMin',
    );
    expect(
      firstError(signupSchema.safeParse({ ...valid, tenantSlug: 'Dhaka Retail!' }), 'tenantSlug'),
    ).toBe('tenantFormat');
  });

  it('rejects a short password', () => {
    expect(firstError(signupSchema.safeParse({ ...valid, password: 'short' }), 'password')).toBe(
      'passwordMin',
    );
  });
});
