# Connection Health & Guarded Integrations — Design

**Date:** 2026-07-07
**Status:** Approved (brainstorming)
**Branch:** feature/chat

## Context

Every workspace integration that stores credentials — S3 storage, AI provider, social
channels, and any future one (SMS gateway, email provider, payment gateway, webhooks) —
can silently break: keys expire, get rotated, get revoked, or were entered wrong. Today
NexConvo has no consistent way to (a) prove a config actually works before it is used,
(b) stop dependent features from running on a broken config, or (c) tell the workspace
owner when a previously-working connection starts failing.

State before this work:

| Integration | Test endpoint | Save gated on test | Health persisted |
|---|---|---|---|
| Channels (Chat) | `POST /test` exists but the tester is a **stub** (always returns success) | No | No |
| AI provider (Integrations) | `POST /ai-config/test` — **real** live probe | Frontend only | No |
| S3 (Integrations) | **None** | No | No |

No entity persists connection health. No scheduled re-testing exists. No failure alerting
exists. Email sending and user/role data live **only** in the Identity service, and there is
**no cross-service path** to reach them (no gRPC, no shared user read model).

**Goal:** a single, reusable "connection health" capability so this behaviour is defined
once and applies to every current and future integration without re-specifying it each time.

## Requirements (decided)

1. **Test-then-save (explicit two-step).** The user clicks **Test Connection** first; a test
   does **not** save. **Save** is a separate manual action, enabled **only** after a
   successful test. The server re-tests on save as a safety net and rejects (422) a config
   that fails, so the client gate cannot be bypassed.
2. **Feature guard (block + warn).** A dependent feature must not run on an unhealthy config.
   The backend refuses to use it (returns a clear `409 ConnectionUnhealthy`), and the
   frontend disables the feature with a warning banner linking to Settings.
3. **On-demand + scheduled re-testing.** Test on user click, and periodically in the
   background to catch expired/rotated keys. A background failure flips status to
   `Failed`/`Degraded` and alerts the owner/admins by email.
4. **Owner/admin email alerts** on background failure, sent through the transactional email
   pool, via a dedicated Notification service.
5. **Bengali-first i18n** for all new UI strings (en + bn).

## Architecture overview

Security-first hybrid. Secrets never leave the service that owns them; the scheduler only
triggers, and notifications flow through events + a synchronous contact lookup.

```
┌──────────────────────┐  (1) Recurring trigger — NO secrets
│  Automation service  │      publish CheckIntegrationHealthCommand
│  (Hangfire recurring)│──────────────────────────────┐
└──────────────────────┘                                ▼
                                               ┌─────────────────┐
        ┌───────────────────────────────────────┤    RabbitMQ    │
        │ (2) Owning service consumes & tests    └─────────────────┘
        ▼                         ▼
┌───────────────┐        ┌────────────────┐   Decrypt own DB secret →
│ Chat service  │        │ Integrations   │   IConnectionTester → ApplyHealth()
│ (channels)    │        │ (S3, AI)       │   Secret NEVER crosses the network
└──────┬────────┘        └───────┬────────┘
       │ (3) On Healthy→Failed transition:      │
       │     publish IntegrationHealthFailedEvent { TenantId, OwnerUserId,
       │              AdminUserIds, IntegrationKind, ConfigName, Error }   (no secrets)
       └───────────────┬────────────────────────┘
                       ▼
             ┌──────────────────────┐  (4) Consume event
             │ Notification service │  (5) REST → Identity GetUserContactInfo(userId)
             │  (NEW — owns email   │  (6) Identity returns { name, email } only
             │   provider secrets)  │  (7) Send email via own provider + template
             └──────────┬───────────┘
                        │ internal REST (JWT/service-auth)
                        ▼
             ┌──────────────────────┐
             │  Identity service    │  owns users/roles/emails only
             │  GET /internal/users │  no email secrets
             │      /{id}/contact   │
             └──────────────────────┘
```

## Components

### 1. Shared abstraction — `NexConvo.BuildingBlocks` (net-new)

Defines the health shape **once** so every integration reports the same way.

```csharp
public enum ConnectionStatus { Untested = 0, Healthy = 1, Degraded = 2, Failed = 3 }

public sealed record ConnectionHealth(
    bool Success,
    ConnectionStatus Status,
    string? Detail,        // e.g. account name / "Bucket reachable"
    string? ErrorMessage,
    int? LatencyMs);

// TInput is the per-integration credential set (S3 keys, AI key, channel token).
public interface IConnectionTester<TInput>
{
    string IntegrationKind { get; }   // "s3" | "ai-provider" | "channel:whatsapp"
    Task<ConnectionHealth> TestAsync(TInput input, CancellationToken ct);
}
```

Reusable persisted health fields, embedded on each config entity:
`LastTestedAt (DateTimeOffset?)`, `LastTestStatus (ConnectionStatus)`,
`LastTestError (string?)`, `LastTestLatencyMs (int?)`, plus a domain method
`ApplyHealth(ConnectionHealth)`. A guard helper `EnsureHealthy(config)` (throws/returns a
`409 ConnectionUnhealthy` result) so any handler guards a feature in one line.

### 2. Per-integration testers

- **S3 (net-new):** `S3ConnectionTester : IConnectionTester<S3TestInput>` using **`AWSSDK.S3`**
  `HeadBucketAsync` (works with custom endpoints for R2/MinIO/Spaces). New package
  `AWSSDK.S3`. New `POST /api/v1/s3-config/test` endpoint + gateway route.
  *(Rationale: hand-rolling AWS SigV4 signing is error-prone; the SDK is the standard,
  supports endpoint override. Announce: AWSSDK.S3 chosen over manual HTTP/SigV4.)*
- **Channels (replace stub):** real `ChannelConnectionTester` — Meta Graph `/me`
  (WhatsApp/Facebook/Instagram), Telegram `getMe`, via the existing
  `ChannelVerificationHttpClientFactory` (Polly resilience). Web widget → always Healthy.
- **AI provider (adapt existing):** wrap the existing `TestAiConnectionCommandHandler`
  live probe to return `ConnectionHealth` (shape parity). No behaviour change.

### 3. Test-then-save gate

- **Test endpoint** = stateless live probe; persists nothing.
- **Frontend:** Save disabled until `testState === 'success'`; credential edits reset test
  state. (This matches the existing `ai-settings-form` `requiresTest`/`canSave` pattern —
  generalise it.)
- **Server safety net:** on Save, if credentials are new/changed, re-run the tester; on
  failure return **422** and do not persist. On success, `ApplyHealth(Healthy)`.

### 4. Feature guard

- **Backend:** before using a config, `EnsureHealthy(config)`; if not `Healthy`, refuse with
  `409 ConnectionUnhealthy` + a clear message. Applies to: knowledge upload → S3,
  AI reply/generation → AI provider, channel send → channel connection.
- **Frontend:** dependent UI disabled + warning banner linking to Settings; config cards
  show a health badge (🟢 Healthy / 🟡 Degraded / 🔴 Failed / ⚪ Untested) + "last tested …".

### 5. Central scheduler — Automation service (net-new logic)

The Automation service is currently an empty scaffold (web host). Add a **Hangfire recurring
job** (Postgres-backed, mirroring Chat's Hangfire setup) that every 6 hours publishes
`CheckIntegrationHealthCommand` to RabbitMQ. It **holds no credentials and tests nothing** —
trigger only. Requires: Hangfire + MassTransit (publish) registration in Automation.

### 6. Decentralized execution — Chat & Integrations consumers (net-new)

Each owning service consumes `CheckIntegrationHealthCommand`, loops its own active configs,
decrypts secrets locally, runs `IConnectionTester`, and saves `ApplyHealth(...)`. On a
`Healthy → Failed/Degraded` transition (and not on already-failed, to avoid repeat spam),
publish `IntegrationHealthFailedEvent`.

**Tenant context in a background consumer (critical — net-new).** There is no HttpContext in
a consumer, so `HttpTenantContext.HasTenant` is `false` and RLS would return zero rows. Two
established options: (a) generalise Identity's `AmbientTenantContext`/`IAmbientTenantSetter`
into BuildingBlocks and `SetTenant(...)` per tenant before DB work; or (b) follow
`KnowledgeIngestionJob`'s approach — pass `tenantId` explicitly and filter every query with
`TenantId == tenantId`. **Decision: (b)** — matches the existing job precedent and avoids a
shared-state ambient context; the health job iterates tenants/configs with explicit filters.
Integrations is currently publish-only — add `AddConsumers` + `ConfigureEndpoints`.

### 7. Notification service (net-new microservice)

Owns all notifications (email now; SMS/push later) and the email-provider secrets. Flow:
consume `IntegrationHealthFailedEvent` → REST call to Identity
`GET /internal/users/{id}/contact` (JWT/service-auth) → receive `{ name, email }` →
send via its own provider + template to owner **and** admins.

- **Identity (net-new):** internal contact endpoint returning name+email only; a query to
  resolve owner + admin user ids/emails for a tenant (reuse the `OwnerGuard` role-query
  pattern). No email secrets in Identity.
- **Internal transport:** REST (codebase has no gRPC; REST is far less overhead and is
  secured with service auth). Migratable to gRPC later. *(Announce: REST over gRPC for the
  internal contact lookup — no existing gRPC infra.)*

### New event/command contracts — `NexConvo.Contracts/Events`

- `CheckIntegrationHealthCommand { OccurredAt }` (fan-out trigger; scheduler → owning services)
- `IntegrationHealthFailedEvent { TenantId, OwnerUserId, AdminUserIds, IntegrationKind, ConfigName, ErrorMessage, OccurredAt }`

## Contracts / boundaries (isolation)

- **Secret boundary:** decrypted credentials never leave the owning service (Chat/Integrations).
  The scheduler and events carry no secrets.
- **`IConnectionTester<TInput>`:** each tester is independently testable with a fake input;
  callers depend only on `ConnectionHealth`.
- **Identity contact endpoint:** returns name+email only — the minimum the Notification
  service needs; user data does not leak further.
- **Notification service:** the only holder of email-provider secrets; other services never
  send email directly.

## Error handling

- Testers never throw to the caller — they catch and return `ConnectionHealth(Success:false, …)`
  with a **sanitised** message (no secret/stack leakage).
- Save re-test failure → `422` (config not persisted).
- Feature-guard failure → `409 ConnectionUnhealthy` with a user-actionable message.
- Notification: repeat failures for an already-`Failed` config do **not** re-email (dedupe on
  status transition). Identity contact-lookup failure → log + retry (MassTransit redelivery);
  never crash the consumer.
- Background consumer: per-config try/catch so one bad config doesn't abort the whole sweep.

## Testing strategy (TDD)

- **Unit:** each `IConnectionTester` with a fake HTTP/S3 client → Healthy/Failed/latency paths.
  `ApplyHealth` transitions. `EnsureHealthy` guard (throws on non-Healthy).
- **Save gate:** handler re-tests on changed credentials → 422 on failure; no persistence.
- **Consumer:** given N configs, tests each with explicit tenant filter; publishes
  `IntegrationHealthFailedEvent` only on Healthy→Failed transition (dedupe verified).
- **Notification:** consumes event → calls Identity contact (mocked) → sends via fake email
  provider to owner+admins; no email on repeat failure.
- **Frontend:** Test button state machine (idle→testing→success/failed), Save gated on
  success, credential edit resets, guard banner rendering, en/bn strings. MSW for network.
- **E2E (Playwright):** S3 settings — test fails → Save disabled → fix → test passes → Save
  enabled → save. Guarded feature shows banner when config unhealthy.

## Scope / phasing

Single spec, but implementable in vertical slices in this order (each shippable):

1. **Shared abstraction** (BuildingBlocks: enum, `ConnectionHealth`, `IConnectionTester`,
   entity fields, `EnsureHealthy`).
2. **S3 slice** — tester + test endpoint + test-then-save + frontend test button + guard on
   knowledge upload. (Directly fixes the current user-facing gap.)
3. **Channels slice** — real tester (replace stub) + persisted health + guard on send.
4. **AI slice** — adapt to `ConnectionHealth` + persisted health + guard on AI usage.
5. **Scheduler + decentralized consumers** — Automation Hangfire trigger + Chat/Integrations
   health consumers + `Healthy→Failed` events.
6. **Notification service + Identity contact endpoint** — email alerts to owner/admins.

## Explicitly out of scope (YAGNI, for now)

- SMS/push notifications (Notification service designed to extend, not built now).
- gRPC (REST internal call instead).
- A central Health *data* service (secrets stay decentralized).
- Auto-retry/self-heal of failing connections (only detect + alert + guard).

## Net-new vs existing (summary)

**Existing:** MassTransit/RabbitMQ (Chat consume+publish, Integrations publish), shared
contracts folder, RLS interceptor + `ITenantContext`, `AmbientTenantContext` (Identity-only),
Hangfire in Chat, `AddNexConvoResilience`, Identity email stack, `OwnerGuard` role query.

**Net-new:** shared `IConnectionTester`/`ConnectionHealth`/entity fields; S3 tester +
`AWSSDK.S3` + `/s3-config/test` + gateway route; real channel tester; scheduler in Automation
(Hangfire recurring + MassTransit publish); health consumers in Chat + Integrations
(+ `AddConsumers` in Integrations); background tenant-filter pattern; `CheckIntegrationHealthCommand`
+ `IntegrationHealthFailedEvent`; Notification microservice; Identity internal contact endpoint.
