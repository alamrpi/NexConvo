import { type NextRequest, NextResponse } from 'next/server';
import axios from 'axios';
import { serverEnv } from '@/shared/lib/env';
import { forgotPasswordSchema } from '@/features/auth/model/password-reset.schema';

/** Public — always succeeds (no account enumeration); the backend decides whether to send. */
export async function POST(req: NextRequest): Promise<NextResponse> {
  const parsed = forgotPasswordSchema.safeParse(await req.json().catch(() => null));
  if (!parsed.success) {
    return NextResponse.json({ title: 'Invalid input' }, { status: 422 });
  }

  const { API_GATEWAY_URL } = serverEnv();
  try {
    await axios.post(`${API_GATEWAY_URL}/api/v1/auth/forgot-password`, parsed.data);
  } catch {
    // Swallow — never reveal whether the account exists.
  }
  return new NextResponse(null, { status: 204 });
}
