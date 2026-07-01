import { NextResponse } from 'next/server';
import axios from 'axios';
import { serverEnv } from '@/shared/lib/env';
import { readSession, clearSession } from '@/shared/api/server/session';

/**
 * BFF logout. Best-effort revokes the refresh token at the gateway, then ALWAYS clears
 * the session cookies and returns 204 — the user is logged out client-side regardless
 * of whether the gateway revoke succeeds (frontend standard S15: resilient).
 */
export async function POST(): Promise<NextResponse> {
  const session = await readSession();

  if (session) {
    try {
      const { API_GATEWAY_URL } = serverEnv();
      await axios.post(
        `${API_GATEWAY_URL}/api/v1/auth/logout`,
        { refreshToken: session.refreshToken },
        { headers: { Authorization: `Bearer ${session.accessToken}` } },
      );
    } catch {
      // Ignore — revoke is best-effort; the token expires server-side anyway.
    }
  }

  const res = new NextResponse(null, { status: 204 });
  clearSession(res);
  return res;
}
