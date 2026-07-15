# NexConvo Chat Widget — Embed Guide

## Overview

The NexConvo chat widget is a self-contained JavaScript bundle that any website can embed with a single `<script>` tag. It renders in a Shadow DOM so tenant styles never interfere with the host site.

## Quick Start

Copy and paste the following snippet before the closing `</body>` tag of your website:

```html
<script
  src="https://cdn.nexconvo.io/widget/nexconvo-widget.js"
  data-tenant="YOUR_TENANT_ID"
  data-api-url="https://api.nexconvo.io"
  defer
></script>
```

Replace the placeholder values:

| Attribute      | Required | Description                                                  |
|----------------|----------|--------------------------------------------------------------|
| `data-tenant`  | ✅ Yes   | Your NexConvo Workspace/Tenant UUID                          |
| `data-api-url` | ✅ Yes   | Base URL of your NexConvo Chat API (without trailing slash)  |

## Finding Your Tenant ID

1. Log in to your NexConvo dashboard.
2. Navigate to **Settings → Workspace → General**.
3. Copy the **Workspace ID** field.

## Customization

All visual settings are controlled from your NexConvo dashboard — no code changes needed:

- **Settings → Communication → Chat Widget**
  - Primary color (launcher button & header background)
  - Secondary color (accents)
  - Widget icon (custom logo for the launcher button)
  - Welcome message (the first message visitors see)

## Self-Hosted Deployment

If you are self-hosting NexConvo, copy `dist/nexconvo-widget.js` (produced by `npm run build` in the `widget/` directory) to your static file server or CDN, then update `data-api-url` to your own Chat API endpoint.

```html
<script
  src="https://your-cdn.example.com/static/nexconvo-widget.js"
  data-tenant="YOUR_TENANT_ID"
  data-api-url="https://chat.your-domain.com"
  defer
></script>
```

## How It Works

```
Visitor opens chat
       │
       ▼
WebSocket connection → /hubs/widget?tenantId=<uuid>
       │
       ▼
Visitor types a message → SendMessageAsync("Hello")
       │
       ▼
WidgetHub:
  1. Loads AI config from Redis cache
  2. Retrieves relevant knowledge chunks (RAG)
  3. Streams tokens from LLM back to visitor
       │
       ▼
Widget renders tokens in real-time as they arrive
```

## Connection States

The widget shows a small icon in the chat header:

| Icon | Meaning                       |
|------|-------------------------------|
| 📶  | Connected — ready to chat     |
| 🔄  | Connecting / reconnecting     |
| 📵  | Disconnected — will retry     |

The widget uses automatic exponential-backoff reconnection (0 s → 2 s → 5 s → 10 s).

## Security Notes

- The widget makes **no authenticated requests** — your `tenant_id` is public by design (it identifies your workspace, not grants access).
- All AI processing happens server-side; no API keys are ever exposed to the browser.
- Responses are streamed over WebSockets using the standard SignalR protocol.
- The Shadow DOM isolates all widget CSS/JS from the host page.

## Browser Support

| Browser        | Support |
|----------------|---------|
| Chrome 80+     | ✅      |
| Firefox 75+    | ✅      |
| Safari 14+     | ✅      |
| Edge 80+       | ✅      |
| IE 11          | ❌      |
