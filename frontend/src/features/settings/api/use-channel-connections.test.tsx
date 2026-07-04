import { describe, expect, it } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/tests/msw/server';
import { createWrapper } from '@/tests/react-query';
import { useChannelConnections } from './use-channel-connections';
import type { ChannelConnectionDto } from '../model/channel-connection.types';

const mockConnection: ChannelConnectionDto = {
  id: 'ch-1',
  channel: 'whatsapp',
  displayName: 'Main WhatsApp',
  externalAccountId: '1234567890',
  status: 'connected',
  errorMessage: null,
  isActive: true,
  createdAt: '2026-01-01T00:00:00Z',
  maskedAccessToken: '●●●●7890',
};

describe('useChannelConnections', () => {
  it('fetches and returns channel connection list', async () => {
    server.use(
      http.get('/api/bff/settings/channels', () =>
        HttpResponse.json([mockConnection]),
      ),
    );
    const { result } = renderHook(() => useChannelConnections(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.data).toEqual([mockConnection]));
  });

  it('returns empty array when no connections', async () => {
    server.use(
      http.get('/api/bff/settings/channels', () => HttpResponse.json([])),
    );
    const { result } = renderHook(() => useChannelConnections(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.data).toEqual([]));
  });

  it('enters error state on 500', async () => {
    server.use(
      http.get('/api/bff/settings/channels', () => new HttpResponse(null, { status: 500 })),
    );
    const { result } = renderHook(() => useChannelConnections(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});
