import { describe, it, expect } from 'vitest';
import { emailSettingsSchema } from './email-settings.schema';

const validSmtp = {
  provider: 'Smtp' as const,
  fromName: 'Acme',
  fromAddress: 'no-reply@acme.com',
  isEnabled: true,
  smtpHost: 'smtp.acme.com',
  smtpPort: 587,
  smtpUsername: '',
  smtpUseSsl: true,
  secret: '',
};

describe('emailSettingsSchema', () => {
  it('accepts a valid SMTP configuration', () => {
    expect(emailSettingsSchema.safeParse(validSmtp).success).toBe(true);
  });

  it('accepts Resend without SMTP transport fields', () => {
    const resend = { ...validSmtp, provider: 'Resend' as const, smtpHost: undefined, smtpPort: undefined };
    expect(emailSettingsSchema.safeParse(resend).success).toBe(true);
  });

  it('rejects SMTP without a host', () => {
    expect(emailSettingsSchema.safeParse({ ...validSmtp, smtpHost: '' }).success).toBe(false);
  });

  it('rejects an invalid from address', () => {
    expect(emailSettingsSchema.safeParse({ ...validSmtp, fromAddress: 'not-an-email' }).success).toBe(false);
  });

  it('rejects an out-of-range SMTP port', () => {
    expect(emailSettingsSchema.safeParse({ ...validSmtp, smtpPort: 70000 }).success).toBe(false);
  });
});
