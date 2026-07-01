---
name: nexconvo-enterprise-standards
description: Use when implementing, designing, scaffolding, or reviewing any feature, service, endpoint, handler, logging, security, or data change in the NexConvo platform — especially under time pressure, for a demo, or when a change feels "too simple" to need the full pattern. Enforces the non-negotiable enterprise engineering standards (Clean Architecture, SOLID, CQRS, TDD, RLS, observability with correlation IDs, resiliency, authorization/RBAC, secrets, audit trail, PII protection, optimistic concurrency, pagination, idempotency, API versioning) so they never have to be restated per prompt.
---

# NexConvo Enterprise Engineering Standards

## Overview

NexConvo is an enterprise, multi-tenant SaaS CRM. Its engineering standards are **architectural guarantees, not style preferences**. A handler that skips CQRS, an endpoint that touches a `DbContext` directly, a class that `new`s its own dependencies, or a log line with no correlation ID is not "a faster version of the same thing" — it is a different, non-compliant system that leaks across layers, tenants, or services, and that cannot be traced in production.

**The core principle: violating the letter of these standards is violating the spirit of them.** "I followed the spirit, just faster" is the exact rationalization this skill exists to stop. Full detail lives in [docs/ARCHITECTURE.md](../../../docs/ARCHITECTURE.md) and [CLAUDE.md](../../../CLAUDE.md) — this skill is the enforcement layer.

## When to Use

- Adding/editing any endpoint, command, query, handler, entity, migration, service, or logging
- Bootstrapping a new microservice
- Reviewing a diff or design
- **Any time you catch yourself thinking "this one is too small/urgent to need the full pattern"** — that thought is the trigger, not the exemption

## The Non-Negotiable Standards

Every one of these applies to *every* change, including one-field updates and demo features.

| # | Standard | Concretely means |
|---|---|---|
| 1 | **Clean Architecture layers** | Domain → Application → Infrastructure → API. The API layer (controller/minimal-API endpoint) NEVER touches a `DbContext`, repository, or external client directly. It dispatches to MediatR and returns the result. |
| 2 | **SOLID & Clean Code** | Constructor injection only — never `new` a dependency or use a service locator. Depend on abstractions (interfaces), not concretions. One reason to change per class — no God handlers/controllers/services. Small methods, guard clauses over deep nesting, intention-revealing names, no dead/commented-out code. Each handler does exactly one thing. |
| 3 | **CQRS via MediatR** | Every write is a `Command` + `Handler` + `Validator`. Every read is a `Query` + `Handler`. No "inline the query just this once." A one-field update is still a Command. |
| 4 | **Test-first (TDD)** | Write the failing test BEFORE the production code (red → green → refactor). Domain/Application unit tests minimum; integration test when persistence, RLS, or eventing is involved. No feature is "done" without its test. |
| 5 | **Database-per-service** | Each service owns its private PostgreSQL database. Never point a new service at another service's database/schema. Cross-context data comes via events/gRPC + local read models — never a cross-service JOIN or FK. |
| 6 | **Multi-tenancy (RLS)** | Every tenant-scoped table has an RLS policy; `app.current_tenant_id` is set per request/connection from the JWT `tenant_id` claim. Tenant is read from the JWT, never the request body. Never bypass RLS. |
| 7 | **Custom fields = JSONB + GIN** | Dynamic fields use JSONB columns with a GIN index, under 8 KB. Never EAV tables. |
| 8 | **Resiliency (Polly)** | Every outbound call to an external API (Vapi, OpenRouter, Deepgram, Cartesia, Meta, Twilio/BulkSMSBD, Resend/SES) is wrapped in a Polly Circuit Breaker + Retry, in the Infrastructure layer only. |
| 9 | **Observability with correlation** | Serilog structured logging + OpenTelemetry tracing + a `/health` check. **Every log line must carry `CorrelationId` (= OTel `TraceId`), `TenantId`, and `Service`** — see the logging contract below. New external dependency → add its health check. |
| 10 | **Reliable eventing** | Integration events are published AFTER commit via the MassTransit outbox; cross-service workflows use orchestration Sagas with compensating actions — never a direct dual-write or 2PC. |
| 11 | **Containerization** | Each service has a multi-stage Dockerfile; config via environment, not hardcoded. |
| 12 | **Authorization (RBAC)** | Every endpoint declares an explicit authorization policy and is **deny-by-default**. AuthN (a valid JWT) is never sufficient — the user's role/permission must allow the action. `[AllowAnonymous]` requires a written reason. Never authorize off a value from the request body. |
| 13 | **Secrets management** | Never hardcode or commit secrets — API keys (Vapi, Deepgram, Twilio, Resend…), connection strings, JWT signing keys. Load from environment / Key Vault / K8s secrets. Never log a secret. `appsettings.json` holds non-secret config only. |
| 14 | **Audit trail** | Every create/update/delete of a business record writes an audit entry — who (user + tenant), what (entity, before→after), when. This is **part of the change, not a follow-up**. Never ship a mutation without its audit record. |
| 15 | **PII & data protection** | TLS in transit; encrypt sensitive fields at rest; data-minimize (collect/return only what's needed). Support hard-delete or anonymize for GDPR "right to be forgotten." Never log PII (see Standard 9). |
| 16 | **Optimistic concurrency** | Any mutable row that two users can edit carries a concurrency token (Postgres `xmin` / rowversion). Updates send the expected version and return **409 Conflict** on mismatch — never silent last-writer-wins. |
| 17 | **Pagination / bounded queries** | List/collection endpoints MUST paginate with a capped page size (e.g. ≤100) and return a total. Never return an unbounded result set or an unfiltered `SELECT *` over a tenant's data. |
| 18 | **Idempotency** | Retry-able writes (payments, SMS/email side-effects, anything a client or broker may resend) and ALL event consumers are idempotent — via idempotency key or inbox dedupe. A redelivery never double-charges or double-sends. |
| 19 | **API versioning & contract stability** | Public endpoints are versioned (`/api/v1/...`). Never break a published request/response shape or event contract — additive changes only, or a new version. Verify cross-service contracts with Pact tests. |
| 20 | **Solution structure mirrors disk** | When you add a new project to `NexConvo.sln` (bootstrapping a service, a layer, or a test project), nest it under the matching Solution Folder — `src/gateway`, `src/services/<Service>`, `src/shared`, or `tests` — via a `GlobalSection(NestedProjects)` mapping (add the folder's `Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = …` entry if it doesn't exist yet). Never leave a project flat at the solution root; the Solution Explorer tree must mirror the on-disk `src/…`/`tests/` layout. |
| 21 | **API Gateway Routing** | Whenever a new microservice or top-level API route is created, you MUST update the YARP Gateway configurations (`appsettings.json` and `appsettings.Development.json` in `NexConvo.Gateway`) to expose the new cluster and route. The frontend cannot access unrouted backend services. |

## The Logging Contract (Standard 9, in detail)

Logging that can't be correlated is noise. Every service follows this exact shape:

- **Context is pushed once per request, not per call.** Middleware pushes `CorrelationId`, `TenantId`, `UserId`, `Service` onto Serilog's `LogContext`; handlers never re-pass them.
  ```csharp
  // API/Middleware/RequestLoggingMiddleware.cs (runs before MediatR dispatch)
  using (LogContext.PushProperty("CorrelationId", Activity.Current?.TraceId.ToString()))
  using (LogContext.PushProperty("TenantId", tenantContext.TenantId))
  using (LogContext.PushProperty("UserId", currentUser.Id))
      await _next(ctx);
  ```
- **Serilog is structured-first**, `Enrich.FromLogContext()`, `Service` enriched, Seq as the prod sink, config from environment.
- **Always structured message templates** — `logger.LogInformation("Lead {LeadId} created", lead.Id)`. NEVER string interpolation/concatenation (`$"...{id}"`) — it flattens properties to unsearchable text.
- **Never log PII** (email, phone, name) in message bodies — log `LeadId`, `TenantId`, `Source`. PII stays in the RLS-protected row, not in an external log store.
- **Never catch-and-swallow to log a fake success.** Let exceptions bubble to the MediatR `UnhandledExceptionBehavior`, which logs once with full context; the request fails honestly.
- **The correlation must cross boundaries.** Background/Hangfire jobs and MassTransit consumers re-establish `CorrelationId` from the job argument / message header (`traceparent`) so one trace spans HTTP → outbox → consumer → worker.

## Red Flags — STOP, you are about to violate a standard

If you think or write any of these, stop and follow the standard instead:

- "It's a trivial one-field update, so I'll skip the Command/Handler" → **CQRS still applies (Standard 3)**
- "I'll inject the `DbContext` into the controller for this one endpoint" → **layer violation (Standard 1)**
- "I'll just `new` up the dependency / grab it from a static" → **constructor injection only (Standard 2)**
- "One big handler that does everything is fine for now" → **one responsibility per class (Standard 2)**
- "Demo in 20 minutes, I'll write the test after" → **test-first is the cheapest path now (Standard 4)**
- "I'll skip the Application layer for now, it's just one endpoint" → **the layer is the contract (Standard 1)**
- `$"Created lead {id}"` or `LogInformation("done")` → **structured template + correlation required (Standard 9)**
- "I'll log the email/phone so we can see what happened" → **no PII in logs (Standard 9)**
- "We already have a DB running, I'll just point this new service at it" → **database-per-service (Standard 5)**
- "The endpoint just needs a valid login" → **AuthN ≠ AuthZ; declare a deny-by-default RBAC policy (Standard 12)**
- "I'll drop the API key in appsettings.json for now" → **secrets come from env/Key Vault, never committed (Standard 13)**
- "I'll add the audit log entry as a follow-up" → **audit is part of the mutation, not a follow-up (Standard 14)**
- "It's a simple update, last write wins is fine" → **concurrency token + 409 on conflict (Standard 16)**
- "It's a small list, I'll just return them all" → **paginate with a capped page size (Standard 17)**
- "The consumer will rarely get the message twice" → **make it idempotent; brokers redeliver (Standard 18)**
- "I'll version the API later when we need to" → **version from `/v1` now; breaking a live contract is the cost (Standard 19)**
- "Just add the project; it'll sit at the solution root for now" → **nest it under its Solution Folder in `NexConvo.sln` (Standard 20)**
- "I've built the API endpoint, so the backend work is done" → **must expose it via YARP Gateway (Standard 21)**
- "We can split / clean up / add tests later" → **'later' is how the distributed monolith was born; do it now**

## Rationalizations and Reality

| Rationalization | Reality |
|---|---|
| "This change is too simple to need CQRS." | Simple changes are where layer-skipping rots the codebase. A Command for a one-field update is ~15 lines and keeps the boundary intact. |
| "Inlining the DbContext is faster." | It's faster to type and slower to live with — it couples the API layer to persistence and is the first thing a review rejects. Same speed via a thin handler. |
| "`new`-ing the dependency saves wiring." | It hard-couples the class, kills testability, and breaks DIP. Constructor injection is one parameter, not a refactor. |
| "One handler doing it all is simpler." | It's simpler to write once and impossible to change safely. SRP keeps each handler reviewable and testable in isolation. |
| "I'll add tests after the demo." | Tests-after answer "what does this do?"; tests-first answer "what should this do?" The baseline proved tests-after means tests-never. Write the failing test first. |
| "A quick `$"...{id}"` log is fine." | Interpolated logs lose searchable properties and usually drop the correlation ID — so in production you can't trace the request at all. The structured template is the same length. |
| "Logging the contact details helps debugging." | It scatters PII across an external log store (Seq). Log identifiers; the PII is one RLS-protected query away. |
| "Sharing the existing DB saves infra cost." | A separate logical database on the same Postgres instance is one line of config. Splitting a shared DB *later* costs a sprint. |
| "A valid JWT is enough to authorize this." | AuthN proves who you are; AuthZ proves you're allowed. Without a deny-by-default policy, any logged-in user of any tenant role can call it — a privilege-escalation hole. |
| "I'll put the key in appsettings to move fast." | A committed secret is a leaked secret — it lives in git history forever. Reading from env is the same one line and isn't a breach. |
| "The audit entry is a nice-to-have follow-up." | Audit is a compliance requirement (Module 7.3) and is unreconstructable after the fact. If the mutation shipped without it, the 'who changed this' question has no answer. A deferred audit entry is a missing one. |
| "Last-writer-wins is simpler than concurrency tokens." | On a money field two reps edit at once, last-writer-wins silently destroys one edit with no error. The `xmin` token + 409 is a few lines and turns a silent data-loss bug into a visible, retryable conflict. |
| "It's a small list, pagination is overkill." | 'Small' today is 50k rows at a scaled tenant tomorrow. An unbounded query is a latent outage; the page-size cap costs nothing now. |
| "Messages basically never arrive twice." | RabbitMQ guarantees at-least-once, not exactly-once. A non-idempotent consumer WILL eventually double-send an SMS or double-charge. Inbox dedupe is the standard, not an optimization. |
| "It's fine if the new project shows at the root." | A flat root of 37+ projects is unnavigable — that's the exact mess the Solution Folders exist to prevent. One `NestedProjects` line keeps the tree mirroring disk; skipping it re-creates the mess one project at a time. |
| "The endpoint works locally in the service, the frontend can just call it." | In a microservices architecture, the API Gateway is the only public entry point. An unrouted endpoint essentially doesn't exist for the frontend. Forgetting to update YARP means a broken feature. |
| "I'm following the spirit, just pragmatically." | Violating the letter IS violating the spirit. The standards are the spirit, expressed precisely. |

## Correct Pattern (the one-field update, done right)

A "trivial" lead phone-number update still flows through every layer, with injected dependencies and a correlated log line:

```csharp
// Application/Features/Leads/Commands/UpdateLeadPhone/UpdateLeadPhoneCommand.cs
public sealed record UpdateLeadPhoneCommand(Guid LeadId, string PhoneNumber) : IRequest<Result>;

// ...Validator.cs — FluentValidation, E.164 enforced
public sealed class UpdateLeadPhoneCommandValidator : AbstractValidator<UpdateLeadPhoneCommand>
{
    public UpdateLeadPhoneCommandValidator() =>
        RuleFor(x => x.PhoneNumber).NotEmpty().Matches(@"^\+[1-9]\d{6,14}$");
}

// ...Handler.cs — constructor injection (S2); no SQL/HTTP (S1); RLS scopes the query (S6)
public sealed class UpdateLeadPhoneCommandHandler(
    ICrmDbContext db,
    ILogger<UpdateLeadPhoneCommandHandler> logger)   // injected abstraction, not new'd
    : IRequestHandler<UpdateLeadPhoneCommand, Result>
{
    public async Task<Result> Handle(UpdateLeadPhoneCommand cmd, CancellationToken ct)
    {
        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == cmd.LeadId, ct);
        if (lead is null) return Result.NotFound();

        lead.UpdatePhone(PhoneNumber.Create(cmd.PhoneNumber));  // domain method + value object
        await db.SaveChangesAsync(ct);

        // Structured template; CorrelationId + TenantId already on LogContext from middleware (S9)
        logger.LogInformation("Lead {LeadId} phone updated", lead.Id);
        return Result.Success();
    }
}

// API/Controllers/LeadsController.cs — dispatch only, never a DbContext here (S1)
[HttpPatch("{id:guid}/phone")]
public async Task<IActionResult> UpdatePhone(Guid id, UpdateLeadPhoneRequest body, CancellationToken ct)
    => (await sender.Send(new UpdateLeadPhoneCommand(id, body.PhoneNumber), ct)).ToActionResult();
```

And the test that exists **before** that handler does:

```csharp
[Fact]
public async Task UpdatePhone_WithValidE164_UpdatesLead()
{
    var lead = await SeedLead(tenantA);
    var result = await _handler.Handle(new(lead.Id, "+8801712345678"), default);
    result.IsSuccess.Should().BeTrue();
    (await _db.Leads.FindAsync(lead.Id))!.Phone.Value.Should().Be("+8801712345678");
}
```

## Common Mistakes

- Treating "demo" or "urgent" as a standards exemption — it never is; the correct pattern is barely slower.
- Interpolated log messages (`$"..."`) that drop the correlation ID and searchable properties.
- Logging PII (email/phone/name) into Seq instead of identifiers.
- `new`-ing dependencies or reaching for a static/service-locator instead of constructor injection.
- A single handler/controller that does many things (SRP violation) because it "felt quicker."
- Adding a new external API call without a Polly policy and a health check.
- Publishing an integration event before `SaveChangesAsync` commits (use the outbox).
- A new service "borrowing" another service's database to save time.
- An endpoint with `[Authorize]` (any login) but no role/permission policy — or no attribute at all.
- A secret committed to `appsettings.json` or source — instead of env/Key Vault.
- A mutation that ships without an audit-trail entry, deferred as a "follow-up."
- An update handler with no concurrency check (silent last-writer-wins) on a row two users can edit.
- A list endpoint that returns every row with no page-size cap.
- A non-idempotent event consumer or retry-able write that double-sends on redelivery.
- A new public endpoint with no version segment, or a breaking change to a published contract.
- A new project left flat at the solution root instead of nested under its Solution Folder (`src/services/<Service>`, `src/shared`, `src/gateway`, `tests`).
- Building a new API endpoint but forgetting to route it through the API Gateway (`NexConvo.Gateway`).
- Marking a feature done with no failing-test-first history.
