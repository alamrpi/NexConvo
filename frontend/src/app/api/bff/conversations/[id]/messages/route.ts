import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';

/** Fetches a conversation's message history (GetConversationMessagesQuery), oldest first. */
export const GET = withBff(async (req, { api }, routeCtx) => {
  const { id } = await routeCtx.params;
  const { searchParams } = new URL(req.url);
  const { data } = await api.get(`/api/v1/conversations/${id}/messages`, {
    params: {
      cursor: searchParams.get('cursor') ?? undefined,
      pageSize: searchParams.get('pageSize') ?? undefined,
    },
  });
  return NextResponse.json(data);
});
