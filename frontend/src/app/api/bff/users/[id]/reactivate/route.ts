import axios from 'axios';
import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';
import { gatedWriteCode } from '@/shared/api/server/gated-write';

/** Re-enable a disabled user. Forwards 403 (unverified | 2FA-required) for the UI. */
export const POST = withBff(async (_req, { api }, routeCtx) => {
  const { id } = await routeCtx.params;
  try {
    await api.post(`/api/v1/users/${id}/reactivate`);
    return new NextResponse(null, { status: 204 });
  } catch (error) {
    if (axios.isAxiosError(error) && error.response) {
      return NextResponse.json({ code: gatedWriteCode(error) }, { status: error.response.status });
    }
    throw error;
  }
});
