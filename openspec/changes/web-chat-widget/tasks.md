## 1. Project Setup

- [x] 1.1 Create a new frontend package/workspace for the embeddable widget.
- [x] 1.2 Configure the bundler to output a single JS file and handle CSS (e.g., via Shadow DOM or inline injection).

## 2. API & CORS Updates

- [x] 2.1 Update `NexConvo.Chat.Api` CORS policy to allow cross-origin requests for the SignalR hub.
- [x] 2.2 Ensure the Chat API can authenticate requests via a public tenant token provided by the widget.
- [x] 2.3 Create an API endpoint to fetch widget configuration (colors, icon, welcome message) based on the tenant token.
- [x] 2.4 Update the database schema to store widget configuration settings per tenant.

## 3. UI Implementation

- [x] 3.1 Implement the widget launcher (floating action button) with dynamic icon support.
- [x] 3.2 Implement the chat window layout (header, message list, input area).
- [x] 3.3 Add dynamic styling to apply fetched CSS variables (colors, themes) to the widget.
- [x] 3.4 Implement a new "Chat Widget Settings" page in the main CRM Workspace Settings to allow tenants to configure icon and colors.

## 4. SignalR Integration

- [x] 4.1 Setup SignalR client within the widget application.
- [x] 4.2 Implement connection logic and display connection state (connecting, connected, disconnected).
- [x] 4.3 Handle sending user messages to the SignalR Hub.
- [x] 4.4 Handle receiving AI response messages and displaying them in the message list.

## 5. Build and Distribution

- [x] 5.1 Set up the build process to output a deployable static asset for the widget.
- [x] 5.2 Document the HTML embed snippet that tenants will use.
