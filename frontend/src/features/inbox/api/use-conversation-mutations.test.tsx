import { describe, expect, it } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/tests/msw/server';
import { createWrapper } from '@/tests/react-query';
import { useTakeOver } from './use-take-over';
import { useResolve } from './use-resolve';
import { useReopen } from './use-reopen';

// Covers the three state-transition mutations (task 5.3) — each is a thin POST with a 204
// No Content response and cache invalidation as documented in the hooks' own doc comments; the
// authoritative state change arrives over SignalR (see use-chat-hub.ts), so these tests only
// verify the request shape and success/error resolution, not cache content.

describe('useTakeOver', () => {
  it('posts to the take-over endpoint and resolves on 204', async () => {
    server.use(
      http.post('/api/bff/conversations/conv-001/take-over', () => new HttpResponse(null, { status: 204 })),
    );
    const { result } = renderHook(() => useTakeOver(), { wrapper: createWrapper() });
    await expect(result.current.mutateAsync('conv-001')).resolves.toBeUndefined();
  });

  it('enters error state on 409 (invalid state transition)', async () => {
    server.use(
      http.post('/api/bff/conversations/conv-001/take-over', () => new HttpResponse(null, { status: 409 })),
    );
    const { result } = renderHook(() => useTakeOver(), { wrapper: createWrapper() });
    result.current.mutate('conv-001');
    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe('useResolve', () => {
  it('posts to the resolve endpoint and resolves on 204', async () => {
    server.use(
      http.post('/api/bff/conversations/conv-001/resolve', () => new HttpResponse(null, { status: 204 })),
    );
    const { result } = renderHook(() => useResolve(), { wrapper: createWrapper() });
    await expect(result.current.mutateAsync('conv-001')).resolves.toBeUndefined();
  });

  it('enters error state on 409 (invalid state transition)', async () => {
    server.use(
      http.post('/api/bff/conversations/conv-001/resolve', () => new HttpResponse(null, { status: 409 })),
    );
    const { result } = renderHook(() => useResolve(), { wrapper: createWrapper() });
    result.current.mutate('conv-001');
    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe('useReopen', () => {
  it('posts to the reopen endpoint and resolves on 204', async () => {
    server.use(
      http.post('/api/bff/conversations/conv-001/reopen', () => new HttpResponse(null, { status: 204 })),
    );
    const { result } = renderHook(() => useReopen(), { wrapper: createWrapper() });
    await expect(result.current.mutateAsync('conv-001')).resolves.toBeUndefined();
  });

  it('enters error state on 409 (invalid state transition)', async () => {
    server.use(
      http.post('/api/bff/conversations/conv-001/reopen', () => new HttpResponse(null, { status: 409 })),
    );
    const { result } = renderHook(() => useReopen(), { wrapper: createWrapper() });
    result.current.mutate('conv-001');
    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});
