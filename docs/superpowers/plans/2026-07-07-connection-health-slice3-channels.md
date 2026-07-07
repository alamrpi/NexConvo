# Connection Health — Slice 3 (Channels) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply connection health to Chat channel connections AND make the channels UI actually work end-to-end: a real per-channel tester (Meta Graph `/me`, Telegram `getMe`) replacing the stub, persisted health, a test-by-id endpoint, and the frontend↔backend DTO/route/save-return mismatches fixed.

**Architecture:** Reuse the shared `ConnectionHealth`/`ConnectionStatus`/`IConnectionTester`/`ConnectionUnhealthyException`/`ConnectionTestFailedException` (BuildingBlocks). Build a real `ChannelConnectionTester : IConnectionTester<ChannelTestInput>` in Chat.Infrastructure using resilient named HttpClients (`AddNexConvoResilience`, currently missing in Chat). Add the 4 health fields to `ChannelConnection` + EF migration. Add a `POST /api/v1/channel-connections/{id}/test` endpoint (test a STORED connection by id, decrypting its token) to match the frontend, keep the existing pre-save `POST /test` (raw token). Extend the backend `ChannelConnectionDto` to the shape the frontend already expects (`status`, `errorMessage`, `displayName`, `maskedAccessToken`, `createdAt`) and make Save return the DTO. No live send/receive path exists, so guard-on-use is deferred; `EnsureHealthy()` is added for the future path.

**Tech Stack:** .NET 9, EF Core (PostgreSQL, snake_case), MediatR (CQRS), MassTransit, Polly via `AddNexConvoResilience`, Meta Graph API + Telegram Bot API, Next.js + React Query + Zod, MSW, xUnit.

## Global Constraints

- .NET 9; TS strict. Clean Architecture; CQRS; TDD; RLS/tenant-from-JWT; Standard 8 resiliency (Polly on outbound HttpClients); Standard 9 (never log tokens); Standard 12 (`settings:manage`); Standard 13 (encrypt tokens, never log/return raw token or raw provider error); Standard 14 audit intact; Standard 16 xmin; Standard 19 `/api/v1`; Standard 22 (test-then-save, persist health, guard). Bengali-first i18n. MSW mocking.
- **String-enum note:** the shared `ConnectionStatus` serializes as its member name. BUT the frontend channels DTO currently uses a LOWERCASE union `'connected'|'disconnected'|'error'` (its own `ConnectionStatus` type), NOT the 4-value health enum. Decision: map the health `ConnectionStatus` → the frontend's existing 3-value badge union in the DTO projection (Healthy→`connected`, Failed/Degraded→`error`, Untested→`disconnected`), so the already-built badge works without frontend churn. Expose the raw health too (`lastTestStatus`, `lastTestError`, `lastTestedAt`, `lastTestLatencyMs`) for parity/future.
- **Announce:** Meta Graph `/me` + Telegram `getMe` chosen as the liveness probes (cheapest authenticated call per provider). Web channel → always Healthy (no external call).

## Reference templates (already shipped — read before mirroring)

- Tester + sanitized errors: `Integrations.Infrastructure/ExternalServices/S3ConnectionTester.cs`, `AiConnectionTester.cs`.
- Entity health: `WorkspaceS3Config.cs` / `WorkspaceAiConfig.cs` (health fields + `ApplyHealth`/`EnsureHealthy`).
- Save gate: `SaveAiConfigCommandHandler.cs` (re-test on new/changed token → `ConnectionTestFailedException` 422, no side-effects, then `ApplyHealth`).
- Shared frontend: `features/settings/model/connection-health.schema.ts`, `components/health-badge.tsx`.
- Chat migration precedent: `Chat.Infrastructure/Migrations/*` (all via `dotnet ef`, `.Designer.cs` present, `ChatDbContextFactory` design-time factory, auto-apply via `ChatDatabaseMigrator` in Development).

---

## Task 1: ChannelConnection health fields + ApplyHealth + EnsureHealthy (Chat.Domain)

**Files:** Modify `src/services/Chat/NexConvo.Chat.Domain/Entities/ChannelConnection.cs`; Test `tests/services/Chat/NexConvo.Chat.Domain.Tests/ChannelConnectionHealthTests.cs` (create the test project if absent — nest under `tests` Solution Folder like the Chat/Integrations test projects).

**Interfaces:** Consumes `ConnectionHealth`/`ConnectionStatus`/`ConnectionUnhealthyException` from `NexConvo.BuildingBlocks.Domain.Health` (Chat.Domain already references BuildingBlocks.Domain). Produces on `ChannelConnection`: `DateTimeOffset? LastTestedAt`, `ConnectionStatus LastTestStatus` (default `Untested`), `string? LastTestError`, `int? LastTestLatencyMs`; `void ApplyHealth(ConnectionHealth)`; `void EnsureHealthy()` (throws `ConnectionUnhealthyException("channel", ...)` when not Healthy).

- [ ] **Step 1: Failing test** — mirror `WorkspaceAiConfigHealthTests`; construct `ChannelConnection` via its ctor (read the real ctor first). 3 cases (ApplyHealth healthy; EnsureHealthy throws when Untested; passes when Healthy).
- [ ] **Step 2: Run → FAIL.** `dotnet test tests/services/Chat/NexConvo.Chat.Domain.Tests --filter ChannelConnectionHealth`
- [ ] **Step 3: Implement** the 4 props (private setters) + `ApplyHealth` + `EnsureHealthy` (kind `"channel"`), `using NexConvo.BuildingBlocks.Domain.Health;`.
- [ ] **Step 4: Run → PASS** (3).
- [ ] **Step 5: Commit** — `feat(chat): ChannelConnection persisted health + ApplyHealth/EnsureHealthy`.

---

## Task 2: EF mapping + migration for channel health columns (Chat)

**Files:** Modify `Chat.Infrastructure/Persistence/Configurations/ChannelConnectionConfiguration.cs`; CLI migration.

- [ ] **Step 1: Add mappings** (snake_case to match the table convention): `last_test_status` (int, default Untested, `HasConversion<int>()`), `last_test_error` (maxlen 1000), `last_tested_at`, `last_test_latency_ms`. `using NexConvo.BuildingBlocks.Domain.Health;`. Keep existing mappings (xmin, filtered unique index) intact.
- [ ] **Step 2: Generate migration** (CLI — produces Designer + snapshot; never hand-write):
```
dotnet ef migrations add AddChannelConnectionHealthFields \
  --project src/services/Chat/NexConvo.Chat.Infrastructure \
  --startup-project src/services/Chat/NexConvo.Chat.Api
```
- [ ] **Step 3: Verify** 3 artifacts + the Chat snapshot (`ChatDbContextModelSnapshot.cs`) now has the health columns; `Up()` = 4 additive `AddColumn` on `channel_connections` only; no RLS/table recreate.
- [ ] **Step 4: Build** `dotnet build src/services/Chat/NexConvo.Chat.Infrastructure`.
- [ ] **Step 5: Commit** — `feat(chat): EF mapping + migration for channel connection health fields`.

---

## Task 3: Real ChannelConnectionTester + resilient HttpClients (Chat.Infrastructure)

**Files:**
- Create: `Chat.Application/Common/Interfaces/` — `ChannelTestInput` record (or place in a `Common` file): `record ChannelTestInput(ChatChannel Channel, string AccessToken, string? ExternalAccountId)`.
- Rewrite: `Chat.Infrastructure/Services/ChannelConnectionTester.cs` → implement `IConnectionTester<ChannelTestInput>` (drop the stub `IChannelConnectionTester`/`ChannelTestResult`, OR keep the interface but return `ConnectionHealth` — prefer switching to the shared `IConnectionTester<ChannelTestInput>` for consistency; update the command handler + DI accordingly).
- Modify: `Chat.Infrastructure/DependencyInjection.cs` — register named HttpClients with resilience + the tester + the `IChannelVerificationHttpClientFactory`.
- Test: `tests/services/Chat/NexConvo.Chat.Infrastructure.Tests/ChannelConnectionTesterTests.cs`.

**Interfaces:** `ChannelConnectionTester : IConnectionTester<ChannelTestInput>`, `IntegrationKind => "channel"`. Uses `IChannelVerificationHttpClientFactory` (already exists) to get a resilient client per channel. Probes:
- WhatsApp/Facebook/Instagram → Meta Graph `GET /me?access_token=…` (or `Authorization: Bearer`), success if 200; parse `name`/`id` into `ConnectionHealth.Healthy(accountName, latency)`.
- Telegram → `GET https://api.telegram.org/bot{token}/getMe`, success if `ok:true`.
- Web → `ConnectionHealth.Healthy("Web widget", 0)` (no call).
- Non-2xx / exception → `ConnectionHealth.Failed("<sanitized per-channel message>", latency)` — NEVER include the token or raw provider body.

- [ ] **Step 1: Wire resilient HttpClients** in `AddChatInfrastructure`: `services.AddHttpClient("MetaGraphApi", c => c.BaseAddress = new Uri("https://graph.facebook.com/")).AddNexConvoResilience();` and `AddHttpClient("TelegramApi", c => c.BaseAddress = new Uri("https://api.telegram.org/")).AddNexConvoResilience();` and a default `"ChannelVerification"`. Register `IChannelVerificationHttpClientFactory` → `ChannelVerificationHttpClientFactory`. (`AddNexConvoResilience` is in `NexConvo.BuildingBlocks.Resilience`, already referenced.)
- [ ] **Step 2: Failing tester test** — NSubstitute/fake the `IChannelVerificationHttpClientFactory` to return an `HttpClient` backed by a fake `HttpMessageHandler` returning 200 (`{"id":"1","name":"Acme"}` / `{"ok":true,"result":{...}}`) or 401. Cases: Meta healthy (Success, Healthy, latency, accountName); Telegram healthy; failure (401 → Failed, sanitized, no token in message); Web (Healthy, no HTTP). Mirror `S3ConnectionTesterTests` structure.
- [ ] **Step 3: Run → FAIL.**
- [ ] **Step 4: Implement** the tester (Stopwatch; per-channel branch; sanitized errors; `internal sealed` + `InternalsVisibleTo` for the test project). Register `IConnectionTester<ChannelTestInput>` in DI.
- [ ] **Step 5: Run → PASS** + `dotnet build src/services/Chat/NexConvo.Chat.Api`.
- [ ] **Step 6: Commit** — `feat(chat): real ChannelConnectionTester (Meta /me, Telegram getMe) + resilient HttpClients`.

---

## Task 4: TestChannelConnection commands — pre-save (token) + by-id (stored) → ConnectionHealth

**Files:**
- Modify: `Features/ChannelConnections/Commands/TestChannelConnectionCommand.cs` (+Handler) → return `ConnectionHealth`, use the new tester.
- Create: `Features/ChannelConnections/Commands/TestChannelConnectionByIdCommand.cs` (+Handler) → `(Guid ConnectionId) : IRequest<ConnectionHealth>`; loads the stored `ChannelConnection` by tenant+id, decrypts the token (`IAesEncryptionService`), tests, and PERSISTS `ApplyHealth(probe)` (this is the "re-test a saved connection" path the frontend drawer + row use).
- Test: extend `Chat.Application` tests.

**Interfaces:** `TestChannelConnectionCommand(ChatChannel Channel, string AccessToken, string? ExternalAccountId) : IRequest<ConnectionHealth>` (pre-save, no persist). `TestChannelConnectionByIdCommand(Guid ConnectionId) : IRequest<ConnectionHealth>` (loads, decrypts, tests, `ApplyHealth`, SaveChanges — so the badge reflects the latest test).

- [ ] **Step 1: Failing tests** — pre-save: explicit token → tester receives it, returns ConnectionHealth. by-id: stored connection + decrypt → tester receives decrypted token; on result, entity `LastTestStatus` persisted; not-found → `ConnectionHealth.Failed("Connection not found.", null)` (or a Result error — pick and be consistent). Web short-circuit stays.
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Implement** both commands/handlers; delete `TestChannelConnectionResult`/`ChannelTestResult`; fix refs.
- [ ] **Step 4: Run → PASS** + build.
- [ ] **Step 5: Commit** — `feat(chat): channel test commands return ConnectionHealth; add test-by-id (persists health)`.

---

## Task 5: Save gate + extend ChannelConnectionDto + Save returns DTO

**Files:**
- Modify: `SaveChannelConnectionCommandHandler.cs` (re-test gate on new/changed token → `ConnectionTestFailedException` 422, `ApplyHealth`, no side-effects on failure), and change its return from `Result<Guid>` to `Result<ChannelConnectionDto>`.
- Modify: `Features/ChannelConnections/Dtos/ChannelConnectionDto.cs` — extend to the frontend-expected shape: `Id, Channel, ExternalAccountId, DisplayName (=AccountName), MaskedAccessToken, IsActive, CreatedAt, Status (frontend 3-value union string), ErrorMessage, LastTestStatus, LastTestedAt, LastTestError, LastTestLatencyMs`.
- Modify: `GetChannelConnectionsQueryHandler.cs` projection to fill the new fields, mapping health `ConnectionStatus` → the 3-value `status` (Healthy→"connected", Failed/Degraded→"error", Untested→"disconnected") and `ErrorMessage` from `LastTestError`.
- Modify: `ChannelConnectionsController.cs` — Save action returns the DTO; add `POST {id}/test` → `TestChannelConnectionByIdCommand`.
- Test: extend Save + Get handler tests.

- [ ] **Step 1: Failing tests** — Save with token + tester Failed → `ConnectionTestFailedException`, nothing persisted/audited/published; Save success → returns a `ChannelConnectionDto` with `.Id` + `Status=="connected"`; Get projection maps a Healthy entity → `status:"connected"`, an untested → `"disconnected"`. Mirror `SaveAiConfigGateTests` + `GetS3ConfigQueryHandlerTests`.
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Implement** the gate + DTO extension + projection + controller changes (Save returns `result.ToActionResult()` now carrying the DTO; add `[HttpPost("{id:guid}/test")]`). Confirm the status→union mapping helper is a single pure function (reused in Save + Get).
- [ ] **Step 4: Run → PASS** + `dotnet build src/services/Chat/NexConvo.Chat.Api`.
- [ ] **Step 5: Commit** — `feat(chat): channel save re-test gate (422) + health DTO + Save returns DTO + POST {id}/test`.

---

## Task 6: Gateway route for {id}/test (Standard 21)

**Files:** Modify `src/gateway/NexConvo.Gateway/appsettings.json`.

- [ ] **Step 1:** Confirm the existing `channel-connections` gateway route covers `/{id}/test` (a catch-all `/api/v1/channel-connections/{**catch-all}` would). If only a root/specific route exists, add the catch-all route to the chat cluster (mirror how `knowledge-documents` or `s3-config` catch-all routes are defined). Report what you found.
- [ ] **Step 2: Commit** if changed — `fix(gateway): route channel-connections/{id}/test to chat cluster`.

---

## Task 7: Frontend — align to backend, health badge, 422 handling

**Files:** `frontend/src/features/settings/model/channel-connection.types.ts` + `channel-connection.schema.ts`; `components/channel-connections-page.tsx`; hooks `use-save-channel-connection.ts`, `use-test-channel-connection.ts`, `use-channel-connections.ts`; BFF `app/api/bff/settings/channels/route.ts`, `[id]/route.ts`, `[id]/test/route.ts`; i18n en/bn.

**Interfaces:** The frontend already has `status`/`errorMessage`/`displayName`/`maskedAccessToken` + a `VerifyState` machine gating "Next" and a status Badge — most of the UI exists. This task ALIGNS it to the now-correct backend and adds the shared `HealthBadge`.

- [ ] **Step 1:** Confirm the backend Save now returns `ChannelConnectionDto` (Task 5) — the drawer's `savedConnectionId = (result as ChannelConnectionDto).id` now works; verify types match the extended DTO (field names: `displayName`, `maskedAccessToken`, `status`, `errorMessage`, `lastTestStatus`, `lastTestedAt`). Update `channel-connection.types.ts` to include the health fields.
- [ ] **Step 2:** `use-test-channel-connection.ts` posts `/settings/channels/{id}/test` → now returns `ConnectionHealth` (`{success,status,detail,errorMessage,latencyMs}`); update its result type + the drawer's `handleVerify` to read `success`/`errorMessage` from the new shape (keep `verifyState` gating).
- [ ] **Step 3:** Replace the row's bespoke status badge with the shared `HealthBadge` where sensible (or keep the 3-value badge but source it from the now-populated `status`). Show `lastTested` relative time.
- [ ] **Step 4:** BFF `channels/route.ts` POST (save) — map 422 `connection-test-failed` → `{code:'test-failed'}` (mirror S3/AI); `use-save-channel-connection.ts` surface it; the drawer shows `t('...credentialsExpired')` on save-422.
- [ ] **Step 5:** i18n — add channel health keys to en.json + bn.json (real Bengali): reuse the shared health status labels + `credentialsExpired` + `lastTested`.
- [ ] **Step 6:** `cd frontend && npx vitest run` + `npx tsc --noEmit` + lint — all green (mind existing channel tests: `use-test-channel-connection.test.tsx`, `use-save-channel-connection.test.tsx`, `channel-connection.schema.test.ts` — update for shape changes).
- [ ] **Step 7: Commit** — `feat(frontend): channel connection health badge + ConnectionHealth test + 422; align to backend DTO`.

---

## Task 8: End-to-end verification

- [ ] **Step 1: Rebuild** `docker compose up -d --build chat gateway`.
- [ ] **Step 2:** `docker logs --tail 20 nexconvo-chat-1` → migration applied (Chat auto-migrates in Development), no `PendingModelChangesWarning`, listening.
- [ ] **Step 3:** Verify columns: `docker exec nexconvo-postgres-1 psql -U nexconvo -d nexconvo_chat -tAc "SELECT count(*) FROM information_schema.columns WHERE table_name='channel_connections' AND column_name LIKE 'last_test%';"` → 4 (confirm the Chat DB name; check docker-compose/appsettings for the Chat connection string DB name).
- [ ] **Step 4:** `curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost:5055/api/v1/channel-connections/<any-guid>/test` → 401 (routed, auth required — NOT 404).
- [ ] **Step 5:** Backend `dotnet test NexConvo.sln` green; frontend `npx vitest run` + `npx tsc --noEmit` clean.
- [ ] **Step 6: Commit** fixtures — `test: channel connection-health verification`.

---

## Self-Review notes

- **Scope beyond S3/AI:** this slice also fixes 3 pre-existing mismatches (route `/{id}/test` missing, backend DTO lacked `status`, Save returned `Result<Guid>` not DTO) — each is a task deliverable, not a placeholder.
- **Two test paths:** pre-save `POST /test` (raw token, no persist) AND by-id `POST /{id}/test` (stored token, persists health). The frontend drawer uses the by-id path after save; keep both.
- **Status mapping:** the frontend badge uses a 3-value lowercase union; the DTO projection maps the 4-value health enum → that union via one pure helper (reused in Save + Get). Also expose raw `lastTestStatus` for future/parity.
- **Secrets:** tester sanitizes per-channel errors (no token, no raw provider body); tokens stay encrypted; by-id handler decrypts only in-memory for the probe.
- **Guard deferred:** no live send/receive path; `EnsureHealthy()` added for the future path (documented), consistent with S3/AI.
- **Type consistency:** `ChannelTestInput`/`IConnectionTester<ChannelTestInput>`/`ConnectionHealth`/health fields/`ConnectionTestFailedException`/`TestChannelConnectionByIdCommand` used consistently.
