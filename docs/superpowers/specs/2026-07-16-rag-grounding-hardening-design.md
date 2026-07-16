# RAG Grounding Hardening — Design

**Date:** 2026-07-16
**Status:** Approved (design), pending implementation plan

## Context

Live end-to-end testing of the web chat widget (real DeepSeek via OpenRouter, against a
tenant with a real knowledge base) revealed that the RAG pipeline **answers from outside the
knowledge base**:

- **"What is the capital of France?"** (not in the KB) → `"The capital of France is Paris [1]."`
  — the model answered from world knowledge **and fabricated a `[1]` citation**.
- **"How long do refunds take?"** (KB says *"within 5 business days"*) → `"Refunds typically take
  5-10 business days … depending on your bank or payment provider [1]. … submit a request via the
  Help Center [1]."` — grounded on the chunk but **embellished with invented details**.

This is **platform-wide**, not widget-specific: `WidgetHub`, `PlaygroundHub`, and
`ReplyOrchestrator` all use the same `GroundedPromptAssembler` + `ChannelProfile.Chat`. The widget
remediation work did not touch the prompt or retrieval.

**Hard requirement (from the user):** the assistant must **never** answer from outside the knowledge
base. Prompt instructions alone are insufficient (that is exactly what is failing) — enforcement must
be at the code level.

### Root-cause gaps in the current design

1. **No pre-generation gate.** `BuildUserPrompt` prints `Context:` only when `context.Count > 0`;
   when retrieval returns nothing (or everything is filtered), the prompt is just
   `Question: …` and the model answers from world knowledge. Nothing structurally forces abstention.
2. **Streaming leaks raw output.** `WidgetHub`/`PlaygroundHub` stream token-by-token with **no
   abstention handling** — the user sees `[[NO_ANSWER]]` or a half-baked answer before any check.
   (`ReplyOrchestrator` buffers the full reply and checks the marker, but the streaming hubs do not.)
3. **Weak-but-present context still invites drift.** A single retrieval threshold (`MinScore: 0.55`)
   governs both "which chunk is retrieved" and "should we answer" — a weak chunk that squeaks past
   0.55 still gets fed to the model, which then wanders.

## Decisions (confirmed with the user)

1. **On no / weak retrieval: A + C** — do **not** call the LLM; return a safe fallback message (A),
   and hand off to a human where one is available (C, via the existing `HandoffAsync`).
2. **Two-layer enforcement (#1)** — a code-level pre-generation gate for the hard guarantee, plus a
   stricter prompt to reduce drift when context *is* present. No post-generation LLM verification
   (rejected #2 for latency/cost; rejected #3 prompt-only as insufficient).
3. **Separate answer-gate threshold** — a new per-channel `AnswerGateScore`, distinct from
   retrieval's `MinScore`; the **actual number is measured from live retrieval scores**, not guessed.
4. **Tenant-configurable fallback** — a new `NoAnswerMessage` on `WorkspaceChatSettings` (en+bn
   default), following the existing `SystemPromptOverride` / `WidgetWelcomeMessage` pattern.
5. **Streaming: leading buffer** — buffer the first ~30 chars to detect the abstention marker before
   any token reaches the user; suppress + fall back on match, otherwise release and stream normally.

## Architecture

Two layers of defense, applied identically across all three RAG paths via shared building blocks:

**Layer 1 — Pre-generation retrieval gate (hard guarantee).**
Before any LLM call, check whether the top retrieval score clears `AnswerGateScore`. If not (or zero
chunks), the LLM is **never called** → fallback message + handoff (where applicable). The model gets
no opportunity to answer from outside the KB.

**Layer 2 — Strict prompt + streaming abstention filter (drift reduction).**
When the gate passes, the LLM is called with a stricter prompt (context-only, outside knowledge
forbidden, no invented citations). Streaming output passes through a leading-buffer filter that
detects `[[NO_ANSWER]]` before the user sees anything and swaps in the fallback on match.

```
user message
  → knowledge.SearchAsync(tenantId, msg, TopK, MinScore)      // retrieval unchanged
  → GroundingGate.ShouldAnswer(matches, profile)?
       ├─ NO  → send NoAnswerMessage + (ReplyOrchestrator) HandoffAsync    // no LLM call
       └─ YES → strict system prompt + numbered context
              → provider.GenerateStreamAsync(...)
              → AbstentionStreamFilter:
                    marker found → drop buffered, send NoAnswerMessage (+handoff)
                    no marker    → release buffer, stream tokens normally
```

## Components

### New shared building blocks (`NexConvo.BuildingBlocks.Rag`)

- **`ChannelProfile.AnswerGateScore`** (new field, per channel) — the minimum top-retrieval score
  required to call the LLM at all. Distinct from `MinScore` (which filters individual chunks).
- **`IGroundingGate` / `GroundingGate`** — a small, pure, testable unit operating on the retrieved
  `KnowledgeChunkMatch` list (`record KnowledgeChunkMatch(string ChunkId, string DocumentId, string
  Content, double Score)` from `IKnowledgeRetrievalClient`):
  `bool ShouldAnswer(IReadOnlyList<KnowledgeChunkMatch> matches, ChannelProfile profile)` →
  `matches.Count > 0 && matches.Max(m => m.Score) >= profile.AnswerGateScore`. Reused by all three
  call-sites (no duplicated logic). Lives in the Chat Application layer (where `KnowledgeChunkMatch`
  and `IKnowledgeRetrievalClient` live); `ChannelProfile`/`AnswerGateScore` stay in BuildingBlocks.Rag.
- **`IAbstentionStreamFilter` / `AbstentionStreamFilter`** — wraps a token stream; buffers the
  leading window (~30 chars, ≥ marker length), scans for `AbstentionMarker`; on match suppresses the
  whole stream and signals abstention; otherwise releases the buffer and passes tokens through.
  Handles the marker being split across chunk boundaries.
- **`GroundedPromptAssembler.BuildSystemPrompt`** — hardened wording: only the numbered context may
  be used; outside/prior knowledge is forbidden; if the context does not fully answer, emit
  `[[NO_ANSWER]]`; never invent a citation.

### Data / config

- **`WorkspaceChatSettings.NoAnswerMessage`** (new, `string`, non-null, sensible default) — EF config
  snake_case (`no_answer_message`), migration, validator length cap. Read by the hubs/orchestrator
  when the gate fails or the filter detects abstention. Not part of `CachedAiConfig`.
- **i18n** — default no-answer copy in `en.json` + `bn.json`; a field in the chat-widget settings
  form so tenants can override it (following `WidgetWelcomeMessage`).

### Call-site changes (same pattern in three places)

- **`WidgetHub.SendMessageAsync`** — after retrieval, `GroundingGate.ShouldAnswer`? If no → send
  `NoAnswerMessage`, skip the LLM. If yes → strict prompt, stream through `AbstentionStreamFilter`.
- **`PlaygroundHub`** — same gate + filter (no handoff in the playground; just shows the fallback).
- **`ReplyOrchestrator`** — same gate; on gate-fail or abstention → existing `HandoffAsync` where a
  human is available, else `NoAnswerMessage`. Keeps its existing post-buffer marker check as a
  backstop.

## Threshold measurement (not guessed)

Temporarily `LogDebug` the actual top retrieval score inside `GroundingGate`, then run against the
live stack with a real KB:

- Questions **in** the KB (e.g. refunds) → record the top score.
- Questions **not** in the KB (e.g. France) → record the top score.
- Set `AnswerGateScore` between the two clusters (for chat/widget; voice may differ). Fix the number
  from observed data, then remove the debug log.

## Testing (TDD — Standard 4 / 20)

- **`GroundingGate`** (unit): score < gate → false; ≥ gate → true; empty matches → false.
- **`AbstentionStreamFilter`** (unit): stream containing the marker → fully suppressed + abstain
  signal; stream without it → passthrough byte-for-byte; marker split across the buffer boundary →
  still detected.
- **`SaveChatSettingsCommandValidator`** (unit): `NoAnswerMessage` length bound.
- **Integration** (real Postgres): gate-fail path returns the fallback and makes **no** provider call
  (verified with a mock provider that fails the test if invoked).
- **Live E2E** (real DeepSeek/OpenRouter, real KB): "capital of France" → fallback (nothing from
  outside the KB); "how long do refunds take" → answer derived **only** from the KB chunk (no
  invented "5-10 days" / "Help Center", no fabricated citation).

## Error handling

- Gate or filter throws → **fail-safe to abstain** (show fallback; never stream an unverified answer).
- Retrieval itself fails (Knowledge service down) → fallback + handoff; the LLM is **not** called.

## Out of scope (YAGNI)

- Post-generation LLM verification of every answer (rejected #2).
- Changes to the embedding model or retrieval algorithm.
- Per-channel voice tuning beyond wiring `AnswerGateScore` (same mechanism; number tuned later).

## Files (representative)

- `src/shared/NexConvo.BuildingBlocks.Rag/ChannelProfile.cs` — add `AnswerGateScore`.
- `src/services/Chat/NexConvo.Chat.Application/Rag/GroundingGate.cs` + `IGroundingGate.cs` (new;
  operates on `KnowledgeChunkMatch`).
- `src/services/Chat/NexConvo.Chat.Application/Rag/AbstentionStreamFilter.cs` + interface (new).
- `src/shared/NexConvo.BuildingBlocks.Rag/GroundedPromptAssembler.cs` — hardened prompt.
- `src/services/Chat/NexConvo.Chat.Domain/Entities/WorkspaceChatSettings.cs` + EF config + migration.
- `src/services/Chat/.../Realtime/WidgetHub.cs`, `PlaygroundHub.cs`,
  `.../Rag/ReplyOrchestrator.cs` — gate + filter + fallback wiring.
- `src/services/Chat/.../ChatSettings/Commands/SaveChatSettingsCommand{,Handler,Validator}.cs` +
  request/DTO — carry `NoAnswerMessage`.
- `frontend/src/features/settings/...` (form, schema, types, i18n) — expose `NoAnswerMessage`.
- Tests alongside each (unit + integration), plus a live E2E pass.
