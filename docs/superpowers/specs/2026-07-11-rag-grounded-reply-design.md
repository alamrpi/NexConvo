# RAG Grounded Reply Orchestrator (Slice 5) — Design

**Date:** 2026-07-11
**Status:** Approved (brainstorming)
**Branch:** feature/chat

## Context

Slices 1–4 built the pieces a grounded chatbot reply needs but never connected them: a
swappable embedding provider (Slice 1), the Knowledge service with real document ingestion
and Bengali-aware chunking (Slices 2–3), and gRPC retrieval (`KnowledgeRetrieval.Search`,
Slice 4). Chat, meanwhile, has only settings/channel-connection management — no
`Conversation`, no `Message`, no consumer for the (already-defined but never consumed or
published) `MessageReceivedIntegrationEvent`, no gRPC client anywhere in the solution, and
no MassTransit outbox anywhere in the solution despite it being documented in
`docs/ARCHITECTURE.md` §8/§9 as the prescribed reliable-eventing pattern.

This slice closes that gap: given an inbound message on a conversation, retrieve grounded
context from Knowledge, assemble a citation-backed prompt, generate a reply through the
tenant's configured LLM, decide confidence, and either answer or hand off to a human —
audited, idempotent, and transactional. It also stands up the shared, channel-parameterized
grounding logic (`BuildingBlocks.Rag`) that Voice will reuse later with stricter latency and
a spoken-register profile, without building Voice itself.

**Goal:** a Chat-only, single-iteration, text RAG reply pipeline that is structurally ready
for the two things known to be coming later — an MCP tool-calling loop and a Voice channel —
without needing a rewrite when they land.

## Scope

**In scope:**
- `BuildingBlocks.Rag` — channel-parameterized grounding logic (pure, no I/O).
- Chat's `Conversation` / `Message` / `Escalation` domain model, RLS migrations.
- A Knowledge gRPC client in Chat (first gRPC client in the solution).
- `MessageReceivedConsumer` (idempotent) → `GenerateRagReplyCommand`.
- `IReplyOrchestrator`: retrieval → grounded prompt → LLM generation → confidence →
  answer-or-handoff, audited, published via the (also newly-wired) MassTransit outbox.
- Handoff on low confidence or trigger phrase.

**Explicitly out of scope (follow-up slices):**
- Real inbound webhook ingestion (WhatsApp/Facebook/Instagram/Telegram/etc. receivers —
  CHATBOT-ARCHITECTURE.md §6). Nothing today publishes `MessageReceivedIntegrationEvent`;
  this slice adds the first consumer only. Verification publishes the event directly.
- SignalR delivery to the UI (Slice 6).
- The actual Voice caller, sentiment analysis, MCP/tool execution (Phase 3).
- True multi-provider fallback (see Decisions, "Fallback providers").

## Decisions

**1. `MessageReceivedIntegrationEvent` is widened additively.** It currently carries
`ConversationId`, `TenantId`, `Channel`, `ExternalSenderId`, `MessageRef` — no message body.
Since ingestion is out of scope, nothing else can supply inbound text to the consumer. Add
`required string Body` and `string? ProviderMessageId`. This is additive (Standard 19-safe)
and the event has zero consumers/publishers today, so there's no compatibility risk — and it
matches the shape a real webhook receiver would naturally populate later (payload → event
fields, no extra hop).

**2. Knowledge gRPC client: copy the `.proto`, client-only.** Chat gets its own copy of
`knowledge.proto` under `NexConvo.Chat.Infrastructure/Protos/`, compiled with
`GrpcServices="Client"`. No project reference to `NexConvo.Knowledge.Api`. This keeps
services autonomous per ARCHITECTURE.md §3 ("only Contracts/BuildingBlocks are shared") —
Voice will do the same later. Drift risk between the two copies is a known, accepted gap;
a Pact/contract test is a follow-up, not solved here.

**3. `IReplyOrchestrator` lives in Chat's Application layer, not `BuildingBlocks.Rag`.**
`BuildingBlocks.Rag` stays pure logic — `ChannelProfile`, prompt assembly, token budgeting —
zero I/O, so Voice can depend on it without pulling in gRPC/EF/MassTransit. The orchestrator
does I/O (gRPC, LLM call, persistence) and belongs where Chat's other Application-layer
handlers live. It depends only on interfaces (`IKnowledgeRetrievalClient`,
`IAiProviderFactory`, `IChatDbContext`) per Standard 1/2.

**4. Answer/Handoff outcome is a small sealed-record hierarchy**, not an overload of the
CQRS `Result<T>` type (which represents success/failure of a command, not two co-equal
domain outcomes):
```csharp
public abstract record ReplyOutcome;
public sealed record AnsweredOutcome(Guid MessageId, string Text, RagConfidence Confidence) : ReplyOutcome;
public sealed record HandoffOutcome(Guid EscalationId, EscalationReason Reason) : ReplyOutcome;
```
`GenerateRagReplyCommandHandler` is a thin MediatR shim: call the orchestrator, `switch` on
`ReplyOutcome`, map to `Result`.

**5. Abstention detection uses an explicit marker, not fuzzy matching.** The system prompt
(baked into `IGroundedPromptAssembler`) instructs the model: "if the context does not
contain the answer, respond with exactly `[[NO_ANSWER]]` and nothing else." The orchestrator
checks for that literal prefix post-stream. This is concrete and testable, and avoids
locale-dependent string sniffing (important given Bengali is a first-class requirement).

**6. Idempotency via MassTransit's EF Core inbox**, not a hand-rolled dedupe table.
`AddEntityFrameworkOutbox<ChatDbContext>()` configures both the outbox (publish-after-commit)
and inbox (consume-dedupe by transport `MessageId`) against the same `ChatDbContext` — the
standard MassTransit mechanism for Standard 18, no bespoke infrastructure.

**7. Fallback providers: retry the primary only; note the gap.**
`WorkspaceChatSettings.FallbackProviders` already exists as a field, but Chat only ever
receives one decrypted API key per tenant (the primary provider's, via the
Redis-cached `AiConfigUpdatedEvent`). True cross-provider fallback needs Integrations to
publish keys for each configured fallback provider too — an upstream schema change, out of
scope here. This slice retries/circuit-breaks the primary provider only
(`.AddStandardResilienceHandler()`); on exhausted retries it hands off to a human rather than
silently no-op'ing an unimplementable fallback. `FallbackProviders` remains forward-looking
schema, documented as not yet actionable.

**8. Token budgeting uses a real tokenizer.** `Microsoft.ML.Tokenizers` +
`Microsoft.ML.Tokenizers.Data.Cl100kBase` are already centrally versioned and used by
Knowledge's `BengaliAwareChunker` — `BuildingBlocks.Rag`'s token budgeter reuses
`TiktokenTokenizer.CreateForEncoding("cl100k_base")` rather than a chars/4 approximation.

**9. Outbox publish mechanism follows existing precedent, and incidentally closes a gap.**
Chat has no domain-event-draining interceptor anywhere today — existing handlers (e.g.
`SaveChannelConnectionCommandHandler`) call `db.SaveChangesAsync()` then
`IPublishEndpoint.Publish(...)` directly. This slice follows that same shape for the handoff
event. The difference: once `AddEntityFrameworkOutbox<ChatDbContext>()` is registered,
MassTransit transparently routes `IPublishEndpoint.Publish` calls made inside a
DbContext-scoped unit of work through the outbox table — so this call site becomes
transactional with its preceding `SaveChangesAsync` with no code-shape change. This fixes the
non-transactional-publish gap for the RAG/handoff path specifically; other existing handlers
keep their current shape (retrofitting them is a follow-up, not this slice).

**10. Query embedding is entirely Knowledge's responsibility.** Chat sends raw query text
over gRPC; `SearchKnowledgeQueryHandler` embeds it server-side via the platform embedder
(BGE-M3/Cohere). Chat's orchestrator does not need `IEmbeddingProviderService` at all — this
significantly simplifies its dependency surface versus what CHATBOT-ARCHITECTURE.md §7.3
originally described (which assumed a per-workspace embedding key that doesn't exist).

## Architecture overview

```
MessageReceivedIntegrationEvent (widened: +Body, +ProviderMessageId)
        │ (published directly for now — real webhook ingestion is a follow-up slice)
        ▼
┌─────────────────────────┐
│ MessageReceivedConsumer │  idempotent (MassTransit EF inbox, dedupe by MessageId)
│ (Chat.Application)      │  resolve-or-create Conversation, AppendInbound, dispatch command
└───────────┬─────────────┘
            │ MediatR: GenerateRagReplyCommand(conversationId)
            ▼
┌──────────────────────────────────────────────────────────────────────┐
│ ReplyOrchestrator (Chat.Application/Rag)                               │
│  1. Load Conversation + history + WorkspaceChatSettings                │
│     — short-circuit if not AiHandling                                  │
│  2. Read AiConfig:{TenantId} from Redis — HandoffOutcome(NoAiConfig)   │
│     if missing                                                         │
│  3. Trigger-phrase check — HandoffOutcome(TriggerPhrase), no LLM call  │
│  4. gRPC: IKnowledgeRetrievalClient.SearchAsync (ChannelProfile.Chat)  │──▶ Knowledge service
│  5. ITokenBudgeter.Fit + IGroundedPromptAssembler.Build*Prompt          │   (BuildingBlocks.Rag,
│  6. Decrypt primary provider key; IAiProviderService.GenerateStreamAsync│    pure logic, reused
│     (resilient, cancelable)                                            │    later by Voice)
│  7. RagConfidence.FromRetrievalAndAbstention(topScore, [[NO_ANSWER]])  │
│  8. Below threshold / abstained -> RequestHandoff, persist Escalation  │
│     + audit, SaveChangesAsync (outbox-transactional)                   │
│  9. Else -> AppendAiReply, persist audit, SaveChangesAsync             │
│     (outbox-transactional)                                             │
└──────────────────────────────────────────────────────────────────────┘
            │
            ▼
     ReplyOutcome (Answered | Handoff) ── mapped to Result by the thin command handler
```

`BuildingBlocks.Rag` (new shared project): `ChannelProfile` (`Chat`/`Voice` presets — TopK,
MinScore, MaxAnswerTokens, EmitCitations, register, stream granularity),
`IGroundedPromptAssembler`, `ITokenBudgeter`. No SignalR, no gRPC, no channel I/O — Voice
depends on this project later with zero change.

## Data model

- **`Conversation`** (aggregate root): `ChannelIdentity` (channel + external conversation id,
  owned type), `ConversationState` (`AiHandling → PendingHuman → HumanHandling → Resolved →
  Closed`, `Reopen` back to `AiHandling`), `ContactId?`, `AssignedAgentUserId?`,
  `LastInboundProviderMessageId?`. Methods: `AppendInbound`, `AppendAiReply` (throws
  `AiReplySuppressedException` unless `AiHandling` — the AI-suppression invariant),
  `AppendAgentReply`, `RequestHandoff`, `TakeOver`, `Resolve`, `Reopen`, `Close`. Guard
  clauses throw `InvalidConversationStateTransitionException` on illegal transitions.
- **`Message`**: `MessageSender` (owned type: `Role` ∈ `{Contact, Ai, Agent, System,
  ToolCall, ToolResult}` — the last two unused today but present now so a future MCP
  tool-calling loop doesn't need a migration rewrite — `DisplayRef`, `UserId?`), `Direction`,
  `Body`, `ProviderMessageId?`, `DeliveryStatus`, `Confidence?`, and a nullable
  `StructuredPayload` JSONB column (GIN-indexed, unused today) reserved for future
  `ToolCall`/`ToolResult` payloads.
- **`Escalation`**: `ConversationId`, `Reason` (`EscalationReason`: `LowConfidence`,
  `TriggerPhrase`, `SentimentNegative`, `MaxUnansweredExceeded`, `ExplicitAgentRequest`,
  `NoAiConfig`), `TriggeredBy`, `RaisedAt`, `AcceptedByUserId?`, `AcceptedAt?`, `ResolvedAt?`.
- **`RagConfidence`** (value object): `Score (0..1)`, `Band` (`High`/`Medium`/`Low`) — banded
  from retrieval top-score, forced `Low` on model abstention.

All three tables get Chat's existing RLS convention (snake_case, non-throwing
`current_setting('app.current_tenant_id', true)`, `xmin` optimistic concurrency per Standard
16, audit row on every mutation per Standard 14). The MassTransit outbox/inbox tables
(`InboxState`, `OutboxMessage`, `OutboxState`) are infrastructure-internal and intentionally
not RLS-scoped — tenant isolation is enforced by the business tables they carry.

## Testing

Mapped to the 7 TDD scenarios from the slice brief:

| # | Scenario | Level |
|---|---|---|
| 1 | Grounded answer, cited, high confidence | Application unit (fakes) + Integration |
| 2 | `ChannelProfile.Voice` differs from Chat | `BuildingBlocks.Rag` unit (no orchestrator needed — Voice ingestion doesn't exist) |
| 3 | Don't-know → handoff, no AI reply sent | Application unit |
| 4 | Trigger phrase → immediate handoff, no LLM call | Application unit |
| 5 | Idempotency on redelivery | Integration (real MassTransit `ITestHarness` + Testcontainers Postgres) |
| 6 | Cancelation mid-stream | Application unit |
| 7 | Audit + RLS + outbox | Integration |

Domain-level state machine and confidence-banding logic get their own focused unit tests
(`ConversationStateMachineTests`, `RagConfidenceTests`) ahead of the orchestrator tests.

## Non-goals / follow-ups

- Real webhook ingestion (§6) — separate slice.
- Multi-provider fallback — needs an Integrations-side change to publish per-fallback-provider
  keys.
- Proto drift protection (Pact/contract test between Chat's copy and Knowledge's source).
- Retrofitting older Chat handlers to the now-transactional outbox-backed publish.
- A consumer for `ConversationHandoffRequestedIntegrationEvent` (agent notification) —
  publishing only in this slice.