import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';
import { e2eChannelDelete } from '@/shared/api/server/e2e-fixtures';

export const DELETE = withBff(async (_req, { api }, routeCtx) => {
  const { id } = await routeCtx.params;
  await api.delete(`/api/v1/channel-connections/${id}`);
  return NextResponse.json({ ok: true });
}, e2eChannelDelete);
