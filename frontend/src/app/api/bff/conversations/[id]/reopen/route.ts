import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';

/** Reopens a resolved/closed conversation back to AI handling (ReopenConversationCommand). Backend returns 204. */
export const POST = withBff(async (_req, { api }, routeCtx) => {
  const { id } = await routeCtx.params;
  await api.post(`/api/v1/conversations/${id}/reopen`);
  return NextResponse.json({ ok: true });
});
