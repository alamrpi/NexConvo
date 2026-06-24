import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';

/** Re-send the email-verification link to the signed-in user (authorized, self). */
export const POST = withBff(async (_req, { api }) => {
  await api.post('/api/v1/auth/resend-verification');
  return new NextResponse(null, { status: 204 });
});
