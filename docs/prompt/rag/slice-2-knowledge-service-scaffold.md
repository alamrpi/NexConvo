# NexConvo — RAG Slice 2: Knowledge Service Scaffold + Relocate + 1024-dim Vector Schema

> Paste this entire prompt into a new Claude Code session at the NexConvo project root.
> This is **Slice 2 of 6** (see `docs/prompt/rag/README.md`). **Prerequisite: Slice 1 complete** (shared `IEmbeddingProviderService`, 1024-dim).

---

## Your Role — Senior Microservices & Data Architect

You are a **Senior .NET microservices architect** with 10+ years bootstrapping bounded contexts, PostgreSQL RLS, pgvector, and gRPC service meshes. You know that a **shared capability needs a single owning service with its own database** — you never let two services share a schema. The knowledge base must serve **both Chat and Voice**, so it belongs in its own **Knowledge service**, not inside Chat. You are doing this relocation now, while the knowledge code is still a thin stub, because moving it later costs a sprint. You scaffold a new service exactly like the existing ones — same layers, same RLS interceptor, same auto-migrator, same solution-folder nesting, same gateway wiring — so it feels native.

**Before writing any code**, invoke and follow:
1. `nexconvo-enterprise-standards`
2. superpowers `brainstorming` (confirm the service boundary + what moves)
3. superpowers `test-driven-development`

**First, read these files completely:**
- `docs/ARCHITECTURE.md` — service layout, RLS, auto-migration, gateway, database-per-service
- `docs/CHATBOT-ARCHITECTURE.md` — §12 pgvector Specifics, §13 Persistence Schema
- `CLAUDE.md`
- A reference service to mirror end-to-end: `src/services/Integrations/` (Domain/Application/Infrastructure/Api layout, `IntegrationsDatabaseMigrator`, `IntegrationsDbContext`, RLS, DI)
- The knowledge code you are **relocating out of Chat**:
  - `src/services/Chat/NexConvo.Chat.Domain/Entities/KnowledgeDocument.cs` + `KnowledgeChunk.cs`
  - `src/services/Chat/.../Features/KnowledgeBase/**` (Upload/Delete/ReEmbed commands, controller)
  - `src/services/Chat/.../Infrastructure/Persistence/Configurations/KnowledgeChunkConfiguration.cs` (shadow `vector` mapping) + `KnowledgeDocumentConfiguration.cs`
  - `src/services/Chat/.../Infrastructure/Migrations/20260701000001_*`, `20260701000002_*Hnsw*`, `20260701000003_*`
  - `src/services/Chat/.../Infrastructure/Jobs/KnowledgeIngestionJob.cs` (the stub — moves too)
- The RLS interceptor + migrator pattern: `src/shared/NexConvo.BuildingBlocks.Infrastructure/Multitenancy/RlsConnectionInterceptor.cs`
- `NexConvo.sln` (`GlobalSection(NestedProjects)`) + `src/gateway/NexConvo.Gateway/appsettings*.json`
- `docker-compose.yml` + `frontend/src/app/api/bff/settings/knowledge/**` (BFF routes that must repoint)

---

## Context — what changes and why

| Item | Now | After this slice |
|---|---|---|
| Knowledge entities/schema/pgvector/ingestion-stub | ✅ In **Chat** service | Moved to a new **Knowledge** service |
| Vector column | `vector(1536)` (wrong for BGE-M3/Cohere) | `vector(1024)` |
| Owner of the knowledge DB | Chat | **Knowledge service** (its own DB) |
| Voice's ability to reach knowledge | ❌ blocked (DB-per-service) | ✅ via gRPC to Knowledge (Slice 4) |

The knowledge base is a **shared, cross-channel capability**; database-per-service (Standard 5) forbids Voice from touching Chat's DB, so knowledge gets its own service. The current tables are empty in every env, so the schema change is destructive-safe.

## Prerequisites
- **Slice 1 done.** `IEmbeddingProviderService.Dimensions == 1024` available in `BuildingBlocks.Ai`.

---

## What to build

### 1. Scaffold the Knowledge service (Clean Architecture)
- New projects under `src/services/Knowledge/`: `NexConvo.Knowledge.Domain`, `.Application`, `.Infrastructure`, `.Api`.
- **Nest them in `NexConvo.sln`** under a `Knowledge` solution folder via `GlobalSection(NestedProjects)` (Standard 20).
- `KnowledgeDbContext` with `UseVector()` + `RlsConnectionInterceptor`; a `KnowledgeDatabaseMigrator` invoked on startup (mirror `IntegrationsDatabaseMigrator`; migrate under a `NullTenantContext`).
- **Its own database** — add a `KnowledgeDb` / `KnowledgeDbMigrator` connection string in `docker-compose.yml` (a separate logical DB on the same Postgres instance is fine). Multi-stage `Dockerfile`; config from env.
- Register the shared `AddEmbeddingProviders()` (Slice 1) — the Knowledge service is the embedder.

### 2. Relocate the knowledge domain out of Chat
- Move `KnowledgeDocument`, `KnowledgeChunk`, `DocumentStatus`, their events, EF configurations, the CQRS commands (Upload/Delete/ReEmbed), the `KnowledgeBaseController`, and the `KnowledgeIngestionJob` **stub** from Chat → Knowledge, adjusting namespaces.
- **Delete the knowledge bits from Chat** (entities, DbSets, migrations, controller, job). Chat keeps only chat concerns; it will *call* Knowledge via gRPC in Slice 5.
- Recreate the schema as fresh Knowledge-service migrations (don't port Chat's migration files verbatim — regenerate for the new context), preserving RLS policies, grants, content-hash dedup index, and `xmin` concurrency.

### 3. Vector schema at 1024 dims
- In the new migration, the `knowledge_chunks.embedding` column is **`vector(1024)`** with the HNSW cosine index
  (`USING hnsw (embedding vector_cosine_ops) WITH (m = 16, ef_construction = 64)`).
- `knowledge_documents.embedding_dimensions` default `1024`, `embedding_model` default `BAAI/bge-m3`.
- Keep `Embedding` **off** the domain entity — map it as an EF **shadow property** (`Property<Vector>("Embedding").HasColumnType("vector(1024)")`), exactly as Chat did.

### 4. The chunk write path — `IKnowledgeChunkWriter`
- In `NexConvo.Knowledge.Application` interfaces, add `IKnowledgeChunkWriter` (persist chunks with `Content`, `Ordinal`, `TokenCount`, `float[] Embedding`, `document_version`); implement in Infrastructure, setting the shadow property via `Pgvector.Vector`. All writes go through `IKnowledgeDbContext` on the RLS-scoped connection; tenant from `ITenantContext`.

### 5. Rewire the edges (Standards 21 + contract stability)
- **Gateway (Standard 21):** move the knowledge REST cluster/route from Chat to the new Knowledge service in `NexConvo.Gateway/appsettings*.json` so the management UI still reaches `/api/v1/knowledge-documents`.
- **Frontend BFF:** repoint `frontend/src/app/api/bff/settings/knowledge/**` to the Knowledge service via the gateway (path can stay stable; only the upstream changes).
- **Docker Compose:** add the Knowledge service + its DB; ensure migrations run on startup.

---

## Reuse — study these patterns first
- `src/services/Integrations/` end-to-end as the service template (layers, migrator, DbContext, DI, Dockerfile).
- The Chat knowledge code you are moving (entities, configs, commands, stub job).
- `RlsConnectionInterceptor`, `ITenantContext`, `Pgvector` mapping.

## Out of scope (later slices)
Real extraction/chunking/embedding (Slice 3), retrieval + gRPC (Slice 4), the RAG reply (Slice 5). This slice just **stands up the service, relocates the code, and gets the schema to 1024**.

---

## Tests first (TDD)
1. **Round-trip + RLS:** `IKnowledgeChunkWriter` persists a chunk with a random 1024-vector for tenant A; readable by A, **invisible to tenant B**.
2. **Dimension guard:** a 1536-length vector is rejected.
3. **Migration + service boot:** the Knowledge service starts, auto-migrates a fresh DB, and `\d knowledge_chunks` shows `vector(1024)` + the HNSW index.
4. **Management endpoints still reachable:** Upload/Delete/ReEmbed commands run against the new service (behaviour unchanged from Chat — still stubbed ingestion).
5. **Chat no longer references knowledge:** Chat builds with the knowledge code removed.

## Non-Negotiables
- Database-per-service (Standard 5) — Knowledge owns its DB; **no shared schema** with Chat.
- New projects nested in the solution (Standard 20); gateway updated (Standard 21).
- RLS on every table; tenant from `ITenantContext`; auto-migrator on startup.
- Constructor injection; structured logging (`CorrelationId`/`TenantId`); no PII in logs; secrets from env.
- Preserve the published REST contract (`/api/v1/knowledge-documents`) — only the upstream service moves (Standard 19).

## Verification / report
1. `docker compose up` → the Knowledge service boots and migrates; `\d knowledge_chunks` shows `vector(1024)` + HNSW.
2. Upload a document via `/api/v1/knowledge-documents` (through the gateway) → it lands in the Knowledge DB (still stub-ingested).
3. `dotnet build NexConvo.sln` clean; Chat has no knowledge references.
4. Report: new project list + solution nesting, the moved/deleted files, gateway + BFF repoint, migration name, test pass count.
