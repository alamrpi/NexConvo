import { NextResponse } from 'next/server';
import type { ChannelConnectionDto } from '@/features/settings/model/channel-connection.types';
import type { KnowledgeDocumentPagedResult, KnowledgeDocumentDto } from '@/features/settings/model/knowledge-document.types';
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
};

export const e2eChannelsGet = () => NextResponse.json(E2E_CHANNELS);
export const e2eChannelsPost = () => NextResponse.json(E2E_SAVED_CHANNEL, { status: 201 });
export const e2eChannelDelete = () => new NextResponse(null, { status: 204 });
export const e2eChannelTest = () =>
  NextResponse.json({ success: true, accountName: 'Test Account (@bot)' });

// ── Knowledge Base ─────────────────────────────────────────────────────────────

const E2E_KNOWLEDGE_DOCS: KnowledgeDocumentDto[] = [
  {
    id: '3',
    title: 'Warranty Terms 2026',
    sourceType: 'url',
    status: 'processing',
    chunkCount: 0,
    embeddingModel: 'text-embedding-3-large',
    failureReason: null,
    version: 1,
    createdAt: '2026-06-30T09:00:00Z',
    updatedAt: '2026-06-30T09:00:00Z',
  },
  {
    id: '1',
    title: 'Product Catalog 2026',
    sourceType: 'file',
    status: 'active',
    chunkCount: 142,
    embeddingModel: 'text-embedding-3-large',
    failureReason: null,
    version: 3,
    createdAt: '2026-06-25T08:00:00Z',
    updatedAt: '2026-06-28T10:00:00Z',
  },
  {
    id: '5',
    title: 'Delivery SLA Guide',
    sourceType: 'file',
    status: 'failed',
    chunkCount: 0,
    embeddingModel: 'text-embedding-3-large',
    failureReason: 'File could not be parsed. Ensure the PDF is not password-protected.',
    version: 1,
    createdAt: '2026-06-29T11:00:00Z',
    updatedAt: '2026-06-29T11:05:00Z',
  },
  {
    id: '2',
    title: 'Return Policy FAQ',
    sourceType: 'faq_pairs',
    status: 'active',
    chunkCount: 24,
    embeddingModel: 'text-embedding-3-large',
    failureReason: null,
    version: 1,
    createdAt: '2026-06-27T14:30:00Z',
    updatedAt: '2026-06-27T14:30:00Z',
  },
  {
    id: '4',
    title: 'Past Chat Import',
    sourceType: 'text',
    status: 'active',
    chunkCount: 318,
    embeddingModel: 'text-embedding-3-large',
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
  sourceType: 'file',
  status: 'pending',
  chunkCount: 0,
  embeddingModel: 'text-embedding-3-large',
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
  return NextResponse.json({ ...E2E_KNOWLEDGE_DOCS[0], chunks: [], versionHistory: [] });
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
  triggerPhrases: ['speak to a manager', 'human agent'],
  maxUnansweredMessages: 3,
  piiMaskingLevel: 'standard',
  dataRetentionDays: 90,
};

export const e2eChatSettingsGet = () => NextResponse.json(E2E_CHAT_SETTINGS);
export const e2eChatSettingsPut = () => NextResponse.json(E2E_CHAT_SETTINGS);
