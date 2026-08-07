import { describe, expect, it } from 'vitest';
import { renderHook } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/tests/msw/server';
import { createWrapper } from '@/tests/react-query';
import { useTestChannelConnection } from './use-test-channel-connection';

describe('useTestChannelConnection', () => {
  it('returns success: true and status Healthy on successful provider ping', async () => {
    server.use(
      http.post('/api/bff/settings/channels/:id/test', () =>
        HttpResponse.json({ success: true, status: 'Healthy', detail: 'Test Account', latencyMs: 120 }),
      ),
    );

    const { result } = renderHook(() => useTestChannelConnection(), { wrapper: createWrapper() });

    const response = await result.current.mutateAsync('ch-1');

    expect(response.success).toBe(true);
    expect(response.status).toBe('Healthy');
    expect(response.detail).toBe('Test Account');
  });

  it('returns success: false, status Failed, and errorMessage on provider failure without throwing', async () => {
    server.use(
      http.post('/api/bff/settings/channels/:id/test', () =>
        HttpResponse.json({ success: false, status: 'Failed', errorMessage: 'Invalid token' }),
      ),
    );

    const { result } = renderHook(() => useTestChannelConnection(), { wrapper: createWrapper() });

    const response = await result.current.mutateAsync('ch-1');

    expect(response.success).toBe(false);
    expect(response.status).toBe('Failed');
    expect(response.errorMessage).toBe('Invalid token');
  });

  it('does not throw or enter error state on provider failure (HTTP 200 with success: false)', async () => {
    server.use(
      http.post('/api/bff/settings/channels/:id/test', () =>
        HttpResponse.json({ success: false, status: 'Failed', errorMessage: 'Invalid token' }),
      ),
    );

    const { result } = renderHook(() => useTestChannelConnection(), { wrapper: createWrapper() });

    await expect(result.current.mutateAsync('ch-1')).resolves.toMatchObject({
      success: false,
      status: 'Failed',
      errorMessage: 'Invalid token',
    });
  });
});
