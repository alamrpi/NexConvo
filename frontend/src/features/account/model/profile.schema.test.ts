import { describe, expect, it } from 'vitest';
import { profileSchema } from './profile.schema';

function firstError(result: ReturnType<typeof profileSchema.safeParse>): string | undefined {
  return result.success ? undefined : result.error.issues[0]?.message;
}

describe('profileSchema', () => {
  it('accepts a non-empty name', () => {
    expect(profileSchema.safeParse({ fullName: 'Md. Alam Hossain' }).success).toBe(true);
  });

  it('trims and rejects a blank name with an i18n key', () => {
    expect(firstError(profileSchema.safeParse({ fullName: '   ' }))).toBe('fullNameRequired');
  });

  it('rejects a name over 200 chars', () => {
    expect(firstError(profileSchema.safeParse({ fullName: 'a'.repeat(201) }))).toBe('fullNameMax');
  });
});
