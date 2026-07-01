import { z } from 'zod';

/**
 * Two-factor validation. Messages are i18n keys under `settings.security.errors` and
 * `auth.twoFactor.errors` (S10/S12). Client validation is UX; the backend verifies the
 * TOTP code / password and consumes backup codes server-side.
 */

/** A 6-digit TOTP code (enrollment confirm + login challenge). */
export const totpCodeSchema = z.object({
  code: z
    .string()
    .trim()
    .regex(/^\d{6}$/, 'codeInvalid'),
});

export type TotpCodeValues = z.infer<typeof totpCodeSchema>;

/**
 * A recovery (backup) code at the login challenge. Lenient on shape — the backend
 * normalizes dashes/case — so we only require something non-trivial was entered.
 */
export const backupCodeSchema = z.object({
  code: z.string().trim().min(8, 'backupCodeInvalid'),
});

export type BackupCodeValues = z.infer<typeof backupCodeSchema>;

/** Disabling 2FA requires re-entering the account password. */
export const disableTwoFactorSchema = z.object({
  password: z.string().min(1, 'passwordRequired'),
});

export type DisableTwoFactorValues = z.infer<typeof disableTwoFactorSchema>;
