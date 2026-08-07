import { describe, expect, it } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/tests/msw/server';
import { createWrapper } from '@/tests/react-query';
import { useSendReply } from './use-send-reply';
import type { MessageDto } from '../model/inbox.types';

const mockReply: MessageDto = {
  id: 'msg-002',
  conversationId: 'conv-001',
  senderRole: 'Agent',
  senderName: 'Alam Hossain',
  body: 'Sure, here is your tracking link.',
  sentAt: '2026-06-30T09:31:00.000Z',
  deliveryStatus: 'Sent',
  confidence: null,
};

describe('useSendReply', () => {
  it('on success, posts the text and returns the created message', async () => {
    server.use(
      http.post('/api/bff/conversations/conv-001/reply', async ({ request }) => {
        const body = await request.json();
        expect(body).toEqual({ text: 'Sure, here is your tracking link.' });
        return HttpResponse.json(mockReply);
      }),
    );

    const { result } = renderHook(() => useSendReply('conv-001'), { wrapper: createWrapper() });
    const returned = await result.current.mutateAsync('Sure, here is your tracking link.');
    expect(returned).toEqual(mockReply);
  });

  it('enters error state on 500', async () => {
    server.use(
      http.post('/api/bff/conversations/conv-001/reply', () => new HttpResponse(null, { status: 500 })),
    );

    const { result } = renderHook(() => useSendReply('conv-001'), { wrapper: createWrapper() });
    result.current.mutate('hello');
    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});
