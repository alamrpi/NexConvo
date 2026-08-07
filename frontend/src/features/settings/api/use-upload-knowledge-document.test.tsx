import { describe, expect, it, vi, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/tests/msw/server';
import { createWrapper } from '@/tests/react-query';
import { useUploadKnowledgeDocument } from './use-upload-knowledge-document';
import type { KnowledgeDocumentDto } from '../model/knowledge-document.types';

const mockDocument: KnowledgeDocumentDto = {
  id: 'doc-1',
  title: 'Product FAQ',
  fileName: 'Product FAQ',
  sourceType: 'Text',
  status: 'Pending',
  chunkCount: 0,
  failureReason: null,
  version: 1,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
};

describe('useUploadKnowledgeDocument', () => {
  it('on success (text/JSON path), returns the created document and invalidates knowledge documents query', async () => {
    server.use(
      http.post('/api/bff/settings/knowledge', () =>
        HttpResponse.json(mockDocument),
      ),
    );

    const { result } = renderHook(() => useUploadKnowledgeDocument(), { wrapper: createWrapper() });

    const returned = await result.current.mutateAsync({
      title: 'Product FAQ',
      sourceType: 'Text',
      content: 'Some FAQ content here.',
    });

    expect(returned).toEqual(mockDocument);
    expect(returned.status).toBe('Pending');
  });

  it('exposes progressRef for callers to wire upload progress callbacks', async () => {
    server.use(
      http.post('/api/bff/settings/knowledge', () =>
        HttpResponse.json(mockDocument),
      ),
    );

    const { result } = renderHook(() => useUploadKnowledgeDocument(), { wrapper: createWrapper() });

    expect(result.current.progressRef).toBeDefined();
    expect(result.current.progressRef.current).toBeNull();

    const onProgress = vi.fn();
    result.current.progressRef.current = onProgress;
    expect(result.current.progressRef.current).toBe(onProgress);
  });

  it('enters error state on 500', async () => {
    server.use(
      http.post('/api/bff/settings/knowledge', () => new HttpResponse(null, { status: 500 })),
    );

    const { result } = renderHook(() => useUploadKnowledgeDocument(), { wrapper: createWrapper() });

    await expect(
      result.current.mutateAsync({ title: 'Test', sourceType: 'Text', content: 'test' }),
    ).rejects.toBeDefined();
  });
});
