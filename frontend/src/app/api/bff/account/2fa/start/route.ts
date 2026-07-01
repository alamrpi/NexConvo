import axios from 'axios';
import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';
import type { TotpEnrollment } from '@/features/account/model/account.types';

/**
 * Begin TOTP enrollment — returns the secret + otpauth:// URI (shown once). The secret
 * is enrollment-scoped (2FA isn't active until confirm), so it may cross to the client to
 * render the QR. A 409 (already enabled) is forwarded as-is for the UI to handle.
 */
export const POST = withBff(async (_req, { api }) => {
  try {
    const { data } = await api.post<TotpEnrollment>('/api/v1/account/2fa/start');
    return NextResponse.json(data);
  } catch (error) {
    if (axios.isAxiosError(error) && error.response) {
      return NextResponse.json({ title: 'Could not start 2FA' }, { status: error.response.status });
    }
    throw error;
  }
});
