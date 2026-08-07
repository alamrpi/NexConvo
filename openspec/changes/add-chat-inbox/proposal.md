## Why

The agent-facing inbox at `/dashboard/chat/inbox` is fully built in the UI but runs entirely on mock data (`frontend/src/features/chat/mock-data.ts`); there is no backend API to list conversations, load message history, or send an agent reply, so no human agent can actually respond to a customer today. Meanwhile the domain model that should back all of this (`Conversation`, `Message`, with a working AI/human handoff state machine) already exists and is unused outside the RAG auto-reply path. This change connects the two: real conversation data end-to-end for the one channel that already has a working inbound pipeline (the web widget), plus a documented pattern so each additional channel (WhatsApp, Facebook, etc.) plugs into the same inbox without new UI, hub, or CQRS work.

## What Changes

- Add a `Conversations` CQRS slice (`NexConvo.Chat.Application/Features/Conversations/`): list conversations (paged, filterable by state/channel/assigned-to-me), fetch message history, send an agent reply, take over, resolve, and reopen a conversation — each backed by the existing `Conversation` domain methods (`AppendAgentReply`, `TakeOver`, `Resolve`, `Reopen`).
- Add a `ConversationsController` REST surface on `NexConvo.Chat.Api` exposing the above, authorized consistently with existing `conversations:read`-style policies.
- Extend `ChatHub`'s real-time event envelope (`ChatEventEnvelope`/`ChatEventTypes`) with new event types for plain agent messages and conversation state changes (assigned/resolved/reopened), fanned out to the existing `conv:{conversationId}` and `tenant:{tenantId}:agents` groups — no new hub, no new groups.
- Complete the web-widget-to-inbox pipeline: widget-originated messages are persisted as `Conversation`/`Message` rows (via the existing `MessageReceivedIntegrationEvent` → `MessageReceivedConsumer` seam) so they appear live in the agent inbox, not just streamed ephemerally back to the widget visitor.
- Wire the frontend inbox page (`dashboard/chat/inbox/page.tsx`) to real data: new React Query hooks under `features/inbox/api/`, and a shared SignalR connection hook (generalized from the playground's inline pattern) that joins the agent dashboard group and per-conversation groups and updates the React Query cache on incoming events.
- Generalize the `ws-ticket` BFF route (`frontend/src/app/api/bff/auth/ws-ticket/route.ts`) to issue a ticket for either the playground or chat hub instead of hardcoding `hubs/playground`.
- **BREAKING (internal only)**: Remove the legacy `frontend/src/app/(chat)/` route group (`layout.tsx`, `inbox/`, `analytics/`, `settings/**`) and its now-dead `ChatNav`/`ChatTopbar`/`ChatSettingsNav` components, superseded by `(dashboard)/dashboard/chat/*`. No public API changes; only removes an already-unlinked internal route.
- Document the channel-extension pattern (new channel = new inbound webhook receiver publishing `MessageReceivedIntegrationEvent` + an outbound sender adapter; no inbox/UI/hub/CQRS changes needed) as a reference doc, so future WhatsApp/Facebook/etc. work follows a known seam.

## Capabilities

### New Capabilities
- `chat-inbox`: Agent-facing capability to list conversations, view message history, send replies, and transition conversation state (take over/resolve/reopen), with live updates over the existing chat real-time fabric. Includes the requirement that widget-originated conversations are persisted (not just streamed ephemerally) so they surface here.

### Modified Capabilities
- None. The `embeddable-chat-widget` capability (proposed, not yet merged, in the still-open `web-chat-widget` change) has no existing requirement about persistence to modify — widget message persistence is new behavior, captured as an ADDED requirement under `chat-inbox` instead of a delta against an unmerged spec.

## Impact

- **Backend**: `NexConvo.Chat.Application` (new `Features/Conversations/*`), `NexConvo.Chat.Api` (new `ConversationsController`, extended `Realtime/ChatEventEnvelope.cs`, changes to `Realtime/WidgetHub.cs` and/or its consumer wiring for persistence).
- **Frontend**: new `frontend/src/features/inbox/` slice, changes to `frontend/src/app/(dashboard)/dashboard/chat/inbox/page.tsx`, a new shared SignalR hook, a generalized `ws-ticket` route; deletion of `frontend/src/app/(chat)/**` and three orphaned nav components.
- **No database schema changes expected** beyond what's already migrated (`Conversation`/`Message`/`Escalation` tables exist); verify no new columns are needed once design is finalized.
- **No new external dependencies** — reuses existing SignalR/MassTransit/EF infrastructure.
