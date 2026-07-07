# Connection Health — Slice 4 (AI Provider) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the existing connection-health abstraction to the AI provider config: migrate the AI test to `ConnectionHealth`, persist health on `WorkspaceAiConfig`, add a server-side test-then-save gate (422), expose health in the read DTO, and add the frontend health badge — mirroring the completed S3 slice.

**Architecture:** Reuse the shared `ConnectionHealth`/`ConnectionStatus`/`IConnectionTester`/`ConnectionUnhealthyException`/`ConnectionTestFailedException` (BuildingBlocks, from Slices 1-2). The AI test today returns a bespoke `TestAiConnectionResult(bool, string?)` and probes the provider inline; we migrate it to return `ConnectionHealth` (with latency + sanitized errors) behind an `IConnectionTester<AiTestInput>`, then wire persistence + save-gate + DTO + frontend exactly as S3 did. No live AI-generation guard target exists yet (consumers only cache the config; nothing reads it to generate), so — like S3 — the enforceable gate is save-time re-test; `EnsureHealthy()` is added to the entity for the future generation path.

**Tech Stack:** .NET 9, EF Core (PostgreSQL), MediatR (CQRS), MassTransit, `NexConvo.BuildingBlocks.Ai` (IAiProviderFactory), Next.js App Router + React Query + Zod, MSW, xUnit.

## Global Constraints

- All `.NET` projects target `net9.0`; frontend TypeScript strict.
- **Clean Architecture** (Standard 1); **CQRS via MediatR** (Standard 3); **TDD** (Standard 4).
- **RLS / tenant from JWT** (Standard 6): tenant via `ITenantContext`, never request body.
- **Observability** (Standard 9): never log secrets.
- **Authorization** (Standard 12): AI endpoints use policy `settings:manage` (already set on the controller).
- **Secrets** (Standard 13): API keys encrypted at rest via `IAesEncryptionService`; **never logged; never returned in error messages**. NOTE: the current AI test handler returns raw `HttpRequestException.Message` — the migration MUST sanitize this (mirror `S3ConnectionTester`, which returns a generic message).
- **Audit** (Standard 14): the existing `integrations.ai-config.*` audit + `AiConfigUpdatedEvent` publish stay intact.
- **Optimistic concurrency** (Standard 16): `WorkspaceAiConfig` carries shadow `xmin`; health updates must not break it.
- **API versioning** (Standard 19): routes under `/api/v1/ai-config` (unchanged).
- **Connection health** (Standard 22): test-then-save, persist health, guard (guard deferred — no live use site). Spec: `docs/superpowers/specs/2026-07-07-connection-health-design.md`.
- **Bengali-first i18n**: every new UI string in both `en.json` and `bn.json`.
- **The string-enum contract:** `ConnectionStatus` serializes as its string member name (`"Untested"|"Healthy"|"Degraded"|"Failed"`) via the API's global `JsonStringEnumConverter`. Frontend models it as a string union, NOT a number. (Reuse the S3 `s3HealthStatusSchema` shape.)

## Reference: the S3 slice is the exact template

These already-shipped files are the pattern to mirror (read them before implementing the matching AI task):
- Entity health: `WorkspaceS3Config.cs` (health fields + `ApplyHealth`/`EnsureHealthy`).
- EF + migration: `WorkspaceS3ConfigConfiguration.cs`, migration `20260707053440_AddS3ConfigHealthFields`.
- Tester: `Infrastructure/ExternalServices/S3ConnectionTester.cs` (Stopwatch, sanitized errors, `ConnectionHealth`).
- Test command/handler: `Features/S3Config/Commands/TestS3ConnectionCommand.cs` (+Handler) — stored-key fallback.
- Save gate: `SaveS3ConfigCommandHandler.cs` (re-test on new/changed key → `ConnectionTestFailedException` 422 → `ApplyHealth`, no side-effects on failure).
- DTO: `Features/S3Config/Queries/S3ConfigDto.cs` (+ handler projection).
- Frontend: `model/s3-config.schema.ts` (`s3HealthStatusSchema`, `s3TestResultSchema`), `model/s3-config.types.ts`, `components/s3-config-form.tsx` (`HealthBadge`), `api/use-test-s3-connection.ts`, BFF `app/api/bff/settings/s3-config/test/route.ts` + `route.ts` (422 discrimination on `code === 'connection-test-failed'`).

---

## Task 1: WorkspaceAiConfig health fields + ApplyHealth + EnsureHealthy

**Files:**
- Modify: `src/services/Integrations/NexConvo.Integrations.Domain/Entities/WorkspaceAiConfig.cs`
- Test: `tests/services/Integrations/NexConvo.Integrations.Domain.Tests/WorkspaceAiConfigHealthTests.cs`

**Interfaces:**
- Consumes: `ConnectionHealth`, `ConnectionStatus`, `ConnectionUnhealthyException` (from `NexConvo.BuildingBlocks.Domain.Health`).
- Produces on `WorkspaceAiConfig`: `DateTimeOffset? LastTestedAt`, `ConnectionStatus LastTestStatus` (default `Untested`), `string? LastTestError`, `int? LastTestLatencyMs`; `void ApplyHealth(ConnectionHealth)`; `void EnsureHealthy()` (throws `ConnectionUnhealthyException("ai", LastTestError ?? "Run a connection test in Settings.")` when `LastTestStatus != Healthy`).

- [ ] **Step 1: Write the failing test** — mirror `WorkspaceS3ConfigHealthTests` exactly, but construct a `WorkspaceAiConfig` via its ctor `(tenantId, provider, encryptedApiKey, baseUrl, defaultModel, systemPrompt, parameters, isActive)`. Three cases: `ApplyHealth(Healthy)` sets status/latency + clears error + stamps `LastTestedAt`; `EnsureHealthy()` throws when Untested; `EnsureHealthy()` passes when Healthy. Use `ConnectionUnhealthyException` from `NexConvo.BuildingBlocks.Domain.Health`.

```csharp
using NexConvo.BuildingBlocks.Domain.Health;
using NexConvo.Integrations.Domain.Entities;
using NexConvo.Contracts.Enums; // AiProviderType — confirm the actual namespace of AiProviderType while implementing
using Xunit;

public sealed class WorkspaceAiConfigHealthTests
{
    private static WorkspaceAiConfig New() =>
        new(Guid.NewGuid(), default /* AiProviderType */, "enc-key", null, "gpt-4o", null, null, true);

    [Fact]
    public void ApplyHealth_healthy_sets_status_and_clears_error()
    {
        var c = New();
        c.ApplyHealth(ConnectionHealth.Healthy("ok", 30));
        Assert.Equal(ConnectionStatus.Healthy, c.LastTestStatus);
        Assert.Null(c.LastTestError);
        Assert.Equal(30, c.LastTestLatencyMs);
        Assert.NotNull(c.LastTestedAt);
    }

    [Fact]
    public void EnsureHealthy_throws_when_not_healthy()
        => Assert.Throws<ConnectionUnhealthyException>(() => New().EnsureHealthy());

    [Fact]
    public void EnsureHealthy_passes_when_healthy()
    {
        var c = New();
        c.ApplyHealth(ConnectionHealth.Healthy("ok", 10));
        c.EnsureHealthy();
    }
}
```
(While implementing, confirm the real `AiProviderType` enum + namespace and pick a valid member instead of `default`.)

- [ ] **Step 2: Run → FAIL.** `dotnet test tests/services/Integrations/NexConvo.Integrations.Domain.Tests --filter WorkspaceAiConfigHealth`
- [ ] **Step 3: Implement** — add the four private-setter props + `ApplyHealth` + `EnsureHealthy` (kind `"ai"`), mirroring `WorkspaceS3Config`. `using NexConvo.BuildingBlocks.Domain.Health;`. No new project reference needed (both types are in BuildingBlocks.Domain).
- [ ] **Step 4: Run → PASS** (3 tests).
- [ ] **Step 5: Commit** — `feat(integrations): WorkspaceAiConfig persisted health + ApplyHealth/EnsureHealthy`.

---

## Task 2: EF mapping + migration for AI health columns

**Files:**
- Modify: `.../Infrastructure/Persistence/Configurations/WorkspaceAiConfigConfiguration.cs`
- Create (CLI): migration `AddAiConfigHealthFields`

- [ ] **Step 1: Add mappings** (mirror the S3 config's health mappings):
```csharp
builder.Property(x => x.LastTestStatus).HasConversion<int>().HasDefaultValue(ConnectionStatus.Untested);
builder.Property(x => x.LastTestError).HasMaxLength(1000);
builder.Property(x => x.LastTestedAt);
builder.Property(x => x.LastTestLatencyMs);
```
Add `using NexConvo.BuildingBlocks.Domain.Health;`.

- [ ] **Step 2: Generate migration via CLI** (produces Designer + snapshot — never hand-write):
```
dotnet ef migrations add AddAiConfigHealthFields \
  --project src/services/Integrations/NexConvo.Integrations.Infrastructure \
  --startup-project src/services/Integrations/NexConvo.Integrations.Api
```
- [ ] **Step 3: Verify** all 3 artifacts exist and snapshot has the AI columns: `grep -c "LastTestStatus" .../Migrations/IntegrationsDbContextModelSnapshot.cs` should now be ≥2 (S3 + AI). Confirm the migration `Up()` is 4 additive `AddColumn` on `WorkspaceAiConfigs` only, no RLS SQL (existing table policy covers new columns).
- [ ] **Step 4: Build** `dotnet build src/services/Integrations/NexConvo.Integrations.Infrastructure`.
- [ ] **Step 5: Commit** — `feat(integrations): EF mapping + migration for AI config health fields`.

---

## Task 3: AiTestInput + IConnectionTester<AiTestInput> (adapt the inline probe)

**Files:**
- Create: `.../Application/Features/AiConfig/AiTestInput.cs`
- Create: `.../Infrastructure/ExternalServices/AiConnectionTester.cs`
- Modify: `.../Infrastructure/DependencyInjection.cs` (register)
- Test: `tests/services/Integrations/NexConvo.Integrations.Infrastructure.Tests/AiConnectionTesterTests.cs`

**Interfaces:**
- Produces: `record AiTestInput(AiProviderType Provider, string ApiKey, string? BaseUrl, string Model)`; `AiConnectionTester : IConnectionTester<AiTestInput>` with `IntegrationKind => "ai"`. Takes `IAiProviderFactory` (ctor-injected). `TestAsync`: `Stopwatch`; `providerFactory.GetProvider(input.Provider)`; consume one chunk of `GenerateStreamAsync("Say OK", null, input.ApiKey, input.Model, baseUrl, ct)` then break; success → `ConnectionHealth.Healthy("Provider reachable", latencyMs)`; catch `HttpRequestException` → `ConnectionHealth.Failed("Provider connection failed. Check the API key, model, and base URL.", latencyMs)` (SANITIZED — do NOT include `ex.Message`, unlike the current handler); catch generic `Exception` → `ConnectionHealth.Failed("Connection failed. Check the API key and model name.", latencyMs)`. Log only the provider + exception type, never the key.

- [ ] **Step 1: Write the failing test** — mirror `S3ConnectionTesterTests` with NSubstitute: substitute `IAiProviderFactory` → returns a fake provider whose `GenerateStreamAsync` yields one chunk (healthy) or throws `HttpRequestException` (failed). Two cases: healthy (`Success`, `Status==Healthy`, `LatencyMs != null`); failed (`!Success`, `Status==Failed`, non-empty `ErrorMessage` that does NOT contain the thrown exception's raw text). Check how `IAiProviderFactory`/the provider's `GenerateStreamAsync` (`IAsyncEnumerable<...>`) is shaped in `NexConvo.BuildingBlocks.Ai.Services` and build the fake accordingly (an async iterator helper).
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Implement** `AiTestInput` + `AiConnectionTester` (internal sealed; add `InternalsVisibleTo` for the test project mirroring `S3ConnectionTester`).
- [ ] **Step 4: Register in DI:** `services.AddScoped<IConnectionTester<AiTestInput>, AiConnectionTester>();` (`AiConnectionTester` takes `IAiProviderFactory` which is already registered — confirm).
- [ ] **Step 5: Run → PASS** (2 tests) + `dotnet build ...Integrations.Api`.
- [ ] **Step 6: Commit** — `feat(integrations): AiConnectionTester (IConnectionTester) wrapping provider probe, sanitized errors`.

---

## Task 4: Migrate TestAiConnectionCommand to return ConnectionHealth

**Files:**
- Modify: `.../Application/Features/AiConfig/Commands/TestAiConnectionCommand.cs` (drop `TestAiConnectionResult`, use `ConnectionHealth`)
- Modify: `TestAiConnectionCommandHandler.cs` (use the tester + stored-key fallback)
- Modify: `.../Api/Controllers/WorkspaceAiConfigController.cs` (Test action returns `ConnectionHealth`) + `Api/Models/TestAiConnectionRequest.cs` (unchanged shape, confirm)
- Test: update/extend `.../Application.UnitTests` AI test-handler tests

**Interfaces:**
- `TestAiConnectionCommand(AiProviderType Provider, string? ApiKey, string? BaseUrl, string Model) : IRequest<ConnectionHealth>`.
- Handler: if `ApiKey` blank → load stored config by tenant+provider, decrypt (`ConnectionHealth.Failed("No API key configured for this provider.", null)` if none); call `tester.TestAsync(new AiTestInput(Provider, key, BaseUrl, Model), ct)`; return the `ConnectionHealth`. Inject `IConnectionTester<AiTestInput>` + keep `IIntegrationsDbContext`, `ITenantContext`, `IAesEncryptionService`. Remove the inline `IAiProviderFactory` probe (now in the tester).

- [ ] **Step 1: Write/adjust failing tests** — mirror `TestS3ConnectionCommandHandlerTests`: (a) explicit key → tester receives it; (b) blank key + stored config → tester receives decrypted key; (c) blank key + no config → `ConnectionHealth.Failed("No API key configured...", null)`. Capturing fake `IConnectionTester<AiTestInput>`.
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Implement** the command + handler changes; update the controller `Test` action to `Ok(await sender.Send(...))` now returning `ConnectionHealth`. Delete the obsolete `TestAiConnectionResult` record and fix any remaining references (search the solution).
- [ ] **Step 4: Run → PASS** + `dotnet build ...Integrations.Api`.
- [ ] **Step 5: Commit** — `feat(integrations): TestAiConnectionCommand returns ConnectionHealth via AiConnectionTester`.

---

## Task 5: Server test-then-save gate in SaveAiConfigCommandHandler

**Files:**
- Modify: `.../Application/Features/AiConfig/Commands/SaveAiConfigCommandHandler.cs`
- Test: extend `.../Application.UnitTests` with `SaveAiConfigGateTests.cs`

**Interfaces:** Inject `IConnectionTester<AiTestInput> tester`. When the API key is new/changed (`!string.IsNullOrWhiteSpace(request.ApiKey)`), run the tester with the effective key BEFORE persisting; `!probe.Success` → throw `ConnectionTestFailedException("ai", probe.ErrorMessage)` (reuse the existing exception → 422); on success `config.ApplyHealth(probe)`. When the key is unchanged, skip the re-test and leave prior health. Preserve the existing single-active enforcement, audit log, and `AiConfigUpdatedEvent` publish. **Ordering: throw before any Add/SaveChanges/audit/publish** (mirror S3 Task 9 exactly — no side effects on failure).

- [ ] **Step 1: Write failing tests** — (a) new key + tester Failed → throws `ConnectionTestFailedException`, nothing persisted/audited/published (assert `DidNotReceive` on Add/SaveChanges/Publish); (b) new key + tester Healthy → persists with `LastTestStatus == Healthy`. Mirror `SaveS3ConfigGateTests`.
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Implement** the gate. Effective key = `request.ApiKey` (new) — on a brand-new config the key is always required; on update with a blank key, skip re-test (key unchanged).
- [ ] **Step 4: Run → PASS** + `dotnet build ...Integrations.Api`.
- [ ] **Step 5: Commit** — `feat(integrations): server re-test gate on AI config save (422) + persist health`.

---

## Task 6: Expose AI health in AiConfigDto + query

**Files:**
- Modify: `.../Application/Features/AiConfig/Queries/AiConfigDto.cs`, `GetAiConfigQueryHandler.cs`
- Test: extend the AI query-handler test.

**Interfaces:** Add to `AiConfigDto`: `ConnectionStatus LastTestStatus, DateTimeOffset? LastTestedAt, string? LastTestError, int? LastTestLatencyMs`. Map them in the handler projection for each config.

- [ ] **Step 1: Write failing test** asserting the DTO carries `LastTestStatus` from an entity with `ApplyHealth(Healthy)` applied.
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Add fields + map in projection.**
- [ ] **Step 4: Run → PASS** + build.
- [ ] **Step 5: Commit** — `feat(integrations): expose AI connection health in GetAiConfig DTO`.

---

## Task 7: Frontend — types/schema, test-result shape, health badge, 422 handling

**Files:**
- Modify: `frontend/src/features/settings/model/ai-settings.types.ts`, `ai-settings.schema.ts`
- Modify: `frontend/src/features/settings/api/use-test-ai-connection.ts` (result → `ConnectionHealth` shape)
- Modify: `frontend/src/features/settings/api/use-update-ai-settings.ts` + BFF `frontend/src/app/api/bff/settings/ai-config/route.ts` (map 422 `connection-test-failed`)
- Modify: `frontend/src/features/settings/components/ai-settings-form.tsx` (HealthBadge + 422 toast)
- Modify: `frontend/src/shared/i18n/messages/en.json` + `bn.json`
- Test: extend `use-test-ai-connection.test.tsx` + schema test.

**Interfaces:** `AiConfigDto` gains `lastTestStatus` (string union `'Untested'|'Healthy'|'Degraded'|'Failed'`), `lastTestedAt`, `lastTestError`, `lastTestLatencyMs`. `TestAiConnectionResult` becomes `{ success, status, detail?, errorMessage?, latencyMs? }` (reuse the S3 `s3TestResultSchema` shape → make a shared `connectionHealthSchema` or copy). Reuse the S3 `HealthBadge` (extract to a shared settings component if trivial, else copy).

- [ ] **Step 1: Types + schema** — add the health enum + fields to AI model; adjust the test-result type. If practical, extract the S3 `healthStatusSchema`/`connectionHealthSchema`/`HealthBadge` into a shared `features/settings/model/connection-health.*` + `components/health-badge.tsx` and reuse from both S3 and AI (DRY). Otherwise copy and note it. Schema test first.
- [ ] **Step 2: Update `use-test-ai-connection.ts`** result type to the ConnectionHealth shape; the form reads `success`/`status`/`errorMessage`/`latencyMs`. Update its MSW test (success with latency; failure at 200-body `success:false`).
- [ ] **Step 3: BFF 422** — in `ai-config/route.ts` POST (save), map a `422` with `code === 'connection-test-failed'` → `{ code: 'test-failed' }` (mirror the S3 route's discrimination); update `use-update-ai-settings.ts` `mapError` to surface it.
- [ ] **Step 4: Form** — add `HealthBadge` driven by `config.lastTestStatus` + "last tested {relative}"; on save-422 show inline/toast `t('ai.credentialsExpired')` and reset `testState`. Keep the existing save-gating (it already gates on `testState === 'success'`), but drive the success/failure feedback from the new `status`/`errorMessage` fields.
- [ ] **Step 5: i18n** — add AI health keys to en.json + bn.json (real Bengali): `ai.credentialsExpired`, `ai.health{Healthy,Degraded,Failed,Untested}`, `ai.lastTested`, `ai.lastTestedNever` (reuse S3 wording; if the extracted shared component uses shared keys, add those once).
- [ ] **Step 6: Run** `cd frontend && npx vitest run` (all pass) + `npx tsc --noEmit` (clean) + lint.
- [ ] **Step 7: Commit** — `feat(frontend): AI provider health badge + ConnectionHealth test result + 422 handling`.

---

## Task 8: End-to-end verification

- [ ] **Step 1: Rebuild** `docker compose up -d --build integrations gateway`.
- [ ] **Step 2:** `docker logs --tail 20 nexconvo-integrations-1` → "Integrations database is up to date.", no `PendingModelChangesWarning`.
- [ ] **Step 3:** Verify AI health columns: `docker exec nexconvo-postgres-1 psql -U nexconvo -d nexconvo_integrations -tAc "SELECT count(*) FROM information_schema.columns WHERE table_name='WorkspaceAiConfigs' AND column_name LIKE 'LastTest%';"` → 4.
- [ ] **Step 4:** `curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost:5055/api/v1/ai-config/test -H "Content-Type: application/json" -d '{}'` → 401 (routed, auth required).
- [ ] **Step 5:** Backend suite `dotnet test NexConvo.sln` green; frontend `cd frontend && npx vitest run` + `npx tsc --noEmit` clean.
- [ ] **Step 6: Commit** any fixtures — `test: AI connection-health verification`.

---

## Self-Review notes

- **Spec coverage:** persisted health (T1-T2, T6), ConnectionHealth migration + sanitized errors (T3-T4), test-then-save gate 422 (T5), frontend badge + 422 (T7). Guard-on-use is DEFERRED (no live AI-generation site — documented; `EnsureHealthy()` added for the future path).
- **Secrets hardening:** T3 explicitly sanitizes the error (the current handler leaks `HttpRequestException.Message` — Standard 13). Verify no key/raw-exception text in any returned message.
- **Breaking change:** dropping `TestAiConnectionResult` for `ConnectionHealth` changes the AI test endpoint's response body + the frontend result type in lockstep (T4 backend + T7 frontend) — they must ship together; the E2E (T8) confirms the wire shape.
- **DRY opportunity (T7):** consider extracting the S3 `HealthBadge` + health schema into shared settings modules reused by both S3 and AI rather than duplicating.
- **Type consistency:** `ConnectionHealth`/`ConnectionStatus`/`IConnectionTester<AiTestInput>`/`AiTestInput`/`ApplyHealth`/`EnsureHealthy`/`ConnectionTestFailedException` used consistently; string-enum contract matches S3.
