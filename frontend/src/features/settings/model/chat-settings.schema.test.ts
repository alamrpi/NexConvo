import { describe, it, expect } from 'vitest';
import { chatSettingsSchema } from './chat-settings.schema';

const valid = {
  primaryProvider: 'OpenAI' as const,
  primaryModel: 'gpt-4o-mini',
  fallbackProviders: [],
  systemPromptOverride: null,
  handoffConfidenceThreshold: 0.65,
  sentimentEscalationEnabled: true,
  sentimentSensitivity: 'medium' as const,
  triggerPhrases: ['speak to a manager', 'human agent'],
  maxUnansweredMessages: 3,
  piiMaskingLevel: 'standard' as const,
  dataRetentionDays: 90,
  widgetIconUrl: null,
  widgetPrimaryColor: '#0F172A',
  widgetSecondaryColor: '#3B82F6',
  widgetWelcomeMessage: 'Hi there! How can I help you today?',
};

describe('chatSettingsSchema', () => {
  it('accepts a fully valid configuration', () => {
    expect(chatSettingsSchema.safeParse(valid).success).toBe(true);
  });

  it('handoffConfidenceThreshold below 0.1 fails', () => {
    expect(
      chatSettingsSchema.safeParse({ ...valid, handoffConfidenceThreshold: 0.09 }).success,
    ).toBe(false);
  });

  it('handoffConfidenceThreshold above 1.0 fails', () => {
    expect(
      chatSettingsSchema.safeParse({ ...valid, handoffConfidenceThreshold: 1.01 }).success,
    ).toBe(false);
  });

  it('handoffConfidenceThreshold 0.65 passes', () => {
    expect(
      chatSettingsSchema.safeParse({ ...valid, handoffConfidenceThreshold: 0.65 }).success,
    ).toBe(true);
  });

  it('handoffConfidenceThreshold exactly 0.1 passes (lower boundary)', () => {
    expect(
      chatSettingsSchema.safeParse({ ...valid, handoffConfidenceThreshold: 0.1 }).success,
    ).toBe(true);
  });

  it('handoffConfidenceThreshold exactly 1.0 passes (upper boundary)', () => {
    expect(
      chatSettingsSchema.safeParse({ ...valid, handoffConfidenceThreshold: 1.0 }).success,
    ).toBe(true);
  });

  it('triggerPhrases with 51 items fails', () => {
    const phrases = Array.from({ length: 51 }, (_, i) => `phrase-${i}`);
    expect(
      chatSettingsSchema.safeParse({ ...valid, triggerPhrases: phrases }).success,
    ).toBe(false);
  });

  it('triggerPhrases with 50 items passes', () => {
    const phrases = Array.from({ length: 50 }, (_, i) => `phrase-${i}`);
    expect(
      chatSettingsSchema.safeParse({ ...valid, triggerPhrases: phrases }).success,
    ).toBe(true);
  });

  it('dataRetentionDays null passes (unlimited retention)', () => {
    expect(
      chatSettingsSchema.safeParse({ ...valid, dataRetentionDays: null }).success,
    ).toBe(true);
  });

  it('dataRetentionDays 6 fails (below minimum of 7)', () => {
    expect(
      chatSettingsSchema.safeParse({ ...valid, dataRetentionDays: 6 }).success,
    ).toBe(false);
  });

  it('dataRetentionDays 7 passes (minimum boundary)', () => {
    expect(
      chatSettingsSchema.safeParse({ ...valid, dataRetentionDays: 7 }).success,
    ).toBe(true);
  });

  it('emits i18n keys as error messages', () => {
    const result = chatSettingsSchema.safeParse({
      ...valid,
      handoffConfidenceThreshold: 0.0,
      triggerPhrases: Array.from({ length: 51 }, (_, i) => `p${i}`),
      dataRetentionDays: 3,
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const messages = result.error.issues.map((i) => i.message);
      expect(messages).toContain('thresholdTooLow');
      expect(messages).toContain('tooManyPhrases');
      expect(messages).toContain('minSevenDays');
    }
  });

  // ── Widget fields (audit H1: previously untested) ──────────────────────────
  it.each(['#0F172A', '#3B82F6', '#FFF', '#11223344'])('accepts hex color %s', (color) => {
    expect(chatSettingsSchema.safeParse({ ...valid, widgetPrimaryColor: color }).success).toBe(true);
  });

  it.each(['0F172A', '#12345', 'red', '#GGGGGG', ''])('rejects invalid color %s', (color) => {
    const result = chatSettingsSchema.safeParse({ ...valid, widgetPrimaryColor: color });
    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues.map((i) => i.message)).toContain('invalidColor');
    }
  });

  it('validates the secondary color too', () => {
    expect(chatSettingsSchema.safeParse({ ...valid, widgetSecondaryColor: 'nope' }).success).toBe(false);
  });

  it.each(['https://example.com/i.png', null])('accepts icon url %s', (url) => {
    expect(chatSettingsSchema.safeParse({ ...valid, widgetIconUrl: url }).success).toBe(true);
  });

  it.each(['http://example.com/i.png', 'javascript:alert(1)', 'data:image/png;base64,AA', 'nope'])(
    'rejects non-https / malformed icon url %s',
    (url) => {
      const result = chatSettingsSchema.safeParse({ ...valid, widgetIconUrl: url });
      expect(result.success).toBe(false);
      if (!result.success) {
        expect(result.error.issues.map((i) => i.message)).toContain('invalidUrl');
      }
    },
  );

  it('rejects an empty welcome message', () => {
    const result = chatSettingsSchema.safeParse({ ...valid, widgetWelcomeMessage: '' });
    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues.map((i) => i.message)).toContain('welcomeMessageRequired');
    }
  });

  it('rejects a welcome message over 500 chars', () => {
    const result = chatSettingsSchema.safeParse({ ...valid, widgetWelcomeMessage: 'a'.repeat(501) });
    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues.map((i) => i.message)).toContain('welcomeMessageTooLong');
    }
  });
});
