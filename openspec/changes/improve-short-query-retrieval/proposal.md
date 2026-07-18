## Why

The recently shipped grounding gate (`GroundingGate`/`ChannelProfile.AnswerGateScore`) enforces that the RAG assistant never answers from outside the knowledge base — but it exposed a pre-existing retrieval-quality gap: short or generically-phrased questions score below `MinScore` (0.55) and return **zero** KB matches, even when a relevant document exists and even in plain English. Live testing against the bioxin tenant's "Japan .NET Career Roadmap" document confirmed the pattern is query-specificity, not language: "roadmap", "Tell me about the roadmap", "Show me the roadmap", "What is the career roadmap for .NET developers?", and the romanized-Bengali "road map ta bolo" all returned 0 results (`Knowledge search completed ... 0 results` in service logs), while the long, fully-specific "What career path does the roadmap recommend for a .NET developer in Japan?" scored 0.678 and answered correctly. A visitor who asks a natural, short question today gets the tenant's no-answer fallback instead of a real answer that exists in the KB — this reads as a broken assistant and directly undermines the value of the KB feature. This was previously tracked informally as a "P1 hybrid search+reranking" backlog item; this change scopes and proposes it concretely.

## What Changes

- Add lexical (keyword/full-text) search as a second retrieval signal alongside the existing pgvector cosine-similarity search in the Knowledge service, and fuse the two result sets (e.g. Reciprocal Rank Fusion) into a single ranked list before the `MinScore` cutoff is applied — so a query that shares exact terms with a chunk (e.g. "roadmap") can surface it even when the embedding-only cosine score is weak.
- Change `MinScore` filtering from a hard SQL predicate applied purely to the vector-similarity score to a filter applied to the **fused** relevance score, so lexical matches aren't discarded before fusion has a chance to lift them.
- Add a query-length/specificity signal to logging so we can measure the before/after distribution of scores for short vs. long queries during rollout (no behavior change, observability only).
- No change to the grounding gate itself (`GroundingGate`, `AnswerGateScore`), the abstention prompt, or `NoAnswerMessage` — this change fixes the retrieval input the gate judges, not the gate's decision logic.

## Capabilities

### New Capabilities
- None. This modifies the existing (unspecced) retrieval behavior of the Knowledge service rather than introducing a new capability.

### Modified Capabilities
- `knowledge-retrieval`: New capability spec (no prior spec file existed for this behavior — the RAG grounding-hardening work that shipped ahead of OpenSpec adoption did not leave one). Captures the requirement that `SearchKnowledge` combines lexical and vector signals rather than relying on vector similarity alone, and that short/generic in-scope queries return results above the tenant's `MinScore`.

## Impact

- **Backend**: `NexConvo.Knowledge.Application` (`Features/Retrieval/Queries/SearchKnowledge/SearchKnowledgeQueryHandler.cs`, `IKnowledgeChunkRepository.SimilaritySearchAsync`), `NexConvo.Knowledge.Infrastructure` (the EF/pgvector repository implementation — new lexical query path, likely PostgreSQL `tsvector`/`ts_rank` on `knowledge_chunks.content`, possibly a new GIN index).
- **Database**: Likely a new migration adding a `tsvector` generated column + GIN index on `knowledge_chunks` for lexical search. No changes to Chat's schema or the `KnowledgeRetrieval.Search` gRPC contract (`Protos/knowledge.proto`) — `SearchRequest`/`SearchReply` shapes are unchanged, this is purely internal to how Knowledge computes matches.
- **No change** to `NexConvo.Chat.*` (`KnowledgeRetrievalClient`, `GroundingGate`, `ReplyOrchestrator`, `WidgetHub`, `PlaygroundHub`) — they keep calling `SearchAsync` with the same signature and trust whatever Knowledge returns.
- **Performance**: Fusion adds a second query path (lexical) per search; needs benchmarking against the existing single-vector-query latency, especially since retrieval already runs before every grounding-gate decision on every message.
