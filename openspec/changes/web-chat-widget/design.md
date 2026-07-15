## Context

NexConvo operates as a multi-tenant SaaS. Tenants need a way to integrate the RAG-powered chatbot into their own websites. Currently, the chat API exists, but there is no public-facing, drop-in UI component.

## Goals / Non-Goals

**Goals:**
- Provide a lightweight JavaScript snippet that tenants can paste into their HTML.
- Render a responsive, modern chat UI (floating action button + chat window).
- Connect to NexConvo backend via SignalR.
- Identify the tenant via a public-facing API key/token provided in the snippet.

**Non-Goals:**
- Integration with external messaging platforms (WhatsApp, Messenger) - this is a separate capability.

## Decisions

- **Widget Technology:** Use Vanilla JS or a very lightweight React/Preact bundle injected into a Shadow DOM to avoid CSS conflicts with the host website.
- **Tenant Identification:** The snippet will accept a specific public widget token.
- **Communication:** SignalR for real-time WebSocket communication, falling back to long polling if necessary. Connects to `NexConvo.Chat.Api`.
- **CORS:** The Chat API SignalR hub must be configured to allow cross-origin requests.
- **Customization Settings:** The widget will fetch a configuration object (JSON) from the Chat API on initialization. This config will contain the icon URL, primary/secondary colors, and welcome message. These settings will be configured in the main CRM dashboard under Workspace Settings.

## Risks / Trade-offs

- [Risk] Host website CSS conflicts with widget UI. → Mitigation: Use Shadow DOM or strict CSS module scoping.
- [Risk] Large bundle size impacts host website performance. → Mitigation: Optimize the widget bundle and load the main widget logic asynchronously.
- [Risk] Unauthorized abuse of the public endpoint. → Mitigation: Rate limiting on the widget API endpoints based on IP/Token.
