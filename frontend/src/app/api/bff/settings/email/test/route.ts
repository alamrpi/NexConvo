import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';

/** Triggers a test email send (to the current user) using the workspace's resolved sender. */
export const POST = withBff(async (_req, { api }) => {
  await api.post('/api/v1/settings/email/test');
  return new NextResponse(null, { status: 204 });
});
