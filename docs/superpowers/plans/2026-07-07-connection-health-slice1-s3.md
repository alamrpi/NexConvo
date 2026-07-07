# Connection Health — Slice 1 (Shared Abstraction) + Slice 2 (S3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the reusable connection-health abstraction and apply it end-to-end to the S3 config: test-then-save, persisted health, and a feature guard on knowledge upload.

**Architecture:** A shared `IConnectionTester<TInput>` + `ConnectionHealth` value object + `ConnectionStatus` enum live in BuildingBlocks so every integration reports health identically. `WorkspaceS3Config` gains persisted health fields. The Integrations service adds a real S3 tester (`AWSSDK.S3` HeadBucket), a `POST /api/v1/s3-config/test` endpoint, a server-side re-test gate on save, and a `EnsureHealthy` guard the Chat knowledge-upload path calls before using S3. The frontend gets a Test button, save gating, a health badge, and a guarded upload banner.

**Tech Stack:** .NET 9, EF Core (PostgreSQL), MediatR (CQRS), FluentValidation, MassTransit, `AWSSDK.S3`, Next.js App Router + React Query + Zod, MSW + Playwright, xUnit.

## Global Constraints

- All `.NET` projects target `net9.0`; frontend TypeScript strict mode on.
- **Clean Architecture** (Standard 1): API → MediatR only; no `DbContext` in controllers.
- **CQRS via MediatR** (Standard 3): every write = Command+Handler+Validator; every read = Query+Handler.
- **TDD** (Standard 4): failing test before production code.
- **RLS / tenant from JWT** (Standard 6): tenant via `ITenantContext`, never request body.
- **Resiliency** (Standard 8): external calls (S3) wrapped via Infrastructure; use `AddNexConvoResilience` where an `HttpClient` is involved.
- **Observability** (Standard 9): structured logs carry `CorrelationId`/`TenantId`/`Service`; never log secrets.
- **Authorization** (Standard 12): every endpoint deny-by-default; S3 endpoints use policy `settings:manage`.
- **Secrets** (Standard 13): credentials encrypted at rest via `IAesEncryptionService`; never logged; decrypted secrets never leave the owning service.
- **Audit** (Standard 14): mutations write an `AuditLog` row.
- **Optimistic concurrency** (Standard 16): `WorkspaceS3Config` already carries `xmin`; health updates must not break it.
- **API versioning** (Standard 19): routes under `/api/v1`; new gateway route required (Standard 21).
- **Connection health** (Standard 22): test-then-save, persist health, guard features. Spec: `docs/superpowers/specs/2026-07-07-connection-health-design.md`.
- **Bengali-first i18n**: every new UI string added to both `en.json` and `bn.json`.
- **Announce tech choices**: `AWSSDK.S3` chosen over hand-rolled AWS SigV4 (SDK is standard, supports custom endpoints for R2/MinIO/Spaces).

---

## File Structure

**Slice 1 — BuildingBlocks (shared):**
- Create `src/shared/NexConvo.BuildingBlocks.Domain/ConnectionHealth/ConnectionStatus.cs` — the enum.
- Create `src/shared/NexConvo.BuildingBlocks.Domain/ConnectionHealth/ConnectionHealth.cs` — the value record.
- Create `src/shared/NexConvo.BuildingBlocks.Application/ConnectionHealth/IConnectionTester.cs` — generic tester interface.
- Create `src/shared/NexConvo.BuildingBlocks.Application/ConnectionHealth/ConnectionUnhealthyException.cs` — thrown by the guard.
- Test `tests/.../BuildingBlocks.Domain.Tests/ConnectionHealthTests.cs`.

**Slice 2 — Integrations (S3):**
- Modify `src/services/Integrations/NexConvo.Integrations.Domain/Entities/WorkspaceS3Config.cs` — add health fields + `ApplyHealth` + `EnsureHealthy`.
- Create `src/services/Integrations/NexConvo.Integrations.Application/Features/S3Config/S3TestInput.cs` — tester input record.
- Create `src/services/Integrations/NexConvo.Integrations.Application/Features/S3Config/Commands/TestS3ConnectionCommand.cs` (+Handler).
- Create `src/services/Integrations/NexConvo.Integrations.Infrastructure/ExternalServices/S3ConnectionTester.cs` — `AWSSDK.S3` HeadBucket.
- Modify `src/services/Integrations/NexConvo.Integrations.Application/Features/S3Config/Commands/SaveS3ConfigCommandHandler.cs` — re-test gate.
- Modify `.../Infrastructure/DependencyInjection.cs` — register tester.
- Modify `.../Api/Controllers/WorkspaceS3ConfigController.cs` — add `POST /test`.
- Modify `.../Api/Models/SaveS3ConfigRequest.cs` (none) / add `TestS3ConfigRequest.cs`.
- Modify EF: `WorkspaceS3ConfigConfiguration.cs` + new migration.
- Modify `src/gateway/NexConvo.Gateway/appsettings.json` — already routed `/api/v1/s3-config/{**catch-all}` covers `/test` (verify).

**Slice 2 — Frontend + guard:**
- Modify `frontend/src/features/settings/model/s3-config.types.ts` + `s3-config.schema.ts` — health fields + test-result type.
- Create `frontend/src/features/settings/api/use-test-s3-connection.ts`.
- Modify `frontend/src/features/settings/components/s3-config-form.tsx` — Test button, gating, badge, 422 toast.
- Create `frontend/src/app/api/bff/settings/s3-config/test/route.ts` — BFF POST.
- Guard: Chat knowledge upload path calls Integrations to check S3 health (cross-service). **Deferred detail** — see Task 12 note.

---

## Task 1: ConnectionStatus enum + ConnectionHealth value record (BuildingBlocks.Domain)

**Files:**
- Create: `src/shared/NexConvo.BuildingBlocks.Domain/ConnectionHealth/ConnectionStatus.cs`
- Create: `src/shared/NexConvo.BuildingBlocks.Domain/ConnectionHealth/ConnectionHealth.cs`
- Test: `tests/shared/NexConvo.BuildingBlocks.Domain.Tests/ConnectionHealthTests.cs` (create test project if absent — see Step 0)

**Interfaces:**
- Produces: `enum ConnectionStatus { Untested=0, Healthy=1, Degraded=2, Failed=3 }`; `record ConnectionHealth(bool Success, ConnectionStatus Status, string? Detail, string? ErrorMessage, int? LatencyMs)` with static factories `Healthy(string? detail, int? latencyMs)` and `Failed(string? error, int? latencyMs)`.

- [ ] **Step 0: Ensure a BuildingBlocks.Domain test project exists**

Check `tests/` for `NexConvo.BuildingBlocks.Domain.Tests`. If absent, create it and nest it under the `tests` Solution Folder (Standard 20):
```bash
dotnet new xunit -n NexConvo.BuildingBlocks.Domain.Tests -o tests/shared/NexConvo.BuildingBlocks.Domain.Tests
dotnet add tests/shared/NexConvo.BuildingBlocks.Domain.Tests reference src/shared/NexConvo.BuildingBlocks.Domain
dotnet sln NexConvo.sln add tests/shared/NexConvo.BuildingBlocks.Domain.Tests
```
Then add its `GlobalSection(NestedProjects)` entry under the `tests` folder GUID.

- [ ] **Step 1: Write the failing test**

```csharp
using NexConvo.BuildingBlocks.Domain.ConnectionHealth;
using Xunit;

public class ConnectionHealthTests
{
    [Fact]
    public void Healthy_factory_sets_success_and_status()
    {
        var h = ConnectionHealth.Healthy("Bucket reachable", 42);
        Assert.True(h.Success);
        Assert.Equal(ConnectionStatus.Healthy, h.Status);
        Assert.Equal("Bucket reachable", h.Detail);
        Assert.Null(h.ErrorMessage);
        Assert.Equal(42, h.LatencyMs);
    }

    [Fact]
    public void Failed_factory_sets_failure_and_error()
    {
        var h = ConnectionHealth.Failed("Access denied", 100);
        Assert.False(h.Success);
        Assert.Equal(ConnectionStatus.Failed, h.Status);
        Assert.Equal("Access denied", h.ErrorMessage);
        Assert.Null(h.Detail);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/shared/NexConvo.BuildingBlocks.Domain.Tests`
Expected: FAIL — `ConnectionHealth`/`ConnectionStatus` do not exist.

- [ ] **Step 3: Write minimal implementation**

`ConnectionStatus.cs`:
```csharp
namespace NexConvo.BuildingBlocks.Domain.ConnectionHealth;

public enum ConnectionStatus
{
    Untested = 0,
    Healthy = 1,
    Degraded = 2,
    Failed = 3,
}
```

`ConnectionHealth.cs`:
```csharp
namespace NexConvo.BuildingBlocks.Domain.ConnectionHealth;

public sealed record ConnectionHealth(
    bool Success,
    ConnectionStatus Status,
    string? Detail,
    string? ErrorMessage,
    int? LatencyMs)
{
    public static ConnectionHealth Healthy(string? detail, int? latencyMs) =>
        new(true, ConnectionStatus.Healthy, detail, null, latencyMs);

    public static ConnectionHealth Failed(string? error, int? latencyMs) =>
        new(false, ConnectionStatus.Failed, null, error, latencyMs);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/shared/NexConvo.BuildingBlocks.Domain.Tests`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/shared/NexConvo.BuildingBlocks.Domain/ConnectionHealth tests/shared/NexConvo.BuildingBlocks.Domain.Tests NexConvo.sln
git commit -m "feat(buildingblocks): ConnectionHealth value object + ConnectionStatus enum"
```

---

## Task 2: IConnectionTester interface + ConnectionUnhealthyException (BuildingBlocks.Application)

**Files:**
- Create: `src/shared/NexConvo.BuildingBlocks.Application/ConnectionHealth/IConnectionTester.cs`
- Create: `src/shared/NexConvo.BuildingBlocks.Application/ConnectionHealth/ConnectionUnhealthyException.cs`

**Interfaces:**
- Consumes: `ConnectionHealth`, `ConnectionStatus` (Task 1).
- Produces: `interface IConnectionTester<TInput> { string IntegrationKind { get; } Task<ConnectionHealth> TestAsync(TInput input, CancellationToken ct); }`; `class ConnectionUnhealthyException(string integrationKind, string? detail) : Exception` used by the guard (maps to HTTP 409).

- [ ] **Step 1: Write the interface (no test — pure contract)**

`IConnectionTester.cs`:
```csharp
using NexConvo.BuildingBlocks.Domain.ConnectionHealth;

namespace NexConvo.BuildingBlocks.Application.ConnectionHealth;

public interface IConnectionTester<TInput>
{
    string IntegrationKind { get; }
    Task<ConnectionHealth> TestAsync(TInput input, CancellationToken ct);
}
```

`ConnectionUnhealthyException.cs`:
```csharp
namespace NexConvo.BuildingBlocks.Application.ConnectionHealth;

public sealed class ConnectionUnhealthyException(string integrationKind, string? detail)
    : Exception($"The {integrationKind} connection is not healthy. {detail}".Trim())
{
    public string IntegrationKind { get; } = integrationKind;
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build src/shared/NexConvo.BuildingBlocks.Application`
Expected: Build succeeded. (Confirm `.Application` references `.Domain`; add the reference if the build fails on the `ConnectionHealth` namespace.)

- [ ] **Step 3: Map the exception to HTTP 409**

Find the shared exception→ProblemDetails mapping (search: `grep -rl "DomainException" src/shared/NexConvo.BuildingBlocks.Infrastructure`). In that mapper, add: `ConnectionUnhealthyException → 409 Conflict` with `type`/`code` `"connection-unhealthy"`. If the mapping lives per-service instead, add it to the Integrations exception handler used by `WorkspaceS3ConfigController`.

- [ ] **Step 4: Commit**

```bash
git add src/shared/NexConvo.BuildingBlocks.Application/ConnectionHealth src/shared/NexConvo.BuildingBlocks.Infrastructure
git commit -m "feat(buildingblocks): IConnectionTester contract + ConnectionUnhealthy 409 mapping"
```

---

## Task 3: WorkspaceS3Config health fields + ApplyHealth + EnsureHealthy (Integrations.Domain)

**Files:**
- Modify: `src/services/Integrations/NexConvo.Integrations.Domain/Entities/WorkspaceS3Config.cs`
- Test: `tests/services/Integrations/NexConvo.Integrations.Domain.Tests/WorkspaceS3ConfigHealthTests.cs` (create project if absent, same pattern as Task 1 Step 0)

**Interfaces:**
- Consumes: `ConnectionHealth`, `ConnectionStatus`, `ConnectionUnhealthyException`.
- Produces on `WorkspaceS3Config`: props `DateTimeOffset? LastTestedAt`, `ConnectionStatus LastTestStatus`, `string? LastTestError`, `int? LastTestLatencyMs`; methods `void ApplyHealth(ConnectionHealth health)`, `void EnsureHealthy()` (throws `ConnectionUnhealthyException` when `LastTestStatus != Healthy`).

- [ ] **Step 1: Write the failing test**

```csharp
using NexConvo.BuildingBlocks.Domain.ConnectionHealth;
using NexConvo.BuildingBlocks.Application.ConnectionHealth;
using NexConvo.Integrations.Domain.Entities;
using Xunit;

public class WorkspaceS3ConfigHealthTests
{
    private static WorkspaceS3Config NewConfig() =>
        new(Guid.NewGuid(), "bucket", "us-east-1", "enc-ak", "enc-sk", null, null, true);

    [Fact]
    public void ApplyHealth_healthy_sets_status_and_clears_error()
    {
        var c = NewConfig();
        c.ApplyHealth(ConnectionHealth.Healthy("ok", 30));
        Assert.Equal(ConnectionStatus.Healthy, c.LastTestStatus);
        Assert.Null(c.LastTestError);
        Assert.Equal(30, c.LastTestLatencyMs);
        Assert.NotNull(c.LastTestedAt);
    }

    [Fact]
    public void EnsureHealthy_throws_when_not_healthy()
    {
        var c = NewConfig(); // default Untested
        Assert.Throws<ConnectionUnhealthyException>(() => c.EnsureHealthy());
    }

    [Fact]
    public void EnsureHealthy_passes_when_healthy()
    {
        var c = NewConfig();
        c.ApplyHealth(ConnectionHealth.Healthy("ok", 10));
        c.EnsureHealthy(); // does not throw
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/services/Integrations/NexConvo.Integrations.Domain.Tests`
Expected: FAIL — members do not exist.

- [ ] **Step 3: Implement on the entity**

Add to `WorkspaceS3Config` (keep private setters; `ApplyHealth` uses `DateTimeOffset.UtcNow`):
```csharp
public DateTimeOffset? LastTestedAt { get; private set; }
public ConnectionStatus LastTestStatus { get; private set; } = ConnectionStatus.Untested;
public string? LastTestError { get; private set; }
public int? LastTestLatencyMs { get; private set; }

public void ApplyHealth(ConnectionHealth health)
{
    LastTestStatus = health.Status;
    LastTestError = health.ErrorMessage;
    LastTestLatencyMs = health.LatencyMs;
    LastTestedAt = DateTimeOffset.UtcNow;
}

public void EnsureHealthy()
{
    if (LastTestStatus != ConnectionStatus.Healthy)
        throw new ConnectionUnhealthyException("s3", LastTestError ?? "Run a connection test in Settings.");
}
```
Add `using NexConvo.BuildingBlocks.Domain.ConnectionHealth;` and `using NexConvo.BuildingBlocks.Application.ConnectionHealth;`. Confirm `Integrations.Domain` references `BuildingBlocks.Application` (it may only reference `.Domain`; if so, move `ConnectionUnhealthyException` + `EnsureHealthy`'s throw to keep the Domain layer clean — alternative: throw a domain-level exception the mapper also maps to 409). Prefer keeping `EnsureHealthy` returning `bool IsHealthy` on the entity and throwing in the Application handler if the reference direction is wrong.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/services/Integrations/NexConvo.Integrations.Domain.Tests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/services/Integrations/NexConvo.Integrations.Domain tests/services/Integrations/NexConvo.Integrations.Domain.Tests
git commit -m "feat(integrations): WorkspaceS3Config persisted health + ApplyHealth/EnsureHealthy"
```

---

## Task 4: EF mapping + migration for S3 health columns

**Files:**
- Modify: `src/services/Integrations/NexConvo.Integrations.Infrastructure/Persistence/Configurations/WorkspaceS3ConfigConfiguration.cs`
- Create (via CLI): a new migration under `.../Infrastructure/Migrations/`

**Interfaces:** Consumes Task 3 entity fields.

- [ ] **Step 1: Add column mappings**

In `WorkspaceS3ConfigConfiguration.Configure`:
```csharp
builder.Property(x => x.LastTestStatus).HasConversion<int>().HasDefaultValue(ConnectionStatus.Untested);
builder.Property(x => x.LastTestError).HasMaxLength(1000);
builder.Property(x => x.LastTestedAt);
builder.Property(x => x.LastTestLatencyMs);
```
Add `using NexConvo.BuildingBlocks.Domain.ConnectionHealth;`.

- [ ] **Step 2: Generate the migration (produces Designer + snapshot — do NOT hand-write)**

Run from repo root:
```bash
dotnet ef migrations add AddS3ConfigHealthFields \
  --project src/services/Integrations/NexConvo.Integrations.Infrastructure \
  --startup-project src/services/Integrations/NexConvo.Integrations.Api
```
Expected: `Done.` Migration `.cs` + `.Designer.cs` created and `IntegrationsDbContextModelSnapshot.cs` updated (verify all three — per the earlier PendingModelChangesWarning bug).

- [ ] **Step 3: Verify the migration builds & the snapshot includes the fields**

Run: `dotnet build src/services/Integrations/NexConvo.Integrations.Infrastructure`
Then: `grep -c LastTestStatus src/services/Integrations/NexConvo.Integrations.Infrastructure/Migrations/IntegrationsDbContextModelSnapshot.cs` → expect ≥1.

- [ ] **Step 4: Commit**

```bash
git add src/services/Integrations/NexConvo.Integrations.Infrastructure/Persistence src/services/Integrations/NexConvo.Integrations.Infrastructure/Migrations
git commit -m "feat(integrations): EF mapping + migration for S3 config health fields"
```

---

## Task 5: S3ConnectionTester (Infrastructure, AWSSDK.S3 HeadBucket)

**Files:**
- Create: `src/services/Integrations/NexConvo.Integrations.Application/Features/S3Config/S3TestInput.cs`
- Create: `src/services/Integrations/NexConvo.Integrations.Infrastructure/ExternalServices/S3ConnectionTester.cs`
- Modify: `Directory.Packages.props` (add `AWSSDK.S3`), Infrastructure `.csproj` (reference it)
- Test: `tests/services/Integrations/NexConvo.Integrations.Infrastructure.Tests/S3ConnectionTesterTests.cs`

**Interfaces:**
- Produces: `record S3TestInput(string BucketName, string Region, string AccessKeyId, string SecretAccessKey, string? CustomEndpoint)`; `S3ConnectionTester : IConnectionTester<S3TestInput>` with `IntegrationKind => "s3"`.

- [ ] **Step 1: Add the package**

In `Directory.Packages.props` add `<PackageVersion Include="AWSSDK.S3" Version="3.7.*" />` (pin the current 3.7 line). In `NexConvo.Integrations.Infrastructure.csproj` add `<PackageReference Include="AWSSDK.S3" />`.

- [ ] **Step 2: Write the input record**

`S3TestInput.cs`:
```csharp
namespace NexConvo.Integrations.Application.Features.S3Config;

public sealed record S3TestInput(
    string BucketName, string Region, string AccessKeyId, string SecretAccessKey, string? CustomEndpoint);
```

- [ ] **Step 3: Write the failing test (fake AmazonS3 via injected client factory)**

The tester takes a `Func<S3TestInput, IAmazonS3>` factory so tests inject a fake. Test asserts: success → `ConnectionHealth.Healthy`; `AmazonS3Exception` → `Failed` with sanitized message; latency populated.
```csharp
using Amazon.S3;
using Amazon.S3.Model;
using NexConvo.BuildingBlocks.Domain.ConnectionHealth;
using NexConvo.Integrations.Application.Features.S3Config;
using NexConvo.Integrations.Infrastructure.ExternalServices;
using Xunit;

public class S3ConnectionTesterTests
{
    private static S3TestInput Input() => new("bucket", "us-east-1", "ak", "sk", null);

    [Fact]
    public async Task Returns_healthy_on_successful_head_bucket()
    {
        var fake = new FakeS3(headOk: true);
        var tester = new S3ConnectionTester(_ => fake);
        var result = await tester.TestAsync(Input(), default);
        Assert.True(result.Success);
        Assert.Equal(ConnectionStatus.Healthy, result.Status);
        Assert.NotNull(result.LatencyMs);
    }

    [Fact]
    public async Task Returns_failed_on_s3_exception()
    {
        var fake = new FakeS3(headOk: false);
        var tester = new S3ConnectionTester(_ => fake);
        var result = await tester.TestAsync(Input(), default);
        Assert.False(result.Success);
        Assert.Equal(ConnectionStatus.Failed, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    // FakeS3 implements IAmazonS3 minimally, overriding GetBucketLocation/HeadBucket-equivalent
    // (ListObjectsV2 with MaxKeys=1) to succeed or throw AmazonS3Exception.
}
```
*(Note: `AWSSDK.S3` has no direct `HeadBucketAsync`; use `ListObjectsV2Async` with `MaxKeys = 1` as the reachability+auth probe, which also works on S3-compatible endpoints. The FakeS3 overrides that one method.)*

- [ ] **Step 4: Run test to verify it fails**

Run: `dotnet test tests/services/Integrations/NexConvo.Integrations.Infrastructure.Tests --filter S3ConnectionTesterTests`
Expected: FAIL — `S3ConnectionTester` not defined.

- [ ] **Step 5: Implement the tester**

```csharp
using System.Diagnostics;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Application.ConnectionHealth;
using NexConvo.BuildingBlocks.Domain.ConnectionHealth;
using NexConvo.Integrations.Application.Features.S3Config;

namespace NexConvo.Integrations.Infrastructure.ExternalServices;

internal sealed class S3ConnectionTester : IConnectionTester<S3TestInput>
{
    private readonly Func<S3TestInput, IAmazonS3> _clientFactory;
    private readonly ILogger<S3ConnectionTester>? _logger;

    public S3ConnectionTester(Func<S3TestInput, IAmazonS3> clientFactory, ILogger<S3ConnectionTester>? logger = null)
    {
        _clientFactory = clientFactory;
        _logger = logger;
    }

    public string IntegrationKind => "s3";

    public async Task<ConnectionHealth> TestAsync(S3TestInput input, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var client = _clientFactory(input);
            await client.ListObjectsV2Async(
                new ListObjectsV2Request { BucketName = input.BucketName, MaxKeys = 1 }, ct);
            return ConnectionHealth.Healthy("Bucket reachable", (int)sw.ElapsedMilliseconds);
        }
        catch (AmazonS3Exception ex)
        {
            _logger?.LogWarning("S3 connection test failed: {Status}", ex.StatusCode);
            return ConnectionHealth.Failed(
                $"S3 error: {ex.ErrorCode ?? ex.StatusCode.ToString()}", (int)sw.ElapsedMilliseconds);
        }
        catch (Exception)
        {
            return ConnectionHealth.Failed("Could not reach S3. Check bucket, region, and endpoint.", (int)sw.ElapsedMilliseconds);
        }
    }
}
```
Add a default production client factory (in DI, Task 6) that builds `AmazonS3Client` with `BasicAWSCredentials` + `AmazonS3Config { RegionEndpoint or ServiceURL = CustomEndpoint, ForcePathStyle = true when custom }`.

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test tests/services/Integrations/NexConvo.Integrations.Infrastructure.Tests --filter S3ConnectionTesterTests`
Expected: PASS (2 tests).

- [ ] **Step 7: Commit**

```bash
git add Directory.Packages.props src/services/Integrations tests/services/Integrations/NexConvo.Integrations.Infrastructure.Tests
git commit -m "feat(integrations): S3ConnectionTester via AWSSDK.S3 (ListObjectsV2 probe)"
```

---

## Task 6: Register the tester + production S3 client factory (DI)

**Files:**
- Modify: `src/services/Integrations/NexConvo.Integrations.Infrastructure/DependencyInjection.cs`

**Interfaces:** Produces DI registration `IConnectionTester<S3TestInput> -> S3ConnectionTester`.

- [ ] **Step 1: Register**

```csharp
services.AddSingleton<Func<S3TestInput, IAmazonS3>>(_ => input =>
{
    var creds = new Amazon.Runtime.BasicAWSCredentials(input.AccessKeyId, input.SecretAccessKey);
    var cfg = new AmazonS3Config();
    if (!string.IsNullOrWhiteSpace(input.CustomEndpoint))
    {
        cfg.ServiceURL = input.CustomEndpoint;
        cfg.ForcePathStyle = true;
    }
    else
    {
        cfg.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(input.Region);
    }
    return new AmazonS3Client(creds, cfg);
});
services.AddScoped<IConnectionTester<S3TestInput>, S3ConnectionTester>();
```

- [ ] **Step 2: Build**

Run: `dotnet build src/services/Integrations/NexConvo.Integrations.Api`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/services/Integrations/NexConvo.Integrations.Infrastructure/DependencyInjection.cs
git commit -m "feat(integrations): register S3ConnectionTester + S3 client factory"
```

---

## Task 7: TestS3ConnectionCommand + Handler (Application)

**Files:**
- Create: `.../Application/Features/S3Config/Commands/TestS3ConnectionCommand.cs`
- Create: `.../Application/Features/S3Config/Commands/TestS3ConnectionCommandHandler.cs`
- Test: `tests/services/Integrations/NexConvo.Integrations.Application.Tests/TestS3ConnectionCommandHandlerTests.cs`

**Interfaces:**
- Produces: `record TestS3ConnectionCommand(string BucketName, string Region, string? AccessKeyId, string? SecretAccessKey, string? CustomEndpoint) : IRequest<ConnectionHealth>`. When `AccessKeyId`/`SecretAccessKey` are null/blank, the handler loads the stored config and decrypts existing keys (so the user can re-test without re-entering secrets).

- [ ] **Step 1: Write the failing test** — two cases: (a) explicit keys → calls tester with those; (b) blank keys + stored config → decrypts stored keys. Use a fake `IConnectionTester<S3TestInput>` capturing the input, and an in-memory `IIntegrationsDbContext` + fake `IAesEncryptionService` (Decrypt returns `"dec:"+input`).

```csharp
[Fact]
public async Task Uses_explicit_keys_when_provided()
{
    var tester = new CapturingTester();
    var handler = new TestS3ConnectionCommandHandler(Db(), Tenant(), Aes(), tester);
    var result = await handler.Handle(
        new TestS3ConnectionCommand("b", "us-east-1", "AK", "SK", null), default);
    Assert.Equal("AK", tester.Last!.AccessKeyId);
    Assert.True(result.Success);
}

[Fact]
public async Task Falls_back_to_stored_decrypted_keys_when_blank()
{
    var db = Db(withStoredConfig: true); // stored EncryptedAccessKeyId="ENC_AK"
    var tester = new CapturingTester();
    var handler = new TestS3ConnectionCommandHandler(db, Tenant(), Aes(), tester);
    await handler.Handle(new TestS3ConnectionCommand("b", "us-east-1", null, null, null), default);
    Assert.Equal("dec:ENC_AK", tester.Last!.AccessKeyId);
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/services/Integrations/NexConvo.Integrations.Application.Tests --filter TestS3ConnectionCommandHandlerTests`
Expected: FAIL.

- [ ] **Step 3: Implement command + handler**

```csharp
public sealed record TestS3ConnectionCommand(
    string BucketName, string Region, string? AccessKeyId, string? SecretAccessKey, string? CustomEndpoint)
    : IRequest<ConnectionHealth>;
```
```csharp
public sealed class TestS3ConnectionCommandHandler(
    IIntegrationsDbContext db, ITenantContext tenant,
    IAesEncryptionService aes, IConnectionTester<S3TestInput> tester)
    : IRequestHandler<TestS3ConnectionCommand, ConnectionHealth>
{
    public async Task<ConnectionHealth> Handle(TestS3ConnectionCommand r, CancellationToken ct)
    {
        string ak = r.AccessKeyId ?? "", sk = r.SecretAccessKey ?? "";
        if (string.IsNullOrWhiteSpace(ak) || string.IsNullOrWhiteSpace(sk))
        {
            var stored = await db.WorkspaceS3Configs
                .FirstOrDefaultAsync(x => x.TenantId == tenant.TenantId, ct);
            if (stored is null)
                return ConnectionHealth.Failed("No S3 credentials configured.", null);
            ak = aes.Decrypt(stored.EncryptedAccessKeyId);
            sk = aes.Decrypt(stored.EncryptedSecretAccessKey);
        }
        return await tester.TestAsync(
            new S3TestInput(r.BucketName, r.Region, ak, sk, r.CustomEndpoint), ct);
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: same filter. Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/services/Integrations/NexConvo.Integrations.Application/Features/S3Config/Commands tests/services/Integrations/NexConvo.Integrations.Application.Tests
git commit -m "feat(integrations): TestS3ConnectionCommand — probe with explicit or stored keys"
```

---

## Task 8: POST /api/v1/s3-config/test endpoint

**Files:**
- Modify: `.../Api/Controllers/WorkspaceS3ConfigController.cs`
- Create: `.../Api/Models/TestS3ConfigRequest.cs`

**Interfaces:** Consumes `TestS3ConnectionCommand`. Produces route `POST /api/v1/s3-config/test` → `Ok(ConnectionHealth)`.

- [ ] **Step 1: Request model**

```csharp
public sealed record TestS3ConfigRequest(
    string BucketName, string Region, string? AccessKeyId, string? SecretAccessKey, string? CustomEndpoint);
```

- [ ] **Step 2: Add the action**

```csharp
[HttpPost("test")]
public async Task<IActionResult> Test([FromBody] TestS3ConfigRequest body, CancellationToken ct)
    => Ok(await sender.Send(new TestS3ConnectionCommand(
        body.BucketName, body.Region, body.AccessKeyId, body.SecretAccessKey, body.CustomEndpoint), ct));
```
(Class already has `[Authorize(Policy = "settings:manage")]` — inherited.)

- [ ] **Step 3: Verify gateway routes it**

The gateway route `/api/v1/s3-config/{**catch-all}` (added earlier) covers `/test`. Confirm: `grep -n "s3-config" src/gateway/NexConvo.Gateway/appsettings.json` shows the catch-all route.

- [ ] **Step 4: Manual smoke (after `docker compose up -d --build integrations gateway`)**

Run: `curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost:5055/api/v1/s3-config/test -H "Content-Type: application/json" -d '{}'`
Expected: `401` (unauthenticated → routed to service, auth required — NOT 404).

- [ ] **Step 5: Commit**

```bash
git add src/services/Integrations/NexConvo.Integrations.Api
git commit -m "feat(integrations): POST /api/v1/s3-config/test endpoint"
```

---

## Task 9: Server-side test-then-save gate (SaveS3ConfigCommandHandler)

**Files:**
- Modify: `.../Application/Features/S3Config/Commands/SaveS3ConfigCommandHandler.cs`
- Test: extend `.../Application.Tests` with `SaveS3ConfigGateTests.cs`

**Interfaces:** Consumes `IConnectionTester<S3TestInput>`. On new/changed credentials, re-tests; on failure throws a validation-style failure mapped to **422**; on success calls `config.ApplyHealth(Healthy)`.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task Save_rejects_with_422_when_retest_fails()
{
    var tester = new FailingTester();  // returns ConnectionHealth.Failed
    var handler = NewSaveHandler(tester);
    await Assert.ThrowsAsync<ConnectionTestFailedException>(() =>
        handler.Handle(NewSaveCommand(withNewKeys: true), default));
}

[Fact]
public async Task Save_persists_and_applies_health_on_success()
{
    var tester = new HealthyTester();
    var (handler, db) = NewSaveHandlerWithDb(tester);
    await handler.Handle(NewSaveCommand(withNewKeys: true), default);
    var saved = db.WorkspaceS3Configs.Single();
    Assert.Equal(ConnectionStatus.Healthy, saved.LastTestStatus);
}
```
Define `ConnectionTestFailedException` in Application (mapped to **422** in the exception mapper — mirror the `DomainException` mapping).

- [ ] **Step 2: Run to verify it fails** — `dotnet test ... --filter SaveS3ConfigGateTests`. Expected: FAIL.

- [ ] **Step 3: Implement the gate**

In the handler, after resolving whether keys are new/changed (`hasNewAccessKey`/`hasNewSecretKey` already computed) and BEFORE persisting:
```csharp
if (hasNewAccessKey || hasNewSecretKey)
{
    var probe = await tester.TestAsync(new S3TestInput(
        request.BucketName, request.Region,
        request.AccessKeyId ?? aes.Decrypt(config?.EncryptedAccessKeyId ?? throw ...),
        request.SecretAccessKey ?? aes.Decrypt(config?.EncryptedSecretAccessKey ?? throw ...),
        request.CustomEndpoint), cancellationToken);
    if (!probe.Success)
        throw new ConnectionTestFailedException("s3", probe.ErrorMessage);
    // apply after entity exists/updated:
}
...
config.ApplyHealth(probe); // (hoist probe var; on unchanged-keys path skip re-test and leave prior health)
```
Inject `IConnectionTester<S3TestInput> tester` into the handler ctor. Keep the existing encrypt/audit/publish flow.

- [ ] **Step 4: Run to verify it passes** — Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/services/Integrations/NexConvo.Integrations.Application
git commit -m "feat(integrations): server re-test gate on S3 save (422 on failure) + persist health"
```

---

## Task 10: Extend GetS3ConfigQuery DTO with health

**Files:**
- Modify: `.../Application/Features/S3Config/Queries/S3ConfigDto.cs`, `GetS3ConfigQueryHandler.cs`
- Test: extend the query handler test.

**Interfaces:** Produces `S3ConfigDto` additional fields: `ConnectionStatus LastTestStatus`, `DateTimeOffset? LastTestedAt`, `string? LastTestError`, `int? LastTestLatencyMs`.

- [ ] **Step 1: Write the failing test** — asserts the DTO carries `LastTestStatus` from the entity.
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Add fields to the record + map in the handler.**
- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5: Commit** — `feat(integrations): expose S3 connection health in GetS3Config DTO`.

---

## Task 11: Frontend — test hook, BFF route, form gating, health badge, 422 toast

**Files:**
- Modify: `frontend/src/features/settings/model/s3-config.types.ts`, `s3-config.schema.ts`
- Create: `frontend/src/features/settings/api/use-test-s3-connection.ts`
- Create: `frontend/src/app/api/bff/settings/s3-config/test/route.ts`
- Modify: `frontend/src/features/settings/components/s3-config-form.tsx`
- Modify: `frontend/src/shared/i18n/messages/en.json` + `bn.json`
- Test: `frontend/src/features/settings/api/use-test-s3-connection.test.tsx` (+ MSW handler) and a form test.

**Interfaces:** Consumes `POST /settings/s3-config/test`. Produces `useTestS3Connection()` → `{ success, status, detail?, errorMessage?, latencyMs? }`.

- [ ] **Step 1: Types + schema** — add `lastTestStatus`, `lastTestedAt`, `lastTestError`, `lastTestLatencyMs` to `S3ConfigDto`; add `S3TestResult` type. (Test: schema parse test in `s3-config.schema.test.ts`.)

- [ ] **Step 2: BFF test route (failing MSW test first)**

`route.ts`:
```typescript
import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';

export const POST = withBff(async (req, { api }) => {
  const body = await req.json().catch(() => ({}));
  const { data } = await api.post('/api/v1/s3-config/test', body);
  return NextResponse.json(data);
});
```

- [ ] **Step 3: `use-test-s3-connection.ts`** — React Query `useMutation` calling `/api/bff/settings/s3-config/test`, returns the result; never throws on credential failure (200 with `success:false`). (Failing hook test with MSW first, mirroring `use-test-channel-connection.test.tsx`.)

- [ ] **Step 4: Form wiring** — add `testState: 'idle'|'testing'|'success'|'failed'`; **Test Connection** button calls the hook; green `CheckCircle2` + detail/latency on success, red `XCircle` + error on failure. Save disabled unless `testState==='success'` (only when keys changed — reuse `requiresTest` logic from `ai-settings-form`). Any credential edit resets `testState` to `idle`. On **422** from save, show a toast/inline: `t('s3.credentialsExpired')` and reset `testState`. Render health badge from `config.lastTestStatus` (🟢/🟡/🔴/⚪) + "last tested {relative time}".

- [ ] **Step 5: i18n** — add keys to `en.json` and `bn.json`: `s3.testConnection`, `s3.testPassed`, `s3.testFailed`, `s3.testRequired`, `s3.credentialsExpired`, `s3.healthHealthy/Degraded/Failed/Untested`, `s3.lastTested`.

- [ ] **Step 6: Run frontend tests**

Run: `cd frontend && pnpm test src/features/settings` (or the repo's configured runner).
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/features/settings frontend/src/app/api/bff/settings/s3-config/test frontend/src/shared/i18n
git commit -m "feat(frontend): S3 test-connection button, save gating, health badge, 422 toast"
```

---

## Task 12: Feature guard — knowledge upload checks S3 health

**Files:**
- Modify (backend): the knowledge-upload path. **Cross-service note:** S3 config lives in Integrations; knowledge upload lives in Chat. Two options:
  1. **Chat calls Integrations** `GET /api/v1/s3-config` (via gateway/internal) and checks `lastTestStatus === Healthy` before enqueuing ingestion; refuse `409` if not.
  2. If knowledge upload already uses S3 through an Integrations-owned component, guard there with `config.EnsureHealthy()`.
- Modify (frontend): `frontend/.../dashboard/chat/settings/knowledge/page.tsx` — disable upload + warning banner when S3 unhealthy, linking to S3 settings.

**Interfaces:** Consumes `S3ConfigDto.lastTestStatus`.

- [ ] **Step 1: Confirm the actual upload→S3 code path** (read the knowledge upload handler + `IKnowledgeIngestionJobRunner`) to pick option 1 vs 2. *(This is the one investigate-first step; the guard call itself is one line — `EnsureHealthy()` or a status check returning 409.)*

- [ ] **Step 2: Write the failing test** — uploading with S3 `LastTestStatus != Healthy` returns/throws 409 `connection-unhealthy`.

- [ ] **Step 3: Implement the guard** at the chosen point.

- [ ] **Step 4: Frontend banner** — when `useS3Config().lastTestStatus !== 'healthy'`, disable the upload control and show `t('knowledge.s3Unhealthy')` banner (en+bn) linking to `/dashboard/settings/s3`.

- [ ] **Step 5: Run tests → PASS. Commit** — `feat: guard knowledge upload on S3 connection health`.

---

## Task 13: End-to-end verification

- [ ] **Step 1: Rebuild & restart**

```bash
docker compose up -d --build integrations gateway chat
```

- [ ] **Step 2: Verify Integrations migrates cleanly**

Run: `docker logs --tail 20 nexconvo-integrations-1`
Expected: "Integrations database is up to date." — no `PendingModelChangesWarning`.

- [ ] **Step 3: Playwright E2E** — extend/create `frontend/e2e/settings-s3.spec.ts`: enter creds → Test fails (bad creds) → Save disabled → fix creds → Test passes → Save enabled → save → badge shows Healthy. And: with S3 unhealthy, knowledge upload shows the guard banner.

Run: `cd frontend && pnpm playwright test settings-s3`
Expected: PASS.

- [ ] **Step 4: Full suite green**

Run: `dotnet test` (Integrations + BuildingBlocks) and `cd frontend && pnpm test`.
Expected: all PASS.

- [ ] **Step 5: Commit any test fixtures**

```bash
git add frontend/e2e/settings-s3.spec.ts
git commit -m "test(e2e): S3 test-then-save + knowledge-upload guard"
```

---

## Self-Review notes

- **Spec coverage:** Test-then-save (Tasks 7–9, 11), persisted health (Tasks 3–4, 10), feature guard (Task 12), shared abstraction (Tasks 1–2), 422 UX (Task 11 Step 4), en/bn i18n (Task 11 Step 5). Scheduled re-test + notification service are **later slices** (out of scope here, per phased plan).
- **Deferred/needs-confirmation:** Task 3 layer-reference direction (Domain → Application) — resolve by either referencing or moving the throw to the Application handler; Task 12 cross-service guard path — one investigate-first step. Both are flagged inline, not placeholders.
- **Type consistency:** `ConnectionHealth`/`ConnectionStatus`/`IConnectionTester<TInput>`/`S3TestInput`/`ApplyHealth`/`EnsureHealthy` names used consistently across tasks.
