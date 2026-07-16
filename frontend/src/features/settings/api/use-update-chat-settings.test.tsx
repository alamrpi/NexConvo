import { describe, expect, it } from 'vitest';
import { renderHook } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/tests/msw/server';
import { createWrapper } from '@/tests/react-query';
import { ChatSettingsError, useUpdateChatSettings } from './use-update-chat-settings';
import type { ChatSettingsValues } from '../model/chat-settings.schema';

const values: ChatSettingsValues = {
  primaryProvider: 'OpenAI',
  primaryModel: 'gpt-4o-mini',
  fallbackProviders: [],
  systemPromptOverride: null,
  handoffConfidenceThreshold: 0.65,
  sentimentEscalationEnabled: true,
  sentimentSensitivity: 'medium',
  triggerPhrases: ['speak to a manager'],
  maxUnansweredMessages: 3,
  piiMaskingLevel: 'standard',
  dataRetentionDays: 90,
  widgetIconUrl: null,
  widgetPrimaryColor: '#0F172A',
  widgetSecondaryColor: '#3B82F6',
  widgetWelcomeMessage: 'Hi there! How can I help you today?',
};

describe('useUpdateChatSettings', () => {
  it('on success, invalidates the chat settings query', async () => {
    server.use(
      http.put('/api/bff/settings/chat-settings', () => new HttpResponse(null, { status: 204 })),
    );

    const { result } = renderHook(() => useUpdateChatSettings(), { wrapper: createWrapper() });

    await expect(result.current.mutateAsync(values)).resolves.toBeUndefined();
  });

  it('on 409 conflict, maps to typed error with code conflict', async () => {
    server.use(
      http.put('/api/bff/settings/chat-settings', () =>
        HttpResponse.json({ code: 'conflict' }, { status: 409 }),
      ),
    );

    const { result } = renderHook(() => useUpdateChatSettings(), { wrapper: createWrapper() });

    await expect(result.current.mutateAsync(values)).rejects.toMatchObject({
      name: 'ChatSettingsError',
      code: 'conflict',
    });
  });

  it('maps other failures to a generic error', async () => {
    server.use(
      http.put('/api/bff/settings/chat-settings', () => new HttpResponse(null, { status: 500 })),
    );

    const { result } = renderHook(() => useUpdateChatSettings(), { wrapper: createWrapper() });

    await expect(result.current.mutateAsync(values)).rejects.toBeInstanceOf(ChatSettingsError);
    await expect(result.current.mutateAsync(values)).rejects.toMatchObject({ code: 'generic' });
  });
});
