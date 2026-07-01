import { describe, expect, it } from 'vitest';
import { renderHook } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/tests/msw/server';
import { createWrapper } from '@/tests/react-query';
import { AiSettingsError, useUpdateAiSettings } from './use-update-ai-settings';
import type { AiSettingsValues } from '../model/ai-settings.schema';

const values: AiSettingsValues = {
  provider: 'OpenAI',
  apiKey: 'sk-new',
  baseUrl: '',
  defaultModel: 'gpt-4o',
  parameters: '',
  isActive: true,
};

describe('useUpdateAiSettings', () => {
  it('posts the settings to the tenant-implicit BFF route', async () => {
    server.use(http.post('/api/bff/settings/ai-config', () => HttpResponse.json({ id: 'cfg-1' })));

    const { result } = renderHook(() => useUpdateAiSettings(), { wrapper: createWrapper() });

    await expect(result.current.mutateAsync(values)).resolves.toBeUndefined();
  });

  it('maps a 409 to a typed conflict error (optimistic concurrency)', async () => {
    server.use(http.post('/api/bff/settings/ai-config', () => HttpResponse.json({ code: 'conflict' }, { status: 409 })));

    const { result } = renderHook(() => useUpdateAiSettings(), { wrapper: createWrapper() });

    await expect(result.current.mutateAsync(values)).rejects.toMatchObject({
      name: 'AiSettingsError',
      code: 'conflict',
    });
  });

  it('maps other failures to a generic error', async () => {
    server.use(http.post('/api/bff/settings/ai-config', () => new HttpResponse(null, { status: 500 })));

    const { result } = renderHook(() => useUpdateAiSettings(), { wrapper: createWrapper() });

    await expect(result.current.mutateAsync(values)).rejects.toBeInstanceOf(AiSettingsError);
    await expect(result.current.mutateAsync(values)).rejects.toMatchObject({ code: 'generic' });
  });
});
