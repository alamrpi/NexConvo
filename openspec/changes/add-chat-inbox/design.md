## Context

The Chat service already has a complete `Conversation`/`Message`/`Escalation` domain model (`NexConvo.Chat.Domain`) with a working AI/human handoff state machine (`AiHandling → PendingHuman → HumanHandling → Resolved/Closed`, `Reopen` back to `AiHandling`), fully RLS-scoped per tenant. `ChatHub` (`/hubs/chat`) already has `conv:{conversationId}` and `tenant:{tenantId}:agents` SignalR groups and an extensible `ChatEventEnvelope{Type, Payload}` scheme, currently fed only by the durable RAG auto-reply pipeline (`ReplyOrchestrator` → `SignalRReplyStreamSink`). None of this is exposed to agents: there is no REST/CQRS surface to list conversations, read message history, reply, or change conversation state, and the frontend inbox (`dashboard/chat/inbox/page.tsx`) runs entirely on `MOCK_CONVERSATIONS`.

Separately, `WidgetHub` (the only channel with a working inbound pipeline today) streams RAG replies to the anonymous visitor ephemerally — it never creates a `Conversation` or persists a `Message`. So even the one channel that "works" end-to-end for the AI would not appear in a real inbox yet.

This design closes both gaps for a single channel (Web widget) end-to-end, while keeping every seam channel-agnostic so WhatsApp/Facebook/etc. can be added later by a new webhook receiver alone.

## Goals / Non-Goals

**Goals:**
- Expose conversation list, message history, agent reply, and state-transition (take-over/resolve/reopen) operations via authenticated REST + MediatR, reusing existing domain methods.
- Fan out these actions live to `ChatHub` subscribers using the existing envelope/group scheme — no new hub, no new groups.
- Make the web widget's inbound path persist `Conversation`/`Message` rows via the existing `MessageReceivedIntegrationEvent` → `MessageReceivedConsumer` seam, so widget conversations are indistinguishable from any other channel's conversations once in the inbox.
- Replace the frontend inbox page's mock data with real data via new hooks and a shared, reusable SignalR connection hook.
- Document the exact seam a new channel must implement (webhook receiver → `MessageReceivedIntegrationEvent`; outbound sender adapter) so it "auto-wires" into everything built here.
- Remove the superseded `(chat)` route group and its dead components.

**Non-Goals:**
- Implementing any new channel's webhook receiver or outbound sender (WhatsApp, Facebook, Instagram, Telegram, TikTok, LinkedIn, SMS, Email, Voice) — only the extension pattern is documented.
- Conversation search, advanced filtering, or analytics wiring beyond what the existing mock-driven UI already renders (the 5 filter tabs, channel pills, search box).
- Changing `ChannelConnectionsController`/connection-health flows — those already exist and are untouched.
- Any change to the AI auto-reply pipeline (`ReplyOrchestrator`, `GroundingGate`, `AbstentionStreamFilter`) beyond adding new envelope event types alongside the existing ones.

## Decisions

### 1. New CQRS slice: `Features/Conversations/` in `NexConvo.Chat.Application`
Mirrors the existing `Features/ChannelConnections/` and `Features/ChatSettings/` shape (Commands/Queries + Handler + Validator + Dto per operation):
- `GetConversationsQuery(TenantId, StateFilter?, ChannelFilter?, AssignedToMe?, Cursor, PageSize)` → `IReadOnlyList<ConversationSummaryDto>`. Backed by `IChatDbContextFactory`/`IChatDbContext` (whichever matches the calling context — controller = DI-scoped `IChatDbContext`, since this is always an HTTP request with ambient tenant).
- `GetConversationMessagesQuery(ConversationId, Cursor, PageSize)` → `IReadOnlyList<MessageDto>`.
- `SendAgentReplyCommand(ConversationId, AgentUserId, Text)` → calls `Conversation.AppendAgentReply`, persists via `IChatDbContext`, then publishes a `message` chat event.
- `TakeOverConversationCommand(ConversationId, AgentUserId)` → `Conversation.TakeOver`, publishes an `assigned` event.
- `ResolveConversationCommand(ConversationId)` → `Conversation.Resolve`, publishes a `resolved` event.
- `ReopenConversationCommand(ConversationId)` → `Conversation.Reopen`, publishes a `reopened` event.

**Why REST + MediatR, not new ChatHub methods** (per user decision): every other agent-facing mutation in the app is REST+MediatR; keeping agent actions there means they get the same auth policies, validation pipeline behavior, and testing patterns as everything else, and keeps `ChatHub` doing exactly one job (broadcast), matching how `SignalRReplyStreamSink` already works from outside the hub via `IHubContext<ChatHub>`.

**Alternative considered**: extending `ChatHub` with `SendAgentReply`/`TakeOverConversation`/etc. methods (mirroring `JoinConversation`'s pattern). Rejected — would duplicate the RLS-scoped lookup/authorization `ChatHub.JoinConversation` already does, and moves business commands onto a transport that's harder to unit test and doesn't get the standard MediatR pipeline (validation, logging behaviors) for free.

### 2. Realtime fan-out: extend the existing envelope, do not add a hub
Add to `ChatEventTypes`: `Message`, `Assigned`, `Resolved`, `Reopened` (alongside existing `Token`, `Complete`, `Handoff`). Add matching payload records (`MessageEventPayload`, `AssignedEventPayload`, `ResolvedEventPayload`, `ReopenedEventPayload`) to `ChatEventEnvelope.cs`.

A new `IChatEventPublisher` (thin wrapper over `IHubContext<ChatHub>`, or reuse/extend `IReplyStreamSink` if its shape fits) is injected into the new command handlers and pushes to both `ChatHub.ConversationGroup(id)` (so an open thread updates live) and `ChatHub.AgentsGroup(tenantId)` (so the conversation list re-sorts/updates unread counts without a refetch). This mirrors exactly how `ReplyOrchestrator` already pushes `token`/`complete`/`handoff` events — new event types on the same pipe, not a parallel mechanism.

### 3. Web widget persistence
`WidgetHub.SendMessageAsync` currently runs the full RAG pipeline inline and streams `receiveToken`/`receiveCompleted` directly back to the caller with no persistence. To make widget conversations inbox-visible, the inbound message must go through the same seam every other channel is meant to use: publish `MessageReceivedIntegrationEvent(TenantId, Channel.Web, ExternalConversationId, Body, ProviderMessageId)` and let the existing `MessageReceivedConsumer` find-or-create the `Conversation` and append the inbound `Message` — the durable `ReplyOrchestrator` path (not `WidgetHub`'s inline pipeline) then produces the AI reply, persists it, and streams it back through `ChatHub`'s groups.

This means `WidgetHub` changes from "run the RAG pipeline itself" to "publish the integration event and let the durable pipeline take over," which also means the widget visitor's live token stream needs to come from `ChatHub`'s `conv:{conversationId}` group rather than a direct hub reply. **Open question below** — this is the one place where "web widget only, minimal change" tension is real, since `WidgetHub` is anonymous/unauthenticated and can't `JoinConversation` under `ChatHub`'s current `[Authorize]` gate.

### 4. Frontend: hooks + shared SignalR hook, keep existing UI components unchanged
New `frontend/src/features/inbox/api/`: `use-conversations.ts`, `use-conversation-messages.ts`, `use-send-reply.ts`, `use-take-over.ts`, `use-resolve.ts`, `use-reopen.ts` — same shape as `features/playground/api/use-playground-models.ts` (React Query + `apiClient` hitting `/api/bff` → Gateway → `ConversationsController`).

New shared hook (location: `frontend/src/features/inbox/api/use-chat-hub.ts` or `frontend/src/shared/lib/use-signalr-hub.ts` if made generic enough to also replace the playground's inline setup — decide during implementation by checking how much the playground's connection logic can be shared without scope creep): fetches a ws-ticket, builds the `HubConnection` to `hubs/chat`, calls `JoinAgentDashboard()` once and `JoinConversation`/`LeaveConversation` as the selected conversation changes, and on `chatEvent` dispatches to React Query (`queryClient.setQueryData`/`invalidateQueries`) keyed by conversation/list query keys.

`dashboard/chat/inbox/page.tsx` swaps `MOCK_CONVERSATIONS`/local `useState` overrides for these hooks; `shared/ui/chat/*` components keep their current props (DTOs are shaped to match the existing mock `Conversation`/`Message` TypeScript types so no component changes are needed).

### 5. `ws-ticket` route generalization
Change `frontend/src/app/api/bff/auth/ws-ticket/route.ts` to accept `?hub=chat|playground` (default `playground` to avoid breaking the existing caller) and return the corresponding `hubs/{hub}` path. No change to the ticket-issuance security model (still the real access token, same bounded-exposure rationale already documented in that file).

### 6. Legacy removal
Delete `frontend/src/app/(chat)/**` (verified: this route group is not linked from any current nav — `(dashboard)/dashboard/chat/*` is the live inbox). Delete `features/chat/components/chat-nav.tsx`, `chat-topbar.tsx` (used only by the deleted `(chat)/layout.tsx`) and `chat-settings-nav.tsx` (zero importers found). Keep `ai-settings-nav.tsx` — actively used by `(dashboard)/dashboard/chat/settings/ai/layout.tsx`.

### 7. Channel-extension pattern documentation
Write a reference doc capturing the seam explicitly: a new channel needs (a) an inbound webhook controller that verifies the platform's signature/token (following `ChannelConnectionTester`'s existing per-platform verification logic as a model) and publishes `MessageReceivedIntegrationEvent`, and (b) an outbound sender adapter invoked wherever a `Message` is appended via `AppendAgentReply`/`AppendAiReply` and needs to leave the system toward that channel. Everything else — `Conversation` persistence, `ChatHub` fan-out, the inbox UI, the CQRS slice built here — is already channel-agnostic (keyed by the existing `ChannelIdentity`/`ChatChannel` enum) and needs no changes. Location: decide between a `.claude/skills/` entry (if the team wants Claude to actively apply it on future channel work) versus a plain `docs/` reference (if it's meant for human engineers first) — leaning toward both, a short human-readable doc that a skill can point to.

## Risks / Trade-offs

- **[Risk] `WidgetHub` currently streams synchronously to the caller; routing through the async `MessageReceivedConsumer` → `ReplyOrchestrator` path changes the latency/consistency model (the visitor's own reply now arrives via a group broadcast, not a direct call return).** → Mitigation: `WidgetHub` connection must call `JoinConversation`-equivalent for its own anonymous session against the newly-created conversation id (returned synchronously from the event publish or looked up immediately after), or a dedicated anonymous-safe join path is added. This is the riskiest single decision in this design — flag explicitly for review before implementation starts, and confirm the widget's existing `[AllowAnonymous]` model can safely receive `conv:{conversationId}` group messages without the `[Authorize]` gate `ChatHub.JoinConversation` currently enforces for agents. If this proves too invasive for this change's scope, an acceptable fallback is: keep `WidgetHub`'s existing ephemeral stream to the visitor unchanged for AI replies, but *additionally* publish `MessageReceivedIntegrationEvent` purely for persistence (fire-and-forget, not on the visitor's response critical path) so the inbox sees the conversation without changing the widget's user-facing behavvior. This fallback avoids the anonymous-join problem entirely and is the recommended default unless investigation shows the full re-route is cheap.
- **[Risk] Duplicate persistence** if both `WidgetHub`'s inline pipeline and `MessageReceivedConsumer` end up writing messages for the same turn. → Mitigation: pick exactly one persistence path (the fallback above keeps `WidgetHub`'s AI-reply persistence out of scope and only persists the inbound visitor message via the event, avoiding double-writes of AI replies).
- **[Risk] RLS/tenant-context mismatch** if a new command handler is called from a context with no ambient tenant (e.g., if invoked from a consumer rather than a controller). → Mitigation: keep all new Conversations commands as controller-invoked (DI-scoped `IChatDbContext`, ambient HTTP tenant) only; nothing here runs from a MassTransit consumer scope except the already-tenant-pinned `MessageReceivedConsumer` path.
- **[Trade-off] Not building a shared cross-hub SignalR abstraction now** — the new `use-chat-hub` hook and the playground's existing inline setup will temporarily coexist as similar-but-separate code. Accepted for scope control; a follow-up change can unify them once both are proven.

## Migration Plan
No schema migration expected (Conversation/Message/Escalation tables already exist per `AddConversationSchema`). If `WidgetHub`'s re-route needs a way to correlate the visitor's SignalR connection to a newly created conversation id, verify whether an existing column (`LastInboundProviderMessageId`) or a new lightweight correlation approach is sufficient — this is an implementation-time check, not expected to need a new migration.

## Open Questions
- Does the anonymous widget visitor need to receive live SignalR updates for this change at all, or is a simple request/response (widget sends message, waits for HTTP-triggered SignalR event, or just polls) acceptable for v1? This determines whether the `WidgetHub` re-route risk above must be resolved now or can be deferred.
- Where should the channel-extension pattern doc live — `.claude/skills/` vs `docs/` vs both? Decide during implementation based on whether the team wants Claude Code to proactively apply it unprompted on future channel work.
- Exact permission policy names for the new `ConversationsController` endpoints (reuse `conversations:read` seen on `ChatHub.JoinAgentDashboard`; confirm a `conversations:write` or similar exists for reply/state-transition actions, or whether it needs to be added to the RBAC policy set).
