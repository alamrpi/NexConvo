---
name: nexconvo-add-channel
description: Add a new omnichannel channel (WhatsApp, Facebook, Instagram, Telegram, TikTok, LinkedIn, SMS, Email, Voice) to NexConvo's chat inbox. Use when the user wants to wire up a new channel's inbound webhook or outbound message delivery for the Chat service.
license: MIT
metadata:
  author: nexconvo
  version: "1.0"
---

# Adding a New Channel to NexConvo's Chat Inbox

## Why this is cheap

As of the `add-chat-inbox` change, the agent inbox (`/dashboard/chat/inbox`), `ChatHub`
real-time fan-out, and the `Conversations` CQRS slice are all **channel-agnostic by
construction**. The discriminator is a single value object,
`ChannelIdentity(LeadSourceChannel Channel, string ExternalConversationId)`
(`src/services/Chat/NexConvo.Chat.Domain/ValueObjects/ChannelIdentity.cs`), stored on
`Conversation`. Nothing downstream of "a `Conversation`/`Message` row exists" cares which
channel produced it.

This means adding a channel is **not** a chat-inbox feature — it is exactly two pieces of new
plumbing:

1. An **inbound webhook receiver** that turns the platform's payload into a
   `MessageReceivedIntegrationEvent`.
2. An **outbound sender adapter** that takes a persisted agent/AI reply and actually delivers
   it to that platform.

Everything else in this document explains precisely where those two pieces plug in, and is
explicit about the one part that is **not** yet generic (outbound dispatch).

## What you do NOT need to touch

State this up front to avoid scope creep — none of the following need any change to add a
channel:

- **Inbox UI** (`frontend/src/app/(dashboard)/dashboard/chat/inbox/page.tsx`,
  `frontend/src/shared/ui/chat/*`) — renders off `ConversationSummaryDto`/`MessageDto`, which
  carry the channel as data, not as a UI branch.
- **`ChatHub`** (`src/services/Chat/NexConvo.Chat.Api/Realtime/ChatHub.cs`) and its envelope
  (`ChatEventEnvelope.cs`) — group membership (`ConversationGroup`, `AgentsGroup`) and event
  types (`token`/`complete`/`handoff`/`message`/`assigned`/`resolved`/`reopened`) are all
  keyed by `conversationId`/`tenantId`, never by channel.
- **The `Conversations` CQRS slice**
  (`src/services/Chat/NexConvo.Chat.Application/Features/Conversations/**`) — `GetConversationsQuery`,
  `SendAgentReplyCommand`, `TakeOverConversationCommand`, `ResolveConversationCommand`,
  `ReopenConversationCommand` all operate on `Conversation` by id/tenant, not by channel.
- **`ChannelConnectionsController`**
  (`src/services/Chat/NexConvo.Chat.Api/Controllers/ChannelConnectionsController.cs`) — its
  generic CRUD (`GetAll`/`Save`/`Delete`/`Test`/`TestById`) already accepts any `ChatChannel`
  enum value; connection storage, access-token handling, and health-check scheduling need no
  new code per channel beyond what step 3 below adds.

If a task for "add channel X" touches any of the files above, that's a signal the task has
scope-crept beyond what's actually required — stop and check this doc again.

## Step 1: Inbound webhook receiver

Add a new `[AllowAnonymous]` (or platform-signature-gated) controller on
`NexConvo.Chat.Api`, e.g. `WhatsAppWebhookController`, alongside `ConversationsController`
and `ChannelConnectionsController` in
`src/services/Chat/NexConvo.Chat.Api/Controllers/`.

**Verify the platform's signature/token before doing anything else.** Model this on
`ChannelConnectionTester` (`src/services/Chat/NexConvo.Chat.Infrastructure/Services/ChannelConnectionTester.cs`)
— not because it does webhook verification itself (it doesn't; it's an outbound
reachability probe, see the note below), but because it already encodes the
per-platform-branch pattern this codebase uses for channel-specific logic:

```csharp
return input.Channel switch
{
    ChatChannel.WhatsApp or ChatChannel.Facebook or ChatChannel.Instagram =>
        await ProbeMetaAsync(client, input.AccessToken, sw, ct),
    ChatChannel.Telegram => await ProbeTelegramAsync(client, input.AccessToken, sw, ct),
    _ => ConnectionHealth.Failed("Connection test not yet supported for this channel.", ...),
};
```

Follow the same shape for verification: switch on `ChatChannel`/the platform, delegate to a
private per-platform verify method, and fail closed (reject, do not silently accept) for any
channel/signature combination you haven't implemented — exactly like `ChannelConnectionTester`
returns `Failed("Connection test not yet supported...")` for `TikTok`/`LinkedIn` rather than
reporting false-healthy. Concretely:
- **Meta family (WhatsApp/Facebook/Instagram)**: verify the `X-Hub-Signature-256` HMAC header
  against the stored app secret (`ChannelConnection.AppSecret`, already modeled in
  `SaveChannelConnectionCommand`/`SaveChannelConnectionCommandHandler.cs` — it exists today
  purely for this future use, storage is already there).
- **Telegram**: verify the secret token Telegram echoes back (`X-Telegram-Bot-Api-Secret-Token`)
  against the value you set when registering the webhook, or use the bot token as a path
  segment secret.
- **SMS/Voice (Twilio, BulkSMSBD/MimSMS)**: verify the provider's request signature per their
  webhook-signing scheme.
- **Email (Resend/Postmark inbound, SES inbound)**: verify via the provider's signed
  webhook/SNS message signature.

Once verified, extract sender id + body and publish
`MessageReceivedIntegrationEvent` (`src/shared/NexConvo.Contracts/Events/Chat/MessageReceivedIntegrationEvent.cs`)
via `IPublishEndpoint` (MassTransit) — the same seam `WidgetHub` uses for the Web channel
(added in `add-chat-inbox` Task 4) and the same event `MessageReceivedConsumer`
(`src/services/Chat/NexConvo.Chat.Application/Features/Rag/EventHandlers/MessageReceivedConsumer.cs`)
already finds-or-creates a `Conversation` from:

```csharp
new MessageReceivedIntegrationEvent
{
    ConversationId = Guid.Empty,       // consumer resolves/creates the real one; not read on publish
    TenantId = tenantId,
    Channel = LeadSourceChannel.WhatsApp,   // <-- the new channel's LeadSourceChannel value
    ExternalSenderId = externalSenderId,    // e.g. WhatsApp wa_id, Telegram chat id, phone number
    MessageRef = providerMessageId ?? Guid.NewGuid().ToString(),
    Body = messageText,
    ProviderMessageId = providerMessageId,
}
```

Note the event's `Channel` field is typed `LeadSourceChannel`
(`src/shared/NexConvo.Contracts/Enums/LeadSourceChannel.cs`), the shared cross-service enum,
**not** `ChatChannel` (`src/services/Chat/NexConvo.Chat.Domain/Enums/ChatChannel.cs`), which is
Chat-service-internal. If your webhook controller works in terms of `ChatChannel`, map it with
the existing `ChatChannelExtensions.ToLeadSourceChannel()`
(`src/services/Chat/NexConvo.Chat.Application/Common/ChatChannelExtensions.cs`) — do not
duplicate this mapping.

`MessageReceivedConsumer` does the rest for free: it is idempotent (MassTransit EF inbox),
handles the concurrent-first-message race via retry-on-conflict, and dispatches
`GenerateRagReplyCommand` so `ReplyOrchestrator` produces the AI reply exactly as it does for
every other channel today.

## Step 2: Outbound sender adapter — **the actual gap**

This is the one piece **not yet built generically** in `add-chat-inbox`. Be honest about this
when scoping new channel work — do not assume it exists.

Today, a reply becomes a persisted `Message` row in exactly two places, and neither one calls
anything that leaves the process:

- `SendAgentReplyCommandHandler.Handle`
  (`src/services/Chat/NexConvo.Chat.Application/Features/Conversations/Commands/SendAgentReplyCommandHandler.cs`,
  around line 47): `var message = conversation.AppendAgentReply(cmd.AgentUserId, cmd.Text); db.Messages.Add(message);`
  followed by `db.SaveChangesAsync` and an `IChatEventPublisher.PublishMessageAsync` call —
  which only broadcasts to `ChatHub` groups (in-app real-time), it does not deliver anything to
  WhatsApp/Telegram/etc.
- `ReplyOrchestrator.RunAsync` and `.AnswerDirectlyAsync`
  (`src/services/Chat/NexConvo.Chat.Application/Rag/ReplyOrchestrator.cs`, around lines 143 and
  180): `var message = conversation.AppendAiReply(replyText, confidence, tokens: null);` — same
  situation; `IReplyStreamSink` pushes to SignalR only.

For the Web widget this is correct as-is (the "channel" *is* the SignalR connection — there is
nowhere else to deliver to). For every external channel, after persistence succeeds you need to
actually call that platform's send API (WhatsApp Cloud API message send, Telegram
`sendMessage`, Twilio SMS send, SES/Resend/Postmark send, etc.), using the same
`ChannelConnection.AccessToken` already stored and health-checked by the existing
connection-management flow.

**This adapter does not exist yet as a generic abstraction.** When you build the first real
external channel, introduce it then — e.g. an `IOutboundMessageSender` resolved per
`conversation.Channel.Channel` (mirroring the `ChatChannel`-keyed switch pattern in
`ChannelConnectionTester`), called from both call sites above after `SaveChangesAsync`
succeeds, symmetrically with how `IChatEventPublisher` is already called from both. Do not
retrofit this now for the Web-only case in this change; note it as a known gap for the first
channel that actually needs it, and build the abstraction generically at that point rather than
hard-coding a single-channel special case into the two handlers above.

## Step 3: Register the channel

Add the new value to `ChatChannel`
(`src/services/Chat/NexConvo.Chat.Domain/Enums/ChatChannel.cs`) and to
`ChatChannelExtensions.ToLeadSourceChannel()`'s switch
(`src/services/Chat/NexConvo.Chat.Application/Common/ChatChannelExtensions.cs`) if the
corresponding `LeadSourceChannel` value doesn't already map (SMS/Email/Voice already exist on
`LeadSourceChannel` — `src/shared/NexConvo.Contracts/Enums/LeadSourceChannel.cs` — but not yet
on `ChatChannel`, so add them there when the channel is a chat-style conversational one, not
just a lead source).

`ChannelConnectionsController` needs **no changes** — its CRUD endpoints
(`GET/POST /api/v1/channel-connections`, `POST .../test`, `POST .../{id}/test`) already accept
any `ChatChannel` value in `SaveChannelConnectionRequest`/`TestConnectionRequest`. Add the new
channel's verification branch to `ChannelConnectionTester.TestAsync`'s switch (replacing its
current `"not yet supported"` fallback for that channel) so "Test Connection" in the settings
UI actually probes the new platform — this is the only Infrastructure-layer change connection
management needs.

## What "done" looks like for a new channel

1. New webhook controller exists, verifies the platform's signature, publishes
   `MessageReceivedIntegrationEvent` with the right `LeadSourceChannel`.
2. New outbound sender exists (or the shared `IOutboundMessageSender` abstraction is
   introduced if this is the first non-Web channel) and is called from both
   `SendAgentReplyCommandHandler` and `ReplyOrchestrator`'s two append sites.
3. `ChatChannel` enum has the new value; `ChannelConnectionTester` has a real verification
   branch instead of the "not yet supported" fallback.
4. **Nothing else changed** — no inbox page/component edits, no `ChatHub`/`ChatEventEnvelope`
   edits, no new/modified files under `Features/Conversations/`. If your diff touches any of
   those, re-read the "What you do NOT need to touch" section above.

## Reference

Full narrative context: `docs/CHATBOT-ARCHITECTURE.md` section 6 ("Inbound Ingestion") and
section 9 ("Human Handoff") describe the same seam at the design-doc level; this skill is the
concrete, file-and-line-level companion for actually doing the work.