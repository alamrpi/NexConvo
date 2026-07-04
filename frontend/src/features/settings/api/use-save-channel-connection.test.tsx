import { describe, expect, it } from 'vitest';
import { renderHook } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/tests/msw/server';
import { createWrapper } from '@/tests/react-query';
import { ChannelConnectionError, useSaveChannelConnection } from './use-save-channel-connection';
import type { SaveChannelConnectionValues } from '../model/channel-connection.schema';
import type { ChannelConnectionDto } from '../model/channel-connection.types';

const values: SaveChannelConnectionValues = {
  channel: 'whatsapp',
  displayName: 'Main WhatsApp',
  accessToken: 'EAAtoken',
  externalAccountId: '1234567890',
  appSecret: 'secret123',
};

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

describe('useSaveChannelConnection', () => {
  it('on success, returns the created connection and invalidates channel connections query', async () => {
    server.use(
      http.get('/api/bff/settings/channels', () => HttpResponse.json([mockConnection])),
      http.post('/api/bff/settings/channels', () => HttpResponse.json(mockConnection)),
    );

    const wrapper = createWrapper();
    const { result } = renderHook(() => useSaveChannelConnection(), { wrapper });

    const returned = await result.current.mutateAsync(values);
    expect(returned).toEqual(mockConnection);
  });

  it('on 409 conflict, maps to typed error with code conflict', async () => {
    server.use(
      http.post('/api/bff/settings/channels', () =>
        HttpResponse.json({ code: 'conflict' }, { status: 409 }),
      ),
    );

    const { result } = renderHook(() => useSaveChannelConnection(), { wrapper: createWrapper() });

    await expect(result.current.mutateAsync(values)).rejects.toMatchObject({
      name: 'ChannelConnectionError',
      code: 'conflict',
    });
  });

  it('on 403 forbidden, maps to typed error with code forbidden', async () => {
    server.use(
      http.post('/api/bff/settings/channels', () =>
        HttpResponse.json({ code: 'forbidden' }, { status: 403 }),
      ),
    );

    const { result } = renderHook(() => useSaveChannelConnection(), { wrapper: createWrapper() });

    await expect(result.current.mutateAsync(values)).rejects.toMatchObject({
      name: 'ChannelConnectionError',
      code: 'forbidden',
    });
  });

  it('maps other failures to a generic error', async () => {
    server.use(
      http.post('/api/bff/settings/channels', () => new HttpResponse(null, { status: 500 })),
    );

    const { result } = renderHook(() => useSaveChannelConnection(), { wrapper: createWrapper() });

    await expect(result.current.mutateAsync(values)).rejects.toBeInstanceOf(ChannelConnectionError);
    await expect(result.current.mutateAsync(values)).rejects.toMatchObject({ code: 'generic' });
  });
});
