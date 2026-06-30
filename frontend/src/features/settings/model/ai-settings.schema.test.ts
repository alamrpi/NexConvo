import { describe, it, expect } from 'vitest';
import { aiSettingsSchema } from './ai-settings.schema';

const valid = {
  provider: 'OpenAI' as const,
  apiKey: 'sk-test',
  baseUrl: '',
  defaultModel: 'gpt-4o',
  parameters: '',
  isActive: true,
};

describe('aiSettingsSchema', () => {
  it('accepts a valid configuration', () => {
    expect(aiSettingsSchema.safeParse(valid).success).toBe(true);
  });

  it('accepts an empty apiKey (keep existing key on update)', () => {
    expect(aiSettingsSchema.safeParse({ ...valid, apiKey: '' }).success).toBe(true);
  });

  it('accepts all five supported providers', () => {
    for (const provider of ['OpenAI', 'Anthropic', 'Gemini', 'OpenRouter', 'DeepSeek'] as const) {
      expect(aiSettingsSchema.safeParse({ ...valid, provider }).success).toBe(true);
    }
  });

  it('rejects an unknown provider string', () => {
    expect(aiSettingsSchema.safeParse({ ...valid, provider: 'Cohere' }).success).toBe(false);
  });

  it('accepts an empty base URL but rejects a malformed one', () => {
    expect(aiSettingsSchema.safeParse({ ...valid, baseUrl: '' }).success).toBe(true);
    expect(aiSettingsSchema.safeParse({ ...valid, baseUrl: 'https://api.openai.com/v1' }).success).toBe(true);
    expect(aiSettingsSchema.safeParse({ ...valid, baseUrl: 'not-a-url' }).success).toBe(false);
  });

  it('accepts a JSON object for parameters but rejects other shapes', () => {
    expect(aiSettingsSchema.safeParse({ ...valid, parameters: '{"temperature":0.7}' }).success).toBe(true);
    expect(aiSettingsSchema.safeParse({ ...valid, parameters: '[1,2,3]' }).success).toBe(false);
    expect(aiSettingsSchema.safeParse({ ...valid, parameters: 'not json' }).success).toBe(false);
  });

  it('emits i18n keys (not English sentences) as error messages', () => {
    const result = aiSettingsSchema.safeParse({
      ...valid,
      defaultModel: '',
      baseUrl: 'not-a-url',
      parameters: 'nope',
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      const messages = result.error.issues.map((i) => i.message);
      expect(messages).toContain('defaultModelRequired');
      expect(messages).toContain('baseUrlInvalid');
      expect(messages).toContain('parametersInvalid');
    }
  });
});
