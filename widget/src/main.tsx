import React from 'react'
import ReactDOM from 'react-dom/client'
import { ChatWidget } from './ChatWidget'
import cssText from './style.css?inline'

// Config comes entirely from the <script> tag — no hardcoded fallbacks (audit M4):
//   <script src="https://widget.example.com/nexconvo-widget.js"
//           data-token="<WidgetToken>" data-api-url="https://api.example.com" defer></script>
// `data-token` is the tenant's unguessable public WidgetToken (never the tenant id).
const currentScript = (document.currentScript as HTMLScriptElement | null)
    ?? (document.querySelector('script[data-token]') as HTMLScriptElement | null);

const token = currentScript?.getAttribute('data-token')?.trim() ?? '';

// API base: explicit data-api-url wins; otherwise infer the origin the script was served from.
function inferApiUrl(): string {
    const explicit = currentScript?.getAttribute('data-api-url')?.trim();
    if (explicit) return explicit.replace(/\/$/, '');
    const src = currentScript?.src;
    if (src) {
        try {
            return new URL(src).origin;
        } catch {
            /* fall through */
        }
    }
    return window.location.origin;
}

if (!token) {
    // Nothing to connect to without a token — fail quietly rather than mounting a broken widget.
    console.error('[NexConvo Widget] Missing required data-token attribute on the embed script.');
} else {
    const apiUrl = inferApiUrl();

    const host = document.createElement('div');
    host.id = 'nexconvo-widget-root';
    host.style.position = 'fixed';
    host.style.bottom = '20px';
    host.style.right = '20px';
    host.style.zIndex = '999999';
    document.body.appendChild(host);

    const shadow = host.attachShadow({ mode: 'open' });

    const style = document.createElement('style');
    style.textContent = cssText;
    shadow.appendChild(style);

    const root = document.createElement('div');
    root.style.display = 'flex';
    root.style.flexDirection = 'column';
    root.style.alignItems = 'flex-end';
    shadow.appendChild(root);

    ReactDOM.createRoot(root).render(
        <React.StrictMode>
            <ChatWidget token={token} apiUrl={apiUrl} />
        </React.StrictMode>
    );
}
