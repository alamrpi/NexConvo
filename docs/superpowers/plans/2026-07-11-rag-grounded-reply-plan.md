# RAG Grounded Reply Orchestrator (Slice 5) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Chat service's grounded RAG reply pipeline — retrieve from Knowledge over gRPC, assemble a citation-backed prompt, generate via the tenant's LLM, decide confidence, and either answer or hand off to a human — audited, idempotent, and transactional via a newly-wired MassTransit outbox.

**Architecture:** A new pure-logic `BuildingBlocks.Rag` shared project (channel-parameterized prompt assembly + token budgeting, reusable by Voice later) sits underneath a Chat-only `Conversation`/`Message`/`Escalation` domain model and an `IReplyOrchestrator` in Chat's Application layer that does the actual I/O (Knowledge gRPC client, tenant LLM call, persistence). An idempotent `MessageReceivedConsumer` is the entry point; `AddEntityFrameworkOutbox<ChatDbContext>()` makes both consumption and the resulting event publishes transactional for the first time in the solution.

**Tech Stack:** .NET 9, EF Core (Npgsql), MediatR (CQRS), MassTransit + RabbitMQ (+ EF outbox/inbox, new), Grpc.Net.ClientFactory (new), Microsoft.ML.Tokenizers (cl100k, already pinned), PostgreSQL RLS, Redis (StackExchange), xUnit + FluentAssertions + NSubstitute + MockQueryable.NSubstitute, Testcontainers.

Full design rationale: [`docs/superpowers/specs/2026-07-11-rag-grounded-reply-design.md`](../../../../d%3A/Resources/Projects/NexConvo/docs/superpowers/specs/2026-07-11-rag-grounded-reply-design.md) (repo path: `docs/superpowers/specs/2026-07-11-rag-grounded-reply-design.md`). Every task below traces to that spec's 10 numbered Decisions — read it first if anything here seems under-justified.

## Global Constraints

- Clean Architecture: Domain → Application → Infrastructure → API; API never touches `DbContext`/repositories directly (Standard 1).
- Constructor injection only, one MediatR `Command`/`Query` + `Handler` + `Validator` per write/read (Standards 2–3).
- Every tenant-scoped table has RLS: `current_setting('app.current_tenant_id', true)::uuid`, `CREATE POLICY "TenantIsolation" ... USING (...) WITH CHECK (...)`, `GRANT SELECT, INSERT, UPDATE, DELETE ON <table> TO nexconvo_service` (Standard 6). Chat's tables live in the `public` schema, snake_case, no schema prefix — confirmed via `InitialChatSchema.cs`.
- `xmin` optimistic concurrency on every new mutable entity: `builder.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();` (Standard 16).
- Every mutation writes an audit row in the same `SaveChangesAsync` call (Standard 14) — use `ChatAuditLog` (existing entity, `src/services/Chat/NexConvo.Chat.Domain/Entities/ChatAuditLog.cs`).
- Structured Serilog logging only — no string interpolation in log calls; never log message body/PII, only `ConversationId`/`MessageId`/`Confidence`/`Handoff` (Standard 9).
- Every outbound call (gRPC, LLM) wrapped in `.AddNexConvoResilience()` (Standard 8). Every event consumer idempotent (Standard 18) — this slice's mechanism is MassTransit's EF inbox (dedupe by transport `MessageId`).
- Integration events publish only after commit, via the outbox (Standard 10) — this slice introduces `AddEntityFrameworkOutbox<ChatDbContext>()`, the first use of it in the solution.
- New project → nest under the matching Solution Folder in `NexConvo.sln` (Standard 20). New public route → update YARP gateway (Standard 21) — **N/A this slice**: zero new public HTTP endpoints are added (consumer-driven + internal gRPC only).
- `net9.0` for all .NET projects; TDD (red→green) — write the failing test before the implementation for every task below.
- Package versions: match what's already centrally pinned in `Directory.Packages.props` (`MassTransit`/`MassTransit.RabbitMQ` = 8.3.4 → pin `MassTransit.EntityFrameworkCore` at 8.3.4 too; reuse existing `Grpc.Net.Client`/`Grpc.AspNetCore`/`Google.Protobuf`/`Microsoft.ML.Tokenizers`/`Microsoft.ML.Tokenizers.Data.Cl100kBase` pins, add `Grpc.Net.ClientFactory` at the same major/minor as `Grpc.Net.Client`).

**Verified corrections vs. naive assumptions (confirm these before coding):**
- `Result`/`Result<T>` are in namespace `NexConvo.BuildingBlocks.Results` (project `NexConvo.BuildingBlocks.Application`), not `...Application.Results`.
- `AddNexConvoResilience()` lives in `NexConvo.BuildingBlocks.Infrastructure` (namespace `NexConvo.BuildingBlocks.Resilience`); `ITenantContext` interface is in `NexConvo.BuildingBlocks.Application.Multitenancy`.
- `AddAiProviders()` (from `NexConvo.BuildingBlocks.Ai`) is **not yet called anywhere in Chat.Api's `Program.cs`** — Task 8 must add it.
- Chat's existing tables have **no schema prefix** (`public.channel_connections`, not `chat.channel_connections`) — new tables follow the same convention.
- `NexConvo.BuildingBlocks.Domain.DomainException` is `sealed` — new domain exceptions must be standalone `sealed class : Exception`, mirroring `ConnectionUnhealthyException`'s shape, not subclass `DomainException`.
- Commit message convention confirmed via `git log`: `feat(chat): ...`, `fix(chat): ...` etc. — scope in parentheses, imperative mood.

---

## Task 1: `BuildingBlocks.Rag` — `ChannelProfile` + grounded prompt assembler

**Files:**
- Create: `src/shared/NexConvo.BuildingBlocks.Rag/NexConvo.BuildingBlocks.Rag.csproj`
- Create: `src/shared/NexConvo.BuildingBlocks.Rag/ChannelProfile.cs`
- Create: `src/shared/NexConvo.BuildingBlocks.Rag/ContextContribution.cs`
- Create: `src/shared/NexConvo.BuildingBlocks.Rag/ConversationTurn.cs`
- Create: `src/shared/NexConvo.BuildingBlocks.Rag/IGroundedPromptAssembler.cs`
- Create: `src/shared/NexConvo.BuildingBlocks.Rag/GroundedPromptAssembler.cs`
- Create: `tests/shared/NexConvo.BuildingBlocks.Rag.Tests/NexConvo.BuildingBlocks.Rag.Tests.csproj`
- Test: `tests/shared/NexConvo.BuildingBlocks.Rag.Tests/GroundedPromptAssemblerTests.cs`
- Modify: `NexConvo.sln` (nest both new csproj under `src/shared` and `tests/shared` Solution Folders — same folders `NexConvo.BuildingBlocks.Ai` / `NexConvo.BuildingBlocks.Domain.Tests` already sit under)

**Interfaces:**
- Produces: `ChannelProfile` record with static presets `ChannelProfile.Chat` / `ChannelProfile.Voice`; `IGroundedPromptAssembler.BuildSystemPrompt(ChannelProfile, string?)` and `.BuildUserPrompt(IReadOnlyList<ContextContribution>, IReadOnlyList<ConversationTurn>, string, ChannelProfile)`.

- [ ] **Step 1: Create the project files**

`src/shared/NexConvo.BuildingBlocks.Rag/NexConvo.BuildingBlocks.Rag.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

`src/shared/NexConvo.BuildingBlocks.Rag/ChannelProfile.cs`:
```csharp
namespace NexConvo.BuildingBlocks.Rag;

public enum RagRegister { Chat, Voice }

public enum StreamGranularity { Token, Sentence }

/// <summary>
/// Per-channel knobs for the shared grounding pipeline. Chat and Voice pass different
/// presets into the same <see cref="IGroundedPromptAssembler"/> — no channel-specific
/// branching lives in the assembler itself.
/// </summary>
public sealed record ChannelProfile(
    RagRegister Register,
    int TopK,
    double MinScore,
    int MaxAnswerTokens,
    bool EmitCitations,
    StreamGranularity StreamGranularity)
{
    public static readonly ChannelProfile Chat = new(
        RagRegister.Chat, TopK: 5, MinScore: 0.55, MaxAnswerTokens: 600,
        EmitCitations: true, StreamGranularity.Token);

    public static readonly ChannelProfile Voice = new(
        RagRegister.Voice, TopK: 3, MinScore: 0.6, MaxAnswerTokens: 80,
        EmitCitations: false, StreamGranularity.Sentence);
}
```

`src/shared/NexConvo.BuildingBlocks.Rag/ContextContribution.cs`:
```csharp
namespace NexConvo.BuildingBlocks.Rag;

/// <summary>One retrieved chunk, numbered for citation. RAG chunks today; MCP tool results later.</summary>
public sealed record ContextContribution(int Index, string ChunkId, string DocumentId, string Content, double Score);
```

`src/shared/NexConvo.BuildingBlocks.Rag/ConversationTurn.cs`:
```csharp
namespace NexConvo.BuildingBlocks.Rag;

/// <summary>A prior turn in conversation history. Decoupled from any service's domain model — Role is "user" or "assistant".</summary>
public sealed record ConversationTurn(string Role, string Text);
```

- [ ] **Step 2: Write the failing test**

`tests/shared/NexConvo.BuildingBlocks.Rag.Tests/NexConvo.BuildingBlocks.Rag.Tests.csproj` — copy the exact `<PropertyGroup>`/package-reference shape from `tests/shared/NexConvo.BuildingBlocks.Domain.Tests/NexConvo.BuildingBlocks.Domain.Tests.csproj` (read that file first to match xUnit/FluentAssertions package references exactly), with a `<ProjectReference Include="..\..\..\src\shared\NexConvo.BuildingBlocks.Rag\NexConvo.BuildingBlocks.Rag.csproj" />`.

`tests/shared/NexConvo.BuildingBlocks.Rag.Tests/GroundedPromptAssemblerTests.cs`:
```csharp
using FluentAssertions;
using NexConvo.BuildingBlocks.Rag;
using Xunit;

namespace NexConvo.BuildingBlocks.Rag.Tests;

public class GroundedPromptAssemblerTests
{
    private readonly GroundedPromptAssembler _sut = new();

    [Fact]
    public void BuildSystemPrompt_ChatProfile_InstructsCitationsAndAbstentionMarker()
    {
        var prompt = _sut.BuildSystemPrompt(ChannelProfile.Chat, tenantSystemPromptOverride: null);

        prompt.Should().Contain("[[NO_ANSWER]]");
        prompt.Should().Contain("only from the numbered context");
        prompt.Should().Contain("[n]");
    }

    [Fact]
    public void BuildSystemPrompt_VoiceProfile_OmitsCitationInstructionAndCapsLength()
    {
        var prompt = _sut.BuildSystemPrompt(ChannelProfile.Voice, tenantSystemPromptOverride: null);

        prompt.Should().Contain("[[NO_ANSWER]]");
        prompt.Should().NotContain("[n]");
        prompt.Should().Contain("1-2 sentence");
    }

    [Fact]
    public void BuildSystemPrompt_WithTenantOverride_AppendsOverrideAfterGroundingRules()
    {
        var prompt = _sut.BuildSystemPrompt(ChannelProfile.Chat, tenantSystemPromptOverride: "Always sign off with 'Team NexConvo'.");

        prompt.Should().Contain("[[NO_ANSWER]]");
        prompt.Should().Contain("Always sign off with 'Team NexConvo'.");
    }

    [Fact]
    public void BuildUserPrompt_ChatProfile_NumbersContextForCitation()
    {
        var context = new[]
        {
            new ContextContribution(1, "chunk-1", "doc-1", "Refunds are processed within 5 business days.", 0.91),
            new ContextContribution(2, "chunk-2", "doc-2", "Contact support for refund status.", 0.77),
        };

        var prompt = _sut.BuildUserPrompt(context, history: [], question: "How long do refunds take?", ChannelProfile.Chat);

        prompt.Should().Contain("[1] Refunds are processed within 5 business days.");
        prompt.Should().Contain("[2] Contact support for refund status.");
        prompt.Should().Contain("How long do refunds take?");
    }

    [Fact]
    public void BuildUserPrompt_IncludesHistoryInOrder()
    {
        var history = new[]
        {
            new ConversationTurn("user", "Hi, I have a question about refunds."),
            new ConversationTurn("assistant", "Sure, what would you like to know?"),
        };

        var prompt = _sut.BuildUserPrompt(context: [], history, question: "How long do refunds take?", ChannelProfile.Chat);

        var firstIndex = prompt.IndexOf("Hi, I have a question about refunds.", StringComparison.Ordinal);
        var secondIndex = prompt.IndexOf("Sure, what would you like to know?", StringComparison.Ordinal);
        firstIndex.Should().BeGreaterThan(-1);
        secondIndex.Should().BeGreaterThan(firstIndex);
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/shared/NexConvo.BuildingBlocks.Rag.Tests/NexConvo.BuildingBlocks.Rag.Tests.csproj`
Expected: FAIL to compile — `GroundedPromptAssembler`, `IGroundedPromptAssembler` do not exist yet.

- [ ] **Step 4: Write minimal implementation**

`src/shared/NexConvo.BuildingBlocks.Rag/IGroundedPromptAssembler.cs`:
```csharp
namespace NexConvo.BuildingBlocks.Rag;

/// <summary>
/// Assembles the final LLM prompt from retrieved context + history + the user's question,
/// driven entirely by a <see cref="ChannelProfile"/>. Pure logic — no I/O, no channel-specific
/// delivery. Shared by Chat today; Voice reuses it unchanged later with <see cref="ChannelProfile.Voice"/>.
/// </summary>
public interface IGroundedPromptAssembler
{
    string BuildSystemPrompt(ChannelProfile profile, string? tenantSystemPromptOverride);

    string BuildUserPrompt(
        IReadOnlyList<ContextContribution> context,
        IReadOnlyList<ConversationTurn> history,
        string question,
        ChannelProfile profile);
}
```

`src/shared/NexConvo.BuildingBlocks.Rag/GroundedPromptAssembler.cs`:
```csharp
using System.Text;

namespace NexConvo.BuildingBlocks.Rag;

public sealed class GroundedPromptAssembler : IGroundedPromptAssembler
{
    private const string AbstentionMarker = "[[NO_ANSWER]]";

    public string BuildSystemPrompt(ChannelProfile profile, string? tenantSystemPromptOverride)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a support assistant. Answer only from the numbered context provided below.");
        sb.AppendLine($"If the context does not contain the answer, respond with exactly {AbstentionMarker} and nothing else.");
        sb.AppendLine("Always reply in the same language the user wrote in.");

        if (profile.EmitCitations)
        {
            sb.AppendLine("Cite the context you used inline with its number in brackets, e.g. [n].");
        }
        else
        {
            sb.AppendLine("Do not include citations, numbered lists, or links. Speak naturally, as in a phone call.");
            sb.AppendLine("Keep the answer to 1-2 sentences.");
        }

        if (!string.IsNullOrWhiteSpace(tenantSystemPromptOverride))
        {
            sb.AppendLine(tenantSystemPromptOverride);
        }

        return sb.ToString().TrimEnd();
    }

    public string BuildUserPrompt(
        IReadOnlyList<ContextContribution> context,
        IReadOnlyList<ConversationTurn> history,
        string question,
        ChannelProfile profile)
    {
        var sb = new StringBuilder();

        if (context.Count > 0)
        {
            sb.AppendLine("Context:");
            foreach (var c in context)
            {
                sb.AppendLine($"[{c.Index}] {c.Content}");
            }
            sb.AppendLine();
        }

        if (history.Count > 0)
        {
            sb.AppendLine("Conversation so far:");
            foreach (var turn in history)
            {
                sb.AppendLine($"{turn.Role}: {turn.Text}");
            }
            sb.AppendLine();
        }

        sb.AppendLine($"Question: {question}");

        return sb.ToString().TrimEnd();
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/shared/NexConvo.BuildingBlocks.Rag.Tests/NexConvo.BuildingBlocks.Rag.Tests.csproj`
Expected: PASS (5/5).

- [ ] **Step 6: Nest new projects in `NexConvo.sln`**

Open `NexConvo.sln`, add `Project(...) = "NexConvo.BuildingBlocks.Rag", "src\shared\NexConvo.BuildingBlocks.Rag\NexConvo.BuildingBlocks.Rag.csproj", "{NEW-GUID}"` and `"NexConvo.BuildingBlocks.Rag.Tests"` under `tests\shared\...`, each with a `NestedProjects` entry pointing at the existing `src/shared` / `tests/shared` Solution Folder GUIDs (copy the exact folder GUIDs already used by `NexConvo.BuildingBlocks.Ai` and `NexConvo.BuildingBlocks.Domain.Tests` respectively — read the `.sln` file to find them; use `dotnet sln add` instead of hand-editing if preferred: `dotnet sln NexConvo.sln add src/shared/NexConvo.BuildingBlocks.Rag/NexConvo.BuildingBlocks.Rag.csproj --solution-folder src/shared` and the equivalent for the test project).

Run: `dotnet build NexConvo.sln` — Expected: builds clean, both new projects appear nested correctly in an IDE's Solution Explorer.

- [ ] **Step 7: Commit**

```bash
git add src/shared/NexConvo.BuildingBlocks.Rag tests/shared/NexConvo.BuildingBlocks.Rag.Tests NexConvo.sln
git commit -m "feat(rag): add BuildingBlocks.Rag with ChannelProfile and grounded prompt assembler"
```

---

## Task 2: `BuildingBlocks.Rag` — token budgeter

**Files:**
- Create: `src/shared/NexConvo.BuildingBlocks.Rag/ITokenBudgeter.cs`
- Create: `src/shared/NexConvo.BuildingBlocks.Rag/TokenBudgeter.cs`
- Modify: `src/shared/NexConvo.BuildingBlocks.Rag/NexConvo.BuildingBlocks.Rag.csproj`
- Test: `tests/shared/NexConvo.BuildingBlocks.Rag.Tests/TokenBudgeterTests.cs`

**Interfaces:**
- Consumes: `ContextContribution`, `ConversationTurn` (Task 1).
- Produces: `ITokenBudgeter.EstimateTokens(string)`, `ITokenBudgeter.Fit(IReadOnlyList<ContextContribution>, IReadOnlyList<ConversationTurn>, int)` returning `(IReadOnlyList<ContextContribution> Context, IReadOnlyList<ConversationTurn> History)`.

- [ ] **Step 1: Add tokenizer package reference**

Confirm exact pinned versions first:
Run: `grep -n "Microsoft.ML.Tokenizers" Directory.Packages.props`
Expected: shows `Microsoft.ML.Tokenizers` and `Microsoft.ML.Tokenizers.Data.Cl100kBase` with version numbers already pinned (used today by Knowledge's `BengaliAwareChunker`).

Add to `src/shared/NexConvo.BuildingBlocks.Rag/NexConvo.BuildingBlocks.Rag.csproj` inside a new `<ItemGroup>`:
```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.ML.Tokenizers" />
    <PackageReference Include="Microsoft.ML.Tokenizers.Data.Cl100kBase" />
  </ItemGroup>
```

- [ ] **Step 2: Write the failing test**

`tests/shared/NexConvo.BuildingBlocks.Rag.Tests/TokenBudgeterTests.cs`:
```csharp
using FluentAssertions;
using NexConvo.BuildingBlocks.Rag;
using Xunit;

namespace NexConvo.BuildingBlocks.Rag.Tests;

public class TokenBudgeterTests
{
    private readonly TokenBudgeter _sut = new();

    [Fact]
    public void EstimateTokens_ReturnsPositiveCountForNonEmptyText()
    {
        _sut.EstimateTokens("Refunds are processed within 5 business days.").Should().BeGreaterThan(0);
    }

    [Fact]
    public void EstimateTokens_EmptyString_ReturnsZero()
    {
        _sut.EstimateTokens("").Should().Be(0);
    }

    [Fact]
    public void Fit_WithinBudget_ReturnsEverythingUnchanged()
    {
        var context = new[] { new ContextContribution(1, "c1", "d1", "Short answer.", 0.9) };
        var history = new[] { new ConversationTurn("user", "Hi") };

        var (fittedContext, fittedHistory) = _sut.Fit(context, history, maxContextTokens: 10_000);

        fittedContext.Should().BeEquivalentTo(context);
        fittedHistory.Should().BeEquivalentTo(history);
    }

    [Fact]
    public void Fit_OverBudget_TrimsOldestHistoryFirst()
    {
        var context = new[] { new ContextContribution(1, "c1", "d1", "Refunds take 5 business days to process once approved.", 0.9) };
        var history = new[]
        {
            new ConversationTurn("user", "This is an old message that should be trimmed first because it is oldest."),
            new ConversationTurn("assistant", "This is a newer reply that should be kept if possible."),
        };

        var (fittedContext, fittedHistory) = _sut.Fit(context, history, maxContextTokens: 20);

        fittedHistory.Should().NotContain(t => t.Text.StartsWith("This is an old message"));
        fittedContext.Should().NotBeEmpty();
    }

    [Fact]
    public void Fit_StillOverBudgetAfterTrimmingHistory_DropsLowestScoreContribution()
    {
        var context = new[]
        {
            new ContextContribution(1, "c1", "d1", "High score chunk with a decently long piece of explanatory text.", 0.95),
            new ContextContribution(2, "c2", "d2", "Low score chunk with a decently long piece of explanatory text.", 0.4),
        };

        var (fittedContext, _) = _sut.Fit(context, history: [], maxContextTokens: 15);

        fittedContext.Should().ContainSingle(c => c.ChunkId == "c1");
        fittedContext.Should().NotContain(c => c.ChunkId == "c2");
    }

    [Fact]
    public void Fit_NeverThrowsWhenEverythingIsTrimmedAway()
    {
        var context = new[] { new ContextContribution(1, "c1", "d1", "Some reasonably long chunk of text content here.", 0.9) };
        var history = new[] { new ConversationTurn("user", "Some reasonably long history message here too.") };

        var act = () => _sut.Fit(context, history, maxContextTokens: 0);

        act.Should().NotThrow();
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/shared/NexConvo.BuildingBlocks.Rag.Tests/NexConvo.BuildingBlocks.Rag.Tests.csproj --filter "FullyQualifiedName~TokenBudgeterTests"`
Expected: FAIL to compile — `TokenBudgeter` does not exist.

- [ ] **Step 4: Write minimal implementation**

`src/shared/NexConvo.BuildingBlocks.Rag/ITokenBudgeter.cs`:
```csharp
namespace NexConvo.BuildingBlocks.Rag;

/// <summary>Trims history and/or retrieved context so the assembled prompt fits the model's context window.</summary>
public interface ITokenBudgeter
{
    int EstimateTokens(string text);

    /// <summary>
    /// Trims oldest history turns first, then drops lowest-score contributions, until the
    /// combined estimated token count of context + history fits <paramref name="maxContextTokens"/>.
    /// Never mutates the inputs; returns new lists.
    /// </summary>
    (IReadOnlyList<ContextContribution> Context, IReadOnlyList<ConversationTurn> History) Fit(
        IReadOnlyList<ContextContribution> context,
        IReadOnlyList<ConversationTurn> history,
        int maxContextTokens);
}
```

`src/shared/NexConvo.BuildingBlocks.Rag/TokenBudgeter.cs`:
```csharp
using Microsoft.ML.Tokenizers;

namespace NexConvo.BuildingBlocks.Rag;

public sealed class TokenBudgeter : ITokenBudgeter
{
    private readonly Tokenizer _tokenizer = TiktokenTokenizer.CreateForEncoding("cl100k_base");

    public int EstimateTokens(string text) =>
        string.IsNullOrEmpty(text) ? 0 : _tokenizer.CountTokens(text);

    public (IReadOnlyList<ContextContribution> Context, IReadOnlyList<ConversationTurn> History) Fit(
        IReadOnlyList<ContextContribution> context,
        IReadOnlyList<ConversationTurn> history,
        int maxContextTokens)
    {
        var remainingHistory = new List<ConversationTurn>(history);
        var remainingContext = context.OrderByDescending(c => c.Score).ToList();

        while (TotalTokens(remainingContext, remainingHistory) > maxContextTokens && remainingHistory.Count > 0)
        {
            remainingHistory.RemoveAt(0); // oldest first
        }

        while (TotalTokens(remainingContext, remainingHistory) > maxContextTokens && remainingContext.Count > 0)
        {
            remainingContext.RemoveAt(remainingContext.Count - 1); // lowest score first (list is score-descending)
        }

        return (remainingContext, remainingHistory);
    }

    private int TotalTokens(IEnumerable<ContextContribution> context, IEnumerable<ConversationTurn> history) =>
        context.Sum(c => EstimateTokens(c.Content)) + history.Sum(h => EstimateTokens(h.Text));
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/shared/NexConvo.BuildingBlocks.Rag.Tests/NexConvo.BuildingBlocks.Rag.Tests.csproj --filter "FullyQualifiedName~TokenBudgeterTests"`
Expected: PASS (6/6).

- [ ] **Step 6: Commit**

```bash
git add src/shared/NexConvo.BuildingBlocks.Rag tests/shared/NexConvo.BuildingBlocks.Rag.Tests
git commit -m "feat(rag): add cl100k-based token budgeter to BuildingBlocks.Rag"
```

---

## Task 3: Chat domain — enums, value objects, domain exceptions

**Files:**
- Create: `src/services/Chat/NexConvo.Chat.Domain/Enums/ConversationState.cs`
- Create: `src/services/Chat/NexConvo.Chat.Domain/Enums/MessageDirection.cs`
- Create: `src/services/Chat/NexConvo.Chat.Domain/Enums/MessageSenderRole.cs`
- Create: `src/services/Chat/NexConvo.Chat.Domain/Enums/MessageDeliveryStatus.cs`
- Create: `src/services/Chat/NexConvo.Chat.Domain/Enums/EscalationReason.cs`
- Create: `src/services/Chat/NexConvo.Chat.Domain/Enums/ConfidenceBand.cs`
- Create: `src/services/Chat/NexConvo.Chat.Domain/ValueObjects/ChannelIdentity.cs`
- Create: `src/services/Chat/NexConvo.Chat.Domain/ValueObjects/MessageSender.cs`
- Create: `src/services/Chat/NexConvo.Chat.Domain/ValueObjects/RagConfidence.cs`
- Create: `src/services/Chat/NexConvo.Chat.Domain/Exceptions/InvalidConversationStateTransitionException.cs`
- Create: `src/services/Chat/NexConvo.Chat.Domain/Exceptions/AiReplySuppressedException.cs`
- Test: `tests/services/Chat/NexConvo.Chat.Domain.Tests/RagConfidenceTests.cs`

**Interfaces:**
- Produces: `ChannelIdentity(LeadSourceChannel Channel, string ExternalConversationId)`, `MessageSender(MessageSenderRole Role, string? DisplayRef, Guid? UserId)`, `RagConfidence(double Score, ConfidenceBand Band)` + `RagConfidence.FromRetrievalAndAbstention(double topRetrievalScore, bool modelAbstained)`.

- [ ] **Step 1: Write the failing test**

First read `src/services/Chat/NexConvo.Chat.Domain/Entities/ChannelConnection.cs` and one existing exception (e.g. search `src/shared/NexConvo.BuildingBlocks.Domain/Health/ConnectionUnhealthyException.cs`) to confirm exact exception-class style (message-only ctor vs. custom fields) before writing — mirror it.

`tests/services/Chat/NexConvo.Chat.Domain.Tests/RagConfidenceTests.cs`:
```csharp
using FluentAssertions;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Chat.Domain.ValueObjects;
using Xunit;

namespace NexConvo.Chat.Domain.Tests;

public class RagConfidenceTests
{
    [Theory]
    [InlineData(0.85, ConfidenceBand.High)]
    [InlineData(0.6, ConfidenceBand.Medium)]
    [InlineData(0.2, ConfidenceBand.Low)]
    public void FromRetrievalAndAbstention_BandsByScoreWhenNotAbstained(double score, ConfidenceBand expectedBand)
    {
        var confidence = RagConfidence.FromRetrievalAndAbstention(score, modelAbstained: false);

        confidence.Score.Should().Be(score);
        confidence.Band.Should().Be(expectedBand);
    }

    [Fact]
    public void FromRetrievalAndAbstention_ModelAbstained_AlwaysLowRegardlessOfScore()
    {
        var confidence = RagConfidence.FromRetrievalAndAbstention(topRetrievalScore: 0.95, modelAbstained: true);

        confidence.Band.Should().Be(ConfidenceBand.Low);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/services/Chat/NexConvo.Chat.Domain.Tests/NexConvo.Chat.Domain.Tests.csproj --filter "FullyQualifiedName~RagConfidenceTests"`
Expected: FAIL to compile — `RagConfidence`, `ConfidenceBand` do not exist.

- [ ] **Step 3: Write minimal implementation**

`src/services/Chat/NexConvo.Chat.Domain/Enums/ConversationState.cs`:
```csharp
namespace NexConvo.Chat.Domain.Enums;

public enum ConversationState { AiHandling, PendingHuman, HumanHandling, Resolved, Closed }
```

`src/services/Chat/NexConvo.Chat.Domain/Enums/MessageDirection.cs`:
```csharp
namespace NexConvo.Chat.Domain.Enums;

public enum MessageDirection { Inbound, Outbound }
```

`src/services/Chat/NexConvo.Chat.Domain/Enums/MessageSenderRole.cs`:
```csharp
namespace NexConvo.Chat.Domain.Enums;

/// <summary>
/// ToolCall/ToolResult are unused today but present now so a future MCP agentic loop
/// (P3) does not require a migration rewrite of the messages table's sender-type column.
/// </summary>
public enum MessageSenderRole { Contact, Ai, Agent, System, ToolCall, ToolResult }
```

`src/services/Chat/NexConvo.Chat.Domain/Enums/MessageDeliveryStatus.cs`:
```csharp
namespace NexConvo.Chat.Domain.Enums;

public enum MessageDeliveryStatus { Pending, Sent, Delivered, Failed }
```

`src/services/Chat/NexConvo.Chat.Domain/Enums/EscalationReason.cs`:
```csharp
namespace NexConvo.Chat.Domain.Enums;

public enum EscalationReason
{
    LowConfidence,
    TriggerPhrase,
    SentimentNegative,
    MaxUnansweredExceeded,
    ExplicitAgentRequest,
    NoAiConfig,
}
```

`src/services/Chat/NexConvo.Chat.Domain/Enums/ConfidenceBand.cs`:
```csharp
namespace NexConvo.Chat.Domain.Enums;

public enum ConfidenceBand { High, Medium, Low }
```

`src/services/Chat/NexConvo.Chat.Domain/ValueObjects/ChannelIdentity.cs`:
```csharp
using NexConvo.Contracts.Enums;

namespace NexConvo.Chat.Domain.ValueObjects;

/// <summary>Identifies which external channel thread a Conversation maps to. Uses the shared Contracts enum, matching MessageReceivedIntegrationEvent's Channel field.</summary>
public sealed record ChannelIdentity(LeadSourceChannel Channel, string ExternalConversationId);
```

`src/services/Chat/NexConvo.Chat.Domain/ValueObjects/MessageSender.cs`:
```csharp
using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Domain.ValueObjects;

public sealed record MessageSender(MessageSenderRole Role, string? DisplayRef, Guid? UserId)
{
    public static MessageSender Contact(string? displayRef) => new(MessageSenderRole.Contact, displayRef, null);
    public static MessageSender Ai() => new(MessageSenderRole.Ai, "AI Assistant", null);
    public static MessageSender Agent(Guid userId, string displayRef) => new(MessageSenderRole.Agent, displayRef, userId);
}
```

`src/services/Chat/NexConvo.Chat.Domain/ValueObjects/RagConfidence.cs`:
```csharp
using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Domain.ValueObjects;

public sealed record RagConfidence(double Score, ConfidenceBand Band)
{
    public static RagConfidence FromRetrievalAndAbstention(double topRetrievalScore, bool modelAbstained)
    {
        if (modelAbstained)
        {
            return new RagConfidence(topRetrievalScore, ConfidenceBand.Low);
        }

        var band = topRetrievalScore switch
        {
            >= 0.8 => ConfidenceBand.High,
            >= 0.5 => ConfidenceBand.Medium,
            _ => ConfidenceBand.Low,
        };

        return new RagConfidence(topRetrievalScore, band);
    }
}
```

`src/services/Chat/NexConvo.Chat.Domain/Exceptions/InvalidConversationStateTransitionException.cs` (mirror the exact base-Exception style found in `ConnectionUnhealthyException.cs` — adjust constructor shape to match what you read there; illustrative shape below):
```csharp
namespace NexConvo.Chat.Domain.Exceptions;

public sealed class InvalidConversationStateTransitionException(string message) : Exception(message);
```

`src/services/Chat/NexConvo.Chat.Domain/Exceptions/AiReplySuppressedException.cs`:
```csharp
namespace NexConvo.Chat.Domain.Exceptions;

public sealed class AiReplySuppressedException(string message) : Exception(message);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/services/Chat/NexConvo.Chat.Domain.Tests/NexConvo.Chat.Domain.Tests.csproj --filter "FullyQualifiedName~RagConfidenceTests"`
Expected: PASS (4/4).

- [ ] **Step 5: Commit**

```bash
git add src/services/Chat/NexConvo.Chat.Domain tests/services/Chat/NexConvo.Chat.Domain.Tests/RagConfidenceTests.cs
git commit -m "feat(chat): add conversation domain enums, value objects, and exceptions"
```

---

## Task 4: Chat domain — `Conversation` aggregate, `Message`, `Escalation`

**Files:**
- Create: `src/services/Chat/NexConvo.Chat.Domain/Events/ConversationHandoffRequestedDomainEvent.cs`
- Create: `src/services/Chat/NexConvo.Chat.Domain/Events/AiReplyAppendedDomainEvent.cs`
- Create: `src/services/Chat/NexConvo.Chat.Domain/Entities/Message.cs`
- Create: `src/services/Chat/NexConvo.Chat.Domain/Entities/Escalation.cs`
- Create: `src/services/Chat/NexConvo.Chat.Domain/Entities/Conversation.cs`
- Test: `tests/services/Chat/NexConvo.Chat.Domain.Tests/ConversationStateMachineTests.cs`

**Interfaces:**
- Consumes: `ChannelIdentity`, `MessageSender`, `RagConfidence`, `ConversationState`, `EscalationReason`, `MessageDirection`, `MessageDeliveryStatus` (Task 3); `BaseAggregateRoot`, `IDomainEvent` (existing `NexConvo.BuildingBlocks.Domain`).
- Produces: `Conversation.StartAiHandling(Guid tenantId, ChannelIdentity channel, Guid? contactId)`; `conversation.AppendInbound(string? providerMessageId, string body)`; `conversation.AppendAiReply(string text, RagConfidence confidence, int? tokens)`; `conversation.AppendAgentReply(Guid agentUserId, string text)`; `conversation.RequestHandoff(EscalationReason reason)` → `Escalation`; `conversation.TakeOver(Guid agentUserId)`; `conversation.Resolve()`; `conversation.Reopen()`; `conversation.Close()`; properties `Channel`, `State`, `ContactId`, `AssignedAgentUserId`, `LastInboundProviderMessageId`.

- [ ] **Step 1: Write the failing test**

First read `src/services/Chat/NexConvo.Chat.Domain/Entities/ChannelConnection.cs` in full to confirm the exact private-constructor + public-factory-method style used in this codebase (EF Core needs a parameterless/private ctor) — mirror it exactly for `Conversation`.

`tests/services/Chat/NexConvo.Chat.Domain.Tests/ConversationStateMachineTests.cs`:
```csharp
using FluentAssertions;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Chat.Domain.Exceptions;
using NexConvo.Chat.Domain.ValueObjects;
using NexConvo.Contracts.Enums;
using Xunit;

namespace NexConvo.Chat.Domain.Tests;

public class ConversationStateMachineTests
{
    private static Conversation NewConversation() =>
        Conversation.StartAiHandling(Guid.NewGuid(), new ChannelIdentity(LeadSourceChannel.Whatsapp, "ext-123"), contactId: null);

    [Fact]
    public void StartAiHandling_CreatesConversationInAiHandlingState()
    {
        var conversation = NewConversation();

        conversation.State.Should().Be(ConversationState.AiHandling);
    }

    [Fact]
    public void AppendInbound_RecordsMessageAndUpdatesLastProviderMessageId()
    {
        var conversation = NewConversation();

        var message = conversation.AppendInbound("provider-msg-1", "Hello, I need help.");

        message.Direction.Should().Be(MessageDirection.Inbound);
        message.Body.Should().Be("Hello, I need help.");
        conversation.LastInboundProviderMessageId.Should().Be("provider-msg-1");
    }

    [Fact]
    public void AppendAiReply_WhileAiHandling_Succeeds()
    {
        var conversation = NewConversation();

        var reply = conversation.AppendAiReply("Refunds take 5 business days.", RagConfidence.FromRetrievalAndAbstention(0.9, false), tokens: 42);

        reply.Direction.Should().Be(MessageDirection.Outbound);
        reply.Sender.Role.Should().Be(MessageSenderRole.Ai);
        reply.Confidence.Should().NotBeNull();
    }

    [Fact]
    public void AppendAiReply_WhilePendingHuman_ThrowsAiReplySuppressed()
    {
        var conversation = NewConversation();
        conversation.RequestHandoff(EscalationReason.LowConfidence);

        var act = () => conversation.AppendAiReply("Should not send.", RagConfidence.FromRetrievalAndAbstention(0.9, false), null);

        act.Should().Throw<AiReplySuppressedException>();
    }

    [Fact]
    public void AppendAiReply_WhileHumanHandling_ThrowsAiReplySuppressed()
    {
        var conversation = NewConversation();
        conversation.RequestHandoff(EscalationReason.LowConfidence);
        conversation.TakeOver(Guid.NewGuid());

        var act = () => conversation.AppendAiReply("Should not send.", RagConfidence.FromRetrievalAndAbstention(0.9, false), null);

        act.Should().Throw<AiReplySuppressedException>();
    }

    [Fact]
    public void RequestHandoff_FromAiHandling_TransitionsToPendingHuman()
    {
        var conversation = NewConversation();

        var escalation = conversation.RequestHandoff(EscalationReason.LowConfidence);

        conversation.State.Should().Be(ConversationState.PendingHuman);
        escalation.Reason.Should().Be(EscalationReason.LowConfidence);
    }

    [Fact]
    public void TakeOver_FromPendingHuman_TransitionsToHumanHandling()
    {
        var conversation = NewConversation();
        conversation.RequestHandoff(EscalationReason.LowConfidence);
        var agentId = Guid.NewGuid();

        conversation.TakeOver(agentId);

        conversation.State.Should().Be(ConversationState.HumanHandling);
        conversation.AssignedAgentUserId.Should().Be(agentId);
    }

    [Fact]
    public void TakeOver_FromAiHandling_ThrowsInvalidTransition()
    {
        var conversation = NewConversation();

        var act = () => conversation.TakeOver(Guid.NewGuid());

        act.Should().Throw<InvalidConversationStateTransitionException>();
    }

    [Fact]
    public void Resolve_FromClosed_ThrowsInvalidTransition()
    {
        var conversation = NewConversation();
        conversation.Close();

        var act = () => conversation.Resolve();

        act.Should().Throw<InvalidConversationStateTransitionException>();
    }

    [Fact]
    public void Reopen_FromResolved_TransitionsBackToAiHandling()
    {
        var conversation = NewConversation();
        conversation.Resolve();

        conversation.Reopen();

        conversation.State.Should().Be(ConversationState.AiHandling);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/services/Chat/NexConvo.Chat.Domain.Tests/NexConvo.Chat.Domain.Tests.csproj --filter "FullyQualifiedName~ConversationStateMachineTests"`
Expected: FAIL to compile — `Conversation`, `Message`, `Escalation` do not exist.

- [ ] **Step 3: Write minimal implementation**

`src/services/Chat/NexConvo.Chat.Domain/Events/ConversationHandoffRequestedDomainEvent.cs`:
```csharp
using NexConvo.BuildingBlocks.Domain;
using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Domain.Events;

public sealed record ConversationHandoffRequestedDomainEvent(Guid ConversationId, EscalationReason Reason) : IDomainEvent;
```

`src/services/Chat/NexConvo.Chat.Domain/Events/AiReplyAppendedDomainEvent.cs`:
```csharp
using NexConvo.BuildingBlocks.Domain;

namespace NexConvo.Chat.Domain.Events;

public sealed record AiReplyAppendedDomainEvent(Guid ConversationId, Guid MessageId, double ConfidenceScore) : IDomainEvent;
```

`src/services/Chat/NexConvo.Chat.Domain/Entities/Message.cs`:
```csharp
using NexConvo.Chat.Domain.Enums;
using NexConvo.Chat.Domain.ValueObjects;

namespace NexConvo.Chat.Domain.Entities;

public class Message
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public Guid ConversationId { get; private set; }
    public MessageSender Sender { get; private set; } = null!;
    public MessageDirection Direction { get; private set; }
    public string Body { get; private set; } = null!;
    public string? ProviderMessageId { get; private set; }
    public MessageDeliveryStatus DeliveryStatus { get; private set; }
    public double? Confidence { get; private set; }

    /// <summary>Nullable JSONB payload reserved for future ToolCall/ToolResult structured data — unused today.</summary>
    public string? StructuredPayload { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private Message() { }

    internal static Message Inbound(Guid tenantId, Guid conversationId, MessageSender sender, string? providerMessageId, string body) =>
        new()
        {
            TenantId = tenantId,
            ConversationId = conversationId,
            Sender = sender,
            Direction = MessageDirection.Inbound,
            Body = body,
            ProviderMessageId = providerMessageId,
            DeliveryStatus = MessageDeliveryStatus.Delivered,
        };

    internal static Message Outbound(Guid tenantId, Guid conversationId, MessageSender sender, string body, double? confidence) =>
        new()
        {
            TenantId = tenantId,
            ConversationId = conversationId,
            Sender = sender,
            Direction = MessageDirection.Outbound,
            Body = body,
            DeliveryStatus = MessageDeliveryStatus.Sent,
            Confidence = confidence,
        };
}
```

`src/services/Chat/NexConvo.Chat.Domain/Entities/Escalation.cs`:
```csharp
using NexConvo.Chat.Domain.Enums;

namespace NexConvo.Chat.Domain.Entities;

public class Escalation
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public Guid ConversationId { get; private set; }
    public EscalationReason Reason { get; private set; }
    public DateTimeOffset RaisedAt { get; private set; } = DateTimeOffset.UtcNow;
    public Guid? AcceptedByUserId { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    private Escalation() { }

    internal static Escalation Raise(Guid tenantId, Guid conversationId, EscalationReason reason) =>
        new() { TenantId = tenantId, ConversationId = conversationId, Reason = reason };

    public void Accept(Guid agentUserId)
    {
        AcceptedByUserId = agentUserId;
        AcceptedAt = DateTimeOffset.UtcNow;
    }

    public void Resolve() => ResolvedAt = DateTimeOffset.UtcNow;
}
```

`src/services/Chat/NexConvo.Chat.Domain/Entities/Conversation.cs`:
```csharp
using NexConvo.BuildingBlocks.Domain;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Chat.Domain.Events;
using NexConvo.Chat.Domain.Exceptions;
using NexConvo.Chat.Domain.ValueObjects;

namespace NexConvo.Chat.Domain.Entities;

public class Conversation : BaseAggregateRoot
{
    public ChannelIdentity Channel { get; private set; } = null!;
    public ConversationState State { get; private set; }
    public Guid? ContactId { get; private set; }
    public Guid? AssignedAgentUserId { get; private set; }
    public string? LastInboundProviderMessageId { get; private set; }

    private Conversation() { }

    public static Conversation StartAiHandling(Guid tenantId, ChannelIdentity channel, Guid? contactId)
    {
        var conversation = new Conversation
        {
            TenantId = tenantId,
            Channel = channel,
            ContactId = contactId,
            State = ConversationState.AiHandling,
        };
        conversation.CreatedAt = DateTimeOffset.UtcNow;
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        return conversation;
    }

    public Message AppendInbound(string? providerMessageId, string body)
    {
        LastInboundProviderMessageId = providerMessageId;
        UpdatedAt = DateTimeOffset.UtcNow;
        return Message.Inbound(TenantId, Id, MessageSender.Contact(displayRef: null), providerMessageId, body);
    }

    public Message AppendAiReply(string text, RagConfidence confidence, int? tokens)
    {
        if (State != ConversationState.AiHandling)
        {
            throw new AiReplySuppressedException(
                $"Cannot append an AI reply to conversation {Id} while its state is {State}.");
        }

        UpdatedAt = DateTimeOffset.UtcNow;
        var message = Message.Outbound(TenantId, Id, MessageSender.Ai(), text, confidence.Score);
        RaiseDomainEvent(new AiReplyAppendedDomainEvent(Id, message.Id, confidence.Score));
        return message;
    }

    public Message AppendAgentReply(Guid agentUserId, string text)
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        return Message.Outbound(TenantId, Id, MessageSender.Agent(agentUserId, "Agent"), text, confidence: null);
    }

    public Escalation RequestHandoff(EscalationReason reason)
    {
        if (State is not (ConversationState.AiHandling or ConversationState.PendingHuman))
        {
            throw new InvalidConversationStateTransitionException(
                $"Cannot request handoff for conversation {Id} while its state is {State}.");
        }

        State = ConversationState.PendingHuman;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new ConversationHandoffRequestedDomainEvent(Id, reason));
        return Escalation.Raise(TenantId, Id, reason);
    }

    public void TakeOver(Guid agentUserId)
    {
        if (State != ConversationState.PendingHuman)
        {
            throw new InvalidConversationStateTransitionException(
                $"Cannot take over conversation {Id} while its state is {State}; expected PendingHuman.");
        }

        State = ConversationState.HumanHandling;
        AssignedAgentUserId = agentUserId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Resolve()
    {
        if (State is not (ConversationState.HumanHandling or ConversationState.AiHandling))
        {
            throw new InvalidConversationStateTransitionException(
                $"Cannot resolve conversation {Id} while its state is {State}.");
        }

        State = ConversationState.Resolved;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Reopen()
    {
        if (State is not (ConversationState.Resolved or ConversationState.Closed))
        {
            throw new InvalidConversationStateTransitionException(
                $"Cannot reopen conversation {Id} while its state is {State}.");
        }

        State = ConversationState.AiHandling;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Close()
    {
        State = ConversationState.Closed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/services/Chat/NexConvo.Chat.Domain.Tests/NexConvo.Chat.Domain.Tests.csproj --filter "FullyQualifiedName~ConversationStateMachineTests"`
Expected: PASS (10/10).

- [ ] **Step 5: Commit**

```bash
git add src/services/Chat/NexConvo.Chat.Domain tests/services/Chat/NexConvo.Chat.Domain.Tests/ConversationStateMachineTests.cs
git commit -m "feat(chat): add Conversation aggregate, Message, and Escalation entities"
```

---

## Task 5: Widen `MessageReceivedIntegrationEvent`

**Files:**
- Modify: `src/shared/NexConvo.Contracts/Events/Chat/MessageReceivedIntegrationEvent.cs`

**Interfaces:**
- Produces: adds `required string Body` and `string? ProviderMessageId` to the existing record.

- [ ] **Step 1: Read the current file to confirm exact shape before editing**

Run: `cat src/shared/NexConvo.Contracts/Events/Chat/MessageReceivedIntegrationEvent.cs` (or use the Read tool) — confirm the exact `using` statements and namespace before editing.

- [ ] **Step 2: Modify the record (additive fields — Standard 19)**

```csharp
using NexConvo.Contracts.Enums;

namespace NexConvo.Contracts.Events.Chat;

public sealed record MessageReceivedIntegrationEvent : IntegrationEvent
{
    public required Guid ConversationId { get; init; }
    public required Guid TenantId { get; init; }
    public required LeadSourceChannel Channel { get; init; }
    public required string ExternalSenderId { get; init; }
    public required string MessageRef { get; init; }

    /// <summary>The inbound message text. Added in Slice 5 — no real webhook ingestion publishes
    /// this event yet, so this is populated directly by whatever seam creates the event (tests,
    /// and later a real webhook receiver).</summary>
    public required string Body { get; init; }

    public string? ProviderMessageId { get; init; }
}
```

(Keep any existing `using`/namespace lines exactly as found in Step 1 — the block above assumes the namespace is `NexConvo.Contracts.Events.Chat`; adjust only if the file you read differs.)

- [ ] **Step 3: Build to confirm no other code references the old shape**

Run: `dotnet build NexConvo.sln`
Expected: builds clean — confirmed via exploration that this event currently has zero consumers/publishers anywhere in the solution, so no call site breaks.

- [ ] **Step 4: Commit**

```bash
git add src/shared/NexConvo.Contracts/Events/Chat/MessageReceivedIntegrationEvent.cs
git commit -m "feat(contracts): widen MessageReceivedIntegrationEvent with Body and ProviderMessageId"
```

---

## Task 6: EF configurations, RLS migration, `IChatDbContext` wiring

**Files:**
- Create: `src/services/Chat/NexConvo.Chat.Infrastructure/Persistence/Configurations/ConversationConfiguration.cs`
- Create: `src/services/Chat/NexConvo.Chat.Infrastructure/Persistence/Configurations/MessageConfiguration.cs`
- Create: `src/services/Chat/NexConvo.Chat.Infrastructure/Persistence/Configurations/EscalationConfiguration.cs`
- Modify: `src/services/Chat/NexConvo.Chat.Application/Common/Interfaces/IChatDbContext.cs`
- Modify: `src/services/Chat/NexConvo.Chat.Infrastructure/Persistence/ChatDbContext.cs`
- Create: `src/services/Chat/NexConvo.Chat.Infrastructure/Migrations/{timestamp}_AddConversationSchema.cs` (timestamp generated by `dotnet ef migrations add`)
- Test: `tests/services/Chat/NexConvo.Chat.Infrastructure.Tests/ConversationSchemaTests.cs`

**Interfaces:**
- Consumes: `Conversation`, `Message`, `Escalation` (Task 4).
- Produces: `IChatDbContext.Conversations`, `.Messages`, `.Escalations` (all `DbSet<T>`).

- [ ] **Step 1: Read the existing configuration + DbContext + IChatDbContext files first**

Read `src/services/Chat/NexConvo.Chat.Infrastructure/Persistence/Configurations/ChannelConnectionConfiguration.cs` and `src/services/Chat/NexConvo.Chat.Infrastructure/Persistence/ChatDbContext.cs` in full to confirm exact `IEntityTypeConfiguration<T>` style, owned-type mapping style, and how `ApplyConfigurationsFromAssembly` is invoked in `OnModelCreating` — mirror exactly.

- [ ] **Step 2: Write the EF configurations**

`src/services/Chat/NexConvo.Chat.Infrastructure/Persistence/Configurations/ConversationConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexConvo.Chat.Domain.Entities;

namespace NexConvo.Chat.Infrastructure.Persistence.Configurations;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(c => c.State).HasColumnName("state").HasConversion<short>().IsRequired();
        builder.Property(c => c.ContactId).HasColumnName("contact_id");
        builder.Property(c => c.AssignedAgentUserId).HasColumnName("assigned_agent_user_id");
        builder.Property(c => c.LastInboundProviderMessageId).HasColumnName("last_inbound_provider_message_id");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(c => c.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();

        builder.OwnsOne(c => c.Channel, ch =>
        {
            ch.Property(x => x.Channel).HasColumnName("channel").HasConversion<short>().IsRequired();
            ch.Property(x => x.ExternalConversationId).HasColumnName("external_conversation_id").IsRequired();
        });

        builder.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();

        builder.HasIndex(c => c.TenantId).HasDatabaseName("ix_conversations_tenant_id");
    }
}
```

`src/services/Chat/NexConvo.Chat.Infrastructure/Persistence/Configurations/MessageConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexConvo.Chat.Domain.Entities;

namespace NexConvo.Chat.Infrastructure.Persistence.Configurations;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(m => m.ConversationId).HasColumnName("conversation_id").IsRequired();
        builder.Property(m => m.Direction).HasColumnName("direction").HasConversion<short>().IsRequired();
        builder.Property(m => m.Body).HasColumnName("body").IsRequired();
        builder.Property(m => m.ProviderMessageId).HasColumnName("provider_message_id");
        builder.Property(m => m.DeliveryStatus).HasColumnName("delivery_status").HasConversion<short>().IsRequired();
        builder.Property(m => m.Confidence).HasColumnName("confidence");
        builder.Property(m => m.StructuredPayload).HasColumnName("structured_payload").HasColumnType("jsonb");
        builder.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.OwnsOne(m => m.Sender, s =>
        {
            s.Property(x => x.Role).HasColumnName("sender_role").HasConversion<short>().IsRequired();
            s.Property(x => x.DisplayRef).HasColumnName("sender_display_ref");
            s.Property(x => x.UserId).HasColumnName("sender_user_id");
        });

        builder.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();

        builder.HasIndex(m => new { m.TenantId, m.ConversationId }).HasDatabaseName("ix_messages_tenant_conversation");
        builder.HasOne<Conversation>().WithMany().HasForeignKey(m => m.ConversationId);
    }
}
```

`src/services/Chat/NexConvo.Chat.Infrastructure/Persistence/Configurations/EscalationConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexConvo.Chat.Domain.Entities;

namespace NexConvo.Chat.Infrastructure.Persistence.Configurations;

public class EscalationConfiguration : IEntityTypeConfiguration<Escalation>
{
    public void Configure(EntityTypeBuilder<Escalation> builder)
    {
        builder.ToTable("escalations");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.ConversationId).HasColumnName("conversation_id").IsRequired();
        builder.Property(e => e.Reason).HasColumnName("reason").HasConversion<short>().IsRequired();
        builder.Property(e => e.RaisedAt).HasColumnName("raised_at").IsRequired();
        builder.Property(e => e.AcceptedByUserId).HasColumnName("accepted_by_user_id");
        builder.Property(e => e.AcceptedAt).HasColumnName("accepted_at");
        builder.Property(e => e.ResolvedAt).HasColumnName("resolved_at");

        builder.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();

        builder.HasIndex(e => new { e.TenantId, e.ConversationId }).HasDatabaseName("ix_escalations_tenant_conversation");
        builder.HasOne<Conversation>().WithMany().HasForeignKey(e => e.ConversationId);
    }
}
```

- [ ] **Step 3: Update `IChatDbContext` and `ChatDbContext`**

In `src/services/Chat/NexConvo.Chat.Application/Common/Interfaces/IChatDbContext.cs`, add three lines inside the interface:
```csharp
DbSet<Conversation> Conversations { get; }
DbSet<Message> Messages { get; }
DbSet<Escalation> Escalations { get; }
```
(Add `using NexConvo.Chat.Domain.Entities;` is already present since `ChannelConnection` uses it.)

In `ChatDbContext.cs`, add the matching `public DbSet<Conversation> Conversations => Set<Conversation>();` (and Messages/Escalations) properties following the exact existing pattern for `ChannelConnections`/`WorkspaceChatSettings` you read in Step 1.

- [ ] **Step 4: Generate the migration**

Run: `dotnet ef migrations add AddConversationSchema --project src/services/Chat/NexConvo.Chat.Infrastructure --startup-project src/services/Chat/NexConvo.Chat.Api --context ChatDbContext`
Expected: creates a new migration file with EF's default C# `CreateTable` calls. **Replace its `Up`/`Down` bodies** with raw SQL matching `InitialChatSchema.cs`'s exact style (confirmed: `public` schema, no prefix, snake_case, `current_setting('app.current_tenant_id', true)::uuid`), e.g.:

```csharp
migrationBuilder.Sql(@"
    CREATE TABLE IF NOT EXISTS conversations (
        id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
        tenant_id uuid NOT NULL,
        channel smallint NOT NULL,
        external_conversation_id text NOT NULL,
        state smallint NOT NULL DEFAULT 0,
        contact_id uuid,
        assigned_agent_user_id uuid,
        last_inbound_provider_message_id text,
        created_at timestamptz NOT NULL DEFAULT now(),
        updated_at timestamptz NOT NULL DEFAULT now(),
        created_by_user_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'
    );

    CREATE UNIQUE INDEX IF NOT EXISTS idx_conversations_unique_open_thread
        ON conversations (tenant_id, channel, external_conversation_id)
        WHERE state IN (0, 1, 2); -- AiHandling, PendingHuman, HumanHandling

    CREATE INDEX IF NOT EXISTS ix_conversations_tenant_id ON conversations (tenant_id);

    ALTER TABLE conversations ENABLE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS ""TenantIsolation"" ON conversations;
    CREATE POLICY ""TenantIsolation"" ON conversations
        USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid)
        WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
    GRANT SELECT, INSERT, UPDATE, DELETE ON conversations TO nexconvo_service;

    CREATE TABLE IF NOT EXISTS messages (
        id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
        tenant_id uuid NOT NULL,
        conversation_id uuid NOT NULL REFERENCES conversations(id),
        sender_role smallint NOT NULL,
        sender_display_ref text,
        sender_user_id uuid,
        direction smallint NOT NULL,
        body text NOT NULL,
        provider_message_id text,
        delivery_status smallint NOT NULL DEFAULT 0,
        confidence float8,
        structured_payload jsonb,
        created_at timestamptz NOT NULL DEFAULT now()
    );

    CREATE INDEX IF NOT EXISTS ix_messages_tenant_conversation ON messages (tenant_id, conversation_id);
    CREATE INDEX IF NOT EXISTS ix_messages_structured_payload_gin ON messages USING gin (structured_payload);

    ALTER TABLE messages ENABLE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS ""TenantIsolation"" ON messages;
    CREATE POLICY ""TenantIsolation"" ON messages
        USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid)
        WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
    GRANT SELECT, INSERT, UPDATE, DELETE ON messages TO nexconvo_service;

    CREATE TABLE IF NOT EXISTS escalations (
        id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
        tenant_id uuid NOT NULL,
        conversation_id uuid NOT NULL REFERENCES conversations(id),
        reason smallint NOT NULL,
        raised_at timestamptz NOT NULL DEFAULT now(),
        accepted_by_user_id uuid,
        accepted_at timestamptz,
        resolved_at timestamptz
    );

    CREATE INDEX IF NOT EXISTS ix_escalations_tenant_conversation ON escalations (tenant_id, conversation_id);

    ALTER TABLE escalations ENABLE ROW LEVEL SECURITY;
    DROP POLICY IF EXISTS ""TenantIsolation"" ON escalations;
    CREATE POLICY ""TenantIsolation"" ON escalations
        USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid)
        WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
    GRANT SELECT, INSERT, UPDATE, DELETE ON escalations TO nexconvo_service;
");
```
`Down` should `DROP TABLE IF EXISTS messages, escalations, conversations CASCADE;` (children first).

- [ ] **Step 5: Write the RLS schema test**

First check whether `tests/services/Chat/NexConvo.Chat.Infrastructure.Tests/` already has a Testcontainers Postgres fixture (search for a `*Fixture.cs` file). If none exists, create one mirroring `tests/services/Knowledge/NexConvo.Knowledge.IntegrationTests/KnowledgeApiFactory.cs`'s Postgres container + `nexconvo_service` role setup (read that file first), scoped down to just a raw `NpgsqlConnection`-based fixture (no `WebApplicationFactory` needed at this layer) named `ChatPostgresFixture.cs` implementing `IAsyncLifetime`.

`tests/services/Chat/NexConvo.Chat.Infrastructure.Tests/ConversationSchemaTests.cs`:
```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Chat.Domain.ValueObjects;
using NexConvo.Chat.Infrastructure.Persistence;
using NexConvo.Contracts.Enums;
using Xunit;

namespace NexConvo.Chat.Infrastructure.Tests;

[Collection("ChatPostgres")]
public class ConversationSchemaTests(ChatPostgresFixture fixture)
{
    [Fact]
    public async Task RlsPolicy_BlocksCrossTenantConversationReads()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using (var seedContext = fixture.CreateDbContext(tenantA))
        {
            var conversation = Conversation.StartAiHandling(tenantA, new ChannelIdentity(LeadSourceChannel.Whatsapp, "ext-1"), null);
            seedContext.Conversations.Add(conversation);
            await seedContext.SaveChangesAsync();
        }

        await using var tenantBContext = fixture.CreateDbContext(tenantB);
        var visibleToTenantB = await tenantBContext.Conversations.ToListAsync();

        visibleToTenantB.Should().BeEmpty();
    }
}
```

(Exact `ChatPostgresFixture.CreateDbContext(Guid tenantId)` shape — a method that opens a connection, runs `SET app.current_tenant_id = '{tenantId}'`, and returns a `ChatDbContext` bound to that connection — should mirror however `KnowledgeApiFactory`/`KnowledgePostgresFixture` sets ambient tenant for its own RLS tests; copy that mechanism exactly rather than inventing a new one.)

- [ ] **Step 6: Run migration + test against a local/dev Postgres**

Run: `dotnet test tests/services/Chat/NexConvo.Chat.Infrastructure.Tests/NexConvo.Chat.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ConversationSchemaTests"`
Expected: PASS — requires Docker running for Testcontainers.

- [ ] **Step 7: Commit**

```bash
git add src/services/Chat/NexConvo.Chat.Infrastructure src/services/Chat/NexConvo.Chat.Application/Common/Interfaces/IChatDbContext.cs tests/services/Chat/NexConvo.Chat.Infrastructure.Tests
git commit -m "feat(chat): add Conversation/Message/Escalation persistence with RLS"
```

---

## Task 7: Knowledge gRPC client in Chat

**Files:**
- Create: `src/services/Chat/NexConvo.Chat.Infrastructure/Protos/knowledge.proto`
- Create: `src/services/Chat/NexConvo.Chat.Application/Common/Interfaces/IKnowledgeRetrievalClient.cs`
- Create: `src/services/Chat/NexConvo.Chat.Infrastructure/ExternalServices/KnowledgeRetrievalClient.cs`
- Modify: `src/services/Chat/NexConvo.Chat.Infrastructure/NexConvo.Chat.Infrastructure.csproj`
- Modify: `src/services/Chat/NexConvo.Chat.Infrastructure/DependencyInjection.cs`
- Modify: `Directory.Packages.props`
- Modify: `src/services/Chat/NexConvo.Chat.Api/appsettings.json`, `appsettings.Development.json`
- Test: `tests/services/Chat/NexConvo.Chat.Infrastructure.Tests/KnowledgeRetrievalClientTests.cs`

**Interfaces:**
- Produces: `IKnowledgeRetrievalClient.SearchAsync(string query, int topK, double minScore, CancellationToken ct) -> Task<IReadOnlyList<KnowledgeChunkMatch>>`; `KnowledgeChunkMatch(string ChunkId, string DocumentId, string Content, double Score)`.

- [ ] **Step 1: Copy the proto file**

`src/services/Chat/NexConvo.Chat.Infrastructure/Protos/knowledge.proto`:
```protobuf
// Copied from src/services/Knowledge/NexConvo.Knowledge.Api/Protos/knowledge.proto.
// Chat.Infrastructure.csproj compiles this with GrpcServices="Client" only — Chat does not
// project-reference NexConvo.Knowledge.Api (services stay autonomous; see ARCHITECTURE.md §3).
// KEEP IN SYNC MANUALLY. Follow-up: add a Pact/contract test to catch drift (Standard 19).
syntax = "proto3";
option csharp_namespace = "NexConvo.Knowledge.Api.Grpc";
package knowledge;

service KnowledgeRetrieval {
  rpc Search (SearchRequest) returns (SearchReply);
}
message SearchRequest {
  string query = 1;
  int32 top_k = 2;
  double min_score = 3;
}
message SearchReply {
  repeated Chunk chunks = 1;
}
message Chunk {
  string chunk_id = 1;
  string document_id = 2;
  string content = 3;
  double score = 4;
}
```

- [ ] **Step 2: Add package references**

Run: `grep -n "Grpc.Net.Client\|Grpc.AspNetCore\|Google.Protobuf\|MassTransit\b" Directory.Packages.props` to see current pinned versions.

Add to `Directory.Packages.props` (in the appropriate alphabetical spot, matching the `Grpc.Net.Client` version's major.minor):
```xml
    <PackageVersion Include="Grpc.Net.ClientFactory" Version="{same version as Grpc.Net.Client}" />
```

Add to `src/services/Chat/NexConvo.Chat.Infrastructure/NexConvo.Chat.Infrastructure.csproj`:
```xml
  <ItemGroup>
    <PackageReference Include="Grpc.Net.Client" />
    <PackageReference Include="Grpc.Net.ClientFactory" />
    <PackageReference Include="Google.Protobuf" />
    <PackageReference Include="Grpc.Tools" PrivateAssets="All" />
  </ItemGroup>

  <ItemGroup>
    <Protobuf Include="Protos\knowledge.proto" GrpcServices="Client" />
  </ItemGroup>
```
(Check whether `Grpc.Tools` is already referenced somewhere else in the solution to confirm its exact `PackageVersion` pin exists in `Directory.Packages.props`; add if missing, matching Knowledge's own pin.)

- [ ] **Step 3: Write the Application-layer interface**

`src/services/Chat/NexConvo.Chat.Application/Common/Interfaces/IKnowledgeRetrievalClient.cs`:
```csharp
namespace NexConvo.Chat.Application.Common.Interfaces;

public interface IKnowledgeRetrievalClient
{
    Task<IReadOnlyList<KnowledgeChunkMatch>> SearchAsync(
        string query, int topK, double minScore, CancellationToken cancellationToken);
}

public sealed record KnowledgeChunkMatch(string ChunkId, string DocumentId, string Content, double Score);
```

- [ ] **Step 4: Write the failing test**

First read one existing Chat test file under `tests/services/Chat/NexConvo.Chat.Infrastructure.Tests/` to confirm NSubstitute is the mocking library in use (already confirmed by exploration) before writing:

`tests/services/Chat/NexConvo.Chat.Infrastructure.Tests/KnowledgeRetrievalClientTests.cs`:
```csharp
using FluentAssertions;
using Grpc.Core;
using NexConvo.BuildingBlocks.Application.Multitenancy;
using NexConvo.Chat.Infrastructure.ExternalServices;
using NexConvo.Knowledge.Api.Grpc;
using NSubstitute;
using Xunit;

namespace NexConvo.Chat.Infrastructure.Tests;

public class KnowledgeRetrievalClientTests
{
    [Fact]
    public async Task SearchAsync_AttachesTenantAndInternalApiKeyHeaders()
    {
        var grpcClient = Substitute.For<KnowledgeRetrieval.KnowledgeRetrievalClient>();
        var tenantId = Guid.NewGuid();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);

        var reply = new SearchReply();
        reply.Chunks.Add(new Chunk { ChunkId = "c1", DocumentId = "d1", Content = "Refund policy text.", Score = 0.9 });

        var call = TestCalls.AsyncUnaryCall(
            Task.FromResult(reply), Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => [], () => { });

        Metadata? capturedHeaders = null;
        grpcClient
            .SearchAsync(Arg.Any<SearchRequest>(), Arg.Do<Metadata>(m => capturedHeaders = m), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(call);

        var sut = new KnowledgeRetrievalClient(grpcClient, tenantContext, internalApiKey: "test-key");

        var results = await sut.SearchAsync("refund policy", topK: 5, minScore: 0.5, CancellationToken.None);

        results.Should().ContainSingle(r => r.ChunkId == "c1" && r.Content == "Refund policy text." && Math.Abs(r.Score - 0.9) < 0.0001);
        capturedHeaders.Should().NotBeNull();
        capturedHeaders!.Get("x-tenant-id")!.Value.Should().Be(tenantId.ToString());
        capturedHeaders.Get("x-internal-api-key")!.Value.Should().Be("test-key");
    }
}
```
(`TestCalls.AsyncUnaryCall` is a small helper you'll need to add — Grpc.Core.Testing ships `TestCalls` for exactly this; add `<PackageReference Include="Grpc.Core.Testing" />` — check `Directory.Packages.props` for its pin, add if missing — as a test-only dependency in `NexConvo.Chat.Infrastructure.Tests.csproj`.)

- [ ] **Step 5: Run test to verify it fails**

Run: `dotnet test tests/services/Chat/NexConvo.Chat.Infrastructure.Tests/NexConvo.Chat.Infrastructure.Tests.csproj --filter "FullyQualifiedName~KnowledgeRetrievalClientTests"`
Expected: FAIL to compile — `KnowledgeRetrievalClient` does not exist.

- [ ] **Step 6: Write minimal implementation**

`src/services/Chat/NexConvo.Chat.Infrastructure/ExternalServices/KnowledgeRetrievalClient.cs`:
```csharp
using System.Diagnostics;
using Grpc.Core;
using NexConvo.BuildingBlocks.Application.Multitenancy;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Knowledge.Api.Grpc;

namespace NexConvo.Chat.Infrastructure.ExternalServices;

public sealed class KnowledgeRetrievalClient(
    KnowledgeRetrieval.KnowledgeRetrievalClient grpcClient,
    ITenantContext tenantContext,
    string internalApiKey) : IKnowledgeRetrievalClient
{
    public async Task<IReadOnlyList<KnowledgeChunkMatch>> SearchAsync(
        string query, int topK, double minScore, CancellationToken cancellationToken)
    {
        var headers = new Metadata
        {
            { "x-internal-api-key", internalApiKey },
            { "x-tenant-id", tenantContext.TenantId.ToString() },
        };

        if (Activity.Current?.Id is { } traceparent)
        {
            headers.Add("traceparent", traceparent);
        }

        var request = new SearchRequest { Query = query, TopK = topK, MinScore = minScore };
        var reply = await grpcClient.SearchAsync(request, headers, cancellationToken: cancellationToken);

        return reply.Chunks
            .Select(c => new KnowledgeChunkMatch(c.ChunkId, c.DocumentId, c.Content, c.Score))
            .ToList();
    }
}
```

Register in `AddChatInfrastructure` (`src/services/Chat/NexConvo.Chat.Infrastructure/DependencyInjection.cs`) — add after the existing `AddHttpClient` calls:
```csharp
        var knowledgeGrpcAddress = configuration["Knowledge:GrpcAddress"]
            ?? throw new InvalidOperationException("Configuration 'Knowledge:GrpcAddress' is not set.");
        var internalApiKey = configuration["Internal:ApiKey"]
            ?? throw new InvalidOperationException("Configuration 'Internal:ApiKey' is not set.");

        services.AddGrpcClient<NexConvo.Knowledge.Api.Grpc.KnowledgeRetrieval.KnowledgeRetrievalClient>(o =>
            o.Address = new Uri(knowledgeGrpcAddress))
            .AddNexConvoResilience();

        services.AddScoped<IKnowledgeRetrievalClient>(sp =>
            new KnowledgeRetrievalClient(
                sp.GetRequiredService<NexConvo.Knowledge.Api.Grpc.KnowledgeRetrieval.KnowledgeRetrievalClient>(),
                sp.GetRequiredService<NexConvo.BuildingBlocks.Application.Multitenancy.ITenantContext>(),
                internalApiKey));
```

Add config keys to `src/services/Chat/NexConvo.Chat.Api/appsettings.json` and `appsettings.Development.json`:
```json
  "Knowledge": {
    "GrpcAddress": "http://knowledge-api:8080"
  }
```
(Confirm `Internal:ApiKey` is already present in Chat's `appsettings.Development.json` — per repo memory it was added platform-wide alongside the notification service; if absent, add `"Internal": { "ApiKey": "dev-internal-key" }` matching whatever value Knowledge's own config uses in dev.)

- [ ] **Step 7: Run test to verify it passes**

Run: `dotnet test tests/services/Chat/NexConvo.Chat.Infrastructure.Tests/NexConvo.Chat.Infrastructure.Tests.csproj --filter "FullyQualifiedName~KnowledgeRetrievalClientTests"`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/services/Chat/NexConvo.Chat.Infrastructure src/services/Chat/NexConvo.Chat.Application/Common/Interfaces/IKnowledgeRetrievalClient.cs src/services/Chat/NexConvo.Chat.Api/appsettings.json src/services/Chat/NexConvo.Chat.Api/appsettings.Development.json Directory.Packages.props tests/services/Chat/NexConvo.Chat.Infrastructure.Tests
git commit -m "feat(chat): add Knowledge gRPC retrieval client"
```

---

## Task 8: MassTransit EF outbox/inbox wiring

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `src/services/Chat/NexConvo.Chat.Infrastructure/NexConvo.Chat.Infrastructure.csproj`
- Modify: `src/services/Chat/NexConvo.Chat.Infrastructure/Persistence/ChatDbContext.cs`
- Modify: `src/services/Chat/NexConvo.Chat.Infrastructure/DependencyInjection.cs`
- Create: `src/services/Chat/NexConvo.Chat.Infrastructure/Migrations/{timestamp}_AddOutboxInboxTables.cs`

**Interfaces:**
- Produces: `AddEntityFrameworkOutbox<ChatDbContext>()` registered; `IPublishEndpoint.Publish(...)` calls made inside a `ChatDbContext`-scoped unit of work become transactional with `SaveChangesAsync`.

- [ ] **Step 1: Pin the package**

Add to `Directory.Packages.props` (alongside the existing `MassTransit`/`MassTransit.RabbitMQ` entries, same version — confirmed 8.3.4):
```xml
    <PackageVersion Include="MassTransit.EntityFrameworkCore" Version="8.3.4" />
```

Add to `src/services/Chat/NexConvo.Chat.Infrastructure/NexConvo.Chat.Infrastructure.csproj`:
```xml
    <PackageReference Include="MassTransit.EntityFrameworkCore" />
```

- [ ] **Step 2: Add outbox/inbox model entities to `ChatDbContext.OnModelCreating`**

In `ChatDbContext.cs`, inside `OnModelCreating` (after the existing `ApplyConfigurationsFromAssembly` call), add:
```csharp
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
```
(Requires `using MassTransit.EntityFrameworkCoreIntegration;` at the top of the file.)

- [ ] **Step 3: Register the outbox in `AddChatInfrastructure`**

Modify the existing `services.AddMassTransit(x => { ... })` block in `DependencyInjection.cs` to add the outbox registration as the first line inside the `x =>` lambda:
```csharp
        services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<ChatDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
            });

            x.AddConsumers(typeof(NexConvo.Chat.Application.DependencyInjection).Assembly);
            x.UsingRabbitMq((context, cfg) =>
            {
                var rmq = configuration.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672";
                cfg.Host(rmq);
                cfg.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter("chat", false));
            });
        });
```

- [ ] **Step 4: Generate the outbox migration**

Run: `dotnet ef migrations add AddOutboxInboxTables --project src/services/Chat/NexConvo.Chat.Infrastructure --startup-project src/services/Chat/NexConvo.Chat.Api --context ChatDbContext`
Expected: generates `InboxState`/`OutboxMessage`/`OutboxState` table creation via EF's normal (non-raw-SQL) migration builder calls — **leave this migration in its generated form** (do not hand-convert to raw SQL like the domain-table migrations); add a leading comment explaining why:
```csharp
// Unlike Chat's other migrations, this one is left in EF's generated (non-raw-SQL) form —
// it defines MassTransit's own InboxState/OutboxMessage/OutboxState schema via
// AddInboxStateEntity()/AddOutboxMessageEntity()/AddOutboxStateEntity(), which is MassTransit's
// own EF tooling contract. These tables are infrastructure-internal (not tenant business data)
// and intentionally have NO RLS policy — tenant isolation is enforced by the business tables
// the outbox messages reference, not by these transport-plumbing tables themselves.
```

- [ ] **Step 5: Verify the solution still builds and existing tests still pass**

Run: `dotnet build NexConvo.sln`
Expected: builds clean.

Run: `dotnet test tests/services/Chat/NexConvo.Chat.Application.UnitTests/NexConvo.Chat.Application.UnitTests.csproj`
Expected: all existing tests still PASS (no regression from outbox wiring).

- [ ] **Step 6: Commit**

```bash
git add Directory.Packages.props src/services/Chat/NexConvo.Chat.Infrastructure
git commit -m "feat(chat): wire MassTransit EF outbox and inbox for ChatDbContext"
```

---

## Task 9: `MessageReceivedConsumer` (idempotent)

**Files:**
- Create: `src/services/Chat/NexConvo.Chat.Application/Features/Rag/EventHandlers/MessageReceivedConsumer.cs`
- Test: `tests/services/Chat/NexConvo.Chat.Application.UnitTests/Features/Rag/MessageReceivedConsumerTests.cs`
- Test: `tests/services/Chat/NexConvo.Chat.IntegrationTests/MessageReceivedConsumerIdempotencyTests.cs` (project created in Task 12 — this file can be added once that project exists; if Task 12 hasn't run yet, stub this file's creation as a TODO cross-reference and complete it in Task 12's steps instead)

**Interfaces:**
- Consumes: `Conversation.StartAiHandling`, `conversation.AppendInbound` (Task 4); `IChatDbContext` (Task 6); `MessageReceivedIntegrationEvent` (Task 5); `GenerateRagReplyCommand` (Task 10 — forward reference, defined as a simple `record` here and reused there).
- Produces: `MessageReceivedConsumer : IConsumer<MessageReceivedIntegrationEvent>`.

- [ ] **Step 1: Read the existing consumer example**

Read `src/services/Chat/NexConvo.Chat.Application/Features/AiConfig/EventHandlers/AiConfigUpdatedEventConsumer.cs` in full to confirm exact `IConsumer<T>` + constructor-injection + `context.Message` access style — mirror exactly.

- [ ] **Step 2: Write the failing test**

First read one existing MockQueryable-based Application unit test (e.g. under `tests/services/Chat/NexConvo.Chat.Application.UnitTests/Features/ChatSettings/`) to confirm the exact in-memory `IChatDbContext` faking convention (MockQueryable.NSubstitute, per earlier exploration) before writing.

`tests/services/Chat/NexConvo.Chat.Application.UnitTests/Features/Rag/MessageReceivedConsumerTests.cs`:
```csharp
using FluentAssertions;
using MassTransit;
using MediatR;
using MockQueryable.NSubstitute;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Application.Features.Rag.Commands;
using NexConvo.Chat.Application.Features.Rag.EventHandlers;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Contracts.Enums;
using NexConvo.Contracts.Events.Chat;
using NSubstitute;
using Xunit;

namespace NexConvo.Chat.Application.UnitTests.Features.Rag;

public class MessageReceivedConsumerTests
{
    [Fact]
    public async Task Consume_NewConversation_CreatesConversationAndDispatchesRagReplyCommand()
    {
        var conversations = new List<Conversation>();
        var db = Substitute.For<IChatDbContext>();
        db.Conversations.Returns(_ => conversations.AsQueryable().BuildMockDbSet());
        db.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var sender = Substitute.For<ISender>();
        var consumer = new MessageReceivedConsumer(db, sender, NSubstitute.Substitute.For<Microsoft.Extensions.Logging.ILogger<MessageReceivedConsumer>>());

        var tenantId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var message = new MessageReceivedIntegrationEvent
        {
            ConversationId = conversationId,
            TenantId = tenantId,
            Channel = LeadSourceChannel.Whatsapp,
            ExternalSenderId = "sender-1",
            MessageRef = "ref-1",
            Body = "How long do refunds take?",
            ProviderMessageId = "provider-1",
        };
        var context = Substitute.For<ConsumeContext<MessageReceivedIntegrationEvent>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);

        await consumer.Consume(context);

        await db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await sender.Received(1).Send(Arg.Is<GenerateRagReplyCommand>(c => c.ConversationId != Guid.Empty), Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/services/Chat/NexConvo.Chat.Application.UnitTests/NexConvo.Chat.Application.UnitTests.csproj --filter "FullyQualifiedName~MessageReceivedConsumerTests"`
Expected: FAIL to compile — `MessageReceivedConsumer`, `GenerateRagReplyCommand` do not exist yet.

- [ ] **Step 4: Write minimal implementation**

First create the minimal command shape needed (full command/handler built out in Task 10 — this task only needs the record to exist so the consumer compiles):

`src/services/Chat/NexConvo.Chat.Application/Features/Rag/Commands/GenerateRagReplyCommand.cs`:
```csharp
using MediatR;
using NexConvo.BuildingBlocks.Results;

namespace NexConvo.Chat.Application.Features.Rag.Commands;

public sealed record GenerateRagReplyCommand(Guid ConversationId) : IRequest<Result>;
```

`src/services/Chat/NexConvo.Chat.Application/Features/Rag/EventHandlers/MessageReceivedConsumer.cs`:
```csharp
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Application.Features.Rag.Commands;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Chat.Domain.ValueObjects;
using NexConvo.Contracts.Events.Chat;

namespace NexConvo.Chat.Application.Features.Rag.EventHandlers;

public sealed class MessageReceivedConsumer(
    IChatDbContext db,
    ISender sender,
    ILogger<MessageReceivedConsumer> logger) : IConsumer<MessageReceivedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<MessageReceivedIntegrationEvent> context)
    {
        var message = context.Message;

        var channelIdentity = new ChannelIdentity(message.Channel, message.ExternalSenderId);
        var conversation = await db.Conversations
            .FirstOrDefaultAsync(
                c => c.TenantId == message.TenantId,
                context.CancellationToken);

        conversation ??= Conversation.StartAiHandling(message.TenantId, channelIdentity, contactId: null);

        if (db.Conversations.Local.All(c => c.Id != conversation.Id) &&
            !await db.Conversations.AnyAsync(c => c.Id == conversation.Id, context.CancellationToken))
        {
            db.Conversations.Add(conversation);
        }

        conversation.AppendInbound(message.ProviderMessageId, message.Body);

        await db.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation(
            "Inbound message processed for conversation {ConversationId}, dispatching RAG reply",
            conversation.Id);

        await sender.Send(new GenerateRagReplyCommand(conversation.Id), context.CancellationToken);
    }
}
```
(Note: the conversation resolve-or-create-by-channel-identity lookup above is simplified for this task's scope — Task 9's real implementation should query by the owned-type `Channel` fields, e.g. `c.Channel.ExternalConversationId == message.ExternalSenderId`; adjust the `FirstOrDefaultAsync` predicate accordingly once `ConversationConfiguration`'s owned-type column mapping from Task 6 is in place, and add a focused test for the "existing conversation" branch alongside the "new conversation" test above.)

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/services/Chat/NexConvo.Chat.Application.UnitTests/NexConvo.Chat.Application.UnitTests.csproj --filter "FullyQualifiedName~MessageReceivedConsumerTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/services/Chat/NexConvo.Chat.Application/Features/Rag
git commit -m "feat(chat): add idempotent MessageReceivedConsumer dispatching GenerateRagReplyCommand"
```

---

## Task 10: `ReplyOutcome`, `IReplyOrchestrator`, `GenerateRagReplyCommand` handler

**Files:**
- Create: `src/services/Chat/NexConvo.Chat.Application/Rag/ReplyOutcome.cs`
- Create: `src/services/Chat/NexConvo.Chat.Application/Rag/IReplyOrchestrator.cs`
- Create: `src/services/Chat/NexConvo.Chat.Application/Rag/ReplyOrchestrator.cs`
- Create: `src/services/Chat/NexConvo.Chat.Application/Features/Rag/Commands/GenerateRagReplyCommandHandler.cs`
- Create: `src/services/Chat/NexConvo.Chat.Application/Features/Rag/Commands/GenerateRagReplyCommandValidator.cs`
- Modify: `src/services/Chat/NexConvo.Chat.Infrastructure/DependencyInjection.cs`
- Modify: `src/services/Chat/NexConvo.Chat.Api/Program.cs` (add `AddAiProviders()`)
- Test: `tests/services/Chat/NexConvo.Chat.Application.UnitTests/Rag/ReplyOrchestratorTests.cs`

**Interfaces:**
- Consumes: `IChatDbContext`, `IKnowledgeRetrievalClient` (Task 7), `IAiProviderFactory`/`IAiProviderService` (existing `NexConvo.BuildingBlocks.Ai`), `IGroundedPromptAssembler`/`ITokenBudgeter` (Task 1-2), `IAesEncryptionService` (existing), `WorkspaceChatSettings` (existing), `Conversation` (Task 4), `GenerateRagReplyCommand` (Task 9).
- Produces: `ReplyOutcome` (abstract) / `AnsweredOutcome` / `HandoffOutcome`; `IReplyOrchestrator.RunAsync(Guid conversationId, CancellationToken) -> Task<ReplyOutcome>`.

- [ ] **Step 1: Read the existing handler + publish precedent**

Read `src/services/Chat/NexConvo.Chat.Application/Features/ChannelConnections/Commands/SaveChannelConnectionCommandHandler.cs` in full to confirm the exact primary-constructor injection style, `Result` usage, and `IPublishEndpoint.Publish(...)` call shape post-`SaveChangesAsync` — this task's orchestrator must mirror that publish shape exactly for the handoff event (Task 11).

Read `src/services/Chat/NexConvo.Chat.Application/Features/AiConfig/EventHandlers/AiConfigUpdatedEventConsumer.cs` again to confirm the exact Redis cache-key format (`AiConfig:{TenantId}`) and JSON deserialization shape used for `AiConfigUpdatedEvent`.

- [ ] **Step 2: Write `ReplyOutcome`**

`src/services/Chat/NexConvo.Chat.Application/Rag/ReplyOutcome.cs`:
```csharp
using NexConvo.Chat.Domain.Enums;
using NexConvo.Chat.Domain.ValueObjects;

namespace NexConvo.Chat.Application.Rag;

public abstract record ReplyOutcome;

public sealed record AnsweredOutcome(Guid MessageId, string Text, RagConfidence Confidence) : ReplyOutcome;

public sealed record HandoffOutcome(Guid EscalationId, EscalationReason Reason) : ReplyOutcome;
```

- [ ] **Step 3: Write the failing orchestrator tests**

First read one existing Application unit test using NSubstitute + `IAsyncEnumerable` faking (search for any existing test mocking `IAiProviderService`; if none exists, use the pattern below directly — it is self-contained) and one MockQueryable-based test for `IChatDbContext` faking, matching Task 9's test conventions.

`tests/services/Chat/NexConvo.Chat.Application.UnitTests/Rag/ReplyOrchestratorTests.cs`:
```csharp
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NexConvo.BuildingBlocks.Ai.Models;
using NexConvo.BuildingBlocks.Ai.Services;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Rag;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Application.Rag;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Chat.Domain.ValueObjects;
using NexConvo.Contracts.Enums;
using NSubstitute;
using System.Text;
using System.Text.Json;
using Xunit;

namespace NexConvo.Chat.Application.UnitTests.Rag;

public class ReplyOrchestratorTests
{
    private static async IAsyncEnumerable<AiStreamChunk> StreamOf(params string[] chunks)
    {
        foreach (var c in chunks)
        {
            yield return new AiStreamChunk { Content = c };
            await Task.Yield();
        }
    }

    private static (ReplyOrchestrator Sut, IChatDbContext Db, IKnowledgeRetrievalClient Knowledge, IAiProviderFactory AiFactory, IDistributedCache Cache)
        BuildSut(Conversation conversation, WorkspaceChatSettings settings, string cachedAiConfigJson)
    {
        var conversations = new List<Conversation> { conversation }.AsQueryable().BuildMockDbSet();
        var settingsSet = new List<WorkspaceChatSettings> { settings }.AsQueryable().BuildMockDbSet();
        var messages = new List<Message>().AsQueryable().BuildMockDbSet();
        var escalations = new List<Escalation>().AsQueryable().BuildMockDbSet();

        var db = Substitute.For<IChatDbContext>();
        db.Conversations.Returns(conversations);
        db.WorkspaceChatSettings.Returns(settingsSet);
        db.Messages.Returns(messages);
        db.Escalations.Returns(escalations);
        db.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var knowledge = Substitute.For<IKnowledgeRetrievalClient>();
        knowledge.SearchAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns([new KnowledgeChunkMatch("chunk-1", "doc-1", "Refunds are processed within 5 business days.", 0.9)]);

        var aiProvider = Substitute.For<IAiProviderService>();
        aiProvider.GenerateStreamAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(StreamOf("Refunds take 5 business days. [1]"));

        var aiFactory = Substitute.For<IAiProviderFactory>();
        aiFactory.GetProvider(Arg.Any<AiProviderType>()).Returns(aiProvider);

        var cache = Substitute.For<IDistributedCache>();
        cache.GetAsync(Arg.Is<string>(k => k == $"AiConfig:{conversation.TenantId}"), Arg.Any<CancellationToken>())
            .Returns(Encoding.UTF8.GetBytes(cachedAiConfigJson));

        var aes = Substitute.For<IAesEncryptionService>();
        aes.Decrypt(Arg.Any<string>()).Returns("decrypted-api-key");

        var sut = new ReplyOrchestrator(
            db, knowledge, aiFactory, cache, aes,
            new GroundedPromptAssembler(), new TokenBudgeter(),
            Substitute.For<ILogger<ReplyOrchestrator>>());

        return (sut, db, knowledge, aiFactory, cache);
    }

    private static string CachedAiConfigJson(Guid tenantId) => JsonSerializer.Serialize(new
    {
        TenantId = tenantId,
        Provider = "OpenAI",
        EncryptedApiKey = "encrypted-blob",
        BaseUrl = (string?)null,
        DefaultModel = "gpt-4o-mini",
        SystemPrompt = (string?)null,
        Parameters = (string?)null,
        IsActive = true,
    });

    [Fact]
    public async Task RunAsync_GroundedQuestion_ReturnsAnsweredOutcomeWithCitation()
    {
        var tenantId = Guid.NewGuid();
        var conversation = Conversation.StartAiHandling(tenantId, new ChannelIdentity(LeadSourceChannel.Whatsapp, "ext-1"), null);
        conversation.AppendInbound("provider-1", "How long do refunds take?");
        var settings = new WorkspaceChatSettings(tenantId, AiProviderType.OpenAI, "gpt-4o-mini", "[]", null, 0.5, false, Chat.Domain.Enums.SentimentSensitivity.Medium, "[]", 3, Chat.Domain.Enums.PiiMaskingLevel.None, null);

        var (sut, _, _, _, _) = BuildSut(conversation, settings, CachedAiConfigJson(tenantId));

        var outcome = await sut.RunAsync(conversation.Id, CancellationToken.None);

        outcome.Should().BeOfType<AnsweredOutcome>();
        ((AnsweredOutcome)outcome).Text.Should().Contain("[1]");
    }

    [Fact]
    public async Task RunAsync_ModelAbstains_ReturnsHandoffOutcomeLowConfidence()
    {
        var tenantId = Guid.NewGuid();
        var conversation = Conversation.StartAiHandling(tenantId, new ChannelIdentity(LeadSourceChannel.Whatsapp, "ext-1"), null);
        conversation.AppendInbound("provider-1", "What is your CEO's home address?");
        var settings = new WorkspaceChatSettings(tenantId, AiProviderType.OpenAI, "gpt-4o-mini", "[]", null, 0.5, false, Chat.Domain.Enums.SentimentSensitivity.Medium, "[]", 3, Chat.Domain.Enums.PiiMaskingLevel.None, null);

        var (sut, _, _, aiFactory, _) = BuildSut(conversation, settings, CachedAiConfigJson(tenantId));
        var provider = aiFactory.GetProvider(Arg.Any<AiProviderType>());
        provider.GenerateStreamAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(StreamOf("[[NO_ANSWER]]"));

        var outcome = await sut.RunAsync(conversation.Id, CancellationToken.None);

        outcome.Should().BeOfType<HandoffOutcome>();
        ((HandoffOutcome)outcome).Reason.Should().Be(Chat.Domain.Enums.EscalationReason.LowConfidence);
    }

    [Fact]
    public async Task RunAsync_TriggerPhraseMatched_HandsOffWithoutCallingAiProvider()
    {
        var tenantId = Guid.NewGuid();
        var conversation = Conversation.StartAiHandling(tenantId, new ChannelIdentity(LeadSourceChannel.Whatsapp, "ext-1"), null);
        conversation.AppendInbound("provider-1", "I want to speak to a manager");
        var settings = new WorkspaceChatSettings(tenantId, AiProviderType.OpenAI, "gpt-4o-mini", "[]", null, 0.5, false, Chat.Domain.Enums.SentimentSensitivity.Medium, "[\"speak to a manager\"]", 3, Chat.Domain.Enums.PiiMaskingLevel.None, null);

        var (sut, _, knowledge, aiFactory, _) = BuildSut(conversation, settings, CachedAiConfigJson(tenantId));

        var outcome = await sut.RunAsync(conversation.Id, CancellationToken.None);

        outcome.Should().BeOfType<HandoffOutcome>();
        ((HandoffOutcome)outcome).Reason.Should().Be(Chat.Domain.Enums.EscalationReason.TriggerPhrase);
        await knowledge.DidNotReceive().SearchAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>());
        aiFactory.DidNotReceive().GetProvider(Arg.Any<AiProviderType>());
    }

    [Fact]
    public async Task RunAsync_MissingAiConfig_ReturnsHandoffOutcomeNoAiConfig()
    {
        var tenantId = Guid.NewGuid();
        var conversation = Conversation.StartAiHandling(tenantId, new ChannelIdentity(LeadSourceChannel.Whatsapp, "ext-1"), null);
        conversation.AppendInbound("provider-1", "How long do refunds take?");
        var settings = new WorkspaceChatSettings(tenantId, AiProviderType.OpenAI, "gpt-4o-mini", "[]", null, 0.5, false, Chat.Domain.Enums.SentimentSensitivity.Medium, "[]", 3, Chat.Domain.Enums.PiiMaskingLevel.None, null);

        var (sut, _, _, _, cache) = BuildSut(conversation, settings, CachedAiConfigJson(tenantId));
        cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);

        var outcome = await sut.RunAsync(conversation.Id, CancellationToken.None);

        outcome.Should().BeOfType<HandoffOutcome>();
        ((HandoffOutcome)outcome).Reason.Should().Be(Chat.Domain.Enums.EscalationReason.NoAiConfig);
    }

    [Fact]
    public async Task RunAsync_CancellationRequestedMidStream_PropagatesAndDoesNotPersistPartialReply()
    {
        var tenantId = Guid.NewGuid();
        var conversation = Conversation.StartAiHandling(tenantId, new ChannelIdentity(LeadSourceChannel.Whatsapp, "ext-1"), null);
        conversation.AppendInbound("provider-1", "How long do refunds take?");
        var settings = new WorkspaceChatSettings(tenantId, AiProviderType.OpenAI, "gpt-4o-mini", "[]", null, 0.5, false, Chat.Domain.Enums.SentimentSensitivity.Medium, "[]", 3, Chat.Domain.Enums.PiiMaskingLevel.None, null);

        var (sut, db, _, aiFactory, _) = BuildSut(conversation, settings, CachedAiConfigJson(tenantId));
        var provider = aiFactory.GetProvider(Arg.Any<AiProviderType>());
        provider.GenerateStreamAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ => ThrowingStream());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await sut.RunAsync(conversation.Id, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static async IAsyncEnumerable<AiStreamChunk> ThrowingStream(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return new AiStreamChunk { Content = "partial" };
        ct.ThrowIfCancellationRequested();
        await Task.Yield();
    }
}
```
(Verify `AiStreamChunk`'s exact property name — `Content` is assumed; read `src/shared/NexConvo.BuildingBlocks.Ai/Models/AiStreamChunk.cs` before finalizing this test and adjust the property name if different. Also verify `WorkspaceChatSettings`'s constructor parameter order/types by re-reading it — the test above assumes the ctor order documented in the Global Constraints section's verified signature.)

- [ ] **Step 4: Run test to verify it fails**

Run: `dotnet test tests/services/Chat/NexConvo.Chat.Application.UnitTests/NexConvo.Chat.Application.UnitTests.csproj --filter "FullyQualifiedName~ReplyOrchestratorTests"`
Expected: FAIL to compile — `ReplyOrchestrator`, `IReplyOrchestrator` do not exist.

- [ ] **Step 5: Write minimal implementation**

`src/services/Chat/NexConvo.Chat.Application/Rag/IReplyOrchestrator.cs`:
```csharp
namespace NexConvo.Chat.Application.Rag;

/// <summary>
/// Runs the grounded-reply pipeline for one inbound message on a conversation. Structured as a
/// single-iteration loop today; a future MCP tool-calling loop (P3) inserts additional iterations
/// here rather than requiring a rewrite. Cancelable end-to-end for a future Voice barge-in caller.
/// </summary>
public interface IReplyOrchestrator
{
    Task<ReplyOutcome> RunAsync(Guid conversationId, CancellationToken cancellationToken);
}
```

`src/services/Chat/NexConvo.Chat.Application/Rag/ReplyOrchestrator.cs`:
```csharp
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using NexConvo.BuildingBlocks.Ai.Services;
using NexConvo.BuildingBlocks.Application.Security;
using NexConvo.BuildingBlocks.Rag;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Chat.Domain.Enums;
using NexConvo.Chat.Domain.ValueObjects;
using NexConvo.Contracts.Enums;

namespace NexConvo.Chat.Application.Rag;

public sealed class ReplyOrchestrator(
    IChatDbContext db,
    IKnowledgeRetrievalClient knowledge,
    IAiProviderFactory aiProviderFactory,
    IDistributedCache cache,
    IAesEncryptionService aes,
    IGroundedPromptAssembler promptAssembler,
    ITokenBudgeter tokenBudgeter,
    ILogger<ReplyOrchestrator> logger) : IReplyOrchestrator
{
    private const string AbstentionMarker = "[[NO_ANSWER]]";

    public async Task<ReplyOutcome> RunAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        for (var iteration = 0; iteration < 1; iteration++) // single-pass today; future MCP loop inserts here
        {
            var conversation = await db.Conversations.FirstAsync(c => c.Id == conversationId, cancellationToken);
            var settings = await db.WorkspaceChatSettings.FirstAsync(s => s.TenantId == conversation.TenantId, cancellationToken);
            var lastInbound = await db.Messages
                .Where(m => m.ConversationId == conversationId && m.Direction == MessageDirection.Inbound)
                .OrderByDescending(m => m.CreatedAt)
                .FirstAsync(cancellationToken);

            if (conversation.State != ConversationState.AiHandling)
            {
                logger.LogInformation(
                    "Skipping RAG reply for conversation {ConversationId}; state is {State}, not AiHandling",
                    conversationId, conversation.State);
                return new HandoffOutcome(Guid.Empty, EscalationReason.LowConfidence);
            }

            var triggerPhrases = JsonSerializer.Deserialize<string[]>(settings.TriggerPhrases) ?? [];
            if (triggerPhrases.Any(p => lastInbound.Body.Contains(p, StringComparison.OrdinalIgnoreCase)))
            {
                return await HandoffAsync(conversation, EscalationReason.TriggerPhrase, cancellationToken);
            }

            var cachedBytes = await cache.GetAsync($"AiConfig:{conversation.TenantId}", cancellationToken);
            if (cachedBytes is null)
            {
                return await HandoffAsync(conversation, EscalationReason.NoAiConfig, cancellationToken);
            }

            var aiConfig = JsonSerializer.Deserialize<CachedAiConfig>(Encoding.UTF8.GetString(cachedBytes))!;

            var matches = await knowledge.SearchAsync(
                lastInbound.Body, ChannelProfile.Chat.TopK, ChannelProfile.Chat.MinScore, cancellationToken);

            var contributions = matches
                .Select((m, i) => new ContextContribution(i + 1, m.ChunkId, m.DocumentId, m.Content, m.Score))
                .ToList();

            var (fittedContext, fittedHistory) = tokenBudgeter.Fit(contributions, [], ChannelProfile.Chat.MaxAnswerTokens * 4);

            var systemPrompt = promptAssembler.BuildSystemPrompt(ChannelProfile.Chat, settings.SystemPromptOverride);
            var userPrompt = promptAssembler.BuildUserPrompt(fittedContext, fittedHistory, lastInbound.Body, ChannelProfile.Chat);

            var apiKey = aes.Decrypt(aiConfig.EncryptedApiKey);
            var provider = aiProviderFactory.GetProvider(Enum.Parse<AiProviderType>(aiConfig.Provider));

            var replyBuilder = new StringBuilder();
            await foreach (var chunk in provider.GenerateStreamAsync(
                userPrompt, systemPrompt, apiKey, aiConfig.DefaultModel, aiConfig.BaseUrl, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                replyBuilder.Append(chunk.Content);
            }

            var replyText = replyBuilder.ToString();
            var abstained = replyText.TrimStart().StartsWith(AbstentionMarker, StringComparison.Ordinal);
            var topScore = matches.Count > 0 ? matches.Max(m => m.Score) : 0d;
            var confidence = RagConfidence.FromRetrievalAndAbstention(topScore, abstained);

            if (confidence.Score < settings.HandoffConfidenceThreshold || abstained)
            {
                return await HandoffAsync(conversation, EscalationReason.LowConfidence, cancellationToken);
            }

            var message = conversation.AppendAiReply(replyText, confidence, tokens: null);
            db.Messages.Add(message);
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "AI reply appended to conversation {ConversationId}, message {MessageId}, confidence {ConfidenceScore}",
                conversationId, message.Id, confidence.Score);

            return new AnsweredOutcome(message.Id, replyText, confidence);
        }

        throw new InvalidOperationException("Unreachable — single-iteration loop always returns.");
    }

    private async Task<ReplyOutcome> HandoffAsync(Conversation conversation, EscalationReason reason, CancellationToken cancellationToken)
    {
        var escalation = conversation.RequestHandoff(reason);
        db.Escalations.Add(escalation);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Conversation {ConversationId} handed off to human, reason {Reason}",
            conversation.Id, reason);

        return new HandoffOutcome(escalation.Id, reason);
    }

    private sealed record CachedAiConfig(
        Guid TenantId, string Provider, string EncryptedApiKey, string? BaseUrl,
        string DefaultModel, string? SystemPrompt, string? Parameters, bool IsActive);
}
```

`src/services/Chat/NexConvo.Chat.Application/Features/Rag/Commands/GenerateRagReplyCommandHandler.cs`:
```csharp
using MediatR;
using NexConvo.BuildingBlocks.Results;
using NexConvo.Chat.Application.Rag;
using Microsoft.Extensions.Logging;

namespace NexConvo.Chat.Application.Features.Rag.Commands;

public sealed class GenerateRagReplyCommandHandler(
    IReplyOrchestrator orchestrator,
    ILogger<GenerateRagReplyCommandHandler> logger) : IRequestHandler<GenerateRagReplyCommand, Result>
{
    public async Task<Result> Handle(GenerateRagReplyCommand request, CancellationToken cancellationToken)
    {
        var outcome = await orchestrator.RunAsync(request.ConversationId, cancellationToken);

        return outcome switch
        {
            AnsweredOutcome answered => LogAndSucceed(answered),
            HandoffOutcome handoff => LogAndSucceed(handoff),
            _ => Result.Failure("Unrecognized reply outcome."),
        };
    }

    private Result LogAndSucceed(AnsweredOutcome answered)
    {
        logger.LogInformation("RAG reply answered for message {MessageId}", answered.MessageId);
        return Result.Success();
    }

    private Result LogAndSucceed(HandoffOutcome handoff)
    {
        logger.LogInformation("RAG reply handed off, reason {Reason}", handoff.Reason);
        return Result.Success();
    }
}
```

`src/services/Chat/NexConvo.Chat.Application/Features/Rag/Commands/GenerateRagReplyCommandValidator.cs`:
```csharp
using FluentValidation;

namespace NexConvo.Chat.Application.Features.Rag.Commands;

public sealed class GenerateRagReplyCommandValidator : AbstractValidator<GenerateRagReplyCommand>
{
    public GenerateRagReplyCommandValidator() =>
        RuleFor(x => x.ConversationId).NotEmpty();
}
```

Register in `DependencyInjection.cs` (`AddChatInfrastructure`), after the gRPC client registration from Task 7:
```csharp
        services.AddSingleton<NexConvo.BuildingBlocks.Rag.IGroundedPromptAssembler, NexConvo.BuildingBlocks.Rag.GroundedPromptAssembler>();
        services.AddSingleton<NexConvo.BuildingBlocks.Rag.ITokenBudgeter, NexConvo.BuildingBlocks.Rag.TokenBudgeter>();
        services.AddScoped<NexConvo.Chat.Application.Rag.IReplyOrchestrator, NexConvo.Chat.Application.Rag.ReplyOrchestrator>();
```

In `src/services/Chat/NexConvo.Chat.Api/Program.cs`, add (near other `builder.Services.Add...` calls):
```csharp
builder.Services.AddAiProviders();
```
(Add `using NexConvo.BuildingBlocks.Ai;` if not already present — confirmed by exploration this call is currently missing from Chat.Api.)

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test tests/services/Chat/NexConvo.Chat.Application.UnitTests/NexConvo.Chat.Application.UnitTests.csproj --filter "FullyQualifiedName~ReplyOrchestratorTests"`
Expected: PASS (5/5).

- [ ] **Step 7: Commit**

```bash
git add src/services/Chat/NexConvo.Chat.Application/Rag src/services/Chat/NexConvo.Chat.Application/Features/Rag/Commands src/services/Chat/NexConvo.Chat.Infrastructure/DependencyInjection.cs src/services/Chat/NexConvo.Chat.Api/Program.cs tests/services/Chat/NexConvo.Chat.Application.UnitTests/Rag
git commit -m "feat(chat): add ReplyOrchestrator and GenerateRagReplyCommand"
```

---

## Task 11: Handoff integration event + publish wiring

**Files:**
- Create: `src/shared/NexConvo.Contracts/Events/Chat/ConversationHandoffRequestedIntegrationEvent.cs`
- Modify: `src/services/Chat/NexConvo.Chat.Application/Rag/ReplyOrchestrator.cs`
- Test: extend `tests/services/Chat/NexConvo.Chat.Application.UnitTests/Rag/ReplyOrchestratorTests.cs`

**Interfaces:**
- Consumes: `IPublishEndpoint` (MassTransit, existing).
- Produces: `ConversationHandoffRequestedIntegrationEvent(Guid TenantId, Guid ConversationId, string Reason, DateTimeOffset RaisedAt)`.

- [ ] **Step 1: Write the event contract**

`src/shared/NexConvo.Contracts/Events/Chat/ConversationHandoffRequestedIntegrationEvent.cs`:
```csharp
namespace NexConvo.Contracts.Events.Chat;

/// <summary>Reason is a string (not the Chat-owned EscalationReason enum) to avoid cross-service
/// enum-drift — mirrors how AiConfigUpdatedEvent.Provider is a string, not an enum.</summary>
public sealed record ConversationHandoffRequestedIntegrationEvent(
    Guid TenantId, Guid ConversationId, string Reason, DateTimeOffset RaisedAt) : IntegrationEvent;
```

- [ ] **Step 2: Write the failing test — assert publish is called on handoff**

Add to `tests/services/Chat/NexConvo.Chat.Application.UnitTests/Rag/ReplyOrchestratorTests.cs` (extend `BuildSut` to also construct and return a `Substitute.For<IPublishEndpoint>()`, pass it into the `ReplyOrchestrator` constructor, and add):
```csharp
    [Fact]
    public async Task RunAsync_TriggerPhraseMatched_PublishesHandoffIntegrationEvent()
    {
        var tenantId = Guid.NewGuid();
        var conversation = Conversation.StartAiHandling(tenantId, new ChannelIdentity(LeadSourceChannel.Whatsapp, "ext-1"), null);
        conversation.AppendInbound("provider-1", "I want to speak to a manager");
        var settings = new WorkspaceChatSettings(tenantId, AiProviderType.OpenAI, "gpt-4o-mini", "[]", null, 0.5, false, Chat.Domain.Enums.SentimentSensitivity.Medium, "[\"speak to a manager\"]", 3, Chat.Domain.Enums.PiiMaskingLevel.None, null);

        var (sut, _, _, _, _, publisher) = BuildSut(conversation, settings, CachedAiConfigJson(tenantId));

        await sut.RunAsync(conversation.Id, CancellationToken.None);

        await publisher.Received(1).Publish(
            Arg.Is<NexConvo.Contracts.Events.Chat.ConversationHandoffRequestedIntegrationEvent>(e =>
                e.ConversationId == conversation.Id && e.Reason == "TriggerPhrase"),
            Arg.Any<CancellationToken>());
    }
```
(Update `BuildSut`'s signature/return tuple and the `ReplyOrchestrator` constructor call inside it to include `MassTransit.IPublishEndpoint publisher = Substitute.For<IPublishEndpoint>()` as a new constructor parameter, threading it through all existing call sites in that test file.)

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/services/Chat/NexConvo.Chat.Application.UnitTests/NexConvo.Chat.Application.UnitTests.csproj --filter "FullyQualifiedName~RunAsync_TriggerPhraseMatched_PublishesHandoffIntegrationEvent"`
Expected: FAIL — `ReplyOrchestrator` has no `IPublishEndpoint` constructor parameter yet, or the publish never happens.

- [ ] **Step 4: Modify `ReplyOrchestrator` to publish on handoff**

Add `MassTransit.IPublishEndpoint publisher` to `ReplyOrchestrator`'s primary constructor parameter list, and change `HandoffAsync` to:
```csharp
    private async Task<ReplyOutcome> HandoffAsync(Conversation conversation, EscalationReason reason, CancellationToken cancellationToken)
    {
        var escalation = conversation.RequestHandoff(reason);
        db.Escalations.Add(escalation);
        await db.SaveChangesAsync(cancellationToken);

        await publisher.Publish(
            new NexConvo.Contracts.Events.Chat.ConversationHandoffRequestedIntegrationEvent(
                conversation.TenantId, conversation.Id, reason.ToString(), DateTimeOffset.UtcNow),
            cancellationToken);

        logger.LogInformation(
            "Conversation {ConversationId} handed off to human, reason {Reason}",
            conversation.Id, reason);

        return new HandoffOutcome(escalation.Id, reason);
    }
```
(This mirrors the exact `SaveChangesAsync` → `IPublishEndpoint.Publish` shape confirmed in Task 10 Step 1's reading of `SaveChannelConnectionCommandHandler`. Once Task 8's `AddEntityFrameworkOutbox<ChatDbContext>()` is registered, this `Publish` call becomes transactional with the preceding `SaveChangesAsync` automatically — no different code shape needed.)

Add `using MassTransit;` to the top of `ReplyOrchestrator.cs` if not already present.

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/services/Chat/NexConvo.Chat.Application.UnitTests/NexConvo.Chat.Application.UnitTests.csproj --filter "FullyQualifiedName~ReplyOrchestratorTests"`
Expected: PASS (all tests in the file, including the new one).

- [ ] **Step 6: Commit**

```bash
git add src/shared/NexConvo.Contracts/Events/Chat/ConversationHandoffRequestedIntegrationEvent.cs src/services/Chat/NexConvo.Chat.Application/Rag/ReplyOrchestrator.cs tests/services/Chat/NexConvo.Chat.Application.UnitTests/Rag/ReplyOrchestratorTests.cs
git commit -m "feat(chat): publish ConversationHandoffRequestedIntegrationEvent on handoff"
```

---

## Task 12: `NexConvo.Chat.IntegrationTests` — outbox, audit, RLS, idempotency end-to-end

**Files:**
- Create: `tests/services/Chat/NexConvo.Chat.IntegrationTests/NexConvo.Chat.IntegrationTests.csproj`
- Create: `tests/services/Chat/NexConvo.Chat.IntegrationTests/ChatApiFactory.cs`
- Create: `tests/services/Chat/NexConvo.Chat.IntegrationTests/RagReplyEndToEndTests.cs`
- Create: `tests/services/Chat/NexConvo.Chat.IntegrationTests/MessageReceivedConsumerIdempotencyTests.cs`
- Modify: `src/services/Chat/NexConvo.Chat.Infrastructure/NexConvo.Chat.Infrastructure.csproj` (add `InternalsVisibleTo`)
- Modify: `NexConvo.sln`

**Interfaces:**
- Consumes: everything built in Tasks 1–11.
- Produces: nothing new — this is the top-level verification layer for TDD scenarios 5 (idempotency) and 7 (audit+RLS+outbox).

- [ ] **Step 1: Read the Knowledge integration test project to mirror it**

Read `tests/services/Knowledge/NexConvo.Knowledge.IntegrationTests/KnowledgeApiFactory.cs` and its `.csproj` in full — confirm the exact `WebApplicationFactory<Program>` + `Testcontainers.PostgreSql` + `nexconvo_service` role-creation + `ConfigureTestServices` override pattern.

- [ ] **Step 2: Scaffold the new project**

`tests/services/Chat/NexConvo.Chat.IntegrationTests/NexConvo.Chat.IntegrationTests.csproj` — copy `NexConvo.Knowledge.IntegrationTests.csproj`'s exact `<PropertyGroup>` and package references (Testcontainers.PostgreSql, Microsoft.AspNetCore.Mvc.Testing, MassTransit.TestFramework — add `MassTransit.TestFramework` `PackageVersion` to `Directory.Packages.props` at the same 8.3.4 pin if not already present), with `<ProjectReference>`s to `NexConvo.Chat.Api.csproj`.

`src/services/Chat/NexConvo.Chat.Infrastructure/NexConvo.Chat.Infrastructure.csproj` — add, mirroring however Knowledge's own csproj exposes its `NullTenantContext`/test seams:
```xml
  <ItemGroup>
    <InternalsVisibleTo Include="NexConvo.Chat.IntegrationTests" />
  </ItemGroup>
```

`tests/services/Chat/NexConvo.Chat.IntegrationTests/ChatApiFactory.cs` — mirror `KnowledgeApiFactory.cs`'s shape exactly, adapted to Chat: spins up a `PostgreSqlBuilder` Testcontainer, creates the `nexconvo_service` role + grants, points `ConnectionStrings:ChatDb` and `ConnectionStrings:Redis`/`ConnectionStrings:RabbitMQ` at test doubles or a lightweight in-memory equivalent where Knowledge's factory does the same, and in `ConfigureTestServices` swaps `IKnowledgeRetrievalClient` and `IAiProviderFactory` for NSubstitute doubles configured with the same canned "refund policy" response used in Task 10's unit tests (real Knowledge/real LLM are not needed for Chat's own outbox/RLS assertions — this test project owns Chat's side of the contract only).

- [ ] **Step 3: Write the failing outbox/audit/RLS end-to-end test**

`tests/services/Chat/NexConvo.Chat.IntegrationTests/RagReplyEndToEndTests.cs`:
```csharp
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using NexConvo.Chat.Domain.Entities;
using NexConvo.Chat.Infrastructure.Persistence;
using NexConvo.Contracts.Enums;
using NexConvo.Contracts.Events.Chat;
using Xunit;

namespace NexConvo.Chat.IntegrationTests;

[Collection("ChatApi")]
public class RagReplyEndToEndTests(ChatApiFactory factory)
{
    [Fact]
    public async Task GroundedQuestion_PersistsAiReplyWithAuditRowAndPublishesNoHandoff()
    {
        var tenantId = Guid.NewGuid();
        await factory.SeedWorkspaceChatSettingsAsync(tenantId, handoffThreshold: 0.3);

        var harness = factory.Services.GetTestHarness();
        await harness.Start();

        await harness.Bus.Publish(new MessageReceivedIntegrationEvent
        {
            ConversationId = Guid.NewGuid(),
            TenantId = tenantId,
            Channel = LeadSourceChannel.Whatsapp,
            ExternalSenderId = "sender-1",
            MessageRef = "ref-1",
            Body = "How long do refunds take?",
            ProviderMessageId = "provider-1",
        });

        (await harness.Consumed.Any<MessageReceivedIntegrationEvent>()).Should().BeTrue();

        await using var db = factory.CreateDbContext(tenantId);
        var reply = await db.Messages.FirstOrDefaultAsync(m => m.Sender.Role == Chat.Domain.Enums.MessageSenderRole.Ai);
        reply.Should().NotBeNull();

        var auditRows = await db.ChatAuditLogs.ToListAsync();
        auditRows.Should().NotBeEmpty();

        await harness.Stop();
    }
}
```
(`factory.SeedWorkspaceChatSettingsAsync` and `factory.CreateDbContext(tenantId)` are helper methods to add to `ChatApiFactory` — the latter mirrors Task 6's `ChatPostgresFixture.CreateDbContext`.)

`tests/services/Chat/NexConvo.Chat.IntegrationTests/MessageReceivedConsumerIdempotencyTests.cs`:
```csharp
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using NexConvo.Contracts.Enums;
using NexConvo.Contracts.Events.Chat;
using Xunit;

namespace NexConvo.Chat.IntegrationTests;

[Collection("ChatApi")]
public class MessageReceivedConsumerIdempotencyTests(ChatApiFactory factory)
{
    [Fact]
    public async Task RedeliveredMessage_SameTransportMessageId_DoesNotCreateDuplicateMessage()
    {
        var tenantId = Guid.NewGuid();
        await factory.SeedWorkspaceChatSettingsAsync(tenantId, handoffThreshold: 0.3);

        var harness = factory.Services.GetTestHarness();
        await harness.Start();

        var messageId = NewId.NextGuid();
        var evt = new MessageReceivedIntegrationEvent
        {
            ConversationId = Guid.NewGuid(),
            TenantId = tenantId,
            Channel = LeadSourceChannel.Whatsapp,
            ExternalSenderId = "sender-2",
            MessageRef = "ref-2",
            Body = "How long do refunds take?",
            ProviderMessageId = "provider-2",
        };

        await harness.Bus.Publish(evt, ctx => ctx.MessageId = messageId);
        await harness.Bus.Publish(evt, ctx => ctx.MessageId = messageId); // redelivery, same MessageId

        await Task.Delay(TimeSpan.FromSeconds(2)); // allow both to be processed

        await using var db = factory.CreateDbContext(tenantId);
        var inboundCount = await db.Messages.CountAsync(m => m.ProviderMessageId == "provider-2");
        inboundCount.Should().Be(1);

        await harness.Stop();
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test tests/services/Chat/NexConvo.Chat.IntegrationTests/NexConvo.Chat.IntegrationTests.csproj`
Expected: FAIL — project doesn't build yet / `ChatApiFactory` incomplete.

- [ ] **Step 5: Complete `ChatApiFactory` and get tests green**

Fill in `ChatApiFactory`'s `SeedWorkspaceChatSettingsAsync` and `CreateDbContext` helpers, wire `ConfigureTestServices` to override `IKnowledgeRetrievalClient`/`IAiProviderFactory` as described in Step 2, and run migrations against the Testcontainer on startup (mirror however `KnowledgeApiFactory` does this — likely via `ChatDatabaseMigrator.MigrateAsync` or `context.Database.MigrateAsync()`).

Run: `dotnet test tests/services/Chat/NexConvo.Chat.IntegrationTests/NexConvo.Chat.IntegrationTests.csproj`
Expected: PASS (both tests) — requires Docker running.

- [ ] **Step 6: Nest the new test project in `NexConvo.sln`**

Run: `dotnet sln NexConvo.sln add tests/services/Chat/NexConvo.Chat.IntegrationTests/NexConvo.Chat.IntegrationTests.csproj --solution-folder tests/services/Chat` (creating the `tests/services/Chat` Solution Folder nesting if the interactive `dotnet sln` command doesn't already place it correctly — verify in an editor afterward, matching how `tests/services/Knowledge` is nested).

Run: `dotnet build NexConvo.sln`
Expected: builds clean.

- [ ] **Step 7: Full regression run**

Run: `dotnet test NexConvo.sln`
Expected: ALL tests across the solution PASS — this is the final gate before considering Slice 5 complete.

- [ ] **Step 8: Commit**

```bash
git add tests/services/Chat/NexConvo.Chat.IntegrationTests src/services/Chat/NexConvo.Chat.Infrastructure/NexConvo.Chat.Infrastructure.csproj NexConvo.sln
git commit -m "test(chat): add Chat integration tests for RAG reply outbox, audit, RLS, and idempotency"
```

---

## Verification (manual, after all tasks complete)

1. `dotnet test NexConvo.sln` — full green run, all 7 TDD scenarios from the spec covered (grounded+cited answer, Voice profile differs, don't-know/handoff, trigger phrase, idempotency, cancelation, audit+RLS+outbox).
2. `docker-compose up -d postgres redis rabbitmq` then run Chat.Api and Knowledge.Api locally; use `harness.Bus.Publish` from a scratch console or a Postman-free MassTransit test harness script to publish a real `MessageReceivedIntegrationEvent` against a tenant with a seeded Knowledge chunk (reuse Slice 4's ingestion pipeline to seed one) and a healthy `WorkspaceAiConfig`; confirm a `messages` row appears with `sender_role = Ai` and a citation, and a `chat_audit_logs` row exists.
3. Repeat with a question outside the seeded knowledge base; confirm `conversations.state = PendingHuman`, an `escalations` row, and no AI reply message.
4. Redeliver the same event (same transport `MessageId`) via the test harness; confirm no duplicate `messages` row.
5. Report: confirm `BuildingBlocks.Rag` has zero I/O dependencies (`dotnet list src/shared/NexConvo.BuildingBlocks.Rag/NexConvo.BuildingBlocks.Rag.csproj package` should show only the tokenizer packages), confirm the orchestrator's cancelable/loop-ready structure, confirm gRPC client wiring, confirm outbox registration, and report final test pass count.
