## 1. Backend: Conversations CQRS slice

- [ ] 1.1 Create `NexConvo.Chat.Application/Features/Conversations/` folder structure (`Queries/`, `Commands/`) mirroring `Features/ChannelConnections/`
- [ ] 1.2 Implement `GetConversationsQuery` + `GetConversationsQueryHandler` + `ConversationSummaryDto` (paged, filter by state/channel/assigned-to-me, scoped via `IChatDbContext`)
- [ ] 1.3 Implement `GetConversationMessagesQuery` + `GetConversationMessagesQueryHandler` + `MessageDto` (paged, authorization: assigned agent or `conversations:read`)
- [ ] 1.4 Implement `SendAgentReplyCommand` + Handler + Validator, calling `Conversation.AppendAgentReply`
- [ ] 1.5 Implement `TakeOverConversationCommand` + Handler, calling `Conversation.TakeOver`
- [ ] 1.6 Implement `ResolveConversationCommand` + Handler, calling `Conversation.Resolve`
- [ ] 1.7 Implement `ReopenConversationCommand` + Handler, calling `Conversation.Reopen`
- [ ] 1.8 Write unit tests for each handler (happy path + invalid state-transition rejection), per TDD convention

## 2. Backend: REST surface

- [ ] 2.1 Create `ConversationsController` on `NexConvo.Chat.Api` with endpoints for list, message history, send reply, take-over, resolve, reopen
- [ ] 2.2 Confirm or add a `conversations:write`-equivalent authorization policy in `Program.cs` for the mutating endpoints (reply/take-over/resolve/reopen); reuse `conversations:read` for read endpoints
- [ ] 2.3 Add integration/controller tests covering authorization refusals (wrong tenant, missing permission, not-assigned-agent)

## 3. Backend: Realtime event extension

- [ ] 3.1 Add `Message`, `Assigned`, `Resolved`, `Reopened` constants to `ChatEventTypes` and matching payload records to `ChatEventEnvelope.cs`
- [ ] 3.2 Add a publisher (new `IChatEventPublisher` or extend existing `IReplyStreamSink` if shape fits) that pushes envelopes to `ChatHub.ConversationGroup` and `ChatHub.AgentsGroup` via `IHubContext<ChatHub>`
- [ ] 3.3 Wire the new publisher into the four mutating command handlers (3.1's events fire after successful persistence)
- [ ] 3.4 Verify no regression to existing `token`/`complete`/`handoff` events used by the RAG auto-reply pipeline

## 4. Backend: Web widget persistence

- [ ] 4.1 Investigate exact mechanism for `WidgetHub` to publish `MessageReceivedIntegrationEvent` for each inbound visitor message (resolve the design's open question: full re-route through `MessageReceivedConsumer`/`ReplyOrchestrator`, or fire-and-forget persistence alongside the existing ephemeral stream)
- [ ] 4.2 Implement the chosen approach; ensure no duplicate `Message` rows are written for the same turn
- [ ] 4.3 Confirm widget-originated `Conversation` rows carry the correct `ChannelIdentity` (Web channel + external conversation id) so they filter/display correctly in the inbox
- [ ] 4.4 Add/adjust tests covering widget-to-inbox persistence

## 5. Frontend: API hooks

- [ ] 5.1 Create `frontend/src/features/inbox/api/use-conversations.ts` (list, paged, filterable) following `use-playground-models.ts` conventions
- [ ] 5.2 Create `use-conversation-messages.ts`
- [ ] 5.3 Create `use-send-reply.ts`, `use-take-over.ts`, `use-resolve.ts`, `use-reopen.ts` (mutations with optimistic/invalidation cache updates)
- [ ] 5.4 Create matching TypeScript DTO types under `frontend/src/features/inbox/model/` mirroring backend DTOs and existing mock `Conversation`/`Message` shapes

## 6. Frontend: Realtime wiring

- [ ] 6.1 Generalize `frontend/src/app/api/bff/auth/ws-ticket/route.ts` to accept `?hub=chat|playground` (default `playground`)
- [ ] 6.2 Build a shared SignalR connection hook for the chat hub (join agent dashboard group + per-conversation join/leave, dispatch `chatEvent` to React Query cache)
- [ ] 6.3 Decide and document hook location (`features/inbox/api/` vs generalized `shared/lib/`) based on how much can realistically be shared with the playground's inline setup without scope creep

## 7. Frontend: Wire inbox page to real data

- [ ] 7.1 Replace `MOCK_CONVERSATIONS`/local state overrides in `dashboard/chat/inbox/page.tsx` with the new hooks from sections 5–6
- [ ] 7.2 Verify `shared/ui/chat/*` components (`MessageBubble`, `ConversationListItem`, `StateBanner`, etc.) render correctly against real DTOs with no prop changes needed
- [ ] 7.3 Wire `MessageComposer`'s `onSend`/`onResolve` handlers (currently `/* TODO: SignalR send */` stubs) to the new mutation hooks
- [ ] 7.4 Wire `SlaCountdown` and `StateBanner`'s existing `// TODO: connect SignalR` markers to live events

## 8. Legacy cleanup

- [ ] 8.1 Delete `frontend/src/app/(chat)/layout.tsx`, `inbox/`, `analytics/`, `settings/**`
- [ ] 8.2 Delete `frontend/src/features/chat/components/chat-nav.tsx`, `chat-topbar.tsx`, `chat-settings-nav.tsx`
- [ ] 8.3 Confirm `ai-settings-nav.tsx` remains and its consuming route (`(dashboard)/dashboard/chat/settings/ai/layout.tsx`) is unaffected
- [ ] 8.4 Run `tsc --noEmit` and grep for any remaining references to deleted files/components

## 9. Documentation

- [ ] 9.1 Write the channel-extension pattern reference doc (webhook receiver → `MessageReceivedIntegrationEvent`; outbound sender adapter; no inbox/UI/hub/CQRS changes needed)
- [ ] 9.2 Decide and record final doc location (`.claude/skills/` vs `docs/` vs both)

## 10. Verification

- [ ] 10.1 Run backend test suite scoped to `NexConvo.Chat.Application`/`NexConvo.Chat.Api` tests
- [ ] 10.2 Run frontend tests for new hooks (Vitest + MSW per project convention)
- [ ] 10.3 Manual E2E: send a message via the web widget, confirm it appears live in `/dashboard/chat/inbox` without refresh
- [ ] 10.4 Manual E2E: reply as an agent, confirm the widget visitor receives it
- [ ] 10.5 Manual E2E: take over, resolve, and reopen a conversation; confirm state chip/banner update live in both the list and open thread
- [ ] 10.6 Confirm legacy `(chat)` routes are gone (404) and the build has no dangling imports
