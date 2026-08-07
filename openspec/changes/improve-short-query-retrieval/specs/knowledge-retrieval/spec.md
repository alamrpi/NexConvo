## ADDED Requirements

### Requirement: Hybrid lexical and vector retrieval
The Knowledge service SHALL combine a lexical (keyword/full-text) search signal with the existing vector cosine-similarity signal when resolving `SearchKnowledge`, instead of relying on vector similarity alone. The two result sets SHALL be fused into a single ranked list (e.g. via Reciprocal Rank Fusion) before the tenant/channel `MinScore` cutoff is applied, so a chunk that shares exact or near-exact terms with the query can surface even when its embedding-only cosine score is weak.

#### Scenario: Short query with exact term match retrieves the relevant chunk
- **WHEN** a tenant with an active KB document containing the word "roadmap" is queried with the single word "roadmap"
- **THEN** `SearchKnowledge` returns at least one chunk from that document with a fused score at or above the caller's `MinScore`

#### Scenario: Generic short question about a document topic retrieves the relevant chunk
- **WHEN** a tenant is queried with "Tell me about the roadmap" or "Show me the roadmap" and an active KB document about a roadmap exists
- **THEN** `SearchKnowledge` returns at least one chunk from that document with a fused score at or above the caller's `MinScore`

#### Scenario: Genuinely out-of-scope query still returns nothing
- **WHEN** a tenant is queried with a question with no lexical or semantic relationship to any active KB document (e.g. "What is the capital of France?" against a KB with no geography content)
- **THEN** `SearchKnowledge` returns zero chunks, unchanged from current behavior

#### Scenario: Long, fully-specific query continues to work exactly as before
- **WHEN** a tenant is queried with a long, specific question that already scored above `MinScore` under vector-only search
- **THEN** `SearchKnowledge` continues to return the same relevant chunk(s), and fusion does not demote it below `MinScore`

### Requirement: MinScore filters the fused relevance score
The `MinScore` threshold passed into `SearchKnowledge` SHALL be evaluated against each candidate's fused (lexical + vector) score, not the raw vector-only cosine score, so that a chunk lifted by a strong lexical match is not discarded before fusion has a chance to raise its rank.

#### Scenario: MinScore cutoff applies after fusion
- **WHEN** a chunk's vector-only cosine score is below `MinScore` but its lexical match is strong enough that the fused score is at or above `MinScore`
- **THEN** the chunk is included in the results returned to the caller

#### Scenario: MinScore cutoff still excludes weak matches on both signals
- **WHEN** a chunk's vector score and lexical score are both weak, such that the fused score remains below `MinScore`
- **THEN** the chunk is excluded from the results, unchanged from current behavior

### Requirement: Retrieval contract is unchanged for callers
The `KnowledgeRetrieval.Search` gRPC contract (`SearchRequest`/`SearchReply`) and the `IKnowledgeChunkRepository.SimilaritySearchAsync`/`SearchKnowledge` handler signatures SHALL remain unchanged. Hybrid retrieval SHALL be implemented entirely inside the Knowledge service's retrieval pipeline, transparent to Chat's `KnowledgeRetrievalClient`, `GroundingGate`, and `ReplyOrchestrator`.

#### Scenario: Chat service requires no code changes
- **WHEN** the hybrid retrieval change is deployed
- **THEN** `NexConvo.Chat.Infrastructure.ExternalServices.KnowledgeRetrievalClient` and all of its callers (`WidgetHub`, `PlaygroundHub`, `ReplyOrchestrator`) continue to function without modification, receiving the same `KnowledgeChunkMatch` shape as before
