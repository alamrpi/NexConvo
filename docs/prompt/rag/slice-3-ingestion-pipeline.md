# NexConvo — RAG Slice 3: Real Ingestion Pipeline (Storage → Extract → Chunk → Embed)

> Paste this entire prompt into a new Claude Code session at the NexConvo project root.
> This is **Slice 3 of 6** (see `docs/prompt/rag/README.md`). **Prerequisites: Slices 1 & 2 complete** (shared embedding client + the **Knowledge service** with its 1024-dim schema and `IKnowledgeChunkWriter`).

---

## Your Role — Senior Document-Processing & Pipeline Engineer

You are a **Senior .NET engineer** with 10+ years building document-ingestion and data pipelines for enterprise SaaS. You have parsed PDFs, DOCX, and messy HTML at scale, and you know that chunking quality — not the embedding model — is where most RAG systems quietly fail. You respect language boundaries: for a Bengali-first product you split on the danda (`।`), not just the English period. You treat an uploaded document as the customer trusting the system with their business data, so ingestion is idempotent, auditable, and fails loudly with a real reason instead of half-writing garbage. You store what you receive (to S3) before you promise to process it.

**Before writing any code**, invoke and follow:
1. `nexconvo-enterprise-standards`
2. superpowers `brainstorming` (nail the chunking strategy + failure model first)
3. superpowers `test-driven-development`

**First, read these files completely:**
- `docs/CHATBOT-ARCHITECTURE.md` — §8 Knowledge Ingestion, §13 Persistence Schema, §14 Data Privacy
- `docs/ARCHITECTURE.md` — Background Jobs (Hangfire) + eventing sections
- `CLAUDE.md`
- In the **Knowledge service** (from Slice 2): `NexConvo.Knowledge.Infrastructure/Jobs/KnowledgeIngestionJob.cs` — the STUB you replace; `UploadKnowledgeDocumentCommandHandler.cs`; `KnowledgeDocument`/`KnowledgeChunk`; `IKnowledgeChunkWriter`
- The S3 pattern in Integrations: `src/services/Integrations/.../Domain/Entities/WorkspaceS3Config.cs` + `ExternalServices/S3ConnectionTester.cs` (+ `EnsureHealthy`, Standard 22)
- From Slice 1: `IEmbeddingProviderService` (in `BuildingBlocks.Ai`)

---

## Context — what exists and what is stubbed

| Item | State |
|---|---|
| Knowledge service + 1024 schema + `IKnowledgeChunkWriter` | ✅ Slice 2 |
| Shared embedding client | ✅ Slice 1 |
| Upload command + dedup + audit + enqueue | ✅ Real, but **metadata only** |
| Uploaded **file bytes** | ❌ **Discarded** — hashed then dropped; nothing stored to S3 |
| `KnowledgeIngestionJob` | ⚠️ **STUB** — `Pending→Processing→Task.Delay(5s)→Ready`, writes **0 chunks** |
| Text extraction / chunking / embedding | ❌ **None** |
| URL / Text-FAQ / Past-chats source types | ❌ **UI-only** — the command has no fields for them |

## Prerequisites
- **Slices 1 & 2 done.** `docker compose up bge-m3` running so embeddings resolve; the Knowledge service boots + migrates.

---

## What to build (all in the **Knowledge service**, `src/services/Knowledge/`)

### 1. Persist the file to S3 (before enqueue)
- On upload, write the raw bytes to the workspace S3 bucket **before** enqueueing ingestion.
- Reuse the workspace S3 config + client + health pattern from Integrations. **`EnsureHealthy(...)` the S3 config first (Standard 22)** — if not `Healthy`, refuse with **409 ConnectionUnhealthy** and do not accept the upload.
- Extend `UploadKnowledgeDocumentCommand` to carry the stored object key (and `SourceType`, `SourceUrl`, `Title` — see item 5).
- (S3 config lives in Integrations; the Knowledge service reads it via the established cross-service mechanism — do not query Integrations' DB directly.)

### 2. Text extraction — `ITextExtractor` (Infrastructure)
- Extract normalized plain text from **PDF, DOCX, TXT, MD, CSV**.
- **Announce your library choices and why** (state the pick and the rejected alternative). Keep extraction in the Infrastructure layer only.

### 3. Chunking — `IChunker` (Application interface, Infrastructure impl)
- Token-bounded (~500 tokens/chunk) with ~15% overlap, structure-aware (respect headings/paragraphs).
- **Bengali-aware:** treat the danda `।` as a sentence boundary alongside `.`/`?`/newline; never split inside a Unicode grapheme cluster.
- Emit per chunk: `Content`, `Ordinal`, `TokenCount`, and metadata (source, language hint).
- Unit-testable in isolation with no I/O.

### 4. Embed + persist
- Call `EmbedBatchAsync(chunks, EmbeddingInputType.Document, ct)` (Slice 1 — note `Document`).
- Persist via `IKnowledgeChunkWriter` (Slice 2) with the correct `document_version`.

### 5. Rework `KnowledgeIngestionJob` (real, idempotent)
- Real flow: `Pending → Processing → [download from S3 → extract → chunk → embed → write chunks → set ChunkCount] → Ready`.
- On any failure: `Failed` with a real `FailureReason`; **no partial chunk rows** left behind.
- **Idempotent:** re-running for the same `(document, version)` replaces prior chunks for that version — never duplicates. Keep the existing status events + audit-log writes.

### 6. Give URL / Text-FAQ source types a real backend (contract fix — Standard 19)
- Add the missing fields to the command/handler.
- **URL:** fetch server-side with an **SSRF allow-list** (https-only, block private/link-local ranges, cap size/time), then extract → chunk.
- **Text / FAQ pairs:** turn provided text or Q&A pairs into a synthetic document + chunks (a FAQ pair becomes one chunk: "Q: … A: …").

---

## 🎙️ Voice/cross-channel note
This is the **shared** knowledge base — the exact chunks ingested here will later answer **both Chat and Voice** queries. Do not tailor chunking to text output; keep chunks channel-neutral (the channel-specific answer shaping happens at generation time, Slice 5, via `ChannelProfile`). **Do NOT ingest dynamic/transactional data** (discounts, prices, stock, order status, live customer records) — that is served live via MCP tools in P3, never embedded.

## Reuse — study these patterns first
- `KnowledgeIngestionJob.cs` (status machine + event publish + audit + retry attributes).
- `UploadKnowledgeDocumentCommandHandler.cs` (dedup, audit, enqueue).
- Integrations S3 config + `S3ConnectionTester` + `EnsureHealthy` (Standard 22).
- Slice 1 `IEmbeddingProviderService`, Slice 2 `IKnowledgeChunkWriter`.

## Out of scope (later slices)
Retrieval / gRPC (Slice 4), grounded generation (Slice 5).

---

## Tests first (TDD)
1. **Bengali chunker** (unit): a mixed Bengali/English paragraph splits on `।`, respects the ~500-token bound and ~15% overlap, and never breaks a grapheme.
2. **Full job** (integration): ingest a small PDF → **N chunks**, each with a **non-NULL 1024 vector**, correct `ChunkCount`, status `Ready`.
3. **Idempotency:** re-run for the same document+version → chunk count unchanged, no duplicates.
4. **Failure:** a corrupt/unsupported file → status `Failed` + `FailureReason`, **zero** chunk rows.
5. **S3 guard:** upload when S3 config is unhealthy → **409**, nothing enqueued.
6. **URL SSRF:** a private-IP / metadata-endpoint URL is rejected.

## Non-Negotiables
- Clean Architecture: extraction/chunking/S3 in Infrastructure; orchestration via the job + command handlers; no `DbContext` in the API layer.
- Polly on S3 + URL-fetch; RLS on all writes; tenant from `ITenantContext`.
- **Audit** every state change; **idempotent** job; structured logs with `CorrelationId`/`TenantId`; **never log document content or PII**.
- Secrets (S3 keys) decrypted only at use-time in Infrastructure; never logged.

## Verification / report
1. With `bge-m3` running and a healthy workspace S3 config, upload a **real Bengali+English PDF** via `POST /api/v1/knowledge-documents`.
2. Query the Knowledge DB: `knowledge_chunks` rows with non-NULL embeddings, `ChunkCount > 0`, status `Ready`.
3. Re-upload the same file → deduped. Upload a corrupt file → status `Failed` + reason.
4. Report: extractor library choices (+ why), chunker strategy, files changed, test pass count.
