# NexConvo — RAG Slice 4: Retrieval + gRPC `SearchKnowledge` (channel-aware)

> Paste this entire prompt into a new Claude Code session at the NexConvo project root.
> This is **Slice 4 of 6** (see `docs/prompt/rag/README.md`). **Prerequisites: Slices 1–3 complete** (the Knowledge service holds real 1024-dim chunks).

---

## Your Role — Senior Search & Service-Interface Engineer

You are a **Senior .NET engineer** specializing in vector search and typed service interfaces, with 10+ years across pgvector, gRPC, and multi-tenant isolation. You know retrieval is where RAG lives or dies, and that a **shared** retrieval capability must be exposed as a clean, fast, typed contract — because it will be called by **both the Chat and the Voice bot**, and Voice has a <700 ms budget. You expose retrieval over **gRPC** (low-latency, typed), embed the query *inside* the service (callers send text, never vectors), enforce tenant isolation via RLS on propagated context, and put the channel-tunable knobs (`topK`, `minScore`) on the request so each channel picks its own trade-off. You design the seam so hybrid search + reranking can be added later without touching callers.

**Before writing any code**, invoke and follow:
1. `nexconvo-enterprise-standards`
2. superpowers `test-driven-development`

**First, read these files completely:**
- `docs/CHATBOT-ARCHITECTURE.md` — §7 RAG Reply Pipeline (retrieval portion), §12 pgvector Specifics / per-tenant vector search
- `docs/ARCHITECTURE.md` — internal comms (gRPC) + tenant/correlation propagation
- `CLAUDE.md`
- In the **Knowledge service**: `KnowledgeBaseController.cs` (stub List/GetById), `KnowledgeChunkConfiguration.cs` (shadow `vector(1024)` mapping), `IKnowledgeDbContext`
- From Slice 1: `IEmbeddingProviderService` (embed the query with `EmbeddingInputType.Query`)
- The RLS interceptor + `ITenantContext`; note there are **no `.proto` files yet** in the repo — you are introducing the first gRPC contract.

---

## Context — what exists and what is missing

| Item | State |
|---|---|
| `knowledge_chunks` with 1024-dim embeddings + HNSW cosine index | ✅ Populated (Slice 3) |
| Any **similarity search** / read of the embedding column | ❌ **None** |
| **gRPC** anywhere in the codebase | ❌ **None** — this is the first `.proto` |
| `KnowledgeBaseController` **List** / **GetById** | ⚠️ **Stubs** — empty / 404 |
| Richer DB columns (source_type, title, chunk_count, failure_reason, version) | ✅ in table, ❌ ignored by DTOs |

## Prerequisites
- **Slices 1–3 done.** A seeded knowledge base with real chunk vectors.

---

## What to build (all in the **Knowledge service**)

### 1. `IKnowledgeChunkRepository.SimilaritySearchAsync` (Application interface, Infrastructure impl)
```csharp
Task<IReadOnlyList<ChunkMatch>> SimilaritySearchAsync(
    float[] queryEmbedding, int topK, double minScore, CancellationToken ct);
// ChunkMatch: chunkId, documentId, content, score (cosine similarity 0..1)
```
- Implement with pgvector cosine distance (`embedding <=> @q`) over the HNSW index, ascending, `LIMIT topK`, active chunks only; convert distance → similarity and drop below `minScore`.
- **RLS scopes it to the tenant** — do not add a tenant filter to the SQL; assert isolation in tests.
- Ensure the query rides the HNSW index (parameterized vector, ORDER BY on the indexed operator).

### 2. gRPC service — `KnowledgeRetrieval` (the shared, cross-channel seam)
- Add the first `.proto` (e.g. `Protos/knowledge.proto`) defining:
  ```proto
  service KnowledgeRetrieval {
    rpc Search (SearchRequest) returns (SearchReply);
  }
  message SearchRequest { string query = 1; int32 top_k = 2; double min_score = 3; }
  message SearchReply   { repeated Chunk chunks = 1; }
  message Chunk { string chunk_id = 1; string document_id = 2; string content = 3; double score = 4; }
  ```
- The gRPC service handler: **embed the `query` text** via `IEmbeddingProviderService.EmbedAsync(text, EmbeddingInputType.Query, ct)` (callers send **text**, not vectors), then call the repository, then map to `SearchReply`.
- `top_k` / `min_score` come from the **caller's `ChannelProfile`** (Chat sends larger `top_k`; Voice sends smaller for its <700 ms budget) — this is the channel-tuning knob. Provide sane server-side caps.
- **Tenant + correlation propagation:** the calling service passes `tenant_id` + `traceparent` via **gRPC metadata**; a server interceptor reads them, sets `app.current_tenant_id` (RLS) and the `CorrelationId` on the log context. This endpoint is **internal service-to-service only** — authenticate it, do not expose it through the public gateway.

### 3. Real REST management queries (for the UI)
- Replace the controller stubs with real handlers:
  - **List:** paginated, **capped page size ≤ 100**, `total` (Standard 17); filter by status/source type.
  - **GetById:** detail + chunk preview (paginated) + version history; **404** only for another tenant's doc.
- Surface the richer columns (source_type, title, chunk_count, failure_reason, version) into DTOs the frontend already expects.

### 4. Leave a clean extension point (do NOT build)
- Structure the repository so **hybrid search (pgvector + `tsvector` + RRF)** and **cross-encoder reranking** slot in later (P1) without changing the gRPC contract or callers.
- **MCP boundary (P3):** retrieval serves **static/unstructured knowledge only**. Dynamic/transactional data (discounts, prices, stock, order status) is **not** retrieval's job — it comes live via MCP tools in P3. Do not add live-lookup logic here.

---

## Reuse — study these patterns first
- `KnowledgeBaseController.cs` (replace List/GetById), `KnowledgeChunkConfiguration` shadow mapping + `Pgvector` `<=>` operators.
- The RLS interceptor + `ITenantContext` for both HTTP and the new gRPC metadata path.
- Existing paginated query handlers for the List shape.

## Out of scope (later slices)
Grounded generation + conversations (Slice 5), streaming (Slice 6), hybrid/rerank (P1). Voice will call this same gRPC later — you are building the contract now, not the voice caller.

---

## Tests first (TDD)
1. **RLS isolation:** seed chunks for tenant A and B → `SimilaritySearchAsync`/`Search` scoped to A **never** returns B's chunks.
2. **gRPC round-trip:** a `Search` call with query text + `top_k` returns ranked chunks; tenant taken from metadata, not the body.
3. **Ranking + threshold:** a known query ranks the expected chunk top-k; `min_score` filters weak matches.
4. **Channel tuning:** a small `top_k` returns fewer chunks (voice profile), a larger `top_k` more (chat profile).
5. **List/GetById:** paginate + cap page size; 404 for another tenant's doc.

## Non-Negotiables
- gRPC service is a thin dispatch layer — no `DbContext` in it; it calls Application/MediatR.
- RLS enforced via propagated metadata (asserted by test), never a body-supplied tenant.
- Polly on the embedding call inside `Search`; pagination on List; structured logging (no query PII); the gRPC endpoint is internal + authenticated (not public-gateway-exposed).

## Verification / report
1. Seed a small KB (Slice 3). From a gRPC test client, `Search("refund policy", top_k=5)` → the refund chunk ranks top-1; a Bengali query works too.
2. Call with `top_k=3` (voice profile) vs `top_k=8` (chat profile) → counts differ; latency for `top_k=3` is well within budget.
3. Load the frontend knowledge list/detail against the real backend (no mocks).
4. Report: the `.proto` contract, the metadata/tenant-propagation interceptor, the retrieval SQL, test pass count, RLS-isolation confirmation.
