# Connection Health — Slice 6 (Notification Service) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** A stateless Notification microservice consumes `IntegrationHealthFailedEvent` (published by the Slice-5 sweeps), resolves the workspace owner+admin emails from Identity via a new internal endpoint (shared-secret auth), and emails them via the platform transactional pool. Closes Standard 22's alerting clause.

**Architecture:** Security-first hybrid. Owning services publish tenant-scoped events (never learn recipients). Identity owns user/contact data and exposes `GET /internal/tenants/{tenantId}/health-alert-recipients` (owner+admin active users' name+email), guarded by an `X-Internal-Api-Key` shared secret. Notification is the sole holder of the alert email-provider secret; it's stateless (no DB) — consume → lookup → send. Dedupe is by the sweep's Healthy→Failed transition (rare redelivery double-email accepted).

**Tech Stack:** .NET 9, MassTransit/RabbitMQ (consume), EF Core (Identity query only), Resend/SMTP (Mailpit in dev), Polly (`AddNexConvoResilience`), xUnit, NSubstitute.

## Global Constraints
- .NET 9; Clean Architecture; CQRS (Identity query); TDD; **Standard 13 — alert email + logs carry only sanitized `ErrorMessage` + non-secret `ConfigName`; email-provider secret lives only in Notification; the shared internal key is env-only**; Standard 8 (Polly on the internal HttpClient + the resend client); Standard 11 (config via env); Standard 12 (the `/internal` endpoint is NOT user-JWT — it's shared-secret-guarded and must NOT be reachable via the public gateway). Central Package Management; CA1050.
- **Locked decisions:** recipients resolved by TenantId via the new Identity endpoint; service auth = `X-Internal-Api-Key` header; dedupe = transition-only, no DB.

## Reference (mirror these)
- Worker host: `src/services/Automation/NexConvo.Automation.Worker/Program.cs` (drop Hangfire) + `Automation.Infrastructure/DependencyInjection.cs` (but CONSUME).
- Consume MassTransit: `Chat.Infrastructure/DependencyInjection.cs` (`AddConsumers` + `ConfigureEndpoints`).
- Owner/admin join: `Identity.Application/Users/OwnerGuard.cs`.
- Email shapes: `Identity.Application/Abstractions/Mailing/IEmailSender.cs`, `Identity.Infrastructure/Mailing/ResendEmailSender.cs`.
- Contract: `NexConvo.Contracts/Events/Health/IntegrationHealthFailedEvent.cs`.

---

## Task 1 — Identity internal recipients endpoint + shared-secret auth
**Files:** Create `Identity.Application/Users/GetHealthAlertRecipients.cs` (query+handler+`RecipientDto`); `Identity.Api/Controllers/InternalController.cs`; `Identity.Api/.../InternalApiKeyMiddleware.cs` (or an endpoint filter). Tests in Identity.Tests.

- [ ] **Step 1: Failing tests** — (query) returns active Owner+Admin users' {Name,Email} for a tenant, dedup by email, excludes inactive users + non-owner/admin roles + other tenants; (middleware) 401 on missing/wrong `X-Internal-Api-Key`, passes on correct key.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3: Implement.** Query `GetHealthAlertRecipientsQuery(Guid TenantId) : IRequest<IReadOnlyList<RecipientDto>>`; handler pins system roles (`IsSystem && (Name=="Owner" || Name=="Admin")`), joins UserRoles→Users filtered `Status==Active` AND `u.TenantId==TenantId` (explicit tenant filter — the caller isn't logged into this tenant, so do NOT rely on RLS; use the Identity DbContext with an explicit `TenantId ==` predicate, mirroring the Slice-5 sweep approach). Return distinct-by-email `RecipientDto(string Name, string Email)`. Controller: `[Route("internal/tenants/{tenantId:guid}/health-alert-recipients")]`, `[HttpGet]`, NO `[Authorize]` user policy — instead the shared-secret middleware/filter guards it. `InternalApiKeyMiddleware`: for paths starting `/internal`, require header `X-Internal-Api-Key == configuration["Internal:ApiKey"]` (reject 401 otherwise); pass through all other paths. Register the middleware early in Identity's pipeline. Add `Internal:ApiKey` to Identity appsettings (env-driven).
- [ ] **Step 4:** Run → PASS + `dotnet build src/services/Identity/NexConvo.Identity.Api`.
- [ ] **Step 5: Commit** — `feat(identity): internal health-alert-recipients endpoint + shared-secret auth`.

## Task 2 — Notification service scaffold + consumer + email sender
**Files:** Create `src/services/Notification/` — `NexConvo.Notification.Application`, `.Infrastructure`, `.Worker` (skip a Domain project — no aggregates; note if the convention wants a stub). Nest in NexConvo.sln under a new `services/Notification` folder. Add `Dockerfile`. Tests: `tests/services/Notification/NexConvo.Notification.*.Tests`.

- [ ] **Step 1:** Scaffold the 3 projects (mirror Automation's csproj/Program/DI). `Worker/Program.cs` = Automation's boilerplate minus Hangfire, `+ AddNotificationInfrastructure`. Add to sln + nest.
- [ ] **Step 2: Email sender (reimplement minimal, platform-default only).** In `Notification.Infrastructure/Mailing/`: `EmailMessage(string ToEmail, string? ToName, string Subject, string HtmlBody)`, `IEmailSender` (in Application), `ResendEmailSender` (named `"resend"` client + Bearer key from `PlatformDefaultEmailOptions.Resend.ApiKey`, POST `emails` — copy the Identity shape), `SmtpEmailSender` (MailKit → Mailpit in dev). `PlatformDefaultEmailOptions` bound from config `Email:Default` (provider + from + Resend.ApiKey / Smtp host:port). A resolver picks Resend vs SMTP by `Email:Default:Provider`. Register the `"resend"` HttpClient with `AddNexConvoResilience()`.
- [ ] **Step 3: Identity contact client.** `IHealthAlertRecipientsClient` (Application) + impl (Infrastructure) → `GET {Identity:BaseUrl}/internal/tenants/{tenantId}/health-alert-recipients` with header `X-Internal-Api-Key` (from `Internal:ApiKey`), named HttpClient + `AddNexConvoResilience()`; deserialize to `RecipientDto[]`.
- [ ] **Step 4: Consumer.** `IntegrationHealthFailedEventConsumer : IConsumer<IntegrationHealthFailedEvent>` (Application) → `client.GetRecipientsAsync(msg.TenantId)` → if empty, log + return (no send); else per recipient build `EmailMessage` (subject `"[NexConvo] {IntegrationKind} connection is failing"`, HTML body: ConfigName + sanitized ErrorMessage + a "review it in Settings" line/link) → `sender.SendAsync`. Per-recipient try/catch (one failure doesn't stop others); if the recipients lookup THROWS, let it bubble so MassTransit retries (don't swallow). NEVER log a secret.
- [ ] **Step 5: DI** (`Notification.Infrastructure/DependencyInjection.cs`): `AddMassTransit` CONSUME (`AddConsumers(Application assembly)` + `UsingRabbitMq` + `ConfigureEndpoints`); register sender/resolver + options + the recipients client + its HttpClient.
- [ ] **Step 6: Tests** (Notification.Application/Infrastructure.Tests): consumer with a fake client returning 2 recipients → `sender.Received(2)`; empty recipients → `sender.DidNotReceive()`, logged; one recipient send throws → the other still sent; the ResendEmailSender shapes the payload (from/to/subject/html) correctly (fake HttpMessageHandler); the contact client attaches the `X-Internal-Api-Key` header (fake handler asserts header). NSubstitute.
- [ ] **Step 7:** Run → PASS + `dotnet build src/services/Notification/NexConvo.Notification.Worker`. Commit — `feat(notification): service scaffold + IntegrationHealthFailed consumer + owner/admin email alerts`.

## Task 3 — Wiring (compose, env)
**Files:** `docker-compose.yml`; Identity + Notification appsettings if needed.

- [ ] **Step 1:** Add a `notification` service to docker-compose: `profiles: ["apps"]`, `build` from `src/services/Notification/NexConvo.Notification.Worker/Dockerfile`, next free host port (check used ports; e.g. `5233:8080`), env `ASPNETCORE_ENVIRONMENT=Development`, `ASPNETCORE_URLS=http://+:8080`, `Seq__Url=http://seq`, `Jwt__Authority=http://identity:8080`, `Jwt__Audience=nexconvo-api`, `ConnectionStrings__RabbitMQ` (mirror Chat's value), `Identity__BaseUrl=http://identity:8080`, `Internal__ApiKey=${INTERNAL_API_KEY:-localdev_internal_key}`, `Email__Default__Provider=Smtp`, `Email__Default__From=alerts@nexconvo.local`, `Email__Default__Smtp__Host=mailpit`, `Email__Default__Smtp__Port=1025` (Mailpit in dev; Resend key blank in dev). `depends_on: rabbitmq (service_healthy), identity`.
- [ ] **Step 2:** Add `Internal__ApiKey=${INTERNAL_API_KEY:-localdev_internal_key}` to the **identity** service env (same value).
- [ ] **Step 3:** No init.sql change (stateless, no DB). No gateway route (no public API). Commit — `feat(infra): notification service in docker-compose + shared internal-api-key env`.

## Task 4 — End-to-end verification
- [ ] **Step 1:** `docker compose --profile apps up -d --build notification identity chat integrations automation`.
- [ ] **Step 2:** `docker logs nexconvo-notification-1` → starts clean, `Bus started`, receive endpoint for `IntegrationHealthFailedEvent` configured.
- [ ] **Step 3:** Ensure a workspace has an active owner+admin (the seeded dev tenant likely does — confirm via the Identity DB) and an active config with a BAD credential so a sweep transitions Healthy→Failed. (Either update an existing AI/channel config's stored key to garbage, or seed one.) Trigger the sweep via the Hangfire dashboard (`http://localhost:5228/hangfire`, "Trigger now" on `integration-health-sweep`).
- [ ] **Step 4:** Verify the chain in logs: sweep publishes `IntegrationHealthFailedEvent` → Notification consumes → calls Identity `/internal/...` (200) → sends email. Open **Mailpit `http://localhost:8025`** and confirm the owner+admin alert email arrived, body has ConfigName + sanitized error (no secrets).
- [ ] **Step 5:** Second sweep for the same already-Failed config → NO new email (transition-dedupe). Also curl the Identity endpoint without the header → 401, with the header → 200.
- [ ] **Step 6:** `dotnet test NexConvo.sln` green. Commit fixtures — `test: slice 6 notification e2e verification`.

## Self-Review notes
- Secrets: alert email + logs carry only sanitized ErrorMessage + non-secret ConfigName; email secret only in Notification; internal key env-only; the `/internal` endpoint is shared-secret-guarded and not gateway-exposed.
- Recipients: active Owner+Admin only, correct tenant, dedup by email.
- Resilience: both the recipients client and the resend client are `AddNexConvoResilience()`-wrapped.
- Dedupe: transition-only (documented); rare redelivery double-email accepted.
- This completes the connection-health feature (Slices 1–6).
