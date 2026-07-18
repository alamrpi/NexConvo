import { describe, expect, it } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/tests/msw/server';
import { createWrapper } from '@/tests/react-query';
import { useConversations } from './use-conversations';
import type { ConversationSummaryDto, CursorPagedResult } from '../model/inbox.types';

const mockConversation: ConversationSummaryDto = {
  id: 'conv-001',
  state: 'AiHandling',
  channel: 'web',
  contactName: 'Sarah Ahmed',
  contactHandle: 'sarah.ahmed@example.com',
  lastMessagePreview: 'Can I track my order in real-time?',
  lastMessageAt: '2026-06-30T09:30:00.000Z',
  lastMessageFromAi: true,
  unreadCount: 0,
  assignedAgentName: null,
  slaExpiresAt: null,
  tags: [],
};

describe('useConversations', () => {
  it('fetches and returns a paged conversation list', async () => {
    const page: CursorPagedResult<ConversationSummaryDto> = {
      items: [mockConversation],
      nextCursor: null,
    };
    server.use(
      http.get('/api/bff/conversations', () => HttpResponse.json(page)),
    );
    const { result } = renderHook(() => useConversations(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.data).toEqual(page));
  });

  it('enters error state on 500', async () => {
    server.use(
      http.get('/api/bff/conversations', () => new HttpResponse(null, { status: 500 })),
    );
    const { result } = renderHook(() => useConversations(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isError).toBe(true));
  });

  it('passes filters as query params', async () => {
    const captured: { url: URL | null } = { url: null };
    server.use(
      http.get('/api/bff/conversations', ({ request }) => {
        captured.url = new URL(request.url);
        return HttpResponse.json({ items: [], nextCursor: null });
      }),
    );
    const { result } = renderHook(
      () => useConversations({ state: 'PendingHuman', assignedToMe: true }),
      { wrapper: createWrapper() },
    );
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(captured.url?.searchParams.get('state')).toBe('PendingHuman');
    expect(captured.url?.searchParams.get('assignedToMe')).toBe('true');
  });
});
