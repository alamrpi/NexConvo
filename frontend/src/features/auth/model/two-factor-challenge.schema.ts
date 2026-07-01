import { z } from 'zod';

/**
 * Login second-factor validation. Two modes: a 6-digit authenticator code, or a recovery
 * (backup) code. Messages are i18n keys under `auth.twoFactor.errors` (S10/S12); the
 * backend verifies/consumes the value, so the recovery schema only checks it's non-trivial.
 */
export const totpChallengeSchema = z.object({
  code: z
    .string()
    .trim()
    .regex(/^\d{6}$/, 'codeInvalid'),
});

export const backupChallengeSchema = z.object({
  code: z.string().trim().min(8, 'backupCodeInvalid'),
});

export type ChallengeCodeValues = z.infer<typeof totpChallengeSchema>;
