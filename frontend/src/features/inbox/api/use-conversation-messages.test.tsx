import { describe, expect, it } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/tests/msw/server';
import { createWrapper } from '@/tests/react-query';
import { useConversationMessages } from './use-conversation-messages';
import type { CursorPagedResult, MessageDto } from '../model/inbox.types';

const mockMessage: MessageDto = {
  id: 'msg-001',
  conversationId: 'conv-001',
  senderRole: 'Contact',
  senderName: 'Sarah Ahmed',
  body: 'Can I track my order in real-time?',
  sentAt: '2026-06-30T09:30:00.000Z',
  deliveryStatus: null,
  confidence: null,
};

describe('useConversationMessages', () => {
  it('fetches and returns a paged message list for the given conversation', async () => {
    const page: CursorPagedResult<MessageDto> = { items: [mockMessage], nextCursor: null };
    server.use(
      http.get('/api/bff/conversations/conv-001/messages', () => HttpResponse.json(page)),
    );
    const { result } = renderHook(() => useConversationMessages('conv-001'), {
      wrapper: createWrapper(),
    });
    await waitFor(() => expect(result.current.data).toEqual(page));
  });

  it('is disabled (does not fetch) when conversationId is null', async () => {
    let called = false;
    server.use(
      http.get('/api/bff/conversations/:id/messages', () => {
        called = true;
        return HttpResponse.json({ items: [], nextCursor: null });
      }),
    );
    const { result } = renderHook(() => useConversationMessages(null), {
      wrapper: createWrapper(),
    });
    expect(result.current.fetchStatus).toBe('idle');
    expect(called).toBe(false);
  });

  it('enters error state on 500', async () => {
    server.use(
      http.get('/api/bff/conversations/conv-001/messages', () => new HttpResponse(null, { status: 500 })),
    );
    const { result } = renderHook(() => useConversationMessages('conv-001'), {
      wrapper: createWrapper(),
    });
    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});
