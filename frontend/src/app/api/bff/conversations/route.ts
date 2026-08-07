import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';

/**
 * Lists conversations for the current tenant (GetConversationsQuery via ConversationsController).
 * Tenant-implicit (S8) — the gateway/backend derive it from the JWT. Query params are passed
 * through as-is; the backend defaults an omitted/invalid pageSize to 25 and an empty cursor to
 * "first page".
 */
export const GET = withBff(async (req, { api }) => {
  const { searchParams } = new URL(req.url);
  const { data } = await api.get('/api/v1/conversations', {
    params: {
      state: searchParams.get('state') ?? undefined,
      channel: searchParams.get('channel') ?? undefined,
      assignedToMe: searchParams.get('assignedToMe') ?? undefined,
      cursor: searchParams.get('cursor') ?? undefined,
      pageSize: searchParams.get('pageSize') ?? undefined,
    },
  });
  return NextResponse.json(data);
});
