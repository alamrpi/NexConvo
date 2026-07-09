# NexConvo — RAG Slice 1: Embedding Provider Layer

> Paste this entire prompt into a new Claude Code session at the NexConvo project root.
> This is **Slice 1 of 6** in the RAG knowledge-base build (see `docs/prompt/rag/README.md`). It has **no prerequisite slices** — start here.

---

## Your Role — Senior AI Platform Engineer

You are a **Senior .NET AI-platform engineer** with 10+ years building production LLM and vector infrastructure. You have integrated OpenAI-compatible inference servers, self-hosted embedding models, and managed embedding APIs behind clean, swappable abstractions. You know that an embedding model is the foundation of retrieval quality — a weak or misconfigured embedder silently poisons every downstream answer, so you are meticulous about model choice, asymmetric query/document encoding, batching, and resilience. You write provider code that is stateless, testable against a mocked transport, and identical in shape to the existing AI building block so the next engineer feels at home.

**Before writing any code**, invoke these skills and follow them:
1. `nexconvo-enterprise-standards` (applies to every class)
2. superpowers `brainstorming` (confirm the interface shape before building)
3. superpowers `test-driven-development` (failing test first, always)

**First, read these files completely:**
- `docs/ARCHITECTURE.md`
- `docs/CHATBOT-ARCHITECTURE.md` — §11 Embeddings, §12 pgvector Specifics
- `CLAUDE.md`
- `src/shared/NexConvo.BuildingBlocks.Ai/Services/IAiProviderService.cs` — the sibling interface you mirror
- `src/shared/NexConvo.BuildingBlocks.Ai/Services/OpenAiCompatibleProviderService.cs` — the SSE/HTTP client pattern
- `src/shared/NexConvo.BuildingBlocks.Ai/Services/AiProviderFactory.cs` + `IAiProviderFactory.cs` — the keyed-service factory pattern
- `src/shared/NexConvo.BuildingBlocks.Ai/DependencyInjection.cs` — `AddAiProviders()` + `.AddStandardResilienceHandler()` (Polly) + `ITokenUsageLogger`
- `docker-compose.yml` — how existing services (postgres/redis/rabbitmq) are declared and wired into service env

---

## Context — what exists and what is missing

| Item | State |
|---|---|
| Multi-provider **chat streaming** (`IAiProviderService.GenerateStreamAsync`, OpenRouter/OpenAI/Anthropic/Gemini/DeepSeek) with Polly | ✅ Real — mirror this pattern |
| **Embeddings capability** anywhere in the codebase | ❌ **Absent** — no `EmbedAsync`, no embeddings endpoint call |
| `AddAiProviders()` registration | ✅ Real (used by Integrations for connection testing) |

This slice adds the **embeddings** counterpart to the existing chat-provider layer. It is pure infrastructure — no database, no chunking, no retrieval. It lives in the shared `BuildingBlocks.Ai` so it can be consumed by the **Knowledge service** (Slice 2 onward) for both ingestion and query embedding — the knowledge base is a shared, cross-channel capability (Chat + Voice), so its embedder must be shared too.

**Locked decision:** default provider **BGE-M3** (self-hosted Docker "Infinity" server), **Cohere** as an env-switchable alternative, both **1024-dim**, selected via `EMBEDDING__PROVIDER`.

---

## Prerequisites

- None. This is the first slice.
- You will add a Docker service; Docker Desktop must be available to verify.

---

## What to build (all in `src/shared/NexConvo.BuildingBlocks.Ai/`)

### 1. The abstraction — `Services/IEmbeddingProviderService.cs`
```csharp
public enum EmbeddingInputType { Query, Document }   // asymmetric encoding — DO NOT skip

public interface IEmbeddingProviderService
{
    int Dimensions { get; }   // 1024
    Task<float[]> EmbedAsync(string text, EmbeddingInputType inputType, CancellationToken ct);
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, EmbeddingInputType inputType, CancellationToken ct);
}
```
`EmbeddingInputType` is load-bearing: BGE-M3 wants a query instruction prefix for `Query`; Cohere wants `input_type=search_query` vs `search_document`. Encoding a query as a document silently degrades retrieval — enforce it.

### 2. `Services/BgeM3EmbeddingProviderService.cs` (default)
- Calls the Infinity server's OpenAI-compatible `POST {baseUrl}/embeddings` with `{ model: "BAAI/bge-m3", input: [...] }`.
- Base URL from env: `EMBEDDING__BGEM3__BASEURL` (dev default `http://bge-m3:7997`).
- For `EmbeddingInputType.Query`, prepend BGE-M3's recommended retrieval instruction to each text; leave `Document` inputs raw.
- Parse `data[].embedding` → `float[]`; assert length == 1024.

### 3. `Services/CohereEmbeddingProviderService.cs` (alternative)
- Calls Cohere `POST https://api.cohere.com/v2/embed` with `model: "embed-multilingual-v3.0"`, `input_type: search_query|search_document` (mapped from `EmbeddingInputType`), `embedding_types: ["float"]`.
- API key from env `EMBEDDING__COHERE__APIKEY` (never hardcode, never log).

### 4. `Services/EmbeddingProviderFactory.cs` + `IEmbeddingProviderFactory.cs`
- Resolves the active provider from env `EMBEDDING__PROVIDER` (`BgeM3` default | `Cohere`). Mirror `AiProviderFactory`'s keyed-service resolution exactly.

### 5. `DependencyInjection.cs` — `AddEmbeddingProviders(this IServiceCollection, IConfiguration)`
- One named `HttpClient` per provider, each with `.AddStandardResilienceHandler()` (Polly retry + circuit breaker + timeout).
- Register providers as keyed transients; register the factory.
- Reuse `ITokenUsageLogger` to record embedding token usage where the provider returns it.

### 6. `docker-compose.yml` — add the embedding server
```yaml
  bge-m3:
    image: michaelf34/infinity:latest
    command: v2 --model-id BAAI/bge-m3 --port 7997
    ports:
      - "7997:7997"
    volumes:
      - bge_model_cache:/app/.cache
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:7997/health"]
      interval: 30s
      timeout: 5s
      retries: 5
```
- Add the `bge_model_cache` named volume.
- Set `EMBEDDING__PROVIDER=BgeM3` and `EMBEDDING__BGEM3__BASEURL=http://bge-m3:7997` in the **Knowledge service's** environment (the primary consumer, created in Slice 2) — and any other service that will embed.

### 7. Health visibility
- Contribute a `/health` check (or reuse the health-check registration pattern) so a down/unreachable embedding server is visible, not a silent 500 at ingestion time.

---

## Reuse — study these patterns first (do not reinvent)
- `OpenAiCompatibleProviderService.cs` — HTTP client construction, JSON shape, error handling.
- `AiProviderFactory.cs` / `IAiProviderFactory.cs` — keyed resolution.
- `DependencyInjection.AddAiProviders` — resilience + keyed registration + token-usage logger wiring.

## Out of scope (later slices)
Chunking, DB writes, similarity search, the schema migration to 1024 dims (that is Slice 2).

---

## Tests first (TDD)
1. **Factory** selects `BgeM3` by default and `Cohere` when `EMBEDDING__PROVIDER=Cohere`.
2. **BGE-M3 provider** — against a mocked `HttpMessageHandler`: sends the correct body, parses a 1024-length vector, and encodes `Query` differently from `Document` (assert the query instruction prefix is present only for `Query`).
3. **Cohere provider** — against a mock: maps `EmbeddingInputType` → `input_type` correctly; never includes the API key in logs.
4. **Guarded integration test** (skippable when the container isn't running): hits a live Infinity server, asserts `EmbedAsync` returns 1024 floats and that batch order is preserved.

## Non-Negotiables
- Constructor injection only; depend on abstractions.
- Polly on every provider `HttpClient`.
- Secrets (`EMBEDDING__COHERE__APIKEY`) from env only; never logged, never in `appsettings.json`.
- Structured logs only (`logger.LogInformation("Embedded {Count} texts with {Provider}", ...)`), no PII (never log the text being embedded).
- Every new project reference nests correctly in `NexConvo.sln` (Standard 20) — but this slice edits an existing project, so no new project is expected.

## Verification / report
1. `docker compose up bge-m3` and wait for healthy.
2. From a small test (or a scratch xUnit test), embed `"টাকা ফেরত"` (Bengali) and `"refund"` (English); print vector length (must be 1024) and confirm both succeed.
3. Flip `EMBEDDING__PROVIDER=Cohere` (with a key set) and confirm the factory switches with no code change.
4. Report: files created, test pass count, and confirmation that Query vs Document encoding differs.
