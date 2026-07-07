import { http, HttpResponse } from 'msw';
import type {
  ChannelConnectionDto,
  TestChannelConnectionResponse,
} from '@/features/settings/model/channel-connection.types';

// ---------------------------------------------------------------------------
// Stable fixtures
// ---------------------------------------------------------------------------

const mockConnections: ChannelConnectionDto[] = [
  {
    id: 'conn-1',
    channel: 'whatsapp',
    displayName: 'WhatsApp Business',
    externalAccountId: '123456789',
    status: 'connected',
    errorMessage: null,
    isActive: true,
    createdAt: '2026-06-01T08:00:00Z',
    maskedAccessToken: 'EAA***abc',
    lastTestStatus: 'Healthy',
    lastTestedAt: '2026-06-01T08:05:00Z',
    lastTestError: null,
    lastTestLatencyMs: 130,
  },
  {
    id: 'conn-2',
    channel: 'facebook',
    displayName: 'Facebook Page',
    externalAccountId: '987654321',
    status: 'disconnected',
    errorMessage: null,
    isActive: false,
    createdAt: '2026-06-15T10:30:00Z',
    maskedAccessToken: 'EAA***xyz',
    lastTestStatus: 'Untested',
    lastTestedAt: null,
    lastTestError: null,
    lastTestLatencyMs: null,
  },
];

const mockCreatedConnection: ChannelConnectionDto = {
  id: 'conn-3',
  channel: 'telegram',
  displayName: 'Telegram Bot',
  externalAccountId: '@nexconvo_bot',
  status: 'connected',
  errorMessage: null,
  isActive: true,
  createdAt: '2026-07-01T12:00:00Z',
  maskedAccessToken: '123***456',
  lastTestStatus: 'Healthy',
  lastTestedAt: '2026-07-01T12:01:00Z',
  lastTestError: null,
  lastTestLatencyMs: 110,
};

// ---------------------------------------------------------------------------
// Success handlers — register these in the default server setup (S20)
// ---------------------------------------------------------------------------

export const channelConnectionHandlers = [
  http.get('/api/bff/settings/channels', () => {
    return HttpResponse.json(mockConnections);
  }),

  http.post('/api/bff/settings/channels', () => {
    return HttpResponse.json(mockCreatedConnection, { status: 201 });
  }),

  http.delete('/api/bff/settings/channels/:id', () => {
    return HttpResponse.json({ ok: true });
  }),

  http.post('/api/bff/settings/channels/:id/test', () => {
    const result: TestChannelConnectionResponse = {
      success: true,
      status: 'Healthy',
      detail: 'Test Account',
    };
    return HttpResponse.json(result);
  }),
];

// ---------------------------------------------------------------------------
// Error handlers — use via `server.use(...channelConnectionErrorHandlers)` in
// individual test cases that exercise failure paths (S20).
// ---------------------------------------------------------------------------

export const channelConnectionErrorHandlers = [
  http.get('/api/bff/settings/channels', () => {
    return HttpResponse.json({ title: 'Internal Server Error' }, { status: 500 });
  }),

  http.post('/api/bff/settings/channels', () => {
    return HttpResponse.json({ title: 'Internal Server Error' }, { status: 500 });
  }),

  http.delete('/api/bff/settings/channels/:id', () => {
    return HttpResponse.json({ title: 'Internal Server Error' }, { status: 500 });
  }),

  http.post('/api/bff/settings/channels/:id/test', () => {
    return HttpResponse.json({ title: 'Internal Server Error' }, { status: 500 });
  }),
];
