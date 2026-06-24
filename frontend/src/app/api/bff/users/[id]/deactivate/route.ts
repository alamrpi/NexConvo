import axios from 'axios';
import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';
import { gatedWriteCode } from '@/shared/api/server/gated-write';

/** Disable a user. Forwards 409 (self / last-Owner) / 403 (unverified | 2FA-required). */
export const POST = withBff(async (_req, { api }, routeCtx) => {
  const { id } = await routeCtx.params;
  try {
    await api.post(`/api/v1/users/${id}/deactivate`);
    return new NextResponse(null, { status: 204 });
  } catch (error) {
    if (axios.isAxiosError(error) && error.response) {
      return NextResponse.json({ code: gatedWriteCode(error, { 409: 'conflict' }) }, { status: error.response.status });
    }
    throw error;
  }
});
