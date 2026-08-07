# Connection Health — Slice 5 (Scheduled Re-Test) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Periodically re-test every tenant's saved integration configs (S3, AI, channels) in the background and, on a Healthy→Failed transition, publish `IntegrationHealthFailedEvent` (Slice 6 emails the owner). A trigger-only Hangfire scheduler in the Automation service fans out `CheckIntegrationHealthCommand`; Chat + Integrations consume it and sweep their own configs on the RLS-free owner connection.

**Architecture:** Security-first hybrid. Automation holds no secrets — it only publishes the trigger on a cron. Each owning service enumerates all tenants' active configs on the `*DbMigrator` (owner) connection (no RLS interceptor, mirroring the migrators), decrypts in-memory, re-tests via the existing `IConnectionTester<T>`, `ApplyHealth` + `SaveChanges`, and publishes on the Healthy→Failed transition only (dedupe).

**Tech Stack:** .NET 9, EF Core (PostgreSQL), MassTransit/RabbitMQ, Hangfire (Postgres storage), xUnit, NSubstitute.

## Global Constraints
- .NET 9; Clean Architecture; CQRS where applicable; TDD; Standard 8 resiliency (testers already Polly-wrapped); Standard 9 (structured logs, correlation); **Standard 13 — NEVER log/return decrypted keys/tokens; `IntegrationHealthFailedEvent` carries only sanitized `ErrorMessage`**; Standard 18 idempotency (event only on transition; per-config try/catch so one failure doesn't abort the sweep); Standard 22 (this is its "scheduled re-test + alert" clause). Central Package Management; CA1050 + TreatWarningsAsErrors.
- **Tenant/RLS (locked):** sweep runs on the OWNER connection (`*DbMigrator`) with NO RLS interceptor — reuse the `IntegrationsDatabaseMigrator` bare-ctor / `ChatDbContextFactory.NullTenantContext` pattern. Enumerate all tenants' `IsActive` configs; act per-config by its own `TenantId`.
- **Prod caveat (document in the S3 sweep):** `WorkspaceS3Configs` has `FORCE ROW LEVEL SECURITY`; a non-superuser owner won't bypass it. Dev `nexconvo` is superuser → works locally. Prod needs `BYPASSRLS` or a per-tenant `set_config` loop.
- **Scheduler host (locked):** Automation service (empty web-host scaffold today; Hangfire+MassTransit wiring is net-new).

## Reference (read before mirroring)
- Owner-connection bare DbContext: `Integrations.Infrastructure/Persistence/IntegrationsDatabaseMigrator.cs`, `Chat.Infrastructure/Persistence/ChatDbContextFactory.cs` (`NullTenantContext`).
- Explicit-tenant background job: `Chat.Infrastructure/Jobs/KnowledgeIngestionJob.cs`.
- Testers/entities: `IConnectionTester<S3TestInput|AiTestInput|ChannelTestInput>` (DI-registered), `WorkspaceS3Config`/`WorkspaceAiConfig`/`ChannelConnection` (`ApplyHealth`, `LastTestStatus`, `IsActive`, encrypted creds).
- Persist-on-retest precedent: `Chat.Application/.../TestChannelConnectionByIdCommandHandler.cs`.
- MassTransit: Chat DI (consume+publish) vs Integrations DI (publish-only — needs `AddConsumers`+`ConfigureEndpoints`). Hangfire: `Chat.Infrastructure/DependencyInjection.cs` (server+storage on `ChatDbMigrator`).
- Contracts base `IntegrationEvent.cs`; example `Events/Integrations/S3ConfigUpdatedEvent.cs`.

---

## Task 1 — Contracts
**Files:** Create `src/shared/NexConvo.Contracts/Messages/CheckIntegrationHealthCommand.cs`; `src/shared/NexConvo.Contracts/Events/Health/IntegrationHealthFailedEvent.cs`.

- [ ] **Step 1:** `CheckIntegrationHealthCommand : IntegrationEvent` — empty payload record (fan-out trigger; `EventId`/`OccurredAt`/`CorrelationId` from base). Put in a new `Messages/` folder (or `Events/Health/` — pick one, note it).
- [ ] **Step 2:** `IntegrationHealthFailedEvent(Guid TenantId, string IntegrationKind, Guid ConfigId, string ConfigName, string? ErrorMessage, ConnectionStatus PreviousStatus) : IntegrationEvent`. `ConnectionStatus` is in `NexConvo.BuildingBlocks.Domain.Health` — confirm `NexConvo.Contracts` can reference it (it may need a project ref to BuildingBlocks.Domain; if that's a layering problem, model `PreviousStatus` as a string). Report the choice.
- [ ] **Step 3:** `dotnet build src/shared/NexConvo.Contracts`. Commit — `feat(contracts): CheckIntegrationHealthCommand + IntegrationHealthFailedEvent`.

## Task 2 — Integrations health sweep + consumer
**Files:** Create `Integrations.Infrastructure/HealthCheck/IntegrationsHealthSweepService.cs` (+ an `IIntegrationsHealthSweepService` interface in Application), `Integrations.Application/.../EventHandlers/CheckIntegrationHealthCommandConsumer.cs`; modify `Integrations.Infrastructure/DependencyInjection.cs` (register sweep + `AddConsumers` + `ConfigureEndpoints`). Tests in `Integrations.Infrastructure.Tests` / `Integrations.Application.UnitTests`.

- [ ] **Step 1: Failing tests** for the sweep logic. Extract the transition/persist logic so it's unit-testable with a fake tester + in-memory DbContext (the owner-connection construction is integration-only; unit-test the per-config decision). Cases: (a) probe Healthy → `ApplyHealth(Healthy)` persisted, NO event; (b) previous `Healthy` + probe Failed → `ApplyHealth(Failed)` persisted, `IntegrationHealthFailedEvent` published once with sanitized error + `PreviousStatus=Healthy`; (c) previous `Failed` + probe Failed → persisted, NO event (dedupe); (d) one config throws → logged, sweep continues to the next.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3: Implement** the sweep: build an owner-connection `IntegrationsDbContext` from `IntegrationsDbMigrator` (bare ctor, no interceptor — mirror `IntegrationsDatabaseMigrator`), load `WorkspaceS3Configs.Where(x=>x.IsActive)` + `WorkspaceAiConfigs.Where(x=>x.IsActive)` (all tenants), per config: decrypt → `TestAsync` → capture prev status → `ApplyHealth` → `SaveChanges` → publish on Healthy→Failed. Per-config try/catch. Sanitized logging (no keys). **Add the S3 FORCE-RLS prod caveat comment.** Register `IConnectionTester<S3TestInput>`/`<AiTestInput>` are already available. The consumer resolves the sweep and calls it.
- [ ] **Step 4:** Integrations MassTransit — add `x.AddConsumers(typeof(Integrations.Application.DependencyInjection).Assembly)` + `cfg.ConfigureEndpoints(context)`.
- [ ] **Step 5:** Run → PASS + `dotnet build src/services/Integrations/NexConvo.Integrations.Api`. Commit — `feat(integrations): health sweep + CheckIntegrationHealth consumer (Healthy→Failed events)`.

## Task 3 — Chat health sweep + consumer
**Files:** Mirror Task 2 for channels: `Chat.Infrastructure/HealthCheck/ChatHealthSweepService.cs` (+ interface), `Chat.Application/.../EventHandlers/CheckIntegrationHealthCommandConsumer.cs`; register sweep in Chat DI (Chat already has AddConsumers+ConfigureEndpoints). Tests in Chat.Infrastructure/Application.

- [ ] **Step 1: Failing tests** (same 4 cases as Task 2, for `ChannelConnection`).
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3: Implement** `ChatHealthSweepService` on the `ChatDbMigrator` owner connection, `ChannelConnections.Where(IsActive)`, decrypt token → `ChannelTestInput` → re-test → `ApplyHealth` → publish on transition. Add the consumer.
- [ ] **Step 4:** Run → PASS + `dotnet build src/services/Chat/NexConvo.Chat.Api`. Commit — `feat(chat): channel health sweep + CheckIntegrationHealth consumer`.

## Task 4 — Automation scheduler (Hangfire recurring, trigger-only)
**Files:** Modify `Automation/.../Worker/Program.cs` + `NexConvo.Automation.Worker.csproj` (+ Infrastructure DI); `docker-compose.yml` (Automation: add `AutomationDb`/`AutomationDbMigrator`, `RabbitMQ` conn strings + `depends_on` postgres+rabbitmq); `appsettings`. Test in a new `NexConvo.Automation.*.Tests` or inline.

- [ ] **Step 1: Failing test** — the publish action (a small `IHealthSweepScheduler`/job class) publishes exactly one `CheckIntegrationHealthCommand` via a fake `IPublishEndpoint`.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3: Implement:** in Automation Worker — `AddHangfire(UsePostgreSqlStorage(AutomationDbMigrator))` + `AddHangfireServer`; `AddMassTransit(UsingRabbitMq, publish-only)`; a job class `IntegrationHealthSweepScheduler.Trigger()` that publishes `CheckIntegrationHealthCommand`; register recurring `RecurringJob.AddOrUpdate("integration-health-sweep", x => x.Trigger(), cron)` with cron from config (default `Cron.HourInterval(6)` / `0 */6 * * *`) at startup (after `app.Build()`). Add packages (Hangfire.*, MassTransit.RabbitMQ) via CPM. Mount `/hangfire` dashboard in dev. The Worker needs NO DbContext for configs (only Hangfire's storage DB).
- [ ] **Step 4:** docker-compose — add the connection strings + `depends_on: [postgres, rabbitmq]` to the `automation` service (currently only identity/seq).
- [ ] **Step 5:** Run test → PASS + `dotnet build src/services/Automation/NexConvo.Automation.Worker`. Commit — `feat(automation): Hangfire recurring integration-health scheduler (trigger-only)`.

## Task 5 — End-to-end verification
- [ ] **Step 1:** `docker compose --profile apps up -d --build automation integrations chat` (+ ensure rabbitmq/postgres up).
- [ ] **Step 2:** `docker logs nexconvo-automation-1` → starts clean, Hangfire tables created in `nexconvo_automation`, recurring job registered, MassTransit bus started.
- [ ] **Step 3:** Trigger one sweep (Hangfire dashboard "Trigger now" on the recurring job, or a one-shot enqueue). Seed a config with a bad credential across 2 tenants beforehand.
- [ ] **Step 4:** Confirm in DB: the bad config's `last_test_status` flips to `Failed`; a temporary logging consumer (or RabbitMQ management UI) shows `IntegrationHealthFailedEvent` fired once; a second sweep does NOT re-fire for the already-Failed config.
- [ ] **Step 5:** `dotnet test NexConvo.sln` green. Commit any fixtures — `test: slice 5 scheduled re-test verification`.

## Self-Review notes
- Secrets: the sweep decrypts in-memory only; verify no key/token in logs or in `IntegrationHealthFailedEvent` (only sanitized `ErrorMessage`).
- Idempotency: event fires only on Healthy→Failed transition (compare prev `LastTestStatus` to probe), never on Failed→Failed.
- Owner-connection: no RLS interceptor attached to the sweep DbContext; cross-tenant enumeration works locally; S3 FORCE-RLS caveat documented.
- This slice's `IntegrationHealthFailedEvent` is the input to Slice 6 (Notification service).
