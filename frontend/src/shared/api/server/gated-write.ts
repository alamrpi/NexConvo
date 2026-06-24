import 'server-only';

import axios from 'axios';

/**
 * Normalizes a gateway error from a sensitive (soft-gated) write into a stable frontend error
 * code the hooks map to localized messages. A 403 is disambiguated by the backend's explicit
 * discriminator: `two-factor-required` → `twoFactorRequired`, `email-not-verified` →
 * `unverified`; a 403 with NO code is a real RBAC permission denial → `forbidden` (never claim
 * "verify your email"). Other statuses fall back to the caller-supplied map, then `generic`.
 */
export function gatedWriteCode(error: unknown, statusCodes: Record<number, string> = {}): string {
  if (!axios.isAxiosError(error) || !error.response) return 'generic';
  const status = error.response.status;
  if (status === 403) {
    const backend = (error.response.data as { code?: string } | undefined)?.code;
    if (backend === 'two-factor-required') return 'twoFactorRequired';
    if (backend === 'email-not-verified') return 'unverified';
    return 'forbidden';
  }
  return statusCodes[status] ?? 'generic';
}
