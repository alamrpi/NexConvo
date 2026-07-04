import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';

/** Trigger re-embedding of a knowledge document at its current content. */
export const POST = withBff(async (_req, { api }, routeCtx) => {
  const { id } = await routeCtx.params;
  await api.post(`/api/v1/knowledge-documents/${id}/re-embed`);
  return new NextResponse(null, { status: 202 });
});
