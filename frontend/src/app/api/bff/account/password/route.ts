import axios from 'axios';
import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';
import { changePasswordRequestSchema } from '@/features/account/model/password.schema';

/**
 * Change the signed-in user's password. A wrong *current* password comes back from the
 * gateway as 401; we translate it to `422 {code:'invalidCurrent'}` so the browser client
 * treats it as an inline form error rather than a dead session (which would redirect to
 * /login). A genuinely expired session still surfaces as 401 via `withBff`.
 */
export const PUT = withBff(async (req, { api }) => {
  const parsed = changePasswordRequestSchema.safeParse(await req.json().catch(() => null));
  if (!parsed.success) {
    return NextResponse.json({ title: 'Invalid input' }, { status: 422 });
  }

  try {
    await api.put('/api/v1/account/password', parsed.data);
    return new NextResponse(null, { status: 204 });
  } catch (error) {
    if (axios.isAxiosError(error) && error.response?.status === 401) {
      return NextResponse.json({ code: 'invalidCurrent' }, { status: 422 });
    }
    throw error;
  }
});
