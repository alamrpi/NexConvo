import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';

/** Takes ownership of a pending conversation (TakeOverConversationCommand). Backend returns 204. */
export const POST = withBff(async (_req, { api }, routeCtx) => {
  const { id } = await routeCtx.params;
  await api.post(`/api/v1/conversations/${id}/take-over`);
  return NextResponse.json({ ok: true });
});
