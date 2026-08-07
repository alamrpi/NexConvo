import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';

/** Marks a conversation resolved (ResolveConversationCommand). Backend returns 204. */
export const POST = withBff(async (_req, { api }, routeCtx) => {
  const { id } = await routeCtx.params;
  await api.post(`/api/v1/conversations/${id}/resolve`);
  return NextResponse.json({ ok: true });
});
