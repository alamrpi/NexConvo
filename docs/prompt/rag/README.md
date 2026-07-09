# NexConvo — RAG Knowledge Base Implementation Prompts (Index)

> This folder contains **six sequential implementation prompts** that build the real RAG (Retrieval-Augmented Generation) pipeline for NexConvo. The knowledge base is a **shared, cross-channel capability**: it must serve both the **Chat** bot (Module 3.2) and the **Voice** bot (Module 2.3). So RAG is built as a **dedicated Knowledge service** that both channels consume — not buried inside Chat.

**How to use:** open each `slice-N-*.md`, paste its whole content into a **fresh Claude Code session** at the NexConvo project root, and let it run. Do the slices **in order** — each depends on the one before it.

---

## The mental model (what you are building)

RAG = **Retrieve** (find the right knowledge) + **Augment** (build a grounded prompt) + **Generate** (write the answer). These three have different natures, which decides where each lives:

| Part | Nature | Home |
|---|---|---|
| **R** — retrieve (embed query + vector search) | channel-neutral, shared | **Knowledge service** (own DB + pgvector), exposed via **gRPC** |
| **A** — augment (grounded prompt assembly) | shared logic, channel-tuned | **`BuildingBlocks.Rag`** — reused by Chat & Voice, parameterized by `ChannelProfile` |
| **G** — generate + deliver | channel-specific | **Chat** (LLM → SignalR text) / **Voice** (LLM → Cartesia TTS audio) |

```
                 ┌──────────────────────────────┐
                 │      Knowledge service        │  ← owns knowledge DB + pgvector
                 │  ingestion + retrieval        │  ← the "R" of RAG
                 │  gRPC: SearchKnowledge(...)   │
                 └──────────────▲────────────────┘
                       gRPC     │     gRPC
             ┌──────────────────┴───────────────────┐
      ┌──────┴───────┐                        ┌──────┴────────┐
      │ Chat service │                        │ Voice service │  (future consumer)
      │ conv + LLM   │                        │ call + LLM    │
      │  → SignalR   │                        │  → TTS        │
      └──────────────┘                        └───────────────┘

  Shared building blocks:
    BuildingBlocks.Ai   → embedding client + LLM providers
    BuildingBlocks.Rag  → grounded prompt assembler + ChannelProfile  ← both channels reuse
```

### Why a separate Knowledge service (not inside Chat)?
- **Database-per-service (Standard 5):** the Voice service cannot query Chat's database. A shared capability needs a single owner with its own DB.
- **gRPC internal comms:** retrieval is a typed, low-latency gRPC call — critical for Voice's **<700 ms** budget.
- **Cheapest now:** the current knowledge code in Chat is mostly a stub — relocating it while it's thin costs little; after it fills in, it costs a sprint.

---

## Slice map

| Slice | Builds | Where |
|---|---|---|
| **1** | Embedding provider layer (BGE-M3 + Cohere, Docker) | `BuildingBlocks.Ai` (shared) |
| **2** | **Knowledge service** scaffold + relocate knowledge out of Chat + 1024-dim vector schema | new `src/services/Knowledge` |
| **3** | Real ingestion: storage + extract + Bengali chunk + embed | Knowledge service |
| **4** | Retrieval + **gRPC** `SearchKnowledge` (channel-profile params) + real management queries | Knowledge service |
| **5** | Grounded RAG reply: Chat calls Knowledge via gRPC, `BuildingBlocks.Rag` + `ChannelProfile`, cancelable orchestrator | Chat + `BuildingBlocks.Rag` |
| **6** | SignalR streaming + real-time handoff (chat delivery) | Chat |

After Slice 6, the **Chat** bot is fully functional end-to-end. **Voice** then becomes a second consumer of the same Knowledge gRPC + `BuildingBlocks.Rag` (a future Voice slice) — no RAG rework needed.

---

## Same RAG for Chat AND Voice — share the engine, parameterize the behavior

Retrieval is safely shared, but **grounding + generation + delivery differ by channel** — sharing them blindly breaks voice. The differences and the fix:

| Problem | Chat | Voice | Handled by |
|---|---|---|---|
| Latency budget | ~3 s ok | **<700 ms** | smaller `topK`, faster model, aggressive streaming (voice) |
| Answer length/format | long, bullets, links, `[1]` citations | 1–2 spoken sentences, **no lists/links/citations** | `ChannelProfile` in the prompt assembler |
| Query source | typed (clean) | **STT output (noisy)** — mis-heard names/numbers | robust retrieval; confirm on low confidence |
| Streaming granularity | tokens to screen | **phrase-boundary** for TTS | channel-specific delivery |
| Barge-in (interrupt) | none | must **cancel** generation+TTS instantly | **cancelable** reply orchestrator (Slice 5) |
| Register | formal written Bengali | **colloquial spoken** Bengali | `ChannelProfile` |
| Confidence → handoff | threshold ~0.65 | stricter (~0.75); handoff = live transfer | per-channel threshold |

**Design rule:** one shared retrieval engine + a `ChannelProfile` (`Chat` | `Voice`) that controls `topK`, answer length/format, citation on/off, model, streaming granularity, and confidence threshold. Slice 4 puts `topK`/`minScore` on the gRPC request; Slice 5 puts `ChannelProfile` on the prompt assembler and makes the orchestrator cancelable.

---

## Locked decisions

- **Embedding model:** default **BGE-M3** (self-hosted Docker "Infinity"); **Cohere `embed-multilingual-v3.0`** as an env-switchable alternative (`EMBEDDING__PROVIDER=BgeM3|Cohere`). Both **1024 dims** → the vector column is `vector(1024)`. Decide before writing vectors — changing later forces a full re-embed.
- **Vector store:** pgvector + HNSW (cosine) in the **Knowledge service's own** Postgres DB. No external vector DB.
- **Retrieval (P0):** dense cosine kNN only, exposed via **gRPC**. Hybrid (pgvector + `tsvector` + RRF) + cross-encoder reranking are **P1**; Slice 4 leaves the seam.
- **Query embedding happens inside the Knowledge service** — callers send **text**, not vectors, so the embedding model stays owned by one service.

---

## Design with MCP in mind (P3 starts right after Slice 6)

After RAG, NexConvo adds **MCP (tool-calling / the "system of action")** — the bot won't just answer, it will *act* (create leads, apply discounts, book appointments) and read **live/dynamic data** via tools. Build so MCP is **additive**:

- **RAG is for static/unstructured knowledge only.** Dynamic/transactional data (discounts, prices, stock, order status) is fetched **live via MCP tools** in P3 — never embedded (Slices 3 & 4 state this).
- **Slice 5's reply pipeline is the MCP seam.** Design the generation as an orchestration step (one grounded LLM call today) that can become a multi-turn **agentic loop** (LLM → tool call → execute → feed result → continue) without a rewrite. (This cancelable orchestrator also serves voice barge-in.)
- **Generalize:** context assembly (RAG chunks are one source; MCP tools another), the decision outcome (`Answer | Handoff` → `+ ToolCall`), and the AI-decision audit (reused for tool executions).
- **Message model** leaves room for `ToolCall` / `ToolResult`; **stream protocol** is a typed-event envelope (not text-only).
- The **LLM provider** gains tool-calling in P3, not now — but Slice 5 must not assume it is permanently text-only.

Keep these seams open; do **not** build MCP inside the RAG slices.

---

## Non-negotiables (every slice, no exceptions)

Every slice invokes the **`nexconvo-enterprise-standards`** skill + superpowers **`brainstorming`** / **`test-driven-development`**, then honors:

- Clean Architecture (API → Application → Infrastructure → Domain); no `DbContext` in controllers/gRPC services.
- CQRS via MediatR; constructor injection only.
- **Test-first (TDD).**
- **Database-per-service (Standard 5)** — the Knowledge service owns its own DB; no cross-service DB access; cross-service data via gRPC/events + local read models.
- **RLS** on every tenant-scoped table; tenant from the JWT (HTTP) or propagated via **gRPC metadata** for internal calls — never a request body.
- **Polly** on every outbound external call (embedding server, S3, LLM, gRPC).
- **Structured logging** with `CorrelationId` + `TenantId` (propagated across gRPC); never log PII.
- **Audit** every mutation; **idempotency** on every consumer/retry-able write; **MassTransit outbox** for events (introduced in Slice 5).
- **API Gateway routing (Standard 21)** for new REST routes; **solution-folder nesting (Standard 20)** for new projects; **secrets from env**; pagination; `xmin` concurrency.

---

## Source of truth (read before any slice)

- `docs/ARCHITECTURE.md`
- `docs/CHATBOT-ARCHITECTURE.md` — §7 RAG Reply Pipeline, §8 Knowledge Ingestion, §11 Embeddings, §12 pgvector Specifics, §13 Persistence Schema, §14 Data Privacy
- `CLAUDE.md`

---

## Backlog after Slice 6

- **Voice consumer:** wire the Voice service (Vapi custom LLM endpoint) to the same Knowledge gRPC + `BuildingBlocks.Rag` with `ChannelProfile.Voice` (STT in → retrieve → ground → LLM → TTS out).
- **P1 — retrieval quality:** hybrid (pgvector + `tsvector` + RRF) + cross-encoder rerank, MMR, metadata filtering, query rewrite, per-step OTel spans.
- **P2 — trust, safety & eval:** prompt-injection sanitizer, PII masking, groundedness/citation judge, eval harness (golden set incl. Bengali), feedback loop, sentiment handoff.
- **P3 — agentic / MCP:** tool-calling on the provider, an MCP server exposing CRM actions with tool authz + action audit, provider fallback chain.
- **Cross-cutting:** migrate `AesEncryptionService` from AES-CBC to **AES-GCM** (Standard 13/15).
