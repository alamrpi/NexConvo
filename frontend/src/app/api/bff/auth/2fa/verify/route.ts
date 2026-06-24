import { type NextRequest, NextResponse } from 'next/server';
import { z } from 'zod';
import axios from 'axios';
import { serverEnv } from '@/shared/lib/env';
import { CHALLENGE_COOKIE, clearChallenge, writeSession } from '@/shared/api/server/session';
import type { AuthTokens, CurrentUser } from '@/features/auth/model/auth.types';

/** Lenient on the wire — the backend decides TOTP vs. backup code and normalizes it. */
const verifyBodySchema = z.object({ code: z.string().trim().min(1) });

/**
 * Complete the 2FA login challenge (step 2). The challenge token lives only in the
 * HttpOnly `nx_2fa` cookie (set by the login route) — the browser sends just the code. On
 * success we set the real session cookies and clear the challenge. No active session is
 * required yet, so this does not use `withBff`.
 */
export async function POST(req: NextRequest): Promise<NextResponse> {
  const parsed = verifyBodySchema.safeParse(await req.json().catch(() => null));
  if (!parsed.success) {
    return NextResponse.json({ title: 'Invalid input' }, { status: 422 });
  }

  const challengeToken = req.cookies.get(CHALLENGE_COOKIE)?.value;
  if (!challengeToken) {
    // Expired or missing challenge — the client uses this code to restart login.
    return NextResponse.json({ code: 'challengeExpired' }, { status: 401 });
  }

  const { API_GATEWAY_URL } = serverEnv();

  try {
    const { data: tokens } = await axios.post<AuthTokens>(
      `${API_GATEWAY_URL}/api/v1/auth/2fa/verify`,
      { challengeToken, code: parsed.data.code },
    );

    const { data: user } = await axios.get<CurrentUser>(`${API_GATEWAY_URL}/api/v1/auth/me`, {
      headers: { Authorization: `Bearer ${tokens.accessToken}` },
    });

    const res = NextResponse.json(user);
    writeSession(res, tokens);
    clearChallenge(res);
    return res;
  } catch (error) {
    const status = axios.isAxiosError(error) ? (error.response?.status ?? 502) : 500;
    return NextResponse.json({ title: 'Verification failed' }, { status });
  }
}
