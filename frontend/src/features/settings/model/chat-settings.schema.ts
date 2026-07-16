import { z } from 'zod';
import { aiProviderTypeSchema } from './ai-settings.schema';

/**
 * Enum schemas for sentiment sensitivity and PII masking level.
 * Error messages are i18n KEYS resolved at render via t(`errors.${key}`),
 * never raw English — so Bengali and the missing-key fallback work correctly (S12).
 */
export const sentimentSensitivitySchema = z.enum(['low', 'medium', 'high']);
export const piiMaskingLevelSchema = z.enum(['off', 'standard', 'strict']);

/**
 * Single validation source for the chat-settings form, shared with the BFF route handler
 * (frontend standard S10). The backend enforces these constraints too — client validation
 * is UX, never the security boundary.
 */
export const chatSettingsSchema = z.object({
  primaryProvider: aiProviderTypeSchema,
  primaryModel: z.string().min(1, 'primaryModelRequired'),
  fallbackProviders: z.array(aiProviderTypeSchema).max(4, 'tooManyFallbacks'),
  systemPromptOverride: z.string().nullable().optional(),
  handoffConfidenceThreshold: z
    .number()
    .min(0.1, 'thresholdTooLow')
    .max(1.0, 'thresholdTooHigh'),
  sentimentEscalationEnabled: z.boolean(),
  sentimentSensitivity: sentimentSensitivitySchema,
  triggerPhrases: z
    .array(z.string().max(200, 'phraseTooLong'))
    .max(50, 'tooManyPhrases'),
  maxUnansweredMessages: z.number().int().min(1, 'minOneMessage').max(20, 'maxTwentyMessages'),
  piiMaskingLevel: piiMaskingLevelSchema,
  dataRetentionDays: z.number().int().min(7, 'minSevenDays').nullable().optional(),
  // Widget config is rendered on public sites — mirror the backend validator exactly (S10/S19):
  // #RGB/#RRGGBB/#RRGGBBAA hex colors, and an https-only icon URL (blocks javascript:/data:/http:).
  widgetIconUrl: z
    .string()
    .refine((v) => !v || /^https:\/\/.+/i.test(v), 'invalidUrl')
    .nullable()
    .optional(),
  widgetPrimaryColor: z.string().regex(/^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$/, 'invalidColor'),
  widgetSecondaryColor: z.string().regex(/^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$/, 'invalidColor'),
  widgetWelcomeMessage: z.string().min(1, 'welcomeMessageRequired').max(500, 'welcomeMessageTooLong'),
  noAnswerMessage: z.string().min(1, 'noAnswerRequired').max(500, 'noAnswerTooLong'),
});

export type ChatSettingsValues = z.infer<typeof chatSettingsSchema>;
