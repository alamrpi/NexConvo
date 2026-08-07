# Prompt: Develop NexConvo AI Playground Feature

## Role Context
You are an Expert System Analyzer, Backend (.NET Core 9) and Frontend (Next.js / React) Developer for the NexConvo project. You strictly adhere to **NexConvo Enterprise Standards** (Clean Architecture, CQRS with MediatR, multi-tenant PostgreSQL RLS) and **Frontend Standards** (Feature-Sliced Design, strict TypeScript, SignalR for real-time WebSockets, responsive Tailwind CSS).

## Task Context
We need to fully develop the backend and frontend integration for the **Playground Feature**, located in the frontend at `frontend/src/app/(dashboard)/dashboard/chat/playground/page.tsx`. 

**Critical Information:** 
- The UI/UX for the Playground is already implemented and currently uses mock data (`generateMockAiResponse`).
- The **RAG (Retrieval-Augmented Generation) pipeline is already complete** in the backend. 
- You do NOT need to build the core RAG components (chunking, embedding, pgvector setup). Your focus is on connecting this existing RAG pipeline to the Playground UI via SignalR and adding dynamic model selection.

## Feature Requirements

### 1. Dynamic Model Selection Feature
- **Backend (CQRS):** Implement a query (e.g., `GetAvailableProvidersAndModelsQuery`) to return a list of AI providers (e.g., OpenRouter, OpenAI, Anthropic, Gemini) and their supported models.
- **Frontend Integration:** Update the `ComparePaneConfig` component in the Playground UI to fetch this data dynamically. When a provider is selected from the dropdown, the corresponding models dropdown must update appropriately. Capture the selected provider and model to send with Playground requests.

### 2. Real-Time Playground Execution via SignalR
- **Architecture Rule:** You MUST use ASP.NET Core SignalR (WebSockets) for all chat interactions. Do not use SSE or polling.
- **Backend Hub (`PlaygroundHub`):** Create or extend a SignalR hub to handle incoming playground execution requests. 
  - The request should include: `userMessage`, `providerId`, `modelId`, and any active `systemPrompt`.
  - The Hub must securely extract the `tenant_id` from the JWT and ensure the existing RAG pipeline executes under this tenant context.
- **Frontend Integration:** Replace the mocked `generateMockAiResponse` logic with a SignalR client connection. The frontend must send the message over the WebSocket and handle the streamed response.

### 3. Debug Payload Integration
The existing UI has a detailed debug panel with tabs (Chunks, Confidence, Prompt, Response, PII, Performance). The backend SignalR stream must return this exact metadata (alongside the streamed text) to populate the UI.
- **Chunks Tab:** The stream must return the retrieved chunks (document name, preview, full text, score) used by the existing RAG pipeline.
- **Confidence Tab:** Return the confidence score (calculated as `score = retrieval_score * 0.6 + groundedness * 0.4`) and confidence band (`High`, `Medium`, `Low`).
- **Prompt & Response Tabs:** Return the token usage (`promptTokens`, `completionTokens`, `totalTokens`), the raw prompt preview, and the raw LLM response payload.
- **PII Tab:** Ensure any PII detected by the pipeline is returned as an array of objects (`{ token, type }`).
- **Performance Tab:** Return accurate timing telemetry in milliseconds for the `embed`, `retrieval`, and `llm` execution phases.

## Implementation Steps
1. **Model Selection API:** Develop the `Provider/Model` list endpoint using CQRS in the backend. Update the frontend UI to consume it.
2. **SignalR Hub Creation:** Implement `PlaygroundHub.cs` and wire it up to the **already completed RAG pipeline**.
3. **Frontend SignalR Connection:** Implement the WebSocket connection on the Next.js side to stream the RAG pipeline's response and debug data into the existing UI components.
4. **Validation:** Ensure the tenant isolation (RLS) is maintained and all debug data correctly renders in the `DebugPanel`.
