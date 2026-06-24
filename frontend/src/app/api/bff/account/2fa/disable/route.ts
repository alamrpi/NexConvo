import axios from 'axios';
import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';
import { disableTwoFactorSchema } from '@/features/account/model/two-factor.schema';

/**
 * Disable 2FA after a password re-check. A wrong password (gateway 401) is translated to
 * `422 {code:'invalidPassword'}` so it shows inline rather than redirecting to /login.
 */
export const POST = withBff(async (req, { api }) => {
  const parsed = disableTwoFactorSchema.safeParse(await req.json().catch(() => null));
  if (!parsed.success) {
    return NextResponse.json({ title: 'Invalid input' }, { status: 422 });
  }

  try {
    await api.post('/api/v1/account/2fa/disable', parsed.data);
    return new NextResponse(null, { status: 204 });
  } catch (error) {
    if (axios.isAxiosError(error) && error.response?.status === 401) {
      return NextResponse.json({ code: 'invalidPassword' }, { status: 422 });
    }
    throw error;
  }
});
