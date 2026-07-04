import { describe, it, expect } from 'vitest';
import { saveChannelConnectionSchema } from './channel-connection.schema';

const validWhatsApp = {
  channel: 'whatsapp' as const,
  displayName: 'Main WhatsApp',
  accessToken: 'EAAtoken',
  externalAccountId: '1234567890',
  appSecret: 'secret123',
};

describe('saveChannelConnectionSchema', () => {
  it('valid whatsapp data passes validation', () => {
    expect(saveChannelConnectionSchema.safeParse(validWhatsApp).success).toBe(true);
  });

  it('missing accessToken fails validation', () => {
    expect(
      saveChannelConnectionSchema.safeParse({ ...validWhatsApp, accessToken: '' }).success,
    ).toBe(false);
  });

  it('web channel does not require externalAccountId', () => {
    const web = {
      channel: 'web' as const,
      displayName: 'Website Widget',
      accessToken: 'tok-web',
    };
    expect(saveChannelConnectionSchema.safeParse(web).success).toBe(true);
  });

  it('whatsapp requires externalAccountId', () => {
    expect(
      saveChannelConnectionSchema.safeParse({
        ...validWhatsApp,
        externalAccountId: undefined,
      }).success,
    ).toBe(false);
  });

  it('whatsapp requires appSecret', () => {
    expect(
      saveChannelConnectionSchema.safeParse({
        ...validWhatsApp,
        appSecret: undefined,
      }).success,
    ).toBe(false);
  });

  it('facebook requires appSecret', () => {
    const fb = {
      channel: 'facebook' as const,
      displayName: 'Brand Page',
      accessToken: 'EAAtoken',
      externalAccountId: '9876543210',
      appSecret: undefined as string | undefined,
    };
    expect(saveChannelConnectionSchema.safeParse(fb).success).toBe(false);
  });

  it('telegram does not require appSecret', () => {
    const tg = {
      channel: 'telegram' as const,
      displayName: 'Bot Channel',
      accessToken: '123456:ABC-DEF',
      externalAccountId: '@mycompanybot',
    };
    expect(saveChannelConnectionSchema.safeParse(tg).success).toBe(true);
  });

  it('displayName is required', () => {
    expect(
      saveChannelConnectionSchema.safeParse({ ...validWhatsApp, displayName: '' }).success,
    ).toBe(false);
  });

  it('displayName max 100 chars enforced', () => {
    const longName = 'a'.repeat(101);
    expect(
      saveChannelConnectionSchema.safeParse({ ...validWhatsApp, displayName: longName }).success,
    ).toBe(false);
  });

  it('displayName of exactly 100 chars passes', () => {
    const maxName = 'a'.repeat(100);
    expect(
      saveChannelConnectionSchema.safeParse({ ...validWhatsApp, displayName: maxName }).success,
    ).toBe(true);
  });
});
