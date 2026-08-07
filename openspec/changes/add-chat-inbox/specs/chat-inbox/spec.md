## ADDED Requirements

### Requirement: List Conversations
The system SHALL allow an authenticated agent with the `conversations:read` permission to retrieve a paged list of conversations for their tenant, filterable by conversation state, channel, and whether the conversation is assigned to the requesting agent.

#### Scenario: Agent lists all open conversations
- **WHEN** an agent with `conversations:read` requests the conversation list with no filters
- **THEN** the system returns a paged list of conversations belonging only to the agent's tenant, ordered by most recent activity

#### Scenario: Agent filters by state
- **WHEN** an agent requests the conversation list filtered to `PendingHuman`
- **THEN** the system returns only conversations currently in the `PendingHuman` state

#### Scenario: Agent filters to their own assigned conversations
- **WHEN** an agent requests the conversation list with "assigned to me" filter enabled
- **THEN** the system returns only conversations where `AssignedAgentUserId` matches the requesting agent

#### Scenario: Unauthorized user is refused
- **WHEN** a user without `conversations:read` requests the conversation list
- **THEN** the system rejects the request

### Requirement: View Conversation Message History
The system SHALL allow an authenticated agent to retrieve the paged message history of a single conversation, provided the agent is the assigned agent or holds `conversations:read`.

#### Scenario: Assigned agent views message history
- **WHEN** the assigned agent requests messages for their conversation
- **THEN** the system returns the conversation's messages in chronological order, including sender role, delivery status, and confidence score where applicable

#### Scenario: Agent without access is refused
- **WHEN** an agent who is neither assigned nor holds `conversations:read` requests messages for a conversation outside their tenant or without access
- **THEN** the system refuses the request with the same response used for a nonexistent conversation, without revealing whether the conversation exists

### Requirement: Agent Sends a Reply
The system SHALL allow the assigned agent (or an agent with sufficient permission) to send a reply message on a conversation, which is persisted and delivered to the customer's channel.

#### Scenario: Agent sends a reply
- **WHEN** an authorized agent submits reply text for a conversation
- **THEN** the system persists an outbound `Message` with sender role `Agent`
- **AND** the conversation's `UpdatedAt` timestamp is refreshed
- **AND** a real-time event is broadcast to subscribers of that conversation and to the tenant's agent dashboard group

#### Scenario: Reply to a resolved conversation is rejected
- **WHEN** an agent attempts to send a reply to a conversation in a terminal state that does not accept new outbound agent messages per the domain's state rules
- **THEN** the system rejects the command and no message is persisted

### Requirement: Agent Takes Over a Conversation
The system SHALL allow an authorized agent to take ownership of a conversation that is pending human handoff, transitioning it to human-handled state.

#### Scenario: Agent takes over a pending conversation
- **WHEN** an authorized agent takes over a conversation in the `PendingHuman` state
- **THEN** the conversation transitions to `HumanHandling`
- **AND** `AssignedAgentUserId` is set to the taking-over agent
- **AND** a real-time "assigned" event is broadcast to the tenant's agent dashboard group

#### Scenario: Take-over of a conversation not pending human handoff is rejected
- **WHEN** an agent attempts to take over a conversation that is not in `PendingHuman` state
- **THEN** the system rejects the command and the conversation's state is unchanged

### Requirement: Agent Resolves a Conversation
The system SHALL allow an authorized agent to mark an actively-handled conversation as resolved.

#### Scenario: Agent resolves a conversation
- **WHEN** an authorized agent resolves a conversation in `HumanHandling` or `AiHandling` state
- **THEN** the conversation transitions to `Resolved`
- **AND** a real-time "resolved" event is broadcast to subscribers of that conversation and to the tenant's agent dashboard group

### Requirement: Agent Reopens a Conversation
The system SHALL allow an authorized agent to reopen a conversation that was previously resolved or closed.

#### Scenario: Agent reopens a resolved conversation
- **WHEN** an authorized agent reopens a conversation in `Resolved` or `Closed` state
- **THEN** the conversation transitions back to `AiHandling`
- **AND** a real-time "reopened" event is broadcast to subscribers of that conversation and to the tenant's agent dashboard group

### Requirement: Live Inbox Updates
The system SHALL push conversation and message events to connected agent clients in real time, so the conversation list and open conversation view update without requiring a manual refresh.

#### Scenario: New message appears live in an open conversation
- **WHEN** a new message is appended to a conversation that an agent currently has open
- **THEN** the agent's client receives a real-time event containing the new message without polling or refreshing

#### Scenario: Conversation list updates live on state change
- **WHEN** a conversation assigned to, or visible within, an agent's tenant changes state (assigned, resolved, reopened) or receives a new message
- **THEN** every connected agent dashboard client for that tenant receives a real-time event reflecting the change

### Requirement: Web Widget Conversations Are Persisted
The system SHALL persist conversations and messages originating from the web chat widget as durable `Conversation` and `Message` records, so they are visible in the agent inbox like any other channel's conversations.

#### Scenario: Widget visitor sends a message
- **WHEN** an anonymous web widget visitor sends a message
- **THEN** the system creates or finds the open `Conversation` for that widget session's external identity
- **AND** persists the inbound message
- **AND** the conversation becomes visible to agents via the conversation list

#### Scenario: Widget-originated conversation supports the same actions as any other channel
- **WHEN** an agent views a conversation that originated from the web widget
- **THEN** the agent can view its message history, reply, take it over, resolve it, and reopen it identically to a conversation from any other channel
