import { type NextRequest, NextResponse } from 'next/server';
import axios from 'axios';
import { serverEnv } from '@/shared/lib/env';
import { correlationHeaders } from '@/shared/api/server/correlation';

/** Public — consumes a reset link token + sets a new password at the gateway. */
export async function POST(req: NextRequest): Promise<NextResponse> {
  const body = (await req.json().catch(() => null)) as { token?: unknown; newPassword?: unknown } | null;
  if (typeof body?.token !== 'string' || typeof body?.newPassword !== 'string' || body.newPassword.length < 8) {
    return NextResponse.json({ title: 'Invalid input' }, { status: 422 });
  }

  const { API_GATEWAY_URL } = serverEnv();
  try {
    await axios.post(
      `${API_GATEWAY_URL}/api/v1/auth/reset-password`,
      { token: body.token, newPassword: body.newPassword },
      { headers: correlationHeaders(req) },
    );
    return new NextResponse(null, { status: 204 });
  } catch (error) {
    const status = axios.isAxiosError(error) ? (error.response?.status ?? 502) : 500;
    return NextResponse.json({ title: 'Reset failed' }, { status });
  }
}
