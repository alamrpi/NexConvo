/**
 * Workspace email-settings contract — mirrors the Identity service `EmailSettingsDto`
 * exactly (frontend standards S2/S19). Source of truth:
 * `NexConvo.Identity.Application/Settings/WorkspaceEmail/GetEmailSettings.cs`.
 * The secret is never sent to the client — only `hasSecret`.
 */
export type EmailProvider = 'Smtp' | 'Resend';

export interface EmailSettings {
  provider: EmailProvider;
  fromName: string;
  fromAddress: string;
  isEnabled: boolean;
  smtpHost: string | null;
  smtpPort: number;
  smtpUsername: string | null;
  smtpUseSsl: boolean;
  hasSecret: boolean;
  lastTestedAt: string | null;
  lastTestSucceeded: boolean | null;
}
