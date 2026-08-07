## ADDED Requirements

### Requirement: Widget Snippet Embedding
The system SHALL provide a JavaScript snippet that tenants can copy and embed on external websites to load the chat widget.

#### Scenario: Host website loads snippet
- **WHEN** the host website renders and executes the embedded script
- **THEN** the chat widget launcher button is rendered on the page
- **AND** the widget identifies itself to the NexConvo API using the provided tenant token

### Requirement: Chat Interface Interaction
The widget SHALL provide a UI for users to view and send messages to the tenant's AI agent.

#### Scenario: User opens chat
- **WHEN** the user clicks the widget launcher button
- **THEN** the chat window opens displaying the welcome message

#### Scenario: User sends a message
- **WHEN** the user types a message and clicks send
- **THEN** the message is displayed in the chat window as a user message
- **AND** the message is sent to the NexConvo API via SignalR
- **AND** the AI's response is streamed or displayed back in the chat window

### Requirement: Widget Customization
The chat widget SHALL be fully customizable in appearance (icon, colors) via the tenant's workspace settings.

#### Scenario: Widget loads customized settings
- **WHEN** the widget initializes
- **THEN** it fetches the customization configuration from the API
- **AND** the widget's UI applies the configured colors and launcher icon
