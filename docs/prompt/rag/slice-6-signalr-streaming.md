# NexConvo — RAG Slice 6: SignalR Streaming + Real-Time Handoff

> Paste this entire prompt into a new Claude Code session at the NexConvo project root.
> This is **Slice 6 of 6** (see `docs/prompt/rag/README.md`). **Prerequisite: Slice 5 complete** (RAG replies are generated + persisted and expose a token stream).

---

## Your Role — Senior Real-Time Systems Engineer

You are a **Senior .NET engineer** specializing in real-time systems, with 10+ years on SignalR, WebSockets, and horizontally-scaled fan-out over a Redis backplane. You know that in a support product the difference between "the bot is thinking" and a live, token-by-token reply is the difference between trust and abandonment. You also know real-time is a security surface: a hub group is a channel, and a user must never join a group for a tenant or conversation they don't belong to. You wire the LLM token stream to the client with zero buffering, you fan handoff events to the right agent dashboards, and you make sure the gateway actually passes WebSockets through.

**Before writing any code**, invoke and follow:
1. `nexconvo-enterprise-standards`
2. superpowers `test-driven-development`

**First, read these files completely:**
- `docs/CHATBOT-ARCHITECTURE.md` — §3 Real-Time Fabric, §7 RAG Reply Pipeline (streaming), §9 Handoff
- `docs/ARCHITECTURE.md` — SignalR + Redis backplane + Gateway sections
- `CLAUDE.md`
- `src/shared/NexConvo.Contracts/Events/Chat/ChannelConnectionUpdatedEvent.cs` — references a future SignalR hub
- The Gateway config: `src/gateway/NexConvo.Gateway/appsettings.json` + `appsettings.Development.json` (YARP clusters/routes)
- From Slice 5: `GenerateRagReplyCommandHandler` and its token stream + handoff events

---

## Context — what exists and what is missing

| Item | State |
|---|---|
| RAG replies generated + persisted, token stream available | ✅ Slice 5 |
| Any **SignalR Hub** anywhere in the codebase | ❌ **None** (`AddSignalR`, `: Hub`, `MapHub` all absent) |
| Redis (for the backplane) | ✅ Running (`redis:7-alpine`) |
| Gateway routing for a hub | ❌ **Not configured** |

Per architecture, real-time chat uses **SignalR over WebSockets with a Redis backplane — never SSE or polling.**

> **Chat-only delivery.** SignalR is the **Chat** channel's delivery mechanism. **Voice does NOT use SignalR** — the Voice bot delivers via Cartesia **TTS** (a separate future Voice slice). The shared brain (Slice 5's `IReplyOrchestrator` + `BuildingBlocks.Rag`) already knows nothing about SignalR; this slice adds the chat-specific transport on top of that channel-neutral core. Keep it that way — do not push SignalR concerns down into the orchestrator or `BuildingBlocks.Rag`.

## Prerequisites
- **Slice 5 done.** The reply orchestrator is channel-neutral, cancelable, and exposes tokens as they arrive.

---

## What to build (in `src/services/Chat/` + Gateway)

### 1. `ChatHub` (SignalR)
- Groups: `conv:{conversationId}` (the customer + assigned agent view a conversation) and `tenant:{tenantId}:agents` (the agent dashboard fan-out).
- **Redis backplane** so tokens fan out across pods (reuse the existing Redis connection).
- **Deny-by-default authorization** on the hub; tenant from the JWT `tenant_id` claim (never from the client).
- **Membership checks:** on `JoinConversation`, verify the caller belongs to the tenant **and** is a participant/assigned agent of that conversation before adding them to `conv:{id}`. A user of another tenant must be refused.

### 2. Wire the token stream (Slice 5 → Hub)
- As `GenerateRagReplyCommandHandler` receives tokens from `GenerateStreamAsync`, fan each token to `conv:{conversationId}` as it arrives (no buffering the whole reply).
- Emit a final **"reply complete"** event carrying citations + `RagConfidence`.
- Keep persistence (Slice 5) authoritative — streaming is a delivery concern, not the source of truth.

### 3. Handoff fan-out
- On a `PendingHuman` transition (Slice 5), broadcast a state-change/handoff event to `tenant:{tenantId}:agents` so dashboards light up in real time. Persist first, then broadcast.

### 4. Gateway (YARP) — route the hub (Standard 21)
- Add the cluster/route so the frontend can reach `ChatHub`, and confirm **WebSocket passthrough** is enabled (YARP + WebSockets) in `appsettings.json` / `appsettings.Development.json`.

### 5. 🔮 Extensible event envelope (MCP forward-compat — design, don't build)
- Define the hub→client messages as a **typed event envelope** (e.g. `{ type, payload }` with types `token`, `complete`, `handoff`), **not** raw text tokens. MCP (P3) will add `tool_executing` / `action_complete` status events so the customer sees "booking your appointment…" mid-reply. Shape the envelope now so those are new `type` values, not a protocol rewrite. Do not build the MCP events here — just don't lock the protocol to text-only.

---

## Reuse — study these patterns first
- Redis configuration already registered in the Chat service (for the backplane).
- The existing Gateway cluster/route entries as the shape for the new hub route.
- Slice 5's reply handler + handoff events.

## Out of scope
Frontend chat UI wiring beyond a smoke check; sentiment (P2); MCP/tools (P3).

---

## Tests first (TDD)
1. **Streaming:** a client joined to `conv:{id}` receives a sequence of token messages followed by a single "complete" event (with citations + confidence).
2. **Authorization:** a user of **tenant B** attempting to join a **tenant A** conversation group is refused.
3. **Handoff fan-out:** a `PendingHuman` transition broadcasts to `tenant:{tenantId}:agents`.
4. **Backplane:** (integration, if feasible) a message published on one node reaches a client connected to another node.

## Non-Negotiables
- SignalR over WebSockets + Redis backplane — no SSE/polling.
- Deny-by-default hub authorization; tenant from JWT; per-group membership checks.
- Structured logging (`"Streamed reply to {ConversationId}"`), **no message content/PII** in logs.
- Gateway updated (Standard 21) — an unrouted hub is unreachable from the frontend.

## Verification / report
1. Run the backend (Compose: postgres/redis/rabbitmq + services) and either the frontend chat view or a minimal WebSocket client.
2. Send an inbound message → watch the AI reply **stream token-by-token** live; confirm the "complete" event carries citations + confidence.
3. Trigger a low-confidence/trigger-phrase message → confirm the agent group is notified in real time.
4. Attempt a cross-tenant group join → confirm refusal.
5. Report: hub + group design, gateway route change, test pass count, and confirmation of live streaming + cross-tenant refusal.
```

---

## 🎉 You've reached the end of the P0 RAG build

After this slice, the knowledge base is fully functional end-to-end: **upload → store → extract → chunk → embed → retrieve → ground → generate → stream**, with citations, confidence-based handoff, RLS isolation, audit, and resilience.

**Next prompt sets to write (see `docs/prompt/rag/README.md` backlog):** P1 hybrid search + reranking, P2 guardrails + eval harness, P3 MCP/agentic actions, and the AES-GCM secrets hardening.
