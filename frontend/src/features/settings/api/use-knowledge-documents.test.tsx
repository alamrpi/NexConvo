import { describe, expect, it } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/tests/msw/server';
import { createWrapper } from '@/tests/react-query';
import { useKnowledgeDocuments } from './use-knowledge-documents';
import type { KnowledgeDocumentDto, KnowledgeDocumentPagedResult } from '../model/knowledge-document.types';

const mockDocument: KnowledgeDocumentDto = {
  id: 'doc-1',
  title: 'Product FAQ',
  fileName: 'product-faq.pdf',
  sourceType: 'File',
  status: 'Ready',
  chunkCount: 42,
  failureReason: null,
  version: 1,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
};

const mockPage: KnowledgeDocumentPagedResult = {
  items: [mockDocument],
  totalCount: 1,
  page: 1,
  pageSize: 20,
};

describe('useKnowledgeDocuments', () => {
  it('fetches and returns paginated document list', async () => {
    server.use(
      http.get('/api/bff/settings/knowledge', () => HttpResponse.json(mockPage)),
    );
    const { result } = renderHook(() => useKnowledgeDocuments(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.data).toEqual(mockPage));
    expect(result.current.data?.items).toHaveLength(1);
    expect(result.current.data?.totalCount).toBe(1);
  });

  it('returns empty items array when no documents', async () => {
    server.use(
      http.get('/api/bff/settings/knowledge', () =>
        HttpResponse.json({ items: [], totalCount: 0, page: 1, pageSize: 20 }),
      ),
    );
    const { result } = renderHook(() => useKnowledgeDocuments(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.data?.items).toEqual([]));
    expect(result.current.data?.totalCount).toBe(0);
  });

  it('enters error state on 500', async () => {
    server.use(
      http.get('/api/bff/settings/knowledge', () => new HttpResponse(null, { status: 500 })),
    );
    const { result } = renderHook(() => useKnowledgeDocuments(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});
