import axios from 'axios';
import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';
import { totpCodeSchema } from '@/features/account/model/two-factor.schema';
import type { BackupCodesResult } from '@/features/account/model/account.types';

/**
 * Confirm TOTP enrollment with a 6-digit code → returns the one-time backup codes. A
 * wrong code (gateway 401) is translated to `422 {code:'codeInvalid'}` so it shows inline
 * rather than bouncing the user to /login.
 */
export const POST = withBff(async (req, { api }) => {
  const parsed = totpCodeSchema.safeParse(await req.json().catch(() => null));
  if (!parsed.success) {
    return NextResponse.json({ title: 'Invalid input' }, { status: 422 });
  }

  try {
    const { data } = await api.post<BackupCodesResult>('/api/v1/account/2fa/confirm', parsed.data);
    return NextResponse.json(data);
  } catch (error) {
    if (axios.isAxiosError(error) && error.response?.status === 401) {
      return NextResponse.json({ code: 'codeInvalid' }, { status: 422 });
    }
    throw error;
  }
});
