import { describe, expect, it } from 'vitest';
import { backupCodeSchema, disableTwoFactorSchema, totpCodeSchema } from './two-factor.schema';

describe('totpCodeSchema', () => {
  it('accepts exactly six digits', () => {
    expect(totpCodeSchema.safeParse({ code: '123456' }).success).toBe(true);
  });

  it.each(['12345', '1234567', '12345a', ''])('rejects %j with codeInvalid', (code) => {
    const result = totpCodeSchema.safeParse({ code });
    expect(result.success).toBe(false);
    if (!result.success) expect(result.error.issues[0]?.message).toBe('codeInvalid');
  });
});

describe('backupCodeSchema', () => {
  it('accepts a dash-grouped recovery code', () => {
    expect(backupCodeSchema.safeParse({ code: 'AB3CD-EF4GH' }).success).toBe(true);
  });

  it('rejects an obviously too-short value', () => {
    const result = backupCodeSchema.safeParse({ code: 'abc' });
    expect(result.success).toBe(false);
    if (!result.success) expect(result.error.issues[0]?.message).toBe('backupCodeInvalid');
  });
});

describe('disableTwoFactorSchema', () => {
  it('requires a password', () => {
    const result = disableTwoFactorSchema.safeParse({ password: '' });
    expect(result.success).toBe(false);
    if (!result.success) expect(result.error.issues[0]?.message).toBe('passwordRequired');
  });
});
