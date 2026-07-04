import { z } from 'zod';

/**
 * Single validation source for the channel-connection form, shared with the BFF route handler
 * (frontend standard S10). Error messages are i18n KEYS resolved at render via t(`errors.${key}`),
 * never raw English — otherwise Bengali (and the missing-key fallback) breaks (S12).
 *
 * Cross-field rules live in superRefine so zod evaluates them after all field-level schemas pass,
 * keeping the order of issue objects predictable for the form's setError calls.
 */
export const saveChannelConnectionSchema = z
  .object({
    channel: z.enum(['whatsapp', 'facebook', 'instagram', 'telegram', 'web']),
    displayName: z
      .string()
      .min(1, 'displayNameRequired')
      .max(100, 'displayNameTooLong'),
    externalAccountId: z.string().optional(),
    accessToken: z.string().optional(),
    appSecret: z.string().optional(),
    verifyToken: z.string().optional(),
  })
  .superRefine((data, ctx) => {
    const { channel, accessToken, externalAccountId, appSecret } = data;

    // All non-web channels require a non-empty access token.
    if (channel !== 'web' && !accessToken) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['accessToken'],
        message: 'accessTokenRequired',
      });
    }

    // WhatsApp, Facebook, and Instagram require an external account ID (page/phone number ID).
    if (
      channel === 'whatsapp' ||
      channel === 'facebook' ||
      channel === 'instagram'
    ) {
      if (!externalAccountId) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ['externalAccountId'],
          message: 'externalAccountIdRequired',
        });
      }

      // WhatsApp, Facebook, and Instagram also need the app secret for webhook verification.
      if (!appSecret) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ['appSecret'],
          message: 'appSecretRequired',
        });
      }
    }

    // Telegram uses the bot username as the external account ID.
    if (channel === 'telegram' && !externalAccountId) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['externalAccountId'],
        message: 'externalAccountIdRequired',
      });
    }
  });

export type SaveChannelConnectionValues = z.infer<typeof saveChannelConnectionSchema>;
