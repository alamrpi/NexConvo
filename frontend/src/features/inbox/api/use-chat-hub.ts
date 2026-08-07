'use client';

import { useEffect, useRef, useState } from 'react';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';
import type {
  ConversationSummaryDto,
  CursorPagedResult,
  DeliveryStatus,
  MessageDto,
  SenderRole,
} from '../model/inbox.types';
import { conversationMessagesQueryKey } from './use-conversation-messages';

// Kept local to features/inbox (design.md decision 4 / tasks.md 6.3): the playground's inline
// SignalR setup (dashboard/chat/playground/page.tsx) is similar but not unified with this hook —
// per design.md's accepted trade-off, unifying them is deferred to a follow-up change once both
// are proven, rather than generalizing into shared/lib now.

/** Mirrors the backend's ChatEventEnvelope (Realtime/ChatEventEnvelope.cs): {type, payload}. */
interface ChatEventEnvelope {
  type: string;
  payload: unknown;
}

// String literals mirroring ChatEventTypes (Realtime/ChatEventEnvelope.cs) — Token/Complete are
// handled by the playground/RAG stream, not the inbox; Handoff moves a conversation into
// PendingHuman and is treated like any other list-changing event here.
const CHAT_EVENT_TYPES = {
  message: 'message',
  assigned: 'assigned',
  resolved: 'resolved',
  reopened: 'reopened',
  handoff: 'handoff',
} as const;

// Mirrors MessageEventPayload (Realtime/ChatEventEnvelope.cs) exactly — note MessageId, not Id,
// is the wire field name (distinct from MessageDto, which uses Id).
interface MessageEventPayload {
  conversationId: string;
  messageId: string;
  senderRole: SenderRole;
  senderName?: string | null;
  body: string;
  sentAt: string;
  deliveryStatus?: DeliveryStatus | null;
  confidence?: number | null;
}

interface AssignedEventPayload {
  conversationId: string;
  agentUserId: string;
  assignedAt: string;
}

interface ResolvedEventPayload {
  conversationId: string;
  resolvedAt: string;
}

interface ReopenedEventPayload {
  conversationId: string;
  reopenedAt: string;
}

interface HandoffEventPayload {
  conversationId: string;
  escalationId: string;
  reason: string;
  raisedAt: string;
}

export type ChatHubConnectionState = 'connecting' | 'connected' | 'reconnecting' | 'disconnected';

/**
 * Owns one SignalR connection to `hubs/chat` for the agent inbox: joins the tenant's agent
 * dashboard group once connected, joins/leaves the currently-open conversation's group as
 * `activeConversationId` changes, and dispatches every `chatEvent` into the React Query cache
 * (setQueryData for messages so an open thread updates without a refetch; invalidateQueries for
 * the conversation list so it re-sorts/updates unread state — see design.md decision 2/4).
 *
 * `activeConversationId` should be the id of the conversation the agent currently has open, or
 * null when the inbox has nothing selected.
 */
export function useChatHub(activeConversationId: string | null) {
  const queryClient = useQueryClient();
  const [connectionState, setConnectionState] = useState<ChatHubConnectionState>('connecting');
  const [error, setError] = useState<string | null>(null);
  const connectionRef = useRef<HubConnection | null>(null);

  // ── Connection lifecycle: established once per mount, not per conversation switch ──
  useEffect(() => {
    let cancelled = false;
    let hubConnection: HubConnection | undefined;

    fetch('/api/bff/auth/ws-ticket?hub=chat')
      .then((res) => res.json())
      .then((data) => {
        if (cancelled) return;
        if (!data.ticket || !data.url) {
          throw new Error('No ticket or url returned from ws-ticket endpoint');
        }

        hubConnection = new HubConnectionBuilder()
          .withUrl(`${data.url}?access_token=${data.ticket}`)
          .withAutomaticReconnect()
          .configureLogging(LogLevel.Information)
          .build();

        hubConnection.on('chatEvent', (envelope: ChatEventEnvelope) => {
          dispatchChatEvent(queryClient, envelope);
        });

        hubConnection.onclose((err) => {
          setConnectionState('disconnected');
          setError(err ? 'Connection lost. Please refresh.' : null);
        });
        hubConnection.onreconnecting(() => setConnectionState('reconnecting'));
        hubConnection.onreconnected(() => {
          setConnectionState('connected');
          setError(null);
          // Re-join the agent dashboard group and any active conversation after a reconnect —
          // SignalR groups do not survive a dropped connection.
          hubConnection?.invoke('JoinAgentDashboard').catch(() => {});
          if (activeConversationId) {
            hubConnection?.invoke('JoinConversation', activeConversationId).catch(() => {});
          }
        });

        return hubConnection.start().then(() => {
          if (cancelled) {
            hubConnection?.stop();
            return;
          }
          connectionRef.current = hubConnection ?? null;
          setConnectionState('connected');
          setError(null);
          return hubConnection?.invoke('JoinAgentDashboard');
        });
      })
      .catch((err) => {
        console.error('Failed to establish chat hub connection:', err);
        if (!cancelled) {
          setConnectionState('disconnected');
          setError('Could not connect to the chat service. Please refresh.');
        }
      });

    return () => {
      cancelled = true;
      connectionRef.current = null;
      hubConnection?.stop();
    };
    // Intentionally connect once per mount — activeConversationId changes are handled by the
    // separate join/leave effect below so switching conversations doesn't tear down the socket.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [queryClient]);

  // ── Per-conversation group membership: join on select, leave on switch/unmount ──
  useEffect(() => {
    const connection = connectionRef.current;
    if (!connection || connectionState !== 'connected' || !activeConversationId) return;

    connection.invoke('JoinConversation', activeConversationId).catch((err) => {
      console.error('Failed to join conversation group:', err);
    });

    return () => {
      if (connection.state === 'Connected') {
        connection.invoke('LeaveConversation', activeConversationId).catch(() => {});
      }
    };
  }, [activeConversationId, connectionState]);

  return { connectionState, error };
}

function dispatchChatEvent(
  queryClient: ReturnType<typeof useQueryClient>,
  envelope: ChatEventEnvelope,
) {
  switch (envelope.type) {
    case CHAT_EVENT_TYPES.message: {
      const payload = envelope.payload as MessageEventPayload;
      const message: MessageDto = {
        id: payload.messageId,
        conversationId: payload.conversationId,
        senderRole: payload.senderRole,
        senderName: payload.senderName,
        body: payload.body,
        sentAt: payload.sentAt,
        deliveryStatus: payload.deliveryStatus,
        confidence: payload.confidence,
      };
      queryClient.setQueriesData<CursorPagedResult<MessageDto>>(
        { queryKey: conversationMessagesQueryKey(payload.conversationId) },
        (current) => {
          if (!current) return current;
          if (current.items.some((m) => m.id === message.id)) return current;
          return { ...current, items: [...current.items, message] };
        },
      );
      queryClient.invalidateQueries({ queryKey: ['inbox', 'conversations'], exact: false });
      break;
    }

    case CHAT_EVENT_TYPES.assigned: {
      const payload = envelope.payload as AssignedEventPayload;
      patchConversationSummary(queryClient, payload.conversationId, { state: 'HumanHandling' });
      break;
    }

    case CHAT_EVENT_TYPES.resolved: {
      const payload = envelope.payload as ResolvedEventPayload;
      patchConversationSummary(queryClient, payload.conversationId, { state: 'Resolved' });
      break;
    }

    case CHAT_EVENT_TYPES.reopened: {
      const payload = envelope.payload as ReopenedEventPayload;
      patchConversationSummary(queryClient, payload.conversationId, { state: 'AiHandling' });
      break;
    }

    case CHAT_EVENT_TYPES.handoff: {
      const payload = envelope.payload as HandoffEventPayload;
      patchConversationSummary(queryClient, payload.conversationId, { state: 'PendingHuman' });
      break;
    }

    default:
      // Token/Complete (RAG streaming) and any future event types are not handled by the inbox —
      // ignore rather than throw so this hook stays additive-only as new event types are added.
      break;
  }
}

function patchConversationSummary(
  queryClient: ReturnType<typeof useQueryClient>,
  conversationId: string,
  patch: Partial<ConversationSummaryDto>,
) {
  queryClient.setQueriesData<CursorPagedResult<ConversationSummaryDto>>(
    { queryKey: ['inbox', 'conversations'], exact: false },
    (current) => {
      if (!current) return current;
      return {
        ...current,
        items: current.items.map((c) => (c.id === conversationId ? { ...c, ...patch } : c)),
      };
    },
  );
  // Also invalidate: filtered views (e.g. "pending" tab) may need the row to move in/out of the
  // result set entirely, which a client-side patch alone can't do.
  queryClient.invalidateQueries({ queryKey: ['inbox', 'conversations'], exact: false });
}
