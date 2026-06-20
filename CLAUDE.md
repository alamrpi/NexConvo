# NexConvo — AI-Native Omnichannel SaaS CRM

## Project Overview

**NexConvo** is an AI-native, multi-tenant SaaS CRM platform engineered as an autonomous "system of action." It unifies omnichannel communication (WhatsApp, Facebook, Instagram, Telegram, TikTok, LinkedIn, SMS, Email, Voice), automates project workflows post-deal-closure, and operates with zero-touch data entry via AI NLU entity extraction.

**Primary target market:** SMEs globally, with a strategic stronghold in Bangladesh/South Asia (APAC) before broader expansion. Key verticals: Retail/E-commerce, Agencies/IT Services, Real Estate/Financial Services.

**PRD Version:** 1.1.0 | **Date:** June 2026

---

## Tech Stack

| Layer | Technology |
|---|---|
| **API Gateway** | YARP (Yet Another Reverse Proxy) — routing, load balancing, rate limiting |
| **Backend** | .NET Core (C#) microservices via gRPC for internal communication |
| **Frontend** | Next.js (App Router) + React + Tailwind CSS (SSR) |
| **Database & ORM** | PostgreSQL 16 via EF Core — RLS for tenant isolation, JSONB for custom fields |
| **Real-time & Caching** | ASP.NET Core SignalR (WebSockets) + Redis (backplane, session, caching) |
| **Message Broker** | RabbitMQ + MassTransit (event-driven async) |
| **Background Jobs** | Hangfire / Quartz.NET |
| **AI Voice** | Vapi (orchestration) + OpenRouter (LLM routing) |
| **STT** | Deepgram Nova-3 (sub-300ms, native Bengali + English code-switching, $0.0043/min) |
| **TTS** | Cartesia Sonic 3 (sub-100ms TTFB, native Bengali prosody) |
| **SMS — Global** | Twilio (NA/EU) + Vonage/Plivo (high-volume international) |
| **SMS — Bangladesh** | BulkSMSBD / MimSMS (prefix-routed on +880, 60–80% cost reduction) |
| **Email — Transactional** | Resend or Postmark (dedicated IP pool, inbox placement priority) |
| **Email — Marketing** | Amazon SES ($0.10/1,000 emails, high-volume bulk engine) |
| **Containers** | Docker (local dev) → Kubernetes with HPA (production) |

---

## Architecture Principles

### Multi-Tenant Data Isolation
- **PostgreSQL Row-Level Security (RLS)** enforced on every tenant-scoped table.
- JWT carries a `tenant_id` claim; every request sets `SET LOCAL app.current_tenant_id` before any query.
- Cross-tenant data leaks are architecturally impossible — never bypass RLS policies.

### Custom Fields — JSONB over EAV
- All dynamic/custom fields use **PostgreSQL JSONB + GIN indexing** (up to 1000× faster than EAV at scale).
- Never implement EAV (Entity-Attribute-Value) for custom field storage.
- Keep JSONB blobs under 8 KB to avoid PostgreSQL TOAST storage degradation.

### Real-time Communication
- All chat/conversation UIs use **WebSockets via ASP.NET Core SignalR** — never SSE or polling.
- Redis acts as the SignalR backplane for horizontal scaling across pods.

### SMS Routing Strategy
- Prefix-match on destination number: `+1`/`+44` → Twilio; `+880` → BulkSMSBD/MimSMS.
- Never route Bangladesh numbers through international gateways (cost penalty of 60–80%).

### Email Separation
- **Transactional emails** (invoices, OTPs, password resets, invitations): Resend or Postmark dedicated pool.
- **Marketing emails** (bulk campaigns): Amazon SES separate pool.
- Never mix transactional and marketing through the same IP pool (destroys sender reputation).

### AI Voice Pipeline
- Vapi handles orchestration; OpenRouter provides cost-effective LLM routing (no custom backend needed).
- Target total cost: **$0.08–$0.10 per minute** (Vapi + OpenRouter + Deepgram + Cartesia).
- Voice AI requires **<700ms end-to-end latency** for natural conversational flow.

---

## Module Breakdown

### Module 1: Core CRM & Dynamic Data Management
- **1.1 Dynamic Data Architecture:** Multi-tenant PostgreSQL RLS via EF Core; JSONB for custom fields.
- **1.2 Autonomous Lead Ingestion:** AI NLU extracts budget, timelines, and intent from call/text transcripts; auto-transitions pipeline stages with zero human input.

### Module 2: AI Voice & Human Telephony Infrastructure
- **2.1 AI Outbound Campaigns:** Hangfire scheduling, Vapi + OpenRouter + Deepgram (Nova-3) STT + Cartesia TTS. Full English/Bengali code-switching support.
- **2.2 Human Agent Telephony:** Next.js web-dialer, automated call logging, post-call disposition forms.
- **2.3 Intelligent Inbound Support:** RAG-powered Level 1 AI agent; SignalR-based instant human handoff on negative sentiment, complex issues, or trigger phrases ("speak to a manager").

### Module 3: Omnichannel Social Media & Chatbot Engine
- **3.1 Multi-Platform Webhook Aggregator:** Unified ingestion from Facebook, Instagram, WhatsApp, Telegram, TikTok, and LinkedIn into a single team inbox. Auto-creates leads from social metadata.
- **3.2 Stateful Conversational AI:** SignalR persistent fabric for real-time token streaming; Social RAG engine for localized/Bengali replies; context-preserving human escalation; central agent reply routing.

### Module 4: Smart Communication Infrastructure (SMS & Email)
- **4.1 Dynamic SMS Gateway Routing:** Prefix-based routing (Twilio global, BulkSMSBD/MimSMS for +880). Handles OTPs, transactional alerts, and bulk marketing.
- **4.2 Dual-Pool Email Delivery:** Resend/Postmark for transactional; Amazon SES for marketing. Strict IP pool separation.

### Module 5: Project Execution & Workflow Automation
- **5.1 Sales-to-Project Lifecycle:** On "Closed-Won," automatically provisions a project workspace — milestones, resource allocation, invoicing — with no manual steps. Interactive Kanban boards and Gantt charts.
- **5.2 Conditional Visual Automation Engine:** Decoupled If/Then logic builder (e.g., milestone → invoice API + Slack/Teams webhook). Scheduler + Executor + State Store architecture.

### Module 6: Internal AI Assistant & Data Hygiene
- **6.1 Proactive Co-Pilot:** Reads email threads and call transcripts; suggests follow-ups; identifies stalled deals and neglected accounts in the PostgreSQL pipeline.
- **6.2 Automated Data Hygiene:** Background duplicate identity resolution and autonomous record merging.

### Module 7: Enterprise & Franchise Operations
- **7.1 Multi-Branch & Franchise Management:** Centralized dashboards with roll-up "master views" for HQ.
- **7.2 Offline-First Mobile App:** Field sales/delivery app with background sync on reconnect.
- **7.3 Advanced RBAC & Audit Trails:** Granular role-based access control + comprehensive audit logging.
- **7.4 AI Predictive Sales Forecasting:** ML models analyzing historical pipeline data for closure rate/revenue prediction.
- **7.5 Gamification & Leaderboards:** Points, badges, and performance tracking for sales teams.

### Module 8: Client & End-Customer Experience
- **8.1 Self-Service Client Portal:** Secure customer-facing login to track projects, view milestones, pay invoices, and submit support tickets.
- **8.2 Automated CSAT & NPS Collection:** Post-resolution surveys via SMS or email.

### Module 9: Developer Experience & Microservices Integration
- **9.1 Developer API Portal:** Docs + sandbox environment for enterprise IT teams.
- **9.2 Custom Webhook Management:** UI for configuring inbound/outbound webhooks to external systems.

### Module 10: Global AI Command Assistant (Omni-Command Center)
- **10.1 Natural Language Execution (NLE) Engine:**
  - Floating Command Palette (Cmd/Ctrl + K) for text and voice input.
  - Cross-module automation ("Send 10% discount SMS to all customers who purchased last month").
  - Context-aware navigation by voice/text command.
- **10.2 Conversational BI & Analytics:**
  - Natural language report generation ("Show me a bar chart of last week's revenue").
  - Deep data summaries from months of conversational/project history.
- **10.3 Prompt-Based System Configuration:**
  - No-Code AI Builder: "Add a 'Delivery Area' dropdown to the lead form" → updates JSONB schema + frontend.
  - Workflow generation via text: "If a lead is marked Cancelled, send me an email."

---

## Key Engineering Decisions

| Decision | Choice | Reason |
|---|---|---|
| Custom fields storage | JSONB + GIN index | 1000× faster than EAV at scale; ACID compliant |
| Tenant isolation | PostgreSQL RLS | Cost-effective vs. per-tenant DB instances; secure |
| Real-time transport | WebSockets (SignalR) | Persistent bi-directional; enables token streaming + human handoff |
| AI Voice orchestration | Vapi + OpenRouter | Avoids expensive proprietary model lock-in; $0.08–0.10/min total |
| Bangladesh SMS | BulkSMSBD / MimSMS | Bypasses international routing fees; 60–80% cost savings |
| Email architecture | Dual-pool (Resend + SES) | Preserves transactional sender reputation at scale |
| Microservice comms | gRPC (internal) | Low-latency, typed contracts between services |
| Inter-service events | RabbitMQ + MassTransit | Event-driven decoupling; reliable async operations |

---

## Market Context (Reference)

- Global CRM market: **$126.17B (2026)** → $320.99B (2034) at 12.40% CAGR
- AI-in-CRM sub-market: **36.2% CAGR**
- SaaS CRM sector: ~$224.43B by 2035
- APAC growth: **15.5–16.3% CAGR** — fastest region globally
- Bangladesh B2C e-commerce: **$6.03B (2025)**, 20–25% annual growth; only 3–5% of national retail online (massive runway)
- Primary competitors: Salesforce/Agentforce (enterprise, slow, expensive), HubSpot/Breeze AI (walled garden), Attio, Breakcold, Folk, Close
- Differentiators: Lifecycle continuity (CRM → Project), Zero-touch data entry, Hyper-localization (Bengali NLP, local SMS/payment gateways)

---

## Development Notes

- The project is **greenfield** — no existing code as of June 2026.
- Always use **App Router** in Next.js (never Pages Router).
- All `.NET` projects target `net9.0`.
- TypeScript strict mode is enabled on the frontend.
- Local development uses **Docker Compose** for PostgreSQL, Redis, and RabbitMQ.
- Kubernetes manifests are for production — do not conflate with local dev setup.
- Bengali language support is a **first-class requirement**, not an afterthought — applies to chatbot RAG, STT, TTS, and UI localization.
