import React from 'react'
import ReactDOM from 'react-dom/client'
import { ChatWidget } from './ChatWidget'
import cssText from './style.css?inline'

// Extract config from the script tag
const currentScript = document.currentScript as HTMLScriptElement;
const tenantId = currentScript?.getAttribute('data-tenant') || '643551d2-d18d-4840-ac4b-f6fcd41b7dca';

let defaultApiUrl = 'http://localhost:5000';
const scriptUrl = currentScript?.src;
if (scriptUrl) {
  try {
    const parsedUrl = new URL(scriptUrl);
    // If the script is loaded from production/external CDN rather than local Vite,
    // infer the API URL to be the same base domain.
    if (parsedUrl.hostname !== 'localhost' && parsedUrl.hostname !== '127.0.0.1') {
      defaultApiUrl = `${parsedUrl.protocol}//${parsedUrl.hostname}`;
    }
  } catch (e) {
    console.warn('[NexConvo Widget] Could not auto-detect API URL from script source:', e);
  }
}

const apiUrl = currentScript?.getAttribute('data-api-url') || defaultApiUrl;

// Create the host element
const host = document.createElement('div');
host.id = 'nexconvo-widget-root';
// Make sure it sits on top
host.style.position = 'fixed';
host.style.bottom = '20px';
host.style.right = '20px';
host.style.zIndex = '999999';
document.body.appendChild(host);

// Create shadow DOM
const shadow = host.attachShadow({ mode: 'open' });

// Inject CSS
const style = document.createElement('style');
style.textContent = cssText;
shadow.appendChild(style);

// Create React root
const root = document.createElement('div');
root.style.display = 'flex';
root.style.flexDirection = 'column';
root.style.alignItems = 'flex-end';
shadow.appendChild(root);

ReactDOM.createRoot(root).render(
  <React.StrictMode>
    <ChatWidget tenantId={tenantId} apiUrl={apiUrl} />
  </React.StrictMode>
)
