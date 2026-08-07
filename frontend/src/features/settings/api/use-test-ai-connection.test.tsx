import { describe, expect, it } from 'vitest';
import { renderHook } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/tests/msw/server';
import { createWrapper } from '@/tests/react-query';
import { useTestAiConnection } from './use-test-ai-connection';

describe('useTestAiConnection', () => {
  it('returns success: true with status/detail/latency on a healthy connection', async () => {
    server.use(
      http.post('/api/bff/settings/ai-config/test', () =>
        HttpResponse.json({
          success: true,
          status: 'Healthy',
          detail: 'Model responded',
          errorMessage: null,
          latencyMs: 214,
        }),
      ),
    );

    const { result } = renderHook(() => useTestAiConnection(), { wrapper: createWrapper() });

    const response = await result.current.mutateAsync({
      provider: 'OpenAI',
      apiKey: 'sk-test',
      model: 'gpt-4o',
    });

    expect(response).toEqual({
      success: true,
      status: 'Healthy',
      detail: 'Model responded',
      errorMessage: null,
      latencyMs: 214,
    });
  });

  it('does not throw on a failed connection (HTTP 200 with success: false)', async () => {
    server.use(
      http.post('/api/bff/settings/ai-config/test', () =>
        HttpResponse.json({
          success: false,
          status: 'Failed',
          detail: null,
          errorMessage: 'Invalid API key',
          latencyMs: 95,
        }),
      ),
    );

    const { result } = renderHook(() => useTestAiConnection(), { wrapper: createWrapper() });

    await expect(
      result.current.mutateAsync({ provider: 'OpenAI', apiKey: 'sk-bad', model: 'gpt-4o' }),
    ).resolves.toMatchObject({
      success: false,
      status: 'Failed',
      errorMessage: 'Invalid API key',
    });
  });

  it('sends the request without an apiKey when reusing the stored key', async () => {
    let capturedBody: unknown;
    server.use(
      http.post('/api/bff/settings/ai-config/test', async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json({ success: true, status: 'Healthy' });
      }),
    );

    const { result } = renderHook(() => useTestAiConnection(), { wrapper: createWrapper() });

    await result.current.mutateAsync({ provider: 'Anthropic', model: 'claude-sonnet-4-6' });

    expect(capturedBody).toMatchObject({
      provider: 'Anthropic',
      model: 'claude-sonnet-4-6',
    });
  });
});
