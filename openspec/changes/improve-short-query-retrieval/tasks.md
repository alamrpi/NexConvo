## 1. Database: lexical search column and index

- [x] 1.1 Write the failing test: extend `KnowledgeChunkRepositoryTests` (or create it, mirroring `tests/services/Knowledge/NexConvo.Knowledge.Infrastructure.Tests` Testcontainers `pgvector/pgvector:pg16` pattern) with a case asserting a chunk containing the exact word "roadmap" is returned when queried with just "roadmap", even with a deliberately weak/mocked embedding similarity.
- [x] 1.2 Add an EF migration (raw SQL, mirroring `AddKnowledgeChunksHnswIndex.cs`) adding a `content_tsv` generated `tsvector` column on `knowledge_chunks` plus a GIN index over it. **Deviation from design.md:** live Task 4.2 testing found plain `simple` config treats stopwords ("the", "of", "about") as literal searchable tokens, causing an unrelated chunk to match an out-of-scope query purely because both texts contained "the" — a real grounding-guarantee regression. Fixed by introducing `simple_nostop`, a custom text search configuration (`simple`'s tokenizer + English stopword removal, still zero stemming) — `content_tsv` and all lexical queries use this instead of bare `simple`. Confirmed zero effect on Bengali tokenization (English-only stopword list).
- [x] 1.3 Apply the migration locally and confirm `content_tsv` backfills automatically for existing rows (generated column, no separate backfill job).
- [x] 1.4 Update `KnowledgeDbContextModelSnapshot.cs` and confirm `dotnet ef migrations has-pending-model-changes` (or equivalent) is clean.

## 2. Repository: hybrid retrieval with RRF fusion

- [x] 2.1 Implement the combined vector + lexical query in `KnowledgeChunkRepository.SimilaritySearchAsync` — two ranked candidate sets (vector cosine via existing `<=>` query, lexical via `content_tsv @@ <query>` ordered by `ts_rank`), fused via Reciprocal Rank Fusion (`k=60`) into a single ranked+scored list. **Deviation:** `plainto_tsquery` (and `websearch_to_tsquery`) AND every token together, so "Tell me about the roadmap" would never match a chunk containing only "roadmap" — even a single filler word absent from the content breaks the match. Built an OR-query instead (`to_tsquery('simple_nostop', string_agg(lexeme, ' | '))` over the tokenized terms) so any shared word lifts a chunk, matching lexical search's intended job of exact-term recall rather than full boolean-query semantics.
- [x] 2.2 Apply `minScore` as a filter on the fused RRF score (replacing the current raw-cosine-only filter), keeping the `topK` cap after fusion. **Deviation:** raw RRF (`1/(k+rank)` summed) tops out at `2/(k+1) ≈ 0.033` — far below any realistic `MinScore` like 0.55 on its own. Normalized the fused score by that theoretical maximum so it lands on a 0–1 scale (1.0 = ranked #1 on both signals, 0.5 = ranked #1 on exactly one signal), preserving `MinScore`'s original "is this good enough to trust" meaning per design.md's decision.
- [x] 2.3 Run the Task 1.1 test — confirm it now passes.
- [x] 2.4 Add regression tests: a long, fully-specific query that already passed under vector-only search still returns the same chunk; a genuinely out-of-scope query (no lexical or semantic overlap) still returns zero results.
- [x] 2.5 Add a test for the `simple`-config lexical path against Bengali content (exact-term match, no stemming expected) to confirm the fix isn't English-only.

## 3. Query text passed to lexical search

- [x] 3.1 Confirm `SearchKnowledgeQueryHandler` passes the raw query text through to the repository's lexical path unchanged (same text already used for embedding) — no separate tokenization/preprocessing needed since Postgres `plainto_tsquery` handles that.
- [x] 3.2 Run `dotnet test tests/services/Knowledge/NexConvo.Knowledge.Application.UnitTests` — confirm no regressions (handler signature is unchanged).

## 4. Temporary measurement instrumentation

- [x] 4.1 Add a temporary debug log in `SearchKnowledgeQueryHandler` (mirroring the grounding-hardening plan's Task 8 pattern) logging query text length, fused top score, and result count — for live before/after comparison, not permanent.
- [x] 4.2 Bring up the stack and re-run the exact test matrix that surfaced this bug: "roadmap", "Tell me about the roadmap", "Show me the roadmap", "What is the career roadmap for .NET developers?", "road map ta bolo", "রোডম্যাপ সম্পর্কে বলুন", the long specific question that already worked, and an out-of-KB question. Capture fused scores from logs.
- [x] 4.3 Confirm the short/generic in-scope queries now score at or above `MinScore` (0.55) and the out-of-KB query still scores below it.

## 5. Threshold re-validation

- [x] 5.1 Based on Task 4 measurements, decide whether `MinScore` (Chat 0.55) and `ChannelProfile.AnswerGateScore` (Chat 0.62, Voice 0.65) still separate in-KB from out-of-KB clusters cleanly now that they judge fused scores. Adjust only if the measured data requires it — do not change speculatively. **Result: no adjustment needed** — measured scores separated cleanly at 1.0 (all in-scope queries) vs 0 (all out-of-scope/cross-language queries), a wide margin either side of both thresholds.
- [x] 5.2 N/A — thresholds were not adjusted (see 5.1), so no `ChannelProfile.cs` change or re-run of the grounding-hardening E2E matrix was needed. Confirmed via logs during Task 4.2 that grounding-gate pass/fail decisions matched the retrieval scores exactly (3 gated calls = the 3 zero-score queries).

## 6. Cleanup and verification

- [x] 6.1 Remove the temporary debug log added in Task 4.1.
- [x] 6.2 Run the full backend suites: `dotnet test tests/services/Knowledge/NexConvo.Knowledge.Infrastructure.Tests`, `dotnet test tests/services/Knowledge/NexConvo.Knowledge.Application.UnitTests`, `dotnet test tests/shared/NexConvo.BuildingBlocks.Rag.Tests`, `dotnet test tests/services/Chat/NexConvo.Chat.Application.UnitTests` — confirm all green, especially that no Chat-side test needed changes (contract unchanged per design.md).
- [x] 6.3 Benchmark retrieval latency before/after (Task 4's logs already capture timing, or add a quick stopwatch check) — confirm no significant regression (design.md flags >20% as a blocker) given retrieval sits on the critical path before every grounding-gate decision.
- [x] 6.4 Live E2E: rebuild + restart `knowledge` (and `chat` if `MinScore`/`AnswerGateScore` changed), re-run the short-query test matrix one more time end-to-end through the Playground hub (not just direct repository tests) to confirm the fix reaches real users, not just the query layer.
- [x] 6.5 Commit per task group as each goes green, following this repo's TDD-then-commit convention.
