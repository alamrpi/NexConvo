import { z } from 'zod';

/**
 * Single validation source for the email-settings form, shared with the BFF route handler
 * (frontend standard S10). Error messages are i18n keys resolved at render. Provider-specific
 * rules are enforced with superRefine so the shape stays flat (friendly to React Hook Form).
 */
export const emailSettingsSchema = z
  .object({
    provider: z.enum(['Smtp', 'Resend']),
    fromName: z.string().min(1, 'fromNameRequired').max(200),
    fromAddress: z.string().min(1, 'fromAddressRequired').email('fromAddressInvalid'),
    isEnabled: z.boolean(),
    smtpHost: z.string().max(255).optional(),
    smtpPort: z.coerce.number().int().min(1, 'smtpPortRange').max(65535, 'smtpPortRange').optional(),
    smtpUsername: z.string().max(255).optional(),
    smtpUseSsl: z.boolean(),
    // Plaintext SMTP password / Resend API key. Empty = keep the existing secret.
    secret: z.string().max(500).optional(),
  })
  .superRefine((value, ctx) => {
    if (value.provider !== 'Smtp') {
      return;
    }
    if (!value.smtpHost) {
      ctx.addIssue({ path: ['smtpHost'], code: z.ZodIssueCode.custom, message: 'smtpHostRequired' });
    }
    if (value.smtpPort == null) {
      ctx.addIssue({ path: ['smtpPort'], code: z.ZodIssueCode.custom, message: 'smtpPortRequired' });
    }
  });

export type EmailSettingsValues = z.infer<typeof emailSettingsSchema>;
