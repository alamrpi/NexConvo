import { type NextRequest, NextResponse } from 'next/server';
import axios from 'axios';
import { serverEnv } from '@/shared/lib/env';
import { writeSession } from '@/shared/api/server/session';
import { signupSchema } from '@/features/auth/model/signup.schema';
import type { AuthTokens, CurrentUser } from '@/features/auth/model/auth.types';

/**
 * BFF signup (frontend standard S3). Creates the tenant + owner at the gateway, sets the
 * issued tokens as HttpOnly cookies, and returns ONLY the browser-safe user profile. A
 * 409 (slug already taken) is forwarded so the form can show a targeted message (S15).
 */
export async function POST(req: NextRequest): Promise<NextResponse> {
  const parsed = signupSchema.safeParse(await req.json().catch(() => null));
  if (!parsed.success) {
    return NextResponse.json({ title: 'Invalid input' }, { status: 422 });
  }

  const { API_GATEWAY_URL } = serverEnv();

  try {
    const { data: tokens } = await axios.post<AuthTokens>(
      `${API_GATEWAY_URL}/api/v1/auth/signup`,
      parsed.data,
    );
    const { data: user } = await axios.get<CurrentUser>(`${API_GATEWAY_URL}/api/v1/auth/me`, {
      headers: { Authorization: `Bearer ${tokens.accessToken}` },
    });

    const res = NextResponse.json(user, { status: 201 });
    writeSession(res, tokens);
    return res;
  } catch (error) {
    const status = axios.isAxiosError(error) ? (error.response?.status ?? 502) : 500;
    return NextResponse.json({ title: 'Signup failed' }, { status });
  }
}
