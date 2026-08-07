import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';
import { e2eKnowledgeGetById, e2eKnowledgeDelete } from '@/shared/api/server/e2e-fixtures';
import type { KnowledgeDocumentDetailDto } from '@/features/settings/model/knowledge-document.types';

/** Knowledge document detail — GET (full detail with chunks) and DELETE (soft delete). */
export const GET = withBff(async (_req, { api }, routeCtx) => {
  const { id } = await routeCtx.params;
  const { data } = await api.get<KnowledgeDocumentDetailDto>(`/api/v1/knowledge-documents/${id}`);
  return NextResponse.json(data);
}, e2eKnowledgeGetById);

export const DELETE = withBff(async (_req, { api }, routeCtx) => {
  const { id } = await routeCtx.params;
  await api.delete(`/api/v1/knowledge-documents/${id}`);
  return new NextResponse(null, { status: 204 });
}, e2eKnowledgeDelete);
