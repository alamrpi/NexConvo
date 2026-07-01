/**
 * Account/2FA contracts — mirror the Identity service DTOs exactly (frontend standard
 * S2/S19). Sources: `Account/TwoFactor/StartTotpEnrollment.cs` (TotpEnrollmentDto) and
 * `ConfirmTotpEnrollment.cs` (BackupCodesDto). Never widen to `any`.
 */

/** `TotpEnrollmentDto` — POST /account/2fa/start. Secret + provisioning URI, shown once. */
export interface TotpEnrollment {
  secret: string;
  otpAuthUri: string;
}

/** `BackupCodesDto` — POST /account/2fa/confirm. Plaintext recovery codes, returned once. */
export interface BackupCodesResult {
  backupCodes: string[];
}
