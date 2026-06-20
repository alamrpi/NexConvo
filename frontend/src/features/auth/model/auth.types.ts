/**
 * Frontend auth contracts — mirror the Identity service DTOs exactly (frontend
 * standard S2/S19). Source of truth: `NexConvo.Identity.Application/Common/Dtos.cs`
 * and `AuthController.cs`. Keep these in sync; never widen to `any`.
 */

/** `AuthTokensDto` — returned by signup/login/refresh. Tokens never reach the browser. */
export interface AuthTokens {
  accessToken: string;
  expiresInSeconds: number;
  refreshToken: string;
}

/** `CurrentUserDto` — returned by GET /api/v1/auth/me. The browser-safe session shape. */
export interface CurrentUser {
  userId: string;
  tenantId: string;
  tenantSlug: string;
  email: string;
  fullName: string;
  roles: string[];
  permissions: string[];
}
