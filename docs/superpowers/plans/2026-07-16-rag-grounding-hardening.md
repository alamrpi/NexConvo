# RAG Grounding Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the RAG assistant never answer from outside the knowledge base — enforced in code, not by prompt trust alone.

**Architecture:** Two layers of defense shared across all three RAG paths (WidgetHub, PlaygroundHub, ReplyOrchestrator): (1) a pre-generation `GroundingGate` that blocks the LLM call entirely when the top retrieval score is below a per-channel `AnswerGateScore` → tenant-configurable fallback + human handoff where available; (2) a hardened prompt plus an `AbstentionStreamFilter` that buffers the leading tokens so the user never sees `[[NO_ANSWER]]` or a half-baked answer.

**Tech Stack:** .NET 9, C#, xUnit + NSubstitute + FluentAssertions, EF Core (PostgreSQL), SignalR. Frontend: Next.js, React Hook Form + zod, next-intl, vitest + MSW.

## Global Constraints

- All .NET projects target `net9.0`; frontend TypeScript is strict (no `any`).
- TDD: write the failing test first, watch it fail, then implement (Standard 4 / 20).
- The abstention marker is the existing constant `GroundedPromptAssembler.AbstentionMarker` = `"[[NO_ANSWER]]"` — never hardcode the literal elsewhere.
- The retrieval match type is `record KnowledgeChunkMatch(string ChunkId, string DocumentId, string Content, double Score)` in `NexConvo.Chat.Application.Common.Interfaces`.
- Constructor injection only; API/hub layers dispatch — no direct `DbContext` in a controller.
- `NoAnswerMessage` user-facing copy flows through i18n; Bengali is first-class (default en + bn).
- Existing retrieval `MinScore` (Chat 0.55, Voice 0.6) is unchanged; the new `AnswerGateScore` is separate.
- Commit after each green task.

---

## File Structure

**New (shared):**
- `src/shared/NexConvo.BuildingBlocks.Rag/ChannelProfile.cs` — add `double AnswerGateScore` field.
- `src/services/Chat/NexConvo.Chat.Application/Rag/IGroundingGate.cs` + `GroundingGate.cs` — score gate over `KnowledgeChunkMatch`.
- `src/services/Chat/NexConvo.Chat.Application/Rag/IAbstentionStreamFilter.cs` + `AbstentionStreamFilter.cs` — leading-buffer streaming filter.

**Modified (backend):**
- `GroundedPromptAssembler.cs` — hardened system prompt.
- `WorkspaceChatSettings.cs` (+ EF config + migration) — `NoAnswerMessage`.
- `SaveChatSettingsCommand{,Handler,Validator}.cs`, `SaveChatSettingsRequest.cs`, `WorkspaceChatSettingsDto.cs`, `GetChatSettingsQueryHandler.cs`, `ChatSettingsController.cs` — thread `NoAnswerMessage`.
- `WidgetHub.cs`, `PlaygroundHub.cs`, `ReplyOrchestrator.cs` — gate + filter + fallback wiring.
- DI registration for the gate + filter.

**Modified (frontend):**
- `chat-settings.schema.ts` (+ test), `chat-settings.types.ts`, `chat-widget-form.tsx`, `en.json`, `bn.json`, and the settings fixtures/handlers.

---

## Task 1: Add `AnswerGateScore` to `ChannelProfile`

**Files:**
- Modify: `src/shared/NexConvo.BuildingBlocks.Rag/ChannelProfile.cs`
- Test: `tests/shared/NexConvo.BuildingBlocks.Rag.Tests/ChannelProfileTests.cs` (create if absent)

**Interfaces:**
- Produces: `ChannelProfile.AnswerGateScore` (double); `ChannelProfile.Chat.AnswerGateScore`, `ChannelProfile.Voice.AnswerGateScore`.

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using NexConvo.BuildingBlocks.Rag;
using Xunit;

namespace NexConvo.BuildingBlocks.Rag.Tests;

public class ChannelProfileTests
{
    [Fact]
    public void Chat_HasAnswerGateAboveRetrievalMinScore()
    {
        // The answer gate must be at least as strict as retrieval — a chunk good enough to
        // retrieve is not automatically good enough to answer from.
        ChannelProfile.Chat.AnswerGateScore.Should().BeGreaterThanOrEqualTo(ChannelProfile.Chat.MinScore);
    }

    [Fact]
    public void Voice_HasAnswerGateAboveRetrievalMinScore()
    {
        ChannelProfile.Voice.AnswerGateScore.Should().BeGreaterThanOrEqualTo(ChannelProfile.Voice.MinScore);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/shared/NexConvo.BuildingBlocks.Rag.Tests --filter FullyQualifiedName~ChannelProfileTests`
Expected: FAIL — `ChannelProfile` does not contain `AnswerGateScore`.

- [ ] **Step 3: Add the field with a provisional value (tuned in Task 8)**

In `ChannelProfile.cs`, add `double AnswerGateScore` to the record and set it on both presets. Provisional value = `MinScore` (safe: gate ≥ retrieval); Task 8 raises it from measured data.

```csharp
public sealed record ChannelProfile(
    RagRegister Register,
    int TopK,
    double MinScore,
    double AnswerGateScore,
    int MaxAnswerTokens,
    bool EmitCitations,
    StreamGranularity StreamGranularity)
{
    public static readonly ChannelProfile Chat = new(
        RagRegister.Chat, TopK: 5, MinScore: 0.55, AnswerGateScore: 0.55, MaxAnswerTokens: 600,
        EmitCitations: true, StreamGranularity.Token);

    public static readonly ChannelProfile Voice = new(
        RagRegister.Voice, TopK: 3, MinScore: 0.6, AnswerGateScore: 0.6, MaxAnswerTokens: 80,
        EmitCitations: false, StreamGranularity.Sentence);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/shared/NexConvo.BuildingBlocks.Rag.Tests --filter FullyQualifiedName~ChannelProfileTests`
Expected: PASS. If the `.Tests` project doesn't exist, create it mirroring an existing `tests/shared/*` csproj (references `NexConvo.BuildingBlocks.Rag`, xUnit, FluentAssertions) and add it to `NexConvo.sln` under the `tests` solution folder (Standard 20).

- [ ] **Step 5: Fix compile fallout & commit**

Any positional `new ChannelProfile(...)` elsewhere needs the extra arg — search `grep -rn "new ChannelProfile(" src` and fix. The two static presets above already cover the shipped ones.

```bash
git add src/shared/NexConvo.BuildingBlocks.Rag/ChannelProfile.cs tests/shared/NexConvo.BuildingBlocks.Rag.Tests
git commit -m "feat(rag): add per-channel AnswerGateScore to ChannelProfile"
```

---

## Task 2: `GroundingGate` — pre-generation retrieval gate

**Files:**
- Create: `src/services/Chat/NexConvo.Chat.Application/Rag/IGroundingGate.cs`
- Create: `src/services/Chat/NexConvo.Chat.Application/Rag/GroundingGate.cs`
- Test: `tests/services/Chat/NexConvo.Chat.Application.UnitTests/Rag/GroundingGateTests.cs`

**Interfaces:**
- Consumes: `KnowledgeChunkMatch` (`NexConvo.Chat.Application.Common.Interfaces`), `ChannelProfile.AnswerGateScore` (Task 1).
- Produces: `IGroundingGate.ShouldAnswer(IReadOnlyList<KnowledgeChunkMatch> matches, ChannelProfile profile) : bool`.

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using NexConvo.BuildingBlocks.Rag;
using NexConvo.Chat.Application.Common.Interfaces;
using NexConvo.Chat.Application.Rag;
using Xunit;

namespace NexConvo.Chat.Application.UnitTests.Rag;

public class GroundingGateTests
{
    private readonly IGroundingGate _gate = new GroundingGate();
    private static readonly ChannelProfile Profile =
        ChannelProfile.Chat with { AnswerGateScore = 0.65 };

    private static KnowledgeChunkMatch Match(double score) => new("c", "d", "content", score);

    [Fact]
    public void NoMatches_DoesNotAnswer() =>
        _gate.ShouldAnswer([], Profile).Should().BeFalse();

    [Fact]
    public void TopScoreBelowGate_DoesNotAnswer() =>
        _gate.ShouldAnswer([Match(0.60), Match(0.40)], Profile).Should().BeFalse();

    [Fact]
    public void TopScoreAtGate_Answers() =>
        _gate.ShouldAnswer([Match(0.65), Match(0.10)], Profile).Should().BeTrue();

    [Fact]
    public void TopScoreAboveGate_Answers() =>
        _gate.ShouldAnswer([Match(0.90)], Profile).Should().BeTrue();
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/services/Chat/NexConvo.Chat.Application.UnitTests --filter FullyQualifiedName~GroundingGateTests`
Expected: FAIL — `IGroundingGate`/`GroundingGate` don't exist.

- [ ] **Step 3: Implement the gate**

`IGroundingGate.cs`:

```csharp
using NexConvo.BuildingBlocks.Rag;
using NexConvo.Chat.Application.Common.Interfaces;

namespace NexConvo.Chat.Application.Rag;

/// <summary>
/// Decides whether retrieval is strong enough to let the LLM answer at all. If not, the caller must
/// NOT call the model — it returns the tenant's NoAnswerMessage (and hands off where a human exists).
/// This is the hard guarantee that the assistant never answers from outside the knowledge base.
/// </summary>
public interface IGroundingGate
{
    bool ShouldAnswer(IReadOnlyList<KnowledgeChunkMatch> matches, ChannelProfile profile);
}
```

`GroundingGate.cs`:

```csharp
using NexConvo.BuildingBlocks.Rag;
using NexConvo.Chat.Application.Common.Interfaces;

namespace NexConvo.Chat.Application.Rag;

public sealed class GroundingGate : IGroundingGate
{
    public bool ShouldAnswer(IReadOnlyList<KnowledgeChunkMatch> matches, ChannelProfile profile) =>
        matches.Count > 0 && matches.Max(m => m.Score) >= profile.AnswerGateScore;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/services/Chat/NexConvo.Chat.Application.UnitTests --filter FullyQualifiedName~GroundingGateTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/services/Chat/NexConvo.Chat.Application/Rag/IGroundingGate.cs src/services/Chat/NexConvo.Chat.Application/Rag/GroundingGate.cs tests/services/Chat/NexConvo.Chat.Application.UnitTests/Rag/GroundingGateTests.cs
git commit -m "feat(rag): add GroundingGate — block LLM call on weak retrieval"
```

---

## Task 3: `AbstentionStreamFilter` — leading-buffer streaming filter

**Files:**
- Create: `src/services/Chat/NexConvo.Chat.Application/Rag/IAbstentionStreamFilter.cs`
- Create: `src/services/Chat/NexConvo.Chat.Application/Rag/AbstentionStreamFilter.cs`
- Test: `tests/services/Chat/NexConvo.Chat.Application.UnitTests/Rag/AbstentionStreamFilterTests.cs`

**Interfaces:**
- Consumes: `GroundedPromptAssembler.AbstentionMarker`.
- Produces: `IAbstentionStreamFilter.FilterAsync(IAsyncEnumerable<string> source, CancellationToken ct) : IAsyncEnumerable<AbstentionResult>` where `record AbstentionResult(string? Token, bool Abstained)`. Each yielded item carries either a `Token` to forward to the client, or `Abstained = true` signalling the caller to suppress everything and send the fallback. Once `Abstained` is yielded, the enumeration ends.

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using NexConvo.Chat.Application.Rag;
using Xunit;

namespace NexConvo.Chat.Application.UnitTests.Rag;

public class AbstentionStreamFilterTests
{
    private readonly IAbstentionStreamFilter _filter = new AbstentionStreamFilter();

    private static async IAsyncEnumerable<string> Stream(params string[] chunks)
    {
        foreach (var c in chunks) { yield return c; await Task.Yield(); }
    }

    private async Task<(string text, bool abstained)> Collect(IAsyncEnumerable<string> src)
    {
        var sb = new System.Text.StringBuilder();
        var abstained = false;
        await foreach (var r in _filter.FilterAsync(src, default))
        {
            if (r.Abstained) { abstained = true; break; }
            if (r.Token is not null) sb.Append(r.Token);
        }
        return (sb.ToString(), abstained);
    }

    [Fact]
    public async Task NormalAnswer_PassesThroughUnchanged()
    {
        var (text, abstained) = await Collect(Stream("Refunds ", "take ", "5 days."));
        abstained.Should().BeFalse();
        text.Should().Be("Refunds take 5 days.");
    }

    [Fact]
    public async Task BareMarker_Abstains_NothingLeaks()
    {
        var (text, abstained) = await Collect(Stream("[[NO_ANSWER]]"));
        abstained.Should().BeTrue();
        text.Should().BeEmpty();
    }

    [Fact]
    public async Task MarkerSplitAcrossChunks_StillDetected()
    {
        var (text, abstained) = await Collect(Stream("[[NO_", "ANSWER", "]]"));
        abstained.Should().BeTrue();
        text.Should().BeEmpty();
    }

    [Fact]
    public async Task MarkerWrappedInText_Abstains()
    {
        // Model sometimes prefaces the marker; the leading buffer must catch it before release.
        var (text, abstained) = await Collect(Stream("I'm sorry, ", "[[NO_ANSWER]]"));
        abstained.Should().BeTrue();
        text.Should().BeEmpty();
    }

    [Fact]
    public async Task LongAnswerWithoutMarker_ReleasesAfterBuffer()
    {
        var (text, abstained) = await Collect(Stream(new string('a', 100)));
        abstained.Should().BeFalse();
        text.Should().Be(new string('a', 100));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/services/Chat/NexConvo.Chat.Application.UnitTests --filter FullyQualifiedName~AbstentionStreamFilterTests`
Expected: FAIL — filter types don't exist.

- [ ] **Step 3: Implement the filter**

`IAbstentionStreamFilter.cs`:

```csharp
namespace NexConvo.Chat.Application.Rag;

/// <summary>One filtered stream item: either a token to forward, or an abstain signal.</summary>
public readonly record struct AbstentionResult(string? Token, bool Abstained);

/// <summary>
/// Wraps an LLM token stream and holds back the leading window until it is sure the reply is NOT an
/// abstention. If the abstention marker appears anywhere in the buffered window (even split across
/// chunks or prefaced by an apology), it yields a single Abstained result and stops — so the user
/// never sees the marker or a half-formed outside-KB answer. Otherwise it releases the buffer and
/// passes the rest through token-by-token.
/// </summary>
public interface IAbstentionStreamFilter
{
    IAsyncEnumerable<AbstentionResult> FilterAsync(IAsyncEnumerable<string> source, CancellationToken ct);
}
```

`AbstentionStreamFilter.cs`:

```csharp
using System.Runtime.CompilerServices;
using System.Text;
using NexConvo.BuildingBlocks.Rag;

namespace NexConvo.Chat.Application.Rag;

public sealed class AbstentionStreamFilter : IAbstentionStreamFilter
{
    // Buffer enough to catch the marker even when models preface it (e.g. "I'm sorry, [[NO_ANSWER]]").
    private const int BufferWindow = 64;
    private static readonly string Marker = GroundedPromptAssembler.AbstentionMarker;

    public async IAsyncEnumerable<AbstentionResult> FilterAsync(
        IAsyncEnumerable<string> source, [EnumeratorCancellation] CancellationToken ct)
    {
        var buffer = new StringBuilder();
        var released = false;

        await foreach (var chunk in source.WithCancellation(ct))
        {
            if (string.IsNullOrEmpty(chunk)) continue;

            if (released)
            {
                yield return new AbstentionResult(chunk, false);
                continue;
            }

            buffer.Append(chunk);
            var text = buffer.ToString();

            if (text.Contains(Marker, StringComparison.OrdinalIgnoreCase))
            {
                yield return new AbstentionResult(null, true);
                yield break;
            }

            // Once the buffer exceeds the window and can no longer be the start of the marker,
            // release it as a single token and stream freely thereafter.
            if (text.Length >= BufferWindow)
            {
                released = true;
                yield return new AbstentionResult(text, false);
                buffer.Clear();
            }
        }

        // Stream ended while still buffering (short answer, no marker) — flush what we held.
        if (!released && buffer.Length > 0)
        {
            yield return new AbstentionResult(buffer.ToString(), false);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/services/Chat/NexConvo.Chat.Application.UnitTests --filter FullyQualifiedName~AbstentionStreamFilterTests`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add src/services/Chat/NexConvo.Chat.Application/Rag/IAbstentionStreamFilter.cs src/services/Chat/NexConvo.Chat.Application/Rag/AbstentionStreamFilter.cs tests/services/Chat/NexConvo.Chat.Application.UnitTests/Rag/AbstentionStreamFilterTests.cs
git commit -m "feat(rag): add AbstentionStreamFilter — never leak [[NO_ANSWER]] to the client"
```

---

## Task 4: Harden the grounded system prompt

**Files:**
- Modify: `src/shared/NexConvo.BuildingBlocks.Rag/GroundedPromptAssembler.cs`
- Test: `tests/shared/NexConvo.BuildingBlocks.Rag.Tests/GroundedPromptAssemblerTests.cs` (create if absent)

**Interfaces:**
- Produces: unchanged public signatures; only prompt text changes.

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using NexConvo.BuildingBlocks.Rag;
using Xunit;

namespace NexConvo.BuildingBlocks.Rag.Tests;

public class GroundedPromptAssemblerTests
{
    private readonly GroundedPromptAssembler _assembler = new();

    [Fact]
    public void SystemPrompt_ForbidsOutsideKnowledgeAndInventedCitations()
    {
        var prompt = _assembler.BuildSystemPrompt(ChannelProfile.Chat, tenantSystemPromptOverride: null);

        prompt.Should().Contain("only");
        prompt.Should().Contain(GroundedPromptAssembler.AbstentionMarker);
        prompt.ToLowerInvariant().Should().Contain("prior knowledge");   // outside knowledge forbidden
        prompt.ToLowerInvariant().Should().Contain("do not invent");     // no fabricated citations
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/shared/NexConvo.BuildingBlocks.Rag.Tests --filter FullyQualifiedName~GroundedPromptAssemblerTests`
Expected: FAIL — current prompt lacks "prior knowledge" / "do not invent".

- [ ] **Step 3: Harden the prompt**

Replace the opening lines of `BuildSystemPrompt` (before the `EmitCitations` branch) with:

```csharp
sb.AppendLine("You are a support assistant. Use ONLY the numbered context provided below to answer.");
sb.AppendLine("Do not use any outside or prior knowledge, and do not guess. If the numbered context does not fully contain the answer, respond with exactly " + AbstentionMarker + " and nothing else.");
sb.AppendLine($"Never translate or localize {AbstentionMarker} — emit it verbatim in every language, even though your answer text itself should follow the instruction below.");
sb.AppendLine("Never invent or guess a citation number — only cite context entries that are actually present below.");
sb.AppendLine("Always reply in the same language the user wrote in.");
```

Keep the existing `EmitCitations` / override blocks below unchanged.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/shared/NexConvo.BuildingBlocks.Rag.Tests --filter FullyQualifiedName~GroundedPromptAssemblerTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/shared/NexConvo.BuildingBlocks.Rag/GroundedPromptAssembler.cs tests/shared/NexConvo.BuildingBlocks.Rag.Tests/GroundedPromptAssemblerTests.cs
git commit -m "feat(rag): harden grounded prompt — context-only, no outside knowledge, no invented citations"
```

---

## Task 5: `NoAnswerMessage` on `WorkspaceChatSettings` (entity + migration + save path)

**Files:**
- Modify: `src/services/Chat/NexConvo.Chat.Domain/Entities/WorkspaceChatSettings.cs`
- Modify: `src/services/Chat/NexConvo.Chat.Infrastructure/Persistence/Configurations/WorkspaceChatSettingsConfiguration.cs`
- Create: migration `..._AddNoAnswerMessageToWorkspaceChatSettings` (via `dotnet ef`)
- Modify: `SaveChatSettingsCommand.cs`, `SaveChatSettingsCommandHandler.cs`, `SaveChatSettingsCommandValidator.cs`, `SaveChatSettingsRequest.cs`, `ChatSettingsController.cs`, `WorkspaceChatSettingsDto.cs`, `GetChatSettingsQueryHandler.cs`
- Test: `tests/services/Chat/NexConvo.Chat.Application.UnitTests/Features/ChatSettings/Commands/SaveChatSettingsCommandValidatorTests.cs` (extend)

**Interfaces:**
- Produces: `WorkspaceChatSettings.NoAnswerMessage` (string, non-null, default `"Sorry, I don't have information about that. Please contact our support team for help."`); constructor + `Update(...)` gain a trailing `string noAnswerMessage` parameter; `SaveChatSettingsCommand`/`Request`/`Dto` gain `NoAnswerMessage`.

- [ ] **Step 1: Write the failing validator test**

Add to `SaveChatSettingsCommandValidatorTests` (extend the `Valid(...)` factory with `noAnswer` param, default `"We don't have that info; please contact support."`, threaded into the command's new `NoAnswerMessage` arg):

```csharp
[Fact]
public void NoAnswerMessage_Empty_Fails()
{
    _validator.TestValidate(Valid(noAnswer: ""))
        .ShouldHaveValidationErrorFor(x => x.NoAnswerMessage);
}

[Fact]
public void NoAnswerMessage_TooLong_Fails()
{
    _validator.TestValidate(Valid(noAnswer: new string('a', 501)))
        .ShouldHaveValidationErrorFor(x => x.NoAnswerMessage);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/services/Chat/NexConvo.Chat.Application.UnitTests --filter FullyQualifiedName~SaveChatSettingsCommandValidatorTests`
Expected: FAIL — `SaveChatSettingsCommand` has no `NoAnswerMessage`.

- [ ] **Step 3: Thread `NoAnswerMessage` through domain + command + request + dto**

Entity (`WorkspaceChatSettings.cs`): add
```csharp
public string NoAnswerMessage { get; private set; } = "Sorry, I don't have information about that. Please contact our support team for help.";
```
Add a trailing `string noAnswerMessage` param to the public constructor and to `Update(...)`, assigning `NoAnswerMessage = noAnswerMessage;` in both.

EF config (`WorkspaceChatSettingsConfiguration.cs`): add near the widget block
```csharp
builder.Property(x => x.NoAnswerMessage)
    .HasColumnName("no_answer_message")
    .IsRequired()
    .HasMaxLength(500)
    .HasDefaultValue("Sorry, I don't have information about that. Please contact our support team for help.");
```

`SaveChatSettingsCommand.cs`: add `string NoAnswerMessage` (place it right after `WidgetWelcomeMessage`, before `ActorUserId`).
`SaveChatSettingsRequest.cs`: add `string NoAnswerMessage = "Sorry, I don't have information about that. Please contact our support team for help."` as the last optional param.
`ChatSettingsController.cs`: pass `body.NoAnswerMessage` into the command (after `body.WidgetWelcomeMessage`).
`SaveChatSettingsCommandHandler.cs`: pass `cmd.NoAnswerMessage` as the new trailing arg to both the constructor and `Update(...)`.
`WorkspaceChatSettingsDto.cs`: add `string NoAnswerMessage` (after `WidgetWelcomeMessage`).
`GetChatSettingsQueryHandler.cs`: map `settings.NoAnswerMessage` into the DTO.

Validator (`SaveChatSettingsCommandValidator.cs`): add
```csharp
RuleFor(x => x.NoAnswerMessage)
    .NotEmpty().WithMessage("No-answer message is required.")
    .MaximumLength(500).WithMessage("No-answer message must not exceed 500 characters.");
```

- [ ] **Step 4: Generate the migration**

Run (from `src/services/Chat/NexConvo.Chat.Infrastructure`):
`dotnet ef migrations add AddNoAnswerMessageToWorkspaceChatSettings --startup-project ../NexConvo.Chat.Api`
Verify the generated `Up` adds `no_answer_message` (text/varchar(500), not null, with the default). No custom SQL needed. Confirm the model snapshot updated.

- [ ] **Step 5: Fix the existing unit-test command factory & run**

Update the `NewCommand(...)` factory in `SaveChatSettingsCommandHandlerTests` and the `existing` entity constructions there to supply the new trailing arg (message string). Then:
Run: `dotnet build src/services/Chat/NexConvo.Chat.Api` then
`dotnet test tests/services/Chat/NexConvo.Chat.Application.UnitTests --filter FullyQualifiedName~SaveChatSettings`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/services/Chat tests/services/Chat/NexConvo.Chat.Application.UnitTests
git commit -m "feat(chat): add tenant-configurable NoAnswerMessage to WorkspaceChatSettings"
```

---

## Task 6: Wire gate + filter + fallback into WidgetHub and PlaygroundHub

**Files:**
- Modify: `src/services/Chat/NexConvo.Chat.Api/Realtime/WidgetHub.cs`
- Modify: `src/services/Chat/NexConvo.Chat.Api/Realtime/PlaygroundHub.cs`
- Modify: DI registration (the file that registers Chat Application services — add `IGroundingGate`/`IAbstentionStreamFilter`)
- Test: `tests/services/Chat/NexConvo.Chat.Application.UnitTests/Rag/*` already cover the units; hub wiring is covered by the live E2E in Task 8 (hubs need SignalR context, so no new unit test here — this is an integration/wiring task).

**Interfaces:**
- Consumes: `IGroundingGate` (Task 2), `IAbstentionStreamFilter` (Task 3), `WorkspaceChatSettings.NoAnswerMessage` (Task 5).

- [ ] **Step 1: Register the two services in DI**

In `src/services/Chat/NexConvo.Chat.Application/DependencyInjection.cs` (after `AddValidatorsFromAssembly`), add:
```csharp
services.AddSingleton<IGroundingGate, GroundingGate>();
services.AddSingleton<IAbstentionStreamFilter, AbstentionStreamFilter>();
```
Add `using NexConvo.Chat.Application.Rag;` if needed.

- [ ] **Step 2: Inject the two services + gate the WidgetHub**

Add `IGroundingGate groundingGate, IAbstentionStreamFilter abstentionFilter` to the `WidgetHub` primary constructor. After retrieval (`var matches = await knowledge.SearchAsync(...)`), and after loading `chatSettings` (so `NoAnswerMessage` is available), insert the gate BEFORE building prompts / calling the provider:

```csharp
var noAnswerMessage = chatSettings?.NoAnswerMessage
    ?? "Sorry, I don't have information about that. Please contact our support team for help.";

if (!groundingGate.ShouldAnswer(matches, ChannelProfile.Chat))
{
    logger.LogInformation("Widget query below grounding gate — returning fallback. Tenant={TenantId}", tenantId);
    await Clients.Caller.SendAsync(ReceiveToken, noAnswerMessage, Context.ConnectionAborted);
    await Clients.Caller.SendAsync(ReceiveCompleted, Context.ConnectionAborted);
    return;
}
```

(Move the `chatSettings` load up so it precedes the gate if it isn't already.)

- [ ] **Step 3: Route WidgetHub streaming through the filter**

Replace the `await foreach (var chunk in provider.GenerateStreamAsync(...))` loop body so tokens flow through the filter; on abstain, discard and send the fallback:

The provider yields `AiStreamChunk` (with `.Content`), but the filter takes `IAsyncEnumerable<string>`. `System.Linq.Async` is NOT referenced in this repo, so do NOT use `.Select(...)` over `IAsyncEnumerable`. Instead add a small local adapter method to the hub:

```csharp
private static async IAsyncEnumerable<string> ContentOf(
    IAsyncEnumerable<AiStreamChunk> chunks,
    [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
{
    await foreach (var c in chunks.WithCancellation(ct))
        yield return c.Content;
}
```

Then the streaming block becomes:

```csharp
var tokenStream = ContentOf(
    provider.GenerateStreamAsync(userPrompt, systemPrompt, apiKey, aiConfig.DefaultModel, aiConfig.BaseUrl, Context.ConnectionAborted),
    Context.ConnectionAborted);

var abstained = false;
await foreach (var result in abstentionFilter.FilterAsync(tokenStream, Context.ConnectionAborted))
{
    if (result.Abstained) { abstained = true; break; }
    if (!string.IsNullOrEmpty(result.Token))
        await Clients.Caller.SendAsync(ReceiveToken, result.Token, Context.ConnectionAborted);
}

if (abstained)
{
    await Clients.Caller.SendAsync(ReceiveToken, noAnswerMessage, Context.ConnectionAborted);
}
await Clients.Caller.SendAsync(ReceiveCompleted, Context.ConnectionAborted);
```

(`AiStreamChunk` is the type already returned by `provider.GenerateStreamAsync` in the current hub loop — confirm its namespace `using` is already present.)

- [ ] **Step 4: Apply the same gate + filter to PlaygroundHub**

In `PlaygroundHub`, after its `matches` retrieval and before the LLM loop (around line 151), add the same gate (send `ReceiveToken` fallback + the hub's completion/debug envelope, then `return`). Wrap its `GenerateStreamAsync` loop with `abstentionFilter.FilterAsync` exactly as above, replacing the raw `SendAsync(ReceiveToken, chunk.Content)`. The Playground has no human handoff — it only shows the fallback. Keep its existing debug-data assembly, but base `abstained` on the filter result instead of the post-hoc `replyText.Contains(...)`.

- [ ] **Step 5: Build & smoke-run**

Run: `dotnet build src/services/Chat/NexConvo.Chat.Api`
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/services/Chat/NexConvo.Chat.Api src/services/Chat/NexConvo.Chat.Application
git commit -m "feat(chat): gate + abstention-filter widget & playground streaming (never answer outside KB)"
```

---

## Task 7: Gate + fallback in ReplyOrchestrator (message path + handoff)

**Files:**
- Modify: `src/services/Chat/NexConvo.Chat.Application/Rag/ReplyOrchestrator.cs`
- Test: `tests/services/Chat/NexConvo.Chat.Application.UnitTests/Rag/ReplyOrchestratorGateTests.cs` (create) OR extend existing orchestrator tests if present.

**Interfaces:**
- Consumes: `IGroundingGate` (Task 2), existing `HandoffAsync`, `WorkspaceChatSettings.NoAnswerMessage`.

- [ ] **Step 1: Write the failing test**

Test that when retrieval is below the gate, the orchestrator hands off (LowConfidence) and never calls the provider. Use NSubstitute: substitute `IKnowledgeRetrievalClient` to return a single low-score match, substitute `IAiProviderFactory`/provider, and assert `provider.GenerateStreamAsync(...)` received **no** calls and a handoff/escalation was produced. Mirror the setup style of the existing orchestrator test if one exists (search `tests/services/Chat` for `ReplyOrchestrator`).

```csharp
[Fact]
public async Task WhenRetrievalBelowGate_HandsOff_AndNeverCallsProvider()
{
    // Arrange: retrieval returns a below-gate match; provider must never be invoked.
    // (Wire substitutes per the existing orchestrator test harness.)
    // Act: run the orchestrator's handle path.
    // Assert:
    provider.DidNotReceive().GenerateStreamAsync(
        Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    // and an Escalation with EscalationReason.LowConfidence was created.
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/services/Chat/NexConvo.Chat.Application.UnitTests --filter FullyQualifiedName~ReplyOrchestratorGateTests`
Expected: FAIL — orchestrator still calls the provider regardless of score.

- [ ] **Step 3: Add the gate to the orchestrator**

Inject `IGroundingGate` into `ReplyOrchestrator`. After retrieval produces `matches` and before decrypting the key / calling the provider, add:

```csharp
if (!groundingGate.ShouldAnswer(matches, ChannelProfile.Chat))
{
    // Below the grounding gate: never call the model. Hand off to a human where the workspace
    // supports it; the handoff itself surfaces the tenant's NoAnswerMessage to the visitor.
    return await HandoffAsync(db, conversation, EscalationReason.LowConfidence, cancellationToken);
}
```

Keep the existing post-buffer `abstained` check as a backstop for the case where the gate passes but the model still emits the marker.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/services/Chat/NexConvo.Chat.Application.UnitTests --filter FullyQualifiedName~ReplyOrchestrator`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/services/Chat/NexConvo.Chat.Application/Rag/ReplyOrchestrator.cs tests/services/Chat/NexConvo.Chat.Application.UnitTests/Rag/ReplyOrchestratorGateTests.cs
git commit -m "feat(chat): gate ReplyOrchestrator — hand off instead of answering outside the KB"
```

---

## Task 8: Measure retrieval scores and set `AnswerGateScore`

**Files:**
- Modify (temporarily): `src/services/Chat/NexConvo.Chat.Application/Rag/GroundingGate.cs` (add a debug log)
- Modify (final): `src/shared/NexConvo.BuildingBlocks.Rag/ChannelProfile.cs`

- [ ] **Step 1: Add a temporary debug log to the gate**

In `GroundingGate.ShouldAnswer`, before the return, add (inject `ILogger<GroundingGate>` for this measurement; remove after):
```csharp
// TEMP (Task 8 measurement): logs the deciding score so we can set AnswerGateScore from data.
logger.LogInformation("GroundingGate topScore={TopScore} gate={Gate} count={Count}",
    matches.Count > 0 ? matches.Max(m => m.Score) : 0d, profile.AnswerGateScore, matches.Count);
```

- [ ] **Step 2: Bring up the stack and drive real queries**

Start the stack (`docker compose --profile apps up -d postgres redis rabbitmq chat gateway identity integrations knowledge`). Using the test account (bioxin) and a tenant with a real KB, send via the widget hub (or playground):
- An **in-KB** question (e.g. "How long do refunds take?") — record `topScore`.
- An **out-of-KB** question (e.g. "What is the capital of France?") — record `topScore`.
Capture from `docker logs nexconvo-chat-1 | grep GroundingGate`.

- [ ] **Step 3: Set `AnswerGateScore` between the clusters**

Pick a value strictly above the highest observed out-of-KB score and at/below the lowest in-KB score (for `ChannelProfile.Chat`). If they overlap, prefer the stricter side (favor abstention) and note it. Update `ChannelProfile.Chat` (and `Voice` proportionally). Update the `ChannelProfileTests` expectation if you asserted a specific number.

- [ ] **Step 4: Remove the temporary log**

Revert the debug log and the `ILogger` injection from `GroundingGate` (keep it pure).

- [ ] **Step 5: Commit**

```bash
git add src/shared/NexConvo.BuildingBlocks.Rag/ChannelProfile.cs src/services/Chat/NexConvo.Chat.Application/Rag/GroundingGate.cs
git commit -m "chore(rag): set AnswerGateScore from measured retrieval scores"
```

---

## Task 9: Frontend — expose `NoAnswerMessage` in settings

**Files:**
- Modify: `frontend/src/features/settings/model/chat-settings.schema.ts` (+ `.test.ts`)
- Modify: `frontend/src/features/settings/model/chat-settings.types.ts`
- Modify: `frontend/src/features/settings/components/chat-widget-form.tsx`
- Modify: `frontend/src/shared/i18n/messages/en.json`, `bn.json`
- Modify: `frontend/src/shared/api/server/e2e-fixtures.ts`, `frontend/src/tests/msw/handlers/chat-settings.ts`, and the two hook test fixtures (add `noAnswerMessage`).

**Interfaces:**
- Consumes: backend DTO `NoAnswerMessage` (Task 5).

- [ ] **Step 1: Write the failing schema test**

In `chat-settings.schema.test.ts`, extend the `valid` fixture with `noAnswerMessage: 'We don\'t have that info; please contact support.'` and add:

```ts
it('rejects an empty no-answer message', () => {
  const result = chatSettingsSchema.safeParse({ ...valid, noAnswerMessage: '' });
  expect(result.success).toBe(false);
  if (!result.success) {
    expect(result.error.issues.map((i) => i.message)).toContain('noAnswerRequired');
  }
});

it('rejects a no-answer message over 500 chars', () => {
  const result = chatSettingsSchema.safeParse({ ...valid, noAnswerMessage: 'a'.repeat(501) });
  expect(result.success).toBe(false);
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx vitest run --pool=forks --poolOptions.forks.singleFork=true src/features/settings/model/chat-settings.schema.test.ts`
Expected: FAIL — schema has no `noAnswerMessage`.

- [ ] **Step 3: Add to schema, types, form, i18n**

`chat-settings.schema.ts`: add
```ts
noAnswerMessage: z.string().min(1, 'noAnswerRequired').max(500, 'noAnswerTooLong'),
```
`chat-settings.types.ts`: add `noAnswerMessage: string;` to `WorkspaceChatSettingsDto`.
`chat-widget-form.tsx`: add a `Field` for `noAnswerMessage` (label `t('noAnswerLabel')`, register, `aria-describedby` on error) and include it in `defaultValues`.
`en.json` widget block: add `"noAnswerLabel": "Message when no answer is found"`, `"noAnswerPlaceholder": "Sorry, I don't have information about that…"`, and errors `"noAnswerRequired"`, `"noAnswerTooLong"`. `bn.json`: Bengali equivalents.
Fixtures (`e2e-fixtures.ts`, `msw/handlers/chat-settings.ts`, both hook test fixtures): add `noAnswerMessage: 'Sorry, I don\'t have information about that. Please contact our support team for help.'`.

- [ ] **Step 4: Run schema test + typecheck**

Run: `cd frontend && npx vitest run --pool=forks --poolOptions.forks.singleFork=true src/features/settings/model/chat-settings.schema.test.ts`
Then: `cd frontend && NODE_OPTIONS="--max-old-space-size=6144" npx tsc --noEmit`
Expected: tests PASS, 0 TS errors.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/features/settings frontend/src/shared/i18n frontend/src/shared/api/server/e2e-fixtures.ts frontend/src/tests/msw/handlers/chat-settings.ts
git commit -m "feat(settings): tenant-configurable no-answer message in chat widget settings"
```

---

## Task 10: Full verification (automated + live E2E)

**Files:** none (verification only).

- [ ] **Step 1: Run the full backend test suites**

Run:
`dotnet test tests/shared/NexConvo.BuildingBlocks.Rag.Tests`
`dotnet test tests/services/Chat/NexConvo.Chat.Application.UnitTests`
`dotnet test tests/services/Chat/NexConvo.Chat.Infrastructure.Tests`
Expected: all green.

- [ ] **Step 2: Frontend tests + typecheck**

Run: `cd frontend && npx vitest run --pool=forks --poolOptions.forks.singleFork=true src/features/settings` and `NODE_OPTIONS="--max-old-space-size=6144" npx tsc --noEmit`
Expected: green, 0 TS errors.

- [ ] **Step 3: Live E2E — the acceptance criterion**

Rebuild + restart chat, gateway (`docker compose --profile apps build chat gateway && docker compose --profile apps up -d chat gateway`). With the bioxin account's real OpenRouter/DeepSeek config cached and a tenant with a real KB, drive the widget hub (real SignalR client via the gateway):
- **Out-of-KB** ("What is the capital of France?") → the tenant's `NoAnswerMessage`, **nothing** from world knowledge, no fabricated citation. Confirm in `docker logs nexconvo-chat-1` that the provider was NOT called (gate short-circuit) — no `POST https://openrouter.ai/...` line for that request.
- **In-KB** ("How long do refunds take?") → an answer derived **only** from the KB chunk (no invented "5-10 days" / "Help Center").
- A borderline/weak question → fallback, not a wandering answer.

- [ ] **Step 4: Commit any final tuning**

If Step 3 shows the gate is mis-tuned, adjust `AnswerGateScore` (Task 8) and re-run. Commit.

```bash
git add -A
git commit -m "test(rag): verify grounding — never answers outside the KB (live E2E)"
```

---

## Self-Review Notes

- **Spec coverage:** Layer 1 gate → Tasks 2,6,7; Layer 2 strict prompt → Task 4; streaming filter → Tasks 3,6; `AnswerGateScore` (measured) → Tasks 1,8; `NoAnswerMessage` tenant-configurable → Tasks 5,9; A+C (fallback + handoff) → Tasks 6 (fallback), 7 (handoff); tests + live E2E → Task 10. All covered.
- **Types consistent:** `IGroundingGate.ShouldAnswer(IReadOnlyList<KnowledgeChunkMatch>, ChannelProfile)`, `IAbstentionStreamFilter.FilterAsync(IAsyncEnumerable<string>, CancellationToken) → IAsyncEnumerable<AbstentionResult>` with `AbstentionResult(string? Token, bool Abstained)` — used identically in Tasks 3, 6, 7.
- **Fallback default string** is identical everywhere: `"Sorry, I don't have information about that. Please contact our support team for help."`
