import axios from 'axios';

/**
 * Stable, translatable classification of an auth request failure (frontend standard S15).
 * Specific statuses (401 invalid creds, 409 slug taken) are mapped by each hook; this covers
 * the shared "something else went wrong" cases so the user gets an actionable message instead
 * of one generic "failed".
 */
export type AuthFailureCode = 'network' | 'server' | 'generic';

export interface ClassifiedAuthError {
  /** HTTP status if the server responded, else null (no response = network/timeout). */
  status: number | null;
  code: AuthFailureCode;
}

export function classifyAuthError(error: unknown): ClassifiedAuthError {
  if (axios.isAxiosError(error)) {
    if (!error.response) {
      // Request never got a response: connection refused, DNS, timeout, offline.
      return { status: null, code: 'network' };
    }
    const { status } = error.response;
    return { status, code: status >= 500 ? 'server' : 'generic' };
  }
  return { status: null, code: 'generic' };
}