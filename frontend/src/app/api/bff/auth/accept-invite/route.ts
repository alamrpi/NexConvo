import { type NextRequest, NextResponse } from 'next/server';
import axios from 'axios';
import { serverEnv } from '@/shared/lib/env';
import { writeSession } from '@/shared/api/server/session';
import type { AuthTokens, CurrentUser } from '@/features/auth/model/auth.types';

/**
 * Public — accepts an invitation, which provisions the user and returns tokens. Mirrors login:
 * tokens go to HttpOnly cookies, only the browser-safe profile is returned (S3).
 */
export async function POST(req: NextRequest): Promise<NextResponse> {
  const body = (await req.json().catch(() => null)) as
    | { token?: unknown; fullName?: unknown; password?: unknown }
    | null;
  if (
    typeof body?.token !== 'string' ||
    typeof body?.fullName !== 'string' ||
    typeof body?.password !== 'string'
  ) {
    return NextResponse.json({ title: 'Invalid input' }, { status: 422 });
  }

  const { API_GATEWAY_URL } = serverEnv();
  try {
    const { data: tokens } = await axios.post<AuthTokens>(
      `${API_GATEWAY_URL}/api/v1/invitations/accept`,
      { token: body.token, fullName: body.fullName, password: body.password },
    );
    const { data: user } = await axios.get<CurrentUser>(`${API_GATEWAY_URL}/api/v1/auth/me`, {
      headers: { Authorization: `Bearer ${tokens.accessToken}` },
    });

    const res = NextResponse.json(user);
    writeSession(res, tokens);
    return res;
  } catch (error) {
    const status = axios.isAxiosError(error) ? (error.response?.status ?? 502) : 500;
    return NextResponse.json({ title: 'Could not accept invitation' }, { status });
  }
}
