import { describe, expect, it } from 'vitest';
import { passwordChangeSchema } from './password.schema';

type Field = 'currentPassword' | 'newPassword' | 'confirmPassword';

function errorFor(
  result: ReturnType<typeof passwordChangeSchema.safeParse>,
  field: Field,
): string | undefined {
  if (result.success) return undefined;
  return result.error.issues.find((issue) => issue.path[0] === field)?.message;
}

const valid = {
  currentPassword: 'old-password',
  newPassword: 'brand-new-123',
  confirmPassword: 'brand-new-123',
};

describe('passwordChangeSchema', () => {
  it('accepts a valid change', () => {
    expect(passwordChangeSchema.safeParse(valid).success).toBe(true);
  });

  it('requires the current password', () => {
    expect(errorFor(passwordChangeSchema.safeParse({ ...valid, currentPassword: '' }), 'currentPassword')).toBe(
      'currentPasswordRequired',
    );
  });

  it('enforces an 8-char minimum on the new password', () => {
    expect(errorFor(passwordChangeSchema.safeParse({ ...valid, newPassword: 'short', confirmPassword: 'short' }), 'newPassword')).toBe(
      'passwordMin',
    );
  });

  it('flags a confirm mismatch on the confirm field', () => {
    expect(errorFor(passwordChangeSchema.safeParse({ ...valid, confirmPassword: 'different-123' }), 'confirmPassword')).toBe(
      'passwordMismatch',
    );
  });
});
