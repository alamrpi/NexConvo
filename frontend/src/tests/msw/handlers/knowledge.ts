import { http, HttpResponse } from 'msw';
import type {
  KnowledgeDocumentPagedResult,
  KnowledgeDocumentDetailDto,
} from '@/features/settings/model/knowledge-document.types';

/** Base URL for all knowledge BFF endpoints. */
const BASE = '/api/bff/settings/knowledge';

// ─── Seed data ────────────────────────────────────────────────────────────────

export const MOCK_KNOWLEDGE_LIST: KnowledgeDocumentPagedResult = {
  items: [
    {
      id: '1',
      title: 'Product Catalog 2026',
      sourceType: 'file',
      status: 'active',
      chunkCount: 142,
      embeddingModel: 'text-embedding-3-large',
      failureReason: null,
      version: 3,
      createdAt: '2026-06-25T08:00:00.000Z',
      updatedAt: '2026-06-28T10:00:00.000Z',
    },
    {
      id: '2',
      title: 'Return Policy FAQ',
      sourceType: 'faq_pairs',
      status: 'active',
      chunkCount: 24,
      embeddingModel: 'text-embedding-3-large',
      failureReason: null,
      version: 1,
      createdAt: '2026-06-27T14:30:00.000Z',
      updatedAt: '2026-06-27T14:30:00.000Z',
    },
    {
      id: '3',
      title: 'Warranty Terms 2026',
      sourceType: 'url',
      status: 'processing',
      chunkCount: 0,
      embeddingModel: 'text-embedding-3-large',
      failureReason: null,
      version: 1,
      createdAt: '2026-06-30T09:00:00.000Z',
      updatedAt: '2026-06-30T09:00:00.000Z',
    },
  ],
  totalCount: 3,
  page: 1,
  pageSize: 20,
};

export const MOCK_KNOWLEDGE_DETAIL: KnowledgeDocumentDetailDto = {
  // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
  ...MOCK_KNOWLEDGE_LIST.items[0]!,
  chunks: [
    {
      id: 'c1',
      ordinal: 1,
      contentPreview: 'Our return policy allows customers to return unused items within 30 days…',
      tokenCount: 198,
    },
    {
      id: 'c2',
      ordinal: 2,
      contentPreview: 'Delivery times vary by region: Dhaka 1–2 days, Chittagong 2–3 days…',
      tokenCount: 215,
    },
  ],
  versionHistory: [
    {
      version: 3,
      embeddingModel: 'text-embedding-3-large',
      chunkCount: 142,
      createdAt: '2026-06-28T10:00:00.000Z',
    },
    {
      version: 2,
      embeddingModel: 'text-embedding-3-large',
      chunkCount: 138,
      createdAt: '2026-06-26T10:00:00.000Z',
    },
    {
      version: 1,
      embeddingModel: 'text-embedding-ada-002',
      chunkCount: 120,
      createdAt: '2026-06-25T08:00:00.000Z',
    },
  ],
};

// ─── Success handlers ──────────────────────────────────────────────────────────

/** GET /api/bff/settings/knowledge — paginated list */
export const handleGetKnowledgeList = http.get(BASE, () =>
  HttpResponse.json(MOCK_KNOWLEDGE_LIST),
);

/** GET /api/bff/settings/knowledge/:id — document detail */
export const handleGetKnowledgeDetail = http.get(`${BASE}/:id`, ({ params }) => {
  if (params['id'] === '1') return HttpResponse.json(MOCK_KNOWLEDGE_DETAIL);
  return new HttpResponse(null, { status: 404 });
});

/** POST /api/bff/settings/knowledge — create (JSON or file upload) */
export const handleCreateKnowledge = http.post(BASE, () =>
  HttpResponse.json(
    {
      id: 'new-1',
      title: 'New Document',
      sourceType: 'file',
      status: 'pending',
      chunkCount: 0,
      embeddingModel: 'text-embedding-3-large',
      failureReason: null,
      version: 1,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    },
    { status: 201 },
  ),
);

/** DELETE /api/bff/settings/knowledge/:id — soft delete */
export const handleDeleteKnowledge = http.delete(
  `${BASE}/:id`,
  () => new HttpResponse(null, { status: 204 }),
);

/** POST /api/bff/settings/knowledge/:id/re-embed */
export const handleReEmbedKnowledge = http.post(
  `${BASE}/:id/re-embed`,
  () => new HttpResponse(null, { status: 202 }),
);

// ─── Error handlers (500 variants for error-state tests) ──────────────────────

export const handleGetKnowledgeListError = http.get(BASE, () =>
  HttpResponse.json({ title: 'Internal Server Error' }, { status: 500 }),
);

export const handleGetKnowledgeDetailError = http.get(`${BASE}/:id`, () =>
  HttpResponse.json({ title: 'Internal Server Error' }, { status: 500 }),
);

export const handleCreateKnowledgeError = http.post(BASE, () =>
  HttpResponse.json({ title: 'Internal Server Error' }, { status: 500 }),
);

export const handleDeleteKnowledgeError = http.delete(`${BASE}/:id`, () =>
  HttpResponse.json({ title: 'Internal Server Error' }, { status: 500 }),
);

export const handleReEmbedKnowledgeError = http.post(`${BASE}/:id/re-embed`, () =>
  HttpResponse.json({ title: 'Internal Server Error' }, { status: 500 }),
);

/** Convenience array of all success handlers — import and spread into server.use(). */
export const knowledgeHandlers = [
  handleGetKnowledgeList,
  handleGetKnowledgeDetail,
  handleCreateKnowledge,
  handleDeleteKnowledge,
  handleReEmbedKnowledge,
];
