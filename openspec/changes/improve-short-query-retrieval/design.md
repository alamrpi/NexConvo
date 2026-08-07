## Context

`SearchKnowledgeQueryHandler` (`NexConvo.Knowledge.Application/Features/Retrieval/Queries/SearchKnowledge/`) embeds the query text and calls `IKnowledgeChunkRepository.SimilaritySearchAsync`, which runs a single pgvector cosine-similarity query (`knowledge_chunks.embedding <=> queryEmbedding`, HNSW-indexed) and drops any row scoring below the caller-supplied `MinScore` before returning (`KnowledgeChunkRepository.cs:35`, `.Where(r => r.Score >= minScore)`). This is the only retrieval path — there is no lexical/keyword fallback.

The grounding-hardening work (shipped just before this change) added `GroundingGate`, which refuses to call the LLM at all when the top retrieval score is below `ChannelProfile.AnswerGateScore` (0.62 for Chat, measured; 0.65 for Voice, approximated). That gate is doing exactly what it was designed to do — but it made an existing weakness in retrieval much more visible: short or generically-phrased in-scope questions were already scoring low under vector-only search, and now they reliably fall all the way to the tenant's `NoAnswerMessage` instead of a real answer, even when the answer is sitting right there in the KB.

Live measurement against the bioxin tenant's single-document KB ("Japan .NET Career Roadmap") showed the failure is about query specificity, not language: "roadmap" and "Tell me about the roadmap" scored 0 results in plain English, same as the romanized-Bengali phrasing that originally surfaced the bug. Embedding models trained primarily on longer natural-language text tend to produce weaker cosine similarity for very short or keyword-like queries against longer document chunks — this is a known limitation of pure dense retrieval, and the standard fix is combining it with a sparse/lexical signal.

## Goals / Non-Goals

**Goals:**
- Short, generic, or keyword-style queries that share terms with an active KB chunk should retrieve that chunk with a fused score at or above the tenant's `MinScore`.
- Genuinely out-of-scope queries (no lexical or semantic overlap with any active chunk) must continue to return zero results — this change must not weaken the grounding guarantee, only widen what counts as "in scope."
- Zero changes to any caller of `SearchKnowledge`/`KnowledgeRetrieval.Search` — Chat's `KnowledgeRetrievalClient`, `GroundingGate`, `ReplyOrchestrator`, `WidgetHub`, `PlaygroundHub` all keep working unmodified.
- Keep the fix inside the Knowledge service's retrieval internals so it benefits every channel (Chat, and Voice once P2 lands there) without per-caller changes.

**Non-Goals:**
- Not re-tuning `MinScore` or `AnswerGateScore` — those already-measured thresholds stay as-is; this change fixes the score they're judging, not the threshold itself.
- Not adding a cross-encoder reranking stage (a heavier, separate P1 item) — RRF fusion of two first-stage retrievers is the scoped fix here.
- Not changing chunking strategy or embedding provider — those are separate backlog items if this change proves insufficient.
- Not exposing lexical-vs-vector score breakdown to callers — `ChunkMatch`/`KnowledgeChunkMatch` keep their single `Score` field; fusion happens entirely server-side.

## Decisions

**Decision: PostgreSQL native full-text search (`tsvector`/`ts_rank`), not a separate search engine.**
The Knowledge service already runs on PostgreSQL 16 with pgvector; adding Elasticsearch/OpenSearch/Meilisearch for lexical search would be a new infrastructure dependency, a new failure mode, and a new thing to keep in sync with document ingestion — disproportionate for closing a scoring gap. Postgres `tsvector` + a GIN index gives lexical search in the same database, same transaction visibility as the vector index, and matches the project's existing "avoid new infra unless the capability genuinely needs it" posture (see CLAUDE.md's JSONB-over-EAV and RLS-over-per-tenant-DB precedents).
*Alternative considered:* dedicated search engine — rejected as premature; revisit only if `tsvector` scoring proves insufficient at scale.

**Decision: Reciprocal Rank Fusion (RRF), not a weighted linear combination of raw scores.**
Cosine similarity (0–1 range, semantically meaningful) and `ts_rank` (unbounded, corpus-frequency-dependent) are not on comparable scales, so summing or weighting the raw scores directly is fragile and needs constant re-tuning as content grows. RRF combines two *rankings* (`1/(k + rank)` per list, summed) rather than two raw scores, which is scale-invariant and is the standard, low-maintenance approach for fusing heterogeneous retrievers (e.g. Elasticsearch's and Weaviate's built-in hybrid search both default to RRF for this reason).
*Alternative considered:* linear score blending — rejected, needs manual weight tuning and breaks when either signal's score distribution shifts (e.g. after a chunking change).

**Decision: `MinScore` is compared against the fused RRF score, expressed on the same 0–1-ish scale via a fixed `k` (industry-standard default `k=60`), not against either raw signal independently.**
This keeps the contract simple for callers: one `MinScore`, one meaning ("is this good enough to trust"), regardless of which signal(s) contributed. The existing measured values (Chat's gate at 0.62, retrieval `MinScore` at 0.55) were tuned against vector-only scores; RRF output magnitude differs from raw cosine similarity, so `MinScore`'s *effective* selectivity will shift when this ships — flagged explicitly in Risks below and handled via the same measurement approach the original grounding-hardening plan already used (compare in-KB vs. out-of-KB query scores against live data, adjust the constant if the clusters don't separate cleanly).
*Alternative considered:* two independent thresholds (`MinVectorScore`, `MinLexicalScore`) — rejected, doubles the tuning surface for marginal benefit given RRF already normalizes the scales.

**Decision: Generated `tsvector` column with `english`-config full-text search plus `simple`-config as a fallback, not a Bengali-specific text search configuration.**
PostgreSQL ships English/simple configurations out of the box; a Bengali-specific `tsvector` configuration would need a custom dictionary (not standard in PostgreSQL) and is a bigger, separate effort. Live testing showed the bug is query-length/specificity, not language — pure-Bengali and romanized-Bengali queries failed for the same structural reason plain English ones did. `simple` config (no stemming, just tokenization/lowercasing) still captures exact-term lexical overlap for Bengali text, which is what's needed for the RRF lift; full Bengali linguistic stemming is out of scope here and tracked separately if measurement shows it's still needed after this change.
*Alternative considered:* custom Bengali `tsvector` configuration — deferred as a follow-up if `simple`-config lexical matching proves insufficient for Bengali queries specifically.

## Risks / Trade-offs

- **[Risk] `MinScore`'s effective selectivity shifts once it's judging RRF output instead of raw cosine similarity — could reopen the door to hallucination-adjacent low-relevance matches if it lands too permissive, or fail to fix the reported bug if it lands too strict.** → Mitigation: re-run the same live-measurement step the grounding-hardening plan used (Task 8 pattern: drive real in-KB and out-of-KB queries, compare `MinScore`/`AnswerGateScore` against measured clusters, adjust before considering this change complete) rather than picking the new threshold analytically.
- **[Risk] Adding a second query path (lexical) per search increases retrieval latency, and retrieval sits on the critical path of every message before the grounding gate decides whether to call the LLM at all.** → Mitigation: the `tsvector` column is GIN-indexed and runs in the same query round-trip alongside the vector search (single SQL statement combining both via CTEs), not a second network call; benchmark against current latency before merging and treat >20% regression as a blocker.
- **[Risk] `simple`-config full-text search has no stemming, so "refund" won't match "refunds" via the lexical signal alone.** → Mitigation: this is acceptable because the vector signal already handles morphological/semantic variation; lexical search's job here is specifically to catch exact-term short queries that vector search under-scores, not to replace vector search's strengths.
- **[Trade-off] This change does not fix genuinely ambiguous or paraphrased short queries with no shared vocabulary with the KB content (e.g. a query using a synonym the document never uses) — that remains a vector-only problem and would need reranking or query expansion, deliberately deferred as a separate follow-up.**

## Migration Plan

1. Add an EF migration for a generated `tsvector` column (`content_tsv`) on `knowledge_chunks` plus a GIN index, following the existing raw-SQL-migration pattern used for the HNSW index (`AddKnowledgeChunksHnswIndex.cs`) since generated columns and GIN index creation need the same "separate from table DDL" treatment.
2. Backfill: the generated column computes itself from existing `content` on migration apply — no separate backfill job needed (`GENERATED ALWAYS AS (...) STORED`).
3. Update `KnowledgeChunkRepository.SimilaritySearchAsync` to run the combined vector+lexical query and apply RRF fusion, either in SQL (a single statement with two CTEs unioned/joined by chunk id) or in C# after fetching both ranked lists — prefer SQL if it can stay readable, per the repository's existing raw-SQL-for-pgvector convention.
4. Add the temporary measurement logging described in the proposal (query length/type alongside fused score) behind existing logging conventions, mirroring the grounding-hardening plan's Task 8 temporary-log-then-remove pattern.
5. Deploy to a dev/staging environment with a real KB, drive the same test matrix used to discover this bug (short EN, short BN, long specific EN, out-of-KB) plus a regression pass on previously-passing long queries, and confirm the score clusters separate cleanly around the (possibly adjusted) `MinScore`.
6. Remove temporary measurement logging once thresholds are confirmed; no feature flag needed — this is a pure retrieval-quality improvement with no behavior change for callers, so it ships directly once verified (consistent with how grounding-hardening's own `AnswerGateScore` tuning shipped).
7. Rollback: revert the migration (drop column/index) and the repository change; `MinScore`/`AnswerGateScore` need no rollback since they're unchanged by this migration plan unless Step 5 required adjusting them, in which case revert those constants alongside the code revert.

## Open Questions

- Should RRF fusion happen in a single SQL statement (two CTEs + `FULL OUTER JOIN` by chunk id, computing RRF in SQL) or fetch both ranked lists via two queries and fuse in C#? Leaning SQL for one round-trip, but needs a spike to confirm it stays maintainable given the existing raw-SQL-for-pgvector precedent.
- Does the measured `MinScore` need to change once it judges fused scores, and if so, does `ChannelProfile.AnswerGateScore` (Chat 0.62, Voice 0.65 approximated) need re-measurement too, or does it stay valid because it's applied to the same (now-improved) `Score` field it always was? This can only be answered empirically per the Migration Plan's measurement step.
- Is `simple`-config lexical matching sufficient for Bengali content long-term, or will a follow-up change need a proper Bengali text-search configuration? Flagged as a candidate follow-up, not blocking this change.
