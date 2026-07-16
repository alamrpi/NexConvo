import { NextResponse } from 'next/server';
import type { ChannelConnectionDto } from '@/features/settings/model/channel-connection.types';
import type { KnowledgeDocumentPagedResult, KnowledgeDocumentDto, KnowledgeDocumentDetailDto } from '@/features/settings/model/knowledge-document.types';
import type { WorkspaceChatSettingsDto } from '@/features/settings/model/chat-settings.types';

// ── Channel Connections ────────────────────────────────────────────────────────

export const E2E_CHANNELS: ChannelConnectionDto[] = [
  {
    id: 'e2e-ch-whatsapp',
    channel: 'whatsapp',
    displayName: 'Main WhatsApp',
    externalAccountId: '+8801700000000',
    status: 'connected',
    errorMessage: null,
    isActive: true,
    createdAt: '2026-06-01T00:00:00Z',
    maskedAccessToken: '●●●●1234',
    lastTestStatus: 'Healthy',
    lastTestedAt: '2026-07-06T12:00:00Z',
    lastTestError: null,
    lastTestLatencyMs: 180,
  },
  {
    id: 'e2e-ch-instagram',
    channel: 'instagram',
    displayName: 'Brand Instagram',
    externalAccountId: '@brandaccount',
    status: 'error',
    errorMessage: 'Access token expired. Reconnect to restore messaging.',
    isActive: true,
    createdAt: '2026-06-01T00:00:00Z',
    maskedAccessToken: '●●●●5678',
    lastTestStatus: 'Failed',
    lastTestedAt: '2026-07-06T09:00:00Z',
    lastTestError: 'Access token expired. Reconnect to restore messaging.',
    lastTestLatencyMs: null,
  },
  {
    id: 'e2e-ch-web',
    channel: 'web',
    displayName: 'Website Widget',
    externalAccountId: null,
    status: 'connected',
    errorMessage: null,
    isActive: true,
    createdAt: '2026-06-01T00:00:00Z',
    maskedAccessToken: null,
    lastTestStatus: 'Untested',
    lastTestedAt: null,
    lastTestError: null,
    lastTestLatencyMs: null,
  },
];

export const E2E_SAVED_CHANNEL: ChannelConnectionDto = {
  id: 'e2e-ch-new',
  channel: 'telegram',
  displayName: 'New Bot',
  externalAccountId: '@newbot',
  status: 'connected',
  errorMessage: null,
  isActive: true,
  createdAt: '2026-06-01T00:00:00Z',
  maskedAccessToken: '●●●●9999',
  lastTestStatus: 'Healthy',
  lastTestedAt: '2026-06-01T00:00:00Z',
  lastTestError: null,
  lastTestLatencyMs: 150,
};

export const e2eChannelsGet = () => NextResponse.json(E2E_CHANNELS);
export const e2eChannelsPost = () => NextResponse.json(E2E_SAVED_CHANNEL, { status: 201 });
export const e2eChannelDelete = () => new NextResponse(null, { status: 204 });
export const e2eChannelTest = () =>
  NextResponse.json({ success: true, status: 'Healthy', detail: 'Test Account (@bot)' });

// ── Knowledge Base ─────────────────────────────────────────────────────────────

const E2E_KNOWLEDGE_DOCS: KnowledgeDocumentDto[] = [
  {
    id: '3',
    title: 'Warranty Terms 2026',
    fileName: 'Warranty Terms 2026',
    sourceType: 'Url',
    status: 'Processing',
    chunkCount: 0,
    failureReason: null,
    version: 1,
    createdAt: '2026-06-30T09:00:00Z',
    updatedAt: '2026-06-30T09:00:00Z',
  },
  {
    id: '1',
    title: 'Product Catalog 2026',
    fileName: 'Product Catalog 2026',
    sourceType: 'File',
    status: 'Ready',
    chunkCount: 142,
    failureReason: null,
    version: 3,
    createdAt: '2026-06-25T08:00:00Z',
    updatedAt: '2026-06-28T10:00:00Z',
  },
  {
    id: '5',
    title: 'Delivery SLA Guide',
    fileName: 'Delivery SLA Guide',
    sourceType: 'File',
    status: 'Failed',
    chunkCount: 0,
    failureReason: 'File could not be parsed. Ensure the PDF is not password-protected.',
    version: 1,
    createdAt: '2026-06-29T11:00:00Z',
    updatedAt: '2026-06-29T11:05:00Z',
  },
  {
    id: '2',
    title: 'Return Policy FAQ',
    fileName: 'Return Policy FAQ',
    sourceType: 'Faq',
    status: 'Ready',
    chunkCount: 24,
    failureReason: null,
    version: 1,
    createdAt: '2026-06-27T14:30:00Z',
    updatedAt: '2026-06-27T14:30:00Z',
  },
  {
    id: '4',
    title: 'Past Chat Import',
    fileName: 'Past Chat Import',
    sourceType: 'Text',
    status: 'Ready',
    chunkCount: 318,
    failureReason: null,
    version: 1,
    createdAt: '2026-06-26T09:00:00Z',
    updatedAt: '2026-06-26T09:00:00Z',
  },
];

const E2E_KNOWLEDGE_RESULT: KnowledgeDocumentPagedResult = {
  items: E2E_KNOWLEDGE_DOCS,
  totalCount: 5,
  page: 1,
  pageSize: 20,
};

const E2E_UPLOADED_DOC: KnowledgeDocumentDto = {
  id: 'e2e-doc-new',
  title: 'New Document',
  fileName: 'New Document',
  sourceType: 'File',
  status: 'Pending',
  chunkCount: 0,
  failureReason: null,
  version: 1,
  createdAt: '2026-07-01T00:00:00Z',
  updatedAt: '2026-07-01T00:00:00Z',
};

export const e2eKnowledgeList = () => NextResponse.json(E2E_KNOWLEDGE_RESULT);
export const e2eKnowledgeUpload = () => NextResponse.json(E2E_UPLOADED_DOC, { status: 201 });
export const e2eKnowledgeDelete = () => new NextResponse(null, { status: 204 });
export const e2eKnowledgeGetById = (_req: unknown, routeCtx: { params: Promise<Record<string, string>> }) => {
  // Return the first doc as a stand-in for any ID request in E2E
  // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
  const doc = E2E_KNOWLEDGE_DOCS[0]!;
  const detail: KnowledgeDocumentDetailDto = {
    id: doc.id,
    title: doc.title,
    fileName: doc.fileName,
    sourceType: doc.sourceType,
    sourceUrl: null,
    status: doc.status,
    chunkCount: doc.chunkCount,
    failureReason: doc.failureReason,
    version: doc.version,
    embeddingModel: 'text-embedding-3-large',
    embeddingDimensions: 1024,
    createdAt: doc.createdAt,
    chunks: [],
    chunkTotal: 0,
    versionHistory: [],
  };
  return NextResponse.json(detail);
};

// ── Chat Settings ──────────────────────────────────────────────────────────────

const E2E_CHAT_SETTINGS: WorkspaceChatSettingsDto = {
  primaryProvider: 'OpenAI',
  primaryModel: 'gpt-4o-mini',
  fallbackProviders: [],
  systemPromptOverride: null,
  handoffConfidenceThreshold: 0.65,
  sentimentEscalationEnabled: true,
  sentimentSensitivity: 'medium',
  triggerPhrases: ['speak to agent', 'help'],
  maxUnansweredMessages: 3,
  piiMaskingLevel: 'off',
  dataRetentionDays: null,
  widgetToken: '22222222-2222-2222-2222-222222222222',
  widgetIconUrl: null,
  widgetPrimaryColor: '#0F172A',
  widgetSecondaryColor: '#3B82F6',
  widgetWelcomeMessage: 'Hi there! How can I help you today?',
};

export const e2eChatSettingsGet = () => NextResponse.json(E2E_CHAT_SETTINGS);
export const e2eChatSettingsPut = () => NextResponse.json(E2E_CHAT_SETTINGS);
