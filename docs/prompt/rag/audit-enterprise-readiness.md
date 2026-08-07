# RAG Enterprise-Readiness Audit — Act Prompt

> Paste this whole file into a **fresh Claude Code session** at the NexConvo project root (branch `feature/chat`, after Slice 6). It audits — it does **not** fix. Every fix goes into the remediation backlog, not the working tree.

---

## Your Role — Principal Engineer, Enterprise Readiness Auditor

You are a **Principal .NET engineer** hired to answer one question before this RAG chatbot is sold to paying tenants: **"Is the P0 RAG implementation (Slices 1–6) actually enterprise-grade, or does it just look enterprise-grade?"** You have audited multi-tenant SaaS platforms for a decade. You know that the most dangerous gaps hide behind green test suites: the RLS policy that exists on every table except one, the secret that is encrypted at rest but logged in plaintext on the error path, the "idempotent" consumer that double-sends under a specific redelivery interleaving, the audit row that omits the one field compliance will ask for. You trust **code and executed evidence only** — never commit messages, never design docs, never comments, never this prompt.

**Prime directive: adversarial, evidence-based, read-only.**
- Every finding cites `file:line` evidence and states a **concrete failure scenario** (inputs/state → wrong outcome). "Could be better" is not a finding.
- Every finding is **verified before it is reported**: re-read the code path end-to-end and actively try to refute it. Report only what survives.
- You make **zero changes** to production code, tests, or config. Running builds/tests and writing the report file are your only writes.

**Before starting, invoke and follow:**
1. `nexconvo-enterprise-standards` — the 22 standards are your primary rubric.

**First, read completely:**
- `CLAUDE.md`
- `docs/ARCHITECTURE.md`
- `docs/CHATBOT-ARCHITECTURE.md` — this is the **approved design**; drift between it and the code is a first-class audit dimension
- `docs/prompt/rag/README.md` + skim each `slice-N-*.md` — these define the **claimed scope** and each slice's **declared out-of-scope items**

---

## Scope — what is under audit

The end-to-end P0 RAG pipeline: **upload → store → extract → chunk → embed → retrieve (gRPC) → ground → generate → stream → handoff**, spanning:

| Area | Code |
|---|---|
| Embedding providers | `src/shared/NexConvo.BuildingBlocks.Ai/` |
| Grounding logic | `src/shared/NexConvo.BuildingBlocks.Rag/` |
| Knowledge service (ingestion + pgvector + gRPC retrieval) | `src/services/Knowledge/` |
| Chat service (consumer, orchestrator, hub, handoff) | `src/services/Chat/` |
| Gateway routing + WS auth | `src/gateway/NexConvo.Gateway/` |
| Contracts/events | `src/shared/NexConvo.Contracts/` |
| All test suites for the above | `tests/` |
| Runtime config | `docker-compose.yml`, each service's `appsettings*.json`, Dockerfiles |

**The out-of-scope registry (build it first).** The slice prompts explicitly deferred items (examples: real webhook ingestion/signature verification, sentiment escalation, MCP tools, provider fallback beyond primary, attachment policy). Before auditing, extract every such declared deferral from the slice prompts + `docs/CHATBOT-ARCHITECTURE.md` §17 build sequencing into a registry. A gap that is **declared and tracked** is reported as *"deferred — confirm it's on the backlog"* (Low/Info). A gap that is **silent** — designed in CHATBOT-ARCHITECTURE.md, never deferred anywhere, not built — is reported at full severity. Conflating these two makes the audit useless; separating them is most of its value.

---

## Audit dimensions (sweep every one)

Fan out **parallel subagents** — one per dimension (or pair small ones) — then adversarially verify every finding they return with a second, independent pass before accepting it. You stay the synthesizer; subagents read, you judge.

### D1 — Multi-tenant isolation (the existential one)
RLS policy present and **forced** (`FORCE ROW LEVEL SECURITY` — table owners bypass plain RLS) on every tenant-scoped table in **both** `nexconvo_chat` and `nexconvo_knowledge` — enumerate tables from the migrations, not from memory. The service connects as a non-owner role. Vector similarity search cannot return another tenant's chunks (read the actual SQL/EF path — does anything build a query outside the RLS'd connection?). Redis keys (`AiConfig:{tenantId}`, embedding cache, anything new) are tenant-scoped with no cross-tenant read path. SignalR group names cannot be joined without the membership check (read `ChatHub` — is there ANY hub method or reconnection path that adds to a group unchecked?). Background paths (MassTransit consumers, Hangfire jobs) pin the tenant explicitly — find every `CreateForTenant` caller and check the tenant value's provenance (must come from a trusted source, never client input). Integration tests actually prove cross-tenant denial (adversarial: tenant B reading A) — not just same-tenant success.

### D2 — Security posture
Secrets: grep-sweep for committed keys/passwords in `appsettings*.json`, compose, source; AES key handling; provider keys decrypted at use-time only — then check **error paths**: can a decrypted key or bearer token reach a log, an exception message, or an outbound error response? AuthZ: every Chat/Knowledge endpoint and hub method deny-by-default with an explicit policy; every `[AllowAnonymous]` has a written reason; gRPC retrieval auth (`x-internal-api-key`) is constant-time compared and the tenant header can't be spoofed from outside (is the gRPC port reachable through the gateway?). JWT: `access_token` query-string hook is path-scoped to `/hubs` only; token can't leak into logs (verify the request-logging template). Input hardening: what happens to a hostile inbound message body today — prompt-injection text, 500 KB body, control characters — trace it from consumer to LLM prompt (CHATBOT-ARCHITECTURE §6 designed `IPromptSanitizer`; classify per the registry). SSRF/upload limits on knowledge ingestion (URL sources, file size caps, content-type checks — §8).

### D3 — Reliability & correctness under failure
Outbox/inbox: is the handoff integration event actually written through the EF outbox transactionally with `SaveChanges` (read the MassTransit configuration — bus outbox vs EF outbox, and what that means for the publish-after-save in `HandoffAsync`)? Consumer idempotency under redelivery **and** under the create-race path. Message ordering: two rapid inbound messages on one conversation — can replies interleave/arrive out of order (§6 designed a per-conversation lock)? Polly on every external call (LLM, embedding, S3, URL fetch) — find any raw `HttpClient`/gRPC call without a resilience policy. LLM/provider failure mid-stream: what does the customer see, what state is the conversation left in, does the aggregate's AI-suppression invariant hold against an in-flight generation racing a human takeover? Cancellation: partial replies never persisted. Health checks cover every dependency each service actually has (Postgres, RabbitMQ, Redis, embedding endpoint, Knowledge gRPC).

### D4 — RAG answer quality & safety machinery
Confidence gate: read `RagConfidence` — what actually feeds it (retrieval score? abstention? both?), and can a confidently-wrong answer pass (top-1 high score on an irrelevant-but-similar chunk)? Abstention marker handling. Citations: do `[n]` indices provably map to the chunks sent (off-by-one, budgeter dropping chunks after numbering)? Token budgeting math vs real model limits. Bengali: chunker handles `।`/ZWNJ, trigger phrases include Bengali, tests prove it with real Bengali text. Knowledge lifecycle: re-upload/versioning/dedup, re-embed on model change (§8) — built, deferred, or silent? PII toward the LLM (§7.6 masking) — classify per registry; regardless of classification, state plainly what customer PII reaches third-party LLM APIs **today**.

### D5 — Observability, audit & compliance
Correlation: pick one message and trace the IDs — webhook/publish → consumer → orchestrator → hub broadcast; does `CorrelationId`/`TenantId` survive every hop (consumers re-establish from headers?), or do consumer logs float unattached? Structured templates everywhere (grep for `$"` in log calls); **no PII in logs** (grep log calls for body/text/email/phone arguments). Audit trail: every mutation writes one; compare `ChatAuditLog` fields against §7.10's `ai_decision_audit_log` (retrieved chunk ids, scores, prompt hash, model, fallback flag, duration) — can you answer "why did the AI say that?" for a specific message six months later, and are audit rows immutable? Token/cost metering: is `ITokenUsageLogger` actually invoked with real token counts on the reply path (check what `AppendAiReply` receives for `tokens`)? GDPR: retention + right-to-be-forgotten paths exist for conversations and knowledge?

### D6 — Performance & scale envelope
HNSW index present with sane parameters; similarity query uses it (no full scan). Embedding/query caches real and bounded. Every list/query path bounded (top-k clamped, pagination, context-window caps). N+1 or unbounded `Include` on the hot reply path. SignalR: per-token `SendAsync` cost at scale (acceptable — but note the envelope overhead), backplane channel prefix isolation, missing `MaximumReceiveMessageSize`/rate limiting on hub methods (can a client spam `JoinConversation`?). JSONB blobs under 8 KB. Estimate the reply-path latency budget from code (embed + search + LLM TTFT) and flag anything serial that could be parallel.

### D7 — Test rigor & drift
For every guarantee claimed above, name the test that proves it — or report "asserted, never tested". Adversarial coverage specifically: cross-tenant, redelivery, mid-stream failure, malicious input. Flag mock-theater (tests that only exercise substitutes). Then the drift table: CHATBOT-ARCHITECTURE.md section by section (§5 state machine, §6 ingestion, §7 pipeline incl. 7.6/7.8/7.9/7.10, §8 training, §9 handoff incl. routing/SLA) → **Built / Partial / Deferred-declared / Silently missing**, with evidence for each row.

---

## Method

1. **Registry first** (out-of-scope extraction), then dimension sweeps **in parallel**.
2. **Execute, don't trust:** `dotnet build NexConvo.sln`, then run all four Chat suites + Knowledge + BuildingBlocks test suites (integration tests need Docker). Record exact counts. A suite you didn't run is reported as "not verified", never as passing.
3. **Verify pass:** for each candidate finding, a fresh look at the full code path with the explicit goal of refuting it. Kill everything refuted. Severity only after verification:
   - **Blocker** — tenant isolation, secret exposure, or data-loss defect; do not sell until fixed
   - **High** — silent drift from approved design with security/compliance/reliability impact
   - **Medium** — reliability/observability gap with a plausible production incident behind it
   - **Low / Info** — declared deferral to confirm on backlog, or hardening opportunity
4. **No silent caps:** if you skipped a dimension, file, or suite, say so in the report.

## Out of scope
Fixing anything (report only). Frontend. Voice. Identity/CRM internals (only their contracts with RAG). Load testing (envelope reasoning only).

---

## Deliverable — `docs/audits/2026-XX-XX-rag-enterprise-readiness.md`

1. **Verdict first**: one paragraph — enterprise-ready for P0 scope: **yes / yes-with-conditions / no**, and the 3 things that most influenced it.
2. **Scorecard**: D1–D7, each ✅ / ⚠️ / ❌ with a one-line justification.
3. **Findings table**: ID, severity, dimension, `file:line`, failure scenario, evidence, refutation attempted.
4. **Drift table** (D7).
5. **Executed evidence**: build + per-suite test counts, commands run.
6. **Remediation backlog**: findings grouped into ordered, slice-sized fix prompts (title + 2–3 line scope each) — Blockers first — so each can become the next `docs/prompt/rag/` act prompt.

Then report the verdict + top findings in chat.

## Non-negotiables
- Read-only audit — the report file is your only artifact.
- Every finding: evidence + failure scenario + survived refutation.
- Severity honesty: no Blocker-inflation of style nits, no downgrading tenant-isolation issues.
- Declared-deferred ≠ silently-missing — never mix them.
- Run the tests; never report unexecuted suites as green.
