import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';
import { e2eKnowledgeList, e2eKnowledgeUpload } from '@/shared/api/server/e2e-fixtures';
import type {
  KnowledgeDocumentPagedResult,
  KnowledgeDocumentDto,
} from '@/features/settings/model/knowledge-document.types';

/**
 * Knowledge documents list + create BFF (frontend standards S3/S4).
 * Tenant is implicit from the session JWT — never forwarded from the client (S8).
 */
export const GET = withBff(async (req, { api }) => {
  const { searchParams } = new URL(req.url);
  const params: Record<string, string> = {};
  for (const key of ['page', 'pageSize', 'status', 'sourceType']) {
    const value = searchParams.get(key);
    if (value !== null) params[key] = value;
  }

  const { data } = await api.get<KnowledgeDocumentPagedResult>(
    '/api/v1/knowledge-documents',
    { params },
  );
  return NextResponse.json(data);
}, e2eKnowledgeList);

export const POST = withBff(async (req, { api }) => {
  const contentType = req.headers.get('content-type') ?? '';

  if (contentType.includes('multipart/form-data')) {
    /**
     * File upload path: read the browser's multipart body and re-stream it to the gateway.
     * Node 18+ and Next.js 14+ both expose the global FormData and File — no extra packages.
     * Axios accepts a native FormData and sets the correct multipart boundary header automatically.
     */
    const incoming = await req.formData();
    const forwardForm = new FormData();

    for (const [key, value] of incoming.entries()) {
      if (value instanceof File) {
        // Re-wrap File so Node can compute the correct multipart boundary.
        forwardForm.append(key, new Blob([await value.arrayBuffer()], { type: value.type }), value.name);
      } else {
        forwardForm.append(key, value);
      }
    }

    const { data } = await api.post<KnowledgeDocumentDto>(
      '/api/v1/knowledge-documents',
      forwardForm,
    );
    return NextResponse.json(data, { status: 201 });
  }

  // Text / URL / FAQ / past-chats: forward JSON body directly.
  const body: unknown = await req.json().catch(() => null);
  if (body === null || typeof body !== 'object') {
    return NextResponse.json({ title: 'Invalid input' }, { status: 422 });
  }

  const { data } = await api.post<KnowledgeDocumentDto>(
    '/api/v1/knowledge-documents',
    body,
  );
  return NextResponse.json(data, { status: 201 });
}, e2eKnowledgeUpload);
