import { describe, expect, it } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/tests/msw/server';
import { createWrapper } from '@/tests/react-query';
import { useAiSettings } from './use-ai-settings';
import type { AiConfigDto } from '../model/ai-settings.types';

const config: AiConfigDto = {
  id: 'cfg-1',
  provider: 'OpenAI',
  hasApiKey: true,
  baseUrl: null,
  defaultModel: 'gpt-4o',
  parameters: null,
  isActive: true,
};

describe('useAiSettings', () => {
  it('reads the workspace AI configs from the tenant-implicit BFF route', async () => {
    server.use(http.get('/api/bff/settings/ai-config', () => HttpResponse.json([config])));

    const { result } = renderHook(() => useAiSettings(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.data).toEqual([config]));
  });

  it('surfaces an error state when the BFF fails', async () => {
    server.use(http.get('/api/bff/settings/ai-config', () => new HttpResponse(null, { status: 500 })));

    const { result } = renderHook(() => useAiSettings(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});
