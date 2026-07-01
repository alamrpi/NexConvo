import { z } from 'zod';

/** Mirrors the backend `AiProviderType` enum (used for the read DTO type). */
export const aiProviderTypeSchema = z.enum([
  'OpenAI',
  'Anthropic',
  'Gemini',
  'OpenRouter',
  'DeepSeek',
]);

/**
 * Providers with a working backend implementation — the only ones the form lets you configure.
 * Kept in sync with the backend SaveAiConfigCommandValidator (which rejects the rest with 422).
 */
export const supportedAiProviderSchema = z.enum(['OpenAI', 'Anthropic', 'Gemini', 'OpenRouter', 'DeepSeek']);

/** Empty string or a JSON object — the backend persists this as jsonb. */
function isEmptyOrJsonObject(value: string | undefined): boolean {
  if (!value) return true;
  try {
    const parsed = JSON.parse(value);
    return typeof parsed === 'object' && parsed !== null && !Array.isArray(parsed);
  } catch {
    return false;
  }
}

/**
 * Single validation source for the AI-settings form, shared with the BFF route handler
 * (frontend standard S10). Error messages are i18n KEYS resolved at render via t(`errors.${key}`),
 * never raw English — otherwise Bengali (and the missing-key fallback) breaks (S12).
 */
export const aiSettingsSchema = z.object({
  provider: supportedAiProviderSchema,
  apiKey: z.string().optional(),
  baseUrl: z
    .string()
    .optional()
    .refine((v) => !v || z.string().url().safeParse(v).success, { message: 'baseUrlInvalid' }),
  defaultModel: z.string().min(1, 'defaultModelRequired'),
  parameters: z.string().optional().refine(isEmptyOrJsonObject, { message: 'parametersInvalid' }),
  isActive: z.boolean().default(true),
});

export type AiSettingsValues = z.infer<typeof aiSettingsSchema>;
