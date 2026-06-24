import { type NextRequest, NextResponse } from 'next/server';
import axios from 'axios';
import { serverEnv } from '@/shared/lib/env';
import { writeSession, writeChallenge } from '@/shared/api/server/session';
import { loginSchema } from '@/features/auth/model/login.schema';
import type { AuthTokens, CurrentUser } from '@/features/auth/model/auth.types';

/** The gateway's login union: issued tokens, or a 2FA challenge to complete. */
interface TwoFactorChallenge {
  twoFactorRequired: true;
  challengeToken: string;
}

function isTwoFactorChallenge(data: unknown): data is TwoFactorChallenge {
  return typeof data === 'object' && data !== null && 'twoFactorRequired' in data;
}

/**
 * BFF login (frontend standard S3). Exchanges credentials at the gateway, then either:
 *  - sets the session tokens as HttpOnly cookies and returns the browser-safe user, or
 *  - if 2FA is required, stashes the challenge token in a short-lived HttpOnly cookie and
 *    returns only `{ twoFactorRequired: true }` (no token reaches the client).
 * Tokens are never serialized to the browser.
 */
export async function POST(req: NextRequest): Promise<NextResponse> {
  const parsed = loginSchema.safeParse(await req.json().catch(() => null));
  if (!parsed.success) {
    return NextResponse.json({ title: 'Invalid input' }, { status: 422 });
  }

  const { API_GATEWAY_URL } = serverEnv();

  try {
    const { data } = await axios.post<AuthTokens | TwoFactorChallenge>(
      `${API_GATEWAY_URL}/api/v1/auth/login`,
      parsed.data,
    );

    if (isTwoFactorChallenge(data)) {
      const res = NextResponse.json({ twoFactorRequired: true });
      writeChallenge(res, data.challengeToken);
      return res;
    }

    const { data: user } = await axios.get<CurrentUser>(`${API_GATEWAY_URL}/api/v1/auth/me`, {
      headers: { Authorization: `Bearer ${data.accessToken}` },
    });

    const res = NextResponse.json(user);
    writeSession(res, data);
    return res;
  } catch (error) {
    // Forward the gateway's status (401 invalid creds, 409, etc.) so the form can map it.
    const status = axios.isAxiosError(error) ? (error.response?.status ?? 502) : 500;
    return NextResponse.json({ title: 'Login failed' }, { status });
  }
}
