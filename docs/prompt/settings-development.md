# NexConvo — Settings Section Development Prompt (Multi-Agent)

> Paste this entire prompt into a new Claude Code session at the NexConvo project root.
> Claude will orchestrate parallel agent teams to build backend API, frontend integration, unit tests, E2E tests, and produce a final report.

---

## Your Role — Staff Engineer & Orchestrator

You are the **Staff Engineer** responsible for delivering the NexConvo Settings section. You write zero feature code yourself — your job is to read the architecture, brief specialist agents precisely, coordinate phases, and do the final integration quality pass.

**Before spawning any agent, read all four of these files completely:**
1. `docs/ARCHITECTURE.md`
2. `docs/CHATBOT-ARCHITECTURE.md`
3. `docs/prompt/chatbot-ui-ux-design.md`
4. `CLAUDE.md`

These are the non-negotiable source of truth. Every agent you spawn must be given the relevant sections.

---

## What Has Already Been Built (Do Not Rebuild)

| Area | Status | Location |
|---|---|---|
| AI Provider Config (global) | ✅ Complete | Integrations service — `WorkspaceAiConfig`, `WorkspaceAiConfigController`, `AiConfigUpdatedEvent` |
| AI Settings frontend (provider/key/model) | ✅ Complete | `frontend/src/features/settings/` — `use-ai-settings.ts`, `ai-settings-form.tsx`, BFF at `/api/bff/settings/ai-config` |
| Identity, Email, Members, Roles settings | ✅ Complete | Various — do not touch |

## What to Build

| Area | Backend | Frontend | Scope |
|---|---|---|---|
| **Channel Connections** | ❌ Zero | ❌ Zero | Full stack |
| **Chat AI Settings** | ❌ Zero | ❌ Zero | Full stack (provider selector + behavior config) |
| **Knowledge Base** | ❌ Zero | ⚠️ Mock UI exists | Backend + wire frontend |

### Architecture Decision — Chat AI Settings vs Global AI Config

**Global AI Config (Integrations service) = Credential Vault**
Admin configures which providers they have API keys for: "I have OpenAI key, DeepSeek key."

**Chat AI Settings (Chat service) = Selector + Behavior**
From the globally configured providers, Chat picks which one the chatbot uses: "Use DeepSeek for chatbot, fallback to OpenRouter."

```
Global (Integrations):              Chat Settings:
┌─────────────────────┐             ┌──────────────────────────────────┐
│ ✅ OpenAI  [key: ●●]│  filters →  │ Primary:  [DeepSeek ▾]          │
│ ✅ DeepSeek[key: ●●]│             │ Model:    [deepseek-chat]        │
│ ✅ OpenRouter[key:●]│             │ Fallback: [OpenRouter] → [GPT-4o]│
│ ❌ Anthropic (none) │             │ (Anthropic greyed — no key)      │
└─────────────────────┘             └──────────────────────────────────┘
```

Chat service **never stores API keys** — it only stores `AiProviderType` enum values. At runtime it reads the actual key from Redis cache `AiConfig:{TenantId}:{Provider}` (populated by `AiConfigUpdatedEvent` from Integrations).

---

## Existing Patterns — Study These Before Briefing Agents

### Backend pattern (follow exactly)
- Entity: `src/services/Integrations/NexConvo.Integrations.Domain/Entities/WorkspaceAiConfig.cs`
- CQRS commands: `src/services/Integrations/NexConvo.Integrations.Application/Features/AiConfig/Commands/`
- Controller: `src/services/Integrations/NexConvo.Integrations.Api/Controllers/WorkspaceAiConfigController.cs`
- Migration: check `src/services/Integrations/NexConvo.Integrations.Infrastructure/Persistence/Migrations/`
- Event: `src/shared/NexConvo.Contracts/Events/Integrations/AiConfigUpdatedEvent.cs`
- Encryption: `AesEncryptionService` in `src/shared/NexConvo.BuildingBlocks.Infrastructure/Security/`

### Frontend pattern (follow exactly)
- Hooks: `frontend/src/features/settings/api/use-ai-settings.ts`
- Mutation: `frontend/src/features/settings/api/use-update-ai-settings.ts`
- Schema: `frontend/src/features/settings/model/ai-settings.schema.ts`
- Types: `frontend/src/features/settings/model/ai-settings.types.ts`
- BFF: `frontend/src/app/api/bff/settings/ai-config/route.ts`
- Unit tests: `frontend/src/features/settings/api/use-ai-settings.test.tsx`

### E2E pattern (follow exactly)
- Config: `frontend/playwright.config.ts` — single worker, `.env.e2e.local`, port 3007
- Example: `frontend/e2e/email-and-invites.spec.ts` — real stack, Mailpit at localhost:8025, `nx_e2e_bypass` cookie

---

## Execution Plan — 5 Phases

Run phases sequentially. Do not start Phase 2 until Phase 1 agents both complete. And so on.

---

### PHASE 1 — Backend API (spawn 2 agents in parallel, single message)

---

**Agent A1 — Channel Connections + Chat AI Settings Backend**

> **Your role:** You are a Senior .NET backend engineer with deep expertise in Domain-Driven Design, Clean Architecture, and CQRS/MediatR. You have built multi-tenant SaaS backends with PostgreSQL RLS and EF Core. You work in the `src/services/Chat/` bounded context. You are meticulous about data security — channel secrets (WhatsApp access tokens, Meta app secrets, Telegram bot tokens) are as sensitive as passwords. You never store them in plaintext.

**First, read these files:**
- `docs/ARCHITECTURE.md` (full)
- `docs/CHATBOT-ARCHITECTURE.md` (§4 Domain Model, §6 Inbound Ingestion, §13 Persistence Schema, §14 Data Privacy)
- `CLAUDE.md` (full)
- `src/services/Integrations/NexConvo.Integrations.Domain/Entities/WorkspaceAiConfig.cs` (your pattern reference)
- `src/services/Integrations/NexConvo.Integrations.Application/Features/AiConfig/Commands/SaveAiConfigCommand.cs`
- `src/services/Integrations/NexConvo.Integrations.Api/Controllers/WorkspaceAiConfigController.cs`

**Build 1 — Channel Connections (in `src/services/Chat/`)**

Domain entity `NexConvo.Chat.Domain/Entities/ChannelConnection.cs`:
```csharp
// Aggregate root — one row per channel account per tenant
// LeadSourceChannel: WhatsApp | Facebook | Instagram | Telegram | Web
// ExternalAccountId: phone number ID (WhatsApp), page ID (FB/IG), bot username (Telegram), null (Web)
// EncryptedAccessToken: AES-256 encrypted, decrypt only at use-time
// EncryptedAppSecret: AES-256 encrypted, used for HMAC signature verification
// VerifyToken: random string set once, used in webhook registration handshake
// IsActive: soft disable without deleting webhook registration
// DisplayName: admin-set friendly name for the connected account
```

CQRS layer in `NexConvo.Chat.Application/Features/ChannelConnections/`:
- `GetChannelConnectionsQuery` + handler — returns all connections for the tenant, **never decrypts secrets in the DTO** (return masked: `"●●●●●●" + last4`)
- `SaveChannelConnectionCommand` + validator + handler — upsert by `(TenantId, Channel, ExternalAccountId)`, encrypt secrets before persistence
- `DeleteChannelConnectionCommand` + handler — soft delete (`IsActive = false`) not hard delete
- `TestChannelConnectionCommand` + handler — decrypt token, make a real API call to verify it's valid (WhatsApp: GET phone number info; Telegram: getMe; Facebook: GET /me; Web widget: return success immediately)

Controller `NexConvo.Chat.Api/Controllers/ChannelConnectionsController.cs`:
- `GET /api/v1/channel-connections` — list all for tenant
- `POST /api/v1/channel-connections` — save/upsert
- `DELETE /api/v1/channel-connections/{id}` — soft delete
- `POST /api/v1/channel-connections/{id}/test` — verify credentials

EF migration for `channel_connections` table:
- `id uuid PK`, `tenant_id uuid NOT NULL`, `channel smallint`, `external_account_id text`, `display_name text`, `encrypted_access_token text NOT NULL`, `encrypted_app_secret text`, `verify_token text NOT NULL`, `is_active bool DEFAULT true`, `created_at timestamptz`, `updated_at timestamptz`, `xmin xid` (optimistic concurrency)
- Index: `(tenant_id, channel, external_account_id)` UNIQUE WHERE `is_active = true`
- RLS policy: `USING (tenant_id = current_setting('app.current_tenant_id')::uuid)`

Contract event `src/shared/NexConvo.Contracts/Events/Chat/ChannelConnectionUpdatedEvent.cs`:
- `TenantId`, `ChannelConnectionId`, `Channel`, `ExternalAccountId`, `IsActive` — no secrets in events

**Build 2 — Chat AI Settings (in `src/services/Chat/`)**

Domain entity `NexConvo.Chat.Domain/Entities/WorkspaceChatSettings.cs`:
```csharp
// One row per tenant — created with sensible defaults on first chatbot activation
// PrimaryProvider: AiProviderType enum (which globally-configured provider chatbot uses)
// PrimaryModel: string e.g., "deepseek-chat", "claude-opus-4-8"
// FallbackProviders: AiProviderType[] stored as JSONB — ordered list tried if primary fails
// SystemPromptOverride: nullable — if null, chatbot uses no system prompt override
// HandoffConfidenceThreshold: double 0-1, default 0.65
// SentimentEscalationEnabled: bool, default true
// SentimentSensitivity: enum Low/Medium/High, default Medium
// TriggerPhrases: string[] JSONB, default includes Bengali escalation phrases
// MaxUnansweredMessages: int default 3
// PiiMaskingLevel: enum Off/Standard/Strict, default Standard
// DataRetentionDays: int? null=platform default (90 days)
```

CQRS layer in `NexConvo.Chat.Application/Features/ChatSettings/`:
- `GetChatSettingsQuery` + handler
- `SaveChatSettingsCommand` + validator + handler
  - Validator: `PrimaryProvider` must exist (not validate against Integrations — that's a runtime concern); `HandoffConfidenceThreshold` between 0.1 and 1.0; `TriggerPhrases` max 50 items, each max 200 chars; `DataRetentionDays` min 7 if set

Controller `NexConvo.Chat.Api/Controllers/ChatSettingsController.cs`:
- `GET /api/v1/chat-settings` — get current settings
- `PUT /api/v1/chat-settings` — save (upsert)

EF migration for `workspace_chat_settings` table:
- `id uuid PK`, `tenant_id uuid UNIQUE NOT NULL` (one per tenant), `primary_provider smallint`, `primary_model text NOT NULL`, `fallback_providers jsonb DEFAULT '[]'`, `system_prompt_override text`, `handoff_confidence_threshold float8 DEFAULT 0.65`, `sentiment_escalation_enabled bool DEFAULT true`, `sentiment_sensitivity smallint DEFAULT 1`, `trigger_phrases jsonb DEFAULT '[]'`, `max_unanswered_messages int DEFAULT 3`, `pii_masking_level smallint DEFAULT 1`, `data_retention_days int`, `created_at timestamptz`, `updated_at timestamptz`, `xmin xid`
- RLS policy same pattern as other Chat tables

Contract event `src/shared/NexConvo.Contracts/Events/Chat/ChatSettingsUpdatedEvent.cs`:
- Published on save — consumed by Chat's own RAG pipeline to re-read config

**Non-negotiables:**
- `nexconvo-enterprise-standards` skill applies to every class you write
- RLS on every table — follow the exact `RlsConnectionInterceptor` pattern from ARCHITECTURE.md
- `AesEncryptionService` — inject and use, never implement your own encryption
- `xmin` optimistic concurrency on aggregate roots
- `[Authorize]` on all controller endpoints — no `[AllowAnonymous]` except where architecture doc explicitly requires it
- Emit Serilog structured logs with `{TenantId}` and `{CorrelationId}` on every handler
- Return `Result<T>` pattern consistent with other handlers in the service

---

**Agent A2 — Knowledge Base Backend**

> **Your role:** You are a Senior .NET backend engineer specializing in document processing pipelines and vector storage. You understand pgvector, HNSW indexing, and embedding workflows. You are building the persistence and API layer for the knowledge base — the actual chunking and embedding runs asynchronously as a Hangfire job, but you build the scaffolding: entity, status machine, job stub, and all CRUD endpoints.

**First, read these files:**
- `docs/CHATBOT-ARCHITECTURE.md` (§8 Knowledge Ingestion, §12 pgvector Specifics, §13 Persistence Schema — `knowledge_documents` and `knowledge_chunks` tables)
- `docs/ARCHITECTURE.md` (§8 Background Jobs — Hangfire pattern)
- `CLAUDE.md`
- `src/services/Integrations/NexConvo.Integrations.Domain/Entities/WorkspaceAiConfig.cs` (aggregate root pattern)

**Build — Knowledge Base (in `src/services/Chat/`)**

Domain entities in `NexConvo.Chat.Domain/Entities/`:

`KnowledgeDocument.cs`:
```csharp
// Aggregate root
// SourceType enum: File | Url | Text | FaqPairs | PastChats
// Title: string
// IngestionStatus enum: Pending | Processing | Active | Failed | Inactive
// EmbeddingModel: string (model used for embedding, stored for re-embed detection)
// EmbeddingDimensions: int
// ChunkCount: int (updated after ingestion)
// ContentHash: string (SHA256 of raw content — dedup guard)
// FailureReason: string? (populated on Failed status)
// version: int (document version, increments on re-embed)
// supersedes_document_id: Guid? (prior version link)
// is_active: bool
// SourceUrl: string? (for Url type)
// OriginalFileName: string? (for File type)
```

`KnowledgeChunk.cs` (owned entity, not aggregate root):
```csharp
// KnowledgeDocumentId: Guid FK
// Ordinal: int (sequence within document)
// Content: string (the chunk text)
// TokenCount: int
// EmbeddingModel: string
// is_active: bool (false on superseded versions)
// Vector column: vector(3072) — pgvector, zero-padded for smaller models
```

CQRS layer in `NexConvo.Chat.Application/Features/KnowledgeBase/`:
- `GetKnowledgeDocumentsQuery` + handler — paginated list, filter by status and source type
- `GetKnowledgeDocumentByIdQuery` + handler — includes chunks (paginated) and version history
- `UploadKnowledgeDocumentCommand` + handler:
  - Accepts: title, sourceType, content (text/URL) or file bytes + filename
  - Creates `KnowledgeDocument` with status `Pending`
  - Enqueues `KnowledgeIngestionJob` via Hangfire
  - Returns document ID immediately (async processing)
- `DeleteKnowledgeDocumentCommand` + handler — soft delete: `is_active = false` on document + all its chunks
- `ReEmbedKnowledgeDocumentCommand` + handler — set status back to `Pending`, increment version, enqueue job

Hangfire job stub `NexConvo.Chat.Infrastructure/Jobs/KnowledgeIngestionJob.cs`:
```csharp
// Status: Pending → Processing → Active (or Failed)
// This stub only implements the status transitions with a simulated delay.
// Full chunking + embedding implementation is deferred to Phase 2 of the product build.
// Stub flow:
//   1. Set status = Processing
//   2. await Task.Delay(TimeSpan.FromSeconds(5)) // simulate work
//   3. Set status = Active, ChunkCount = 0 (no real chunks yet)
//   4. Publish KnowledgeDocumentIngestionStatusChangedEvent
// On exception: set status = Failed, FailureReason = exception message
```

Controller `NexConvo.Chat.Api/Controllers/KnowledgeBaseController.cs`:
- `GET /api/v1/knowledge-documents` — paginated list (page, pageSize, status filter, sourceType filter)
- `GET /api/v1/knowledge-documents/{id}` — detail + chunks (paginated) + version history
- `POST /api/v1/knowledge-documents` — upload (`multipart/form-data` for files, JSON for text/URL)
- `DELETE /api/v1/knowledge-documents/{id}` — soft delete
- `POST /api/v1/knowledge-documents/{id}/re-embed` — trigger re-embedding

EF migrations:
- `knowledge_documents` table: `id uuid PK`, `tenant_id uuid`, `source_type smallint`, `title text`, `ingestion_status smallint`, `embedding_model text`, `embedding_dimensions int`, `chunk_count int DEFAULT 0`, `content_hash text`, `failure_reason text`, `version int DEFAULT 1`, `supersedes_document_id uuid`, `is_active bool DEFAULT true`, `source_url text`, `original_file_name text`, `created_at timestamptz`, `updated_at timestamptz`, `xmin xid`
- RLS on `knowledge_documents`: `USING (tenant_id = current_setting('app.current_tenant_id')::uuid)`
- `knowledge_chunks` table: `id uuid PK`, `knowledge_document_id uuid FK`, `tenant_id uuid`, `ordinal int`, `content text`, `token_count int`, `embedding_model text`, `embedding vector(3072)`, `is_active bool DEFAULT true`, `created_at timestamptz`
- Enable pgvector extension: `CREATE EXTENSION IF NOT EXISTS vector;`
- HNSW index: `CREATE INDEX ON knowledge_chunks USING hnsw (embedding vector_cosine_ops) WITH (m = 16, ef_construction = 64);`
- RLS on `knowledge_chunks`: same pattern

Contract event `src/shared/NexConvo.Contracts/Events/Chat/KnowledgeDocumentIngestionStatusChangedEvent.cs`:
- `TenantId`, `DocumentId`, `NewStatus`, `ChunkCount`, `FailureReason?`

**Non-negotiables:** Same as Agent A1.

---

### PHASE 2 — BFF + Frontend Integration (spawn 2 agents in parallel after Phase 1 completes)

---

**Agent B1 — Channel Connections + Chat AI Settings Frontend**

> **Your role:** You are a Senior Next.js engineer who obsesses over matching designs pixel-perfectly and writing clean, type-safe API integration code. You work exclusively in the `frontend/` directory. You have read `docs/prompt/chatbot-ui-ux-design.md` Surface 4 (Channel Connections) and Surface 5 (AI Configuration) and your implementation must match them exactly — no improvisation. You follow the established frontend patterns in this codebase without exception.

**First, read these files:**
- `docs/prompt/chatbot-ui-ux-design.md` (Surface 4 and Surface 5 in full)
- `CLAUDE.md`
- `frontend/src/features/settings/api/use-ai-settings.ts` (your hook pattern)
- `frontend/src/features/settings/model/ai-settings.schema.ts` (your schema pattern)
- `frontend/src/app/api/bff/settings/ai-config/route.ts` (your BFF pattern)
- `frontend/src/shared/api/server/bff.ts` (BFF helper utilities)

**Build 1 — Channel Connections**

Types `frontend/src/features/settings/model/channel-connection.types.ts`:
```typescript
export type ChannelType = 'whatsapp' | 'facebook' | 'instagram' | 'telegram' | 'web'
export type ConnectionStatus = 'connected' | 'disconnected' | 'error'

export interface ChannelConnectionDto {
  id: string
  channel: ChannelType
  displayName: string
  externalAccountId: string | null
  status: ConnectionStatus
  errorMessage: string | null
  isActive: boolean
  createdAt: string
  // access token is NEVER returned — only masked: "●●●●1234"
  maskedAccessToken: string | null
}

export interface SaveChannelConnectionRequest {
  channel: ChannelType
  displayName: string
  externalAccountId?: string
  accessToken: string      // plaintext — encrypted server-side
  appSecret?: string       // plaintext — encrypted server-side
  verifyToken?: string
}

export interface TestChannelConnectionResponse {
  success: boolean
  accountName?: string     // e.g. WhatsApp business name
  errorMessage?: string
}
```

Zod schema `frontend/src/features/settings/model/channel-connection.schema.ts`:
- Validate `SaveChannelConnectionRequest` — `accessToken` required and non-empty; `externalAccountId` required for whatsapp/facebook/instagram/telegram; `appSecret` required for whatsapp/facebook/instagram

BFF routes:
- `frontend/src/app/api/bff/settings/channels/route.ts` — GET (list) + POST (save)
- `frontend/src/app/api/bff/settings/channels/[id]/route.ts` — DELETE
- `frontend/src/app/api/bff/settings/channels/[id]/test/route.ts` — POST (test connection)

React Query hooks in `frontend/src/features/settings/api/`:
- `use-channel-connections.ts` — `useChannelConnections()` query
- `use-save-channel-connection.ts` — mutation, invalidates channel connections query on success
- `use-delete-channel-connection.ts` — mutation with optimistic update (remove from list immediately, restore on error)
- `use-test-channel-connection.ts` — mutation, returns `TestChannelConnectionResponse`

Settings page `frontend/src/app/(dashboard)/dashboard/settings/channels/page.tsx`:
- Implement exactly as described in `docs/prompt/chatbot-ui-ux-design.md` Surface 4
- Channel cards grid (3-col desktop, 2-col tablet, 1-col mobile)
- Connect drawer (multi-step, slides from right) — use Shadcn `<Drawer>`
- Step indicator component (1–5 steps)
- Web Widget card: color picker + welcome message + position radio + live preview pane
- Replace any placeholder/mock data with `useChannelConnections()` hook

Settings nav: add Channels entry in `frontend/src/shared/config/nav.ts` (or wherever settings nav is configured — check `frontend/src/features/settings/components/settings-nav.tsx`)

**Build 2 — Chat AI Settings**

Types `frontend/src/features/settings/model/chat-settings.types.ts`:
```typescript
export type SentimentSensitivity = 'low' | 'medium' | 'high'
export type PiiMaskingLevel = 'off' | 'standard' | 'strict'

export interface WorkspaceChatSettingsDto {
  primaryProvider: AiProviderType   // reuse existing AiProviderType from ai-settings.types.ts
  primaryModel: string
  fallbackProviders: AiProviderType[]
  systemPromptOverride: string | null
  handoffConfidenceThreshold: number  // 0-1
  sentimentEscalationEnabled: boolean
  sentimentSensitivity: SentimentSensitivity
  triggerPhrases: string[]
  maxUnansweredMessages: number
  piiMaskingLevel: PiiMaskingLevel
  dataRetentionDays: number | null
}
```

Zod schema + React Query hooks + BFF route following exact same pattern as AI settings.

Settings page — extend existing AI settings page `frontend/src/app/(dashboard)/dashboard/settings/ai/page.tsx`:

The page now has TWO sections that come from TWO different API calls:

**Section 1 — "AI Provider" (top):** uses existing `useAiSettings()` to get globally configured providers. Renders a provider selector dropdown showing only providers with `isActive: true` from the global config. Greyed-out options (with "Not configured" tooltip) for providers not configured globally. Model input. Fallback list (drag-reorder or up/down buttons). All of this saves to `/bff/settings/chat-settings` (new endpoint), NOT to the global AI config.

**Section 2 — "Chatbot Behavior" (below separator):** reads from `/bff/settings/chat-settings`:
- System Prompt Override — large textarea + token estimate + `[Test this prompt →]` opens `/playground?systemPrompt=<base64>`
- Handoff Thresholds — confidence slider (0–100%), sentiment toggle + sensitivity radio
- Trigger Phrases — tag-input component: type + Enter to add, × to remove, pre-seeded with `["আমি মানুষের সাথে কথা বলতে চাই", "speak to a manager", "human agent"]`
- PII Masking — radio group with explanation text per option
- Data Retention — number input (days), empty = platform default (90 days)

The `[Test this prompt →]` link must encode the current textarea value as base64 URL param: `router.push('/playground?systemPrompt=' + btoa(value))`

**Non-negotiables:**
- `nexconvo-frontend-standards` skill applies throughout
- No `any` types — all API responses typed
- Axios via `apiClient` (browser) and `apiServer` (BFF) — never raw fetch
- React Query for all server state — no `useState` for async data
- MSW mock handler added for every new BFF endpoint (for unit tests to use)
- All forms use React Hook Form + Zod resolver — no manual validation
- Error boundaries and loading skeletons on every data-dependent section
- i18n keys for all user-visible strings — follow the pattern in `frontend/src/shared/i18n/messages/en.json`

---

**Agent B2 — Knowledge Base Frontend**

> **Your role:** You are a Senior Next.js engineer who specializes in file upload UIs, async processing pipelines, and document management interfaces. You have read `docs/prompt/chatbot-ui-ux-design.md` Surface 3 (Knowledge Base) and your implementation must match it exactly. The most important UX detail: when a user uploads a document, they need live feedback on exactly which processing step is happening. Ambiguity kills trust.

**First, read these files:**
- `docs/prompt/chatbot-ui-ux-design.md` (Surface 3 in full)
- `CLAUDE.md`
- `frontend/src/app/(dashboard)/dashboard/chat/settings/knowledge/page.tsx` (existing mock UI — understand it, then replace mock data with real API)
- `frontend/src/features/settings/api/use-ai-settings.ts` (hook pattern)
- `frontend/src/app/api/bff/settings/ai-config/route.ts` (BFF pattern)
- `frontend/src/shared/api/server/bff.ts`

**Types** `frontend/src/features/settings/model/knowledge-document.types.ts`:
```typescript
export type SourceType = 'file' | 'url' | 'text' | 'faq_pairs' | 'past_chats'
export type IngestionStatus = 'pending' | 'processing' | 'active' | 'failed' | 'inactive'

export interface KnowledgeDocumentDto {
  id: string
  title: string
  sourceType: SourceType
  status: IngestionStatus
  chunkCount: number
  embeddingModel: string
  failureReason: string | null
  version: number
  createdAt: string
  updatedAt: string
}

export interface KnowledgeDocumentDetailDto extends KnowledgeDocumentDto {
  chunks: KnowledgeChunkDto[]
  versionHistory: KnowledgeDocumentVersionDto[]
}

export interface KnowledgeChunkDto {
  id: string
  ordinal: number
  contentPreview: string   // first 100 chars
  tokenCount: number
}

export interface KnowledgeDocumentVersionDto {
  version: number
  embeddingModel: string
  chunkCount: number
  createdAt: string
}
```

**BFF routes:**
- `frontend/src/app/api/bff/settings/knowledge/route.ts` — GET (paginated list) + POST (upload, `multipart/form-data`)
- `frontend/src/app/api/bff/settings/knowledge/[id]/route.ts` — GET (detail) + DELETE
- `frontend/src/app/api/bff/settings/knowledge/[id]/re-embed/route.ts` — POST

**React Query hooks:**
- `use-knowledge-documents.ts` — `useKnowledgeDocuments()` paginated query
- `use-knowledge-document.ts` — `useKnowledgeDocument(id)` detail query
- `use-upload-knowledge-document.ts` — upload mutation (multipart), shows upload progress via `onUploadProgress`
- `use-delete-knowledge-document.ts` — mutation, optimistic remove
- `use-re-embed-knowledge-document.ts` — mutation

**Ingestion progress polling:** When ANY document in the list has status `pending` or `processing`, set up a `useEffect` + `setInterval` that refetches `useKnowledgeDocuments()` every 3 seconds. Clear interval when no pending/processing documents remain.

**Wire up existing pages — remove ALL mock/hardcoded data:**

`frontend/src/app/(dashboard)/dashboard/chat/settings/knowledge/page.tsx`:
- Replace mock document array with `useKnowledgeDocuments()` hook
- Add Knowledge modal — 4 tabs:
  1. **Upload File**: drag-and-drop zone with `onDrop` handler, file type validation (PDF/DOCX/TXT/CSV/MD), shows filename + size, progress bar during upload using `onUploadProgress`
  2. **From URL**: URL input + Fetch button — calls BFF, shows page title preview card
  3. **Write Text / FAQ**: toggle between free text textarea and Q&A pair builder (add/remove rows)
  4. **Import Past Chats**: date range picker + "only resolved" checkbox + estimated count
- Ingestion progress step indicator (5 steps: Uploading → Extracting → Chunking → Embedding → Active) — current step pulses with `animate-pulse`
- Status badges matching Surface 3 design

`frontend/src/app/(dashboard)/dashboard/chat/settings/knowledge/[id]/page.tsx`:
- Replace mock with `useKnowledgeDocument(id)` hook
- Inline editable title (click → input → blur to save via mutation)
- Two tabs: Chunks | Version History
- Chunks table: ordinal, 40-char preview + expand button, token count
- Version History table: version, date, model, chunk count, `[Restore]` button

**Non-negotiables:** Same as Agent B1. Additionally: file uploads must show byte-level progress (not just spinner); never block the UI during upload.

---

### PHASE 3 — Tests (spawn 2 agents in parallel after Phase 2 completes)

---

**Agent C1 — Backend Unit Tests**

> **Your role:** You are a Senior .NET test engineer who practices TDD. You write tests that document behavior, not implementation. You test handler logic in isolation using in-memory EF Core, mock external dependencies (Hangfire, encryption service, event bus) with NSubstitute or Moq, and assert on domain state — not SQL output.

**Study existing test patterns before writing a single test:**
- Find existing handler tests in `src/services/Integrations/` or `src/services/Chat/` — follow their exact structure, naming, and assertion style

**Build:**

Tests for Channel Connection handlers:
- `SaveChannelConnectionCommandHandlerTests`: create new connection → secrets encrypted; update existing → new token replaces old; duplicate account on same channel → same tenant same channel same externalAccountId upserts rather than creates second row
- `TestChannelConnectionCommandHandlerTests`: valid token → decrypt + call (mock HTTP) → returns success; invalid token → returns failure with reason; Web widget → always success without HTTP call
- `DeleteChannelConnectionCommandHandlerTests`: existing → sets `IsActive = false`; non-existent → `NotFound` result
- `GetChannelConnectionsQueryHandlerTests`: returns connections for correct tenant; secrets masked in DTO (`maskedAccessToken` = "●●●●" + last4 of actual token)

Tests for Chat AI Settings handlers:
- `SaveChatSettingsCommandHandlerTests`: first save → creates row with defaults merged; update → updates only provided fields; invalid `HandoffConfidenceThreshold` (> 1.0) → validation error; too many trigger phrases (> 50) → validation error
- `GetChatSettingsQueryHandlerTests`: returns settings for correct tenant; tenant with no settings → returns defaults

Tests for Knowledge Base handlers:
- `UploadKnowledgeDocumentCommandHandlerTests`: creates document with `Pending` status; enqueues Hangfire job; content hash computed correctly; duplicate content hash for same tenant → returns existing document ID (dedup)
- `DeleteKnowledgeDocumentCommandHandlerTests`: sets `is_active = false`, does NOT hard delete
- `ReEmbedKnowledgeDocumentCommandHandlerTests`: increments version; sets status to `Pending`; enqueues job

---

**Agent C2 — Frontend Unit Tests**

> **Your role:** You are a Senior React test engineer expert in MSW + Vitest + React Testing Library. You write tests that give confidence in real HTTP behavior — not implementation details. Every test exercises a hook through a real (mocked at network layer) HTTP call. You follow the established patterns in this codebase without exception.

**Study these files before writing any test:**
- `frontend/src/features/settings/api/use-ai-settings.test.tsx`
- `frontend/src/features/settings/api/use-update-ai-settings.test.tsx`
- `frontend/src/tests/msw/server.ts`
- `frontend/src/tests/react-query.tsx`

**Build MSW handlers** in `frontend/src/tests/msw/handlers/` (or wherever existing handlers are placed):
- Handlers for every new BFF endpoint: channels list, channel save, channel delete, channel test, chat settings GET/PUT, knowledge list GET, knowledge detail GET, knowledge upload POST, knowledge delete DELETE, knowledge re-embed POST
- Include both success responses and error responses (400, 403, 409, 500)

**Build hook tests:**

`use-channel-connections.test.tsx`:
- Fetches and returns channel list on success
- Returns empty array when no connections
- Error state on 500

`use-save-channel-connection.test.tsx`:
- Success → invalidates channel connections query (verify refetch triggered)
- 409 conflict → maps to typed error
- 403 forbidden → maps to typed error

`use-test-channel-connection.test.tsx`:
- Returns `{ success: true, accountName }` on success
- Returns `{ success: false, errorMessage }` on failure
- Does not throw on failure (error is in response body, not HTTP error)

`use-chat-settings.test.tsx`:
- Returns settings on 200
- Error state on 500

`use-update-chat-settings.test.tsx`:
- Success → invalidates chat settings query
- 409 (optimistic concurrency conflict) → maps to typed error with `'conflict'` code

`use-knowledge-documents.test.tsx`:
- Returns paginated list
- Returns empty state

`use-upload-knowledge-document.test.tsx`:
- Multipart upload → success → invalidates list
- Progress callback called during upload

**Zod schema tests:**
- `channel-connection.schema.test.ts`: valid data passes; missing `accessToken` fails; `externalAccountId` required for all channels except web; `appSecret` required for meta channels
- `chat-settings.schema.test.ts`: `handoffConfidenceThreshold` must be between 0.1 and 1.0; `triggerPhrases` max 50 items; `dataRetentionDays` min 7 if provided

---

### PHASE 4 — E2E Tests (1 agent after Phase 3 completes)

---

**Agent D — Playwright E2E Engineer**

> **Your role:** You are a Senior Playwright engineer who writes tests against the real running stack. You do not mock anything at the E2E level — Docker Compose is running with real PostgreSQL, Redis, RabbitMQ, and all services. You follow the exact patterns in `frontend/e2e/email-and-invites.spec.ts`. You use `nx_e2e_bypass` cookie for auth bypass and `.env.e2e.local` for credentials.

**First, read these files completely:**
- `frontend/playwright.config.ts`
- `frontend/e2e/email-and-invites.spec.ts` (your pattern — auth, page navigation, assertions)
- `frontend/e2e/auth.spec.ts` (login helper pattern)
- `docs/prompt/chatbot-ui-ux-design.md` (Surface 3 and Surface 4 — what the UI looks like)

**Build `frontend/e2e/settings-channels.spec.ts`:**

```
Setup: sign in (reuse auth pattern from existing e2e tests), navigate to /dashboard/settings/channels

Test: "shows all channel cards in disconnected state"
  - 5 channel cards visible: WhatsApp, Facebook, Instagram, Telegram, Web Widget
  - Each shows "Not Connected" status
  - [Connect] button visible on each

Test: "can connect and disconnect a Web Widget"
  - Click [Connect] on Web Widget card
  - Step 1 drawer opens, shows instructions
  - Click Next → Step 2: fill Display Name
  - Click [Verify Credentials] → success checkmark
  - Step 3: webhook URL shown, [Copy] button works (clipboard API)
  - Step 4: [Send Test Message] → success message appears
  - Step 5: confetti/checkmark, [Close] button
  - Card now shows "Connected" status with display name

Test: "can configure Web Widget appearance"
  - Widget is connected
  - Color picker changes preview bubble color
  - Welcome message input updates preview text
  - Position radio changes bubble position in preview

Test: "shows inline error on invalid credentials"
  - Click [Connect] on Telegram card
  - Enter invalid bot token
  - Click [Verify Credentials] → error message appears inline (not a toast)
  - Form stays open, token field highlighted

Test: "can disconnect a connected channel"
  - Web Widget is connected
  - Click [Configure] → drawer opens
  - [Disconnect] button → confirmation dialog
  - Confirm → card returns to "Not Connected" state
```

**Build `frontend/e2e/settings-knowledge.spec.ts`:**

```
Setup: sign in, navigate to /dashboard/settings/knowledge (or /dashboard/chat/settings/knowledge)

Test: "shows empty state when no documents"
  - Empty state illustration visible
  - "No knowledge documents yet" text visible
  - [+ Add Knowledge] button visible

Test: "can upload a text document and see it become Active"
  - Click [+ Add Knowledge]
  - Switch to "Write Text" tab
  - Enter title: "Test Document"
  - Enter content: "This is test knowledge content."
  - Click Save
  - Modal closes
  - Document appears in list with status "Processing"
  - Wait for status to change to "Active" (poll up to 30 seconds)
  - Chunk count updates from 0 to > 0

Test: "shows ingestion progress steps during processing"
  - Upload a document
  - Immediately after upload: step indicator visible
  - At minimum "Uploading" step is highlighted

Test: "can view document detail and chunks"
  - Active document exists
  - Click [View] on the document row
  - Detail page opens
  - Chunks tab shows at least 1 chunk row
  - Chunk row expandable to show full content

Test: "can delete a document with confirmation"
  - Active document exists
  - Click [Delete] on document row
  - Confirmation dialog appears
  - Click Confirm
  - Document disappears from list
  - List shows one fewer document

Test: "can re-embed a document"
  - Active document exists
  - Click [Re-embed] on document row
  - Status changes to Processing
  - Eventually returns to Active

Test: "can view version history after re-embed"
  - Document has been re-embedded at least once (version > 1)
  - Open detail page
  - Version History tab shows 2+ rows
  - [Restore] button visible on each row
```

---

### PHASE 5 — Final Integration Report (you do this yourself)

After all agents complete, run these checks yourself:

1. `cd frontend && npm run build` — fix any TypeScript compile errors
2. `dotnet build NexConvo.sln` — fix any .NET compile errors
3. `cd frontend && npm test` — record pass/fail count
4. `cd frontend && npm run e2e -- --grep "settings"` — record pass/fail count
5. Check: does connecting a Web Widget in E2E show "Connected" status? ✓/✗
6. Check: does uploading a text document show "Active" after Hangfire job runs? ✓/✗
7. Check: does AI settings page show only globally-configured providers as selectable? ✓/✗

Write a final report to the user with:
- What was built (list of new files created)
- Test results (unit: X/Y passing, E2E: X/Y passing)
- Any deferred items (embedding implementation is deferred by design)
- Any issues found and how they were resolved

---

## Non-Negotiables (Every Agent, No Exceptions)

**Backend:**
- Read and apply the `nexconvo-enterprise-standards` skill before writing any class
- RLS policy on every new table — follow existing Integrations migration pattern exactly
- `AesEncryptionService` for secrets — never store plaintext tokens in DB
- `xmin` optimistic concurrency on aggregate roots
- Correlation ID + TenantId in every Serilog log statement
- Paginate every list endpoint — no unbounded queries
- `[Authorize]` on all controller actions
- MassTransit outbox for all published events

**Frontend:**
- Read and apply the `nexconvo-frontend-standards` skill before writing any component
- UI must match `docs/prompt/chatbot-ui-ux-design.md` exactly — no design improvisation
- No `any` TypeScript, no `// @ts-ignore`
- Axios via `apiClient`/`apiServer` only — never raw fetch
- React Query for all server state
- All forms: React Hook Form + Zod resolver
- BFF route handlers use `gatedWrite` helper for mutations
- i18n keys for all user-visible strings
- MSW handler required for every new BFF endpoint (unit tests depend on it)
- Loading skeletons (not spinners) on all data-dependent sections
- Inline errors (not toasts) for form validation failures
- Toast notifications only for async success (upload complete, connection verified)

**Tests:**
- Backend: every new handler has at least 3 tests (happy path, validation error, not-found)
- Frontend: every new hook has tests for success + error states
- E2E: no mocking — real stack only
