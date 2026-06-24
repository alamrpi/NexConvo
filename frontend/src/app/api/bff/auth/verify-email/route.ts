import { type NextRequest, NextResponse } from 'next/server';
import axios from 'axios';
import { serverEnv } from '@/shared/lib/env';
import { correlationHeaders } from '@/shared/api/server/correlation';

/** Public — consumes an email-verification link token at the gateway. */
export async function POST(req: NextRequest): Promise<NextResponse> {
  const body = (await req.json().catch(() => null)) as { token?: unknown } | null;
  if (typeof body?.token !== 'string' || body.token.length === 0) {
    return NextResponse.json({ title: 'Invalid input' }, { status: 422 });
  }

  const { API_GATEWAY_URL } = serverEnv();
  try {
    await axios.post(`${API_GATEWAY_URL}/api/v1/auth/verify-email`, { token: body.token }, { headers: correlationHeaders(req) });
    return new NextResponse(null, { status: 204 });
  } catch (error) {
    const status = axios.isAxiosError(error) ? (error.response?.status ?? 502) : 500;
    return NextResponse.json({ title: 'Verification failed' }, { status });
  }
}
