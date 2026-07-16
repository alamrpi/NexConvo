import { http, HttpResponse } from 'msw';
import type { WorkspaceChatSettingsDto } from '@/features/settings/model/chat-settings.types';

const mockChatSettings: WorkspaceChatSettingsDto = {
  primaryProvider: 'OpenRouter',
  primaryModel: 'openai/gpt-4o',
  fallbackProviders: ['Anthropic'],
  systemPromptOverride: 'You are a helpful customer service assistant.',
  handoffConfidenceThreshold: 0.65,
  sentimentEscalationEnabled: true,
  sentimentSensitivity: 'medium',
  triggerPhrases: ['operator', 'human', 'speak to a person'],
  maxUnansweredMessages: 5,
  piiMaskingLevel: 'standard',
  dataRetentionDays: 90,
  widgetToken: '33333333-3333-3333-3333-333333333333',
  widgetIconUrl: null,
  widgetPrimaryColor: '#0F172A',
  widgetSecondaryColor: '#3B82F6',
  widgetWelcomeMessage: 'Hi there! How can I help you today?',
  noAnswerMessage: "Sorry, I don't have information about that. Please contact our support team for help.",
};

/**
 * Happy-path handlers for workspace chat settings (frontend standard S20).
 * Import into a test's `server.use(...chatSettingsHandlers)` to simulate a healthy backend.
 */
export const chatSettingsHandlers = [
  http.get('/api/bff/settings/chat-settings', () =>
    HttpResponse.json(mockChatSettings),
  ),

  http.put('/api/bff/settings/chat-settings', () =>
    HttpResponse.json(mockChatSettings),
  ),
];

/**
 * Error handlers that simulate a 500 from the backend.
 * Import into a test's `server.use(...chatSettingsErrorHandlers)` to exercise error states (S15).
 */
export const chatSettingsErrorHandlers = [
  http.get('/api/bff/settings/chat-settings', () =>
    HttpResponse.json({ title: 'Internal Server Error' }, { status: 500 }),
  ),

  http.put('/api/bff/settings/chat-settings', () =>
    HttpResponse.json({ title: 'Internal Server Error' }, { status: 500 }),
  ),
];
