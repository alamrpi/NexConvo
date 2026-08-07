## Why

Now that the RAG implementation is complete, NexConvo needs a way for tenants to embed the AI chatbot directly on their external websites. This web-based chat widget will serve as the primary customer-facing interface, allowing website visitors to interact with the tenant's AI agent seamlessly.

## What Changes

- A new embeddable JavaScript snippet that can be added to any external website.
- A client-side web application (UI) that renders the chatbot interface.
- Automatic tenant identification (via a unique token/ID in the embed script) to route conversations properly.
- Integration with the existing Chat API to send/receive real-time messages using SignalR.
- Full customization of the widget's design (icon, colors, welcome message) controlled from the NexConvo workspace settings.

## Capabilities

### New Capabilities
- `embeddable-chat-widget`: A script and UI application that renders a chat widget on external websites, connecting to the NexConvo Chat API via SignalR for real-time, tenant-specific conversations.

### Modified Capabilities

## Impact

- **Frontend:** A new distinct UI build/package will be created for the chat widget to keep it lightweight and isolated from the main CRM dashboard.
- **Backend APIs:** May require CORS updates or public endpoint exposure for the chat SignalR hub to allow cross-origin requests from client websites.
- **Tenant Management:** Needs a way to generate and validate widget embed tokens/keys for each tenant.
