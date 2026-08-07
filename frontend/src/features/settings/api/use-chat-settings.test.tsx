import { describe, expect, it } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/tests/msw/server';
import { createWrapper } from '@/tests/react-query';
import { useChatSettings } from './use-chat-settings';
import type { WorkspaceChatSettingsDto } from '../model/chat-settings.types';

const mockSettings: WorkspaceChatSettingsDto = {
  primaryProvider: 'OpenAI',
  primaryModel: 'gpt-4o-mini',
  fallbackProviders: [],
  systemPromptOverride: null,
  handoffConfidenceThreshold: 0.65,
  sentimentEscalationEnabled: true,
  sentimentSensitivity: 'medium',
  triggerPhrases: ['speak to a manager', 'human agent'],
  maxUnansweredMessages: 3,
  piiMaskingLevel: 'standard',
  dataRetentionDays: 90,
  widgetToken: '11111111-1111-1111-1111-111111111111',
  widgetIconUrl: null,
  widgetPrimaryColor: '#0F172A',
  widgetSecondaryColor: '#3B82F6',
  widgetWelcomeMessage: 'Hi there! How can I help you today?',
  noAnswerMessage: "Sorry, I don't have information about that. Please contact our support team for help.",
};

describe('useChatSettings', () => {
  it('fetches and returns chat settings', async () => {
    server.use(
      http.get('/api/bff/settings/chat-settings', () => HttpResponse.json(mockSettings)),
    );
    const { result } = renderHook(() => useChatSettings(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.data).toEqual(mockSettings));
  });

  it('enters error state on 500', async () => {
    server.use(
      http.get('/api/bff/settings/chat-settings', () => new HttpResponse(null, { status: 500 })),
    );
    const { result } = renderHook(() => useChatSettings(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});
