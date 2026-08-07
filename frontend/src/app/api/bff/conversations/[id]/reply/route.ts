import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';

/** Sends an agent reply on a conversation (SendAgentReplyCommand). */
export const POST = withBff(async (req, { api }, routeCtx) => {
  const { id } = await routeCtx.params;
  const body = await req.json().catch(() => null);
  const { data } = await api.post(`/api/v1/conversations/${id}/reply`, body);
  return NextResponse.json(data);
});
