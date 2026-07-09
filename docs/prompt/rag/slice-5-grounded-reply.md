# NexConvo — RAG Slice 5: Grounded Reply (Chat) via Knowledge gRPC + shared `BuildingBlocks.Rag`

> Paste this entire prompt into a new Claude Code session at the NexConvo project root.
> This is **Slice 5 of 6** (see `docs/prompt/rag/README.md`). **Prerequisites: Slices 1–4 complete** (the Knowledge service exposes gRPC `SearchKnowledge`).

---

## Your Role — Senior RAG / Conversational-AI Engineer

You are a **Senior .NET engineer** specializing in RAG orchestration and conversational AI, with 10+ years in event-driven systems, gRPC clients, and LLM integration. You know the first rule of enterprise RAG: **answer only from retrieved context, cite sources, and say "I don't know" rather than hallucinate.** You also know this bot's brain will be **reused by Voice**, so you put the shared grounding logic in a building block parameterized by a `ChannelProfile`, keep the Chat-specific delivery (SignalR) out of it, and make the reply orchestrator **cancelable** (voice barge-in) and **loop-ready** (MCP tool-calling, P3) — even though this slice builds only the Chat, single-iteration, text path. You make consumers idempotent because brokers redeliver, and you audit every AI decision.

**Before writing any code**, invoke and follow:
1. `nexconvo-enterprise-standards`
2. superpowers `brainstorming` (design the orchestrator, ChannelProfile, and confidence model first)
3. superpowers `test-driven-development`

**First, read these files completely:**
- `docs/CHATBOT-ARCHITECTURE.md` — §4 Domain Model, §5 Conversation state machine, §7 RAG Reply Pipeline, §9 Handoff, §14 Data Privacy
- `docs/ARCHITECTURE.md` — eventing / MassTransit / outbox / gRPC client sections
- `CLAUDE.md`
- `src/shared/NexConvo.BuildingBlocks.Ai/Services/IAiProviderService.cs` — `GenerateStreamAsync` (the LLM call point)
- `src/services/Chat/NexConvo.Chat.Domain/Entities/WorkspaceChatSettings.cs` — handoff threshold, system-prompt override, trigger phrases
- `src/services/Integrations/.../WorkspaceAiConfig.cs` + `src/services/AiAssistant/.../AiConfigUpdatedEventConsumer.cs` — the Redis-cached per-tenant LLM config
- The Knowledge service gRPC contract from Slice 4 (`Protos/knowledge.proto`, `KnowledgeRetrieval.Search`)
- `src/shared/NexConvo.Contracts/Events/Chat/` — `MessageReceivedIntegrationEvent` (currently **no consumer**)

---

## Context — what exists and what is missing

| Item | State |
|---|---|
| Retrieval over gRPC (`KnowledgeRetrieval.Search`) | ✅ Slice 4 (in Knowledge service) |
| LLM streaming (`GenerateStreamAsync`) | ✅ Real — only used for connection testing today |
| Per-tenant LLM config, Redis-cached | ✅ Real |
| `Conversation` / `Message` / `Escalation` entities | ❌ **None** |
| `MessageReceivedIntegrationEvent` consumer | ❌ **None** |
| Grounded prompt assembly / citations / confidence | ❌ **None** |
| `BuildingBlocks.Rag` (shared grounding + `ChannelProfile`) | ❌ **None** — you create it here |
| Handoff runtime; MassTransit **outbox** | ❌ **None** (outbox missing platform-wide) |

## Prerequisites
- **Slices 1–4 done.** A tenant with an ingested KB and a configured, healthy LLM provider; the Knowledge gRPC reachable from Chat.

---

## What to build

### 1. `BuildingBlocks.Rag` — the shared, channel-parameterized "A" of RAG (new shared project)
- `ChannelProfile` (`Chat` | `Voice`) carrying the channel knobs: `TopK`, `MinScore`, `MaxAnswerTokens`, `EmitCitations`, `Register` (formal-written vs colloquial-spoken), `StreamGranularity` (token vs phrase).
- `IGroundedPromptAssembler` that accepts a **list of context contributions** (RAG chunks today; MCP tools later) + conversation history + user question + a `ChannelProfile`, and produces the final prompt. It bakes in: "answer only from the numbered context; if absent, say you don't know; reply in the user's language" — and, driven by the profile, **Chat** → allow length + `[n]` citations; **Voice** → 1–2 spoken sentences, **no citations/lists/links**, colloquial register.
- A token **budgeter** that trims history / caps `TopK` to fit the model window.
- This project is referenced by Chat now, and by Voice later — **no SignalR, no gRPC, no channel I/O inside it** (pure logic).

### 2. Chat domain — conversation model (`src/services/Chat/`)
- `Conversation` aggregate (state `AiHandling → PendingHuman → HumanHandling → Resolved → Closed`); RLS; `xmin`; audit.
- `Message` (sender role Contact/Ai/Agent/System; body; sentAt; delivery status; optional `confidence`). **Design the sender/type to allow future `ToolCall`/`ToolResult` without a migration rewrite** (open sender-type + optional structured-payload column).
- Migrations with RLS matching existing Chat tables.

### 3. Knowledge gRPC client (in Chat)
- Register a typed `KnowledgeRetrieval` gRPC **client** (from Slice 4's `.proto`) with **Polly**. Propagate `tenant_id` + `traceparent` via gRPC metadata on every call.

### 4. Inbound consumer → reply command
- A `MessageReceived` consumer persists the inbound `Message`, then dispatches `GenerateRagReplyCommand`. **Idempotent** on the inbound message id (inbox dedupe).

### 5. `IReplyOrchestrator` — the cancelable, loop-ready generation seam
- Today it runs **one iteration**: `Search` (gRPC, with `ChannelProfile.Chat` `TopK`/`MinScore`) → `IGroundedPromptAssembler` → `GenerateStreamAsync` (tenant's LLM from the Redis cache) → compute `RagConfidence` (from top retrieval score + whether the model abstained) → persist reply `Message` + **audit** + publish events via **outbox**.
- **Structure it as a loop that today iterates once** so a future MCP **agentic loop** (LLM → tool call → execute → feed result → continue) is an insertion, not a rewrite.
- **Cancelable:** thread a `CancellationToken` through generation so a caller (voice barge-in later) can stop mid-reply.
- Model the outcome as a small result type `Answer | Handoff` that can grow a `ToolCall` case later — don't bury the branch in an `if`.

### 6. Handoff
- If `RagConfidence < WorkspaceChatSettings.HandoffConfidenceThreshold` **or** a configured trigger phrase matches, transition to `PendingHuman` and publish a handoff event **instead of** sending the AI reply. (The threshold is per-channel — Voice will use a stricter one via its `ChannelProfile`.)

### 7. MassTransit outbox (platform gap — introduce here)
- Wire the EF/MassTransit transactional outbox for Chat so this slice's events publish only after commit (Standard 10). Document the pattern for other services.

---

## 🎙️ Voice-readiness checklist (design now, build in a future Voice slice)
- Grounding logic is in `BuildingBlocks.Rag` (shared) — Voice reuses it with `ChannelProfile.Voice`.
- The orchestrator is **cancelable** (barge-in) and takes a `ChannelProfile` — Voice passes its own.
- Retrieval is a **gRPC** call with per-channel `TopK`/`MinScore` — Voice sends smaller values for <700 ms.
- **Do NOT** put SignalR or any Chat-only delivery inside `BuildingBlocks.Rag` or the orchestrator core.

## Reuse — study these patterns first
- `IAiProviderService` / `AiProviderFactory`; `WorkspaceChatSettings`; `WorkspaceAiConfig` + Redis cache; the Slice 4 gRPC contract; existing MediatR + audit patterns.

## Out of scope (later slices)
SignalR streaming to the UI (Slice 6 — this slice may persist/return the full reply and expose the token stream); sentiment analysis; the actual Voice caller; MCP/tools (P3).

---

## Tests first (TDD)
1. **Grounded answer:** a question answered by a seeded chunk → reply cites `[n]` (Chat profile) and matches; confidence high.
2. **ChannelProfile:** the same input under `ChannelProfile.Voice` yields a short, citation-free, colloquial answer (assert the assembler output differs).
3. **Don't-know / handoff:** an out-of-scope question → model abstains, low confidence, `PendingHuman`, handoff event; no AI reply sent.
4. **Trigger phrase:** immediate handoff.
5. **Idempotency:** redelivered `MessageReceived` → no duplicate message/reply.
6. **Cancelation:** cancelling the token mid-generation stops the stream cleanly.
7. **Audit + RLS + outbox:** every reply audited; conversations tenant-isolated; events not on the bus until commit.

## Non-Negotiables
- Clean Architecture/CQRS; constructor injection; no `DbContext` in the API layer; `BuildingBlocks.Rag` has no I/O.
- RLS + tenant from `ITenantContext`; tenant propagated on the gRPC call; `xmin` concurrency.
- Polly on the LLM + gRPC calls; **idempotent** consumers; **outbox** for events; **audit** every AI decision (reusable for future tool executions).
- Structured logging with `CorrelationId`/`TenantId`; **never log message content/PII** — log `ConversationId`, `MessageId`, confidence, `Handoff`.

## Verification / report
1. Publish a `MessageReceived` for a tenant whose KB has a refund-policy chunk → grounded, cited, high-confidence reply persisted.
2. Run the same through `ChannelProfile.Voice` in a test → short, citation-free answer.
3. Publish an unrelated question → `PendingHuman` + handoff, no AI reply. Redeliver the first → no duplicate.
4. Report: `BuildingBlocks.Rag` design, the orchestrator (cancelable + loop-ready) location, gRPC client wiring, outbox setup, test pass count.
