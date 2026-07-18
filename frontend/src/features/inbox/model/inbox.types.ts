// Mirrors the backend DTOs field-for-field (NexConvo.Chat.Application/Features/Conversations/Dtos/*)
// which are themselves shaped to match frontend/src/features/chat/mock-data.ts's Conversation/Message
// types exactly, so the existing shared/ui/chat/* components need zero prop changes.
//
// Known gaps carried over from ConversationSummaryDto's own doc comment (no schema for these exists
// yet on the Conversation entity): assignedAgentName is always null, unreadCount is always 0,
// slaExpiresAt is always null, tags is always [], contactName is always null.

import type { Channel, ConversationState, DeliveryStatus, SenderRole } from '@/features/chat/mock-data';

// Re-exported so consumers of this feature's DTOs don't also need to import from
// features/chat/mock-data directly.
export type { Channel, ConversationState, DeliveryStatus, SenderRole };

export interface ConversationSummaryDto {
  id: string;
  state: ConversationState;
  channel: Channel;
  contactName?: string | null;
  contactHandle: string;
  lastMessagePreview: string;
  lastMessageAt: string;
  lastMessageFromAi: boolean;
  unreadCount: number;
  assignedAgentName?: string | null;
  slaExpiresAt?: string | null;
  tags: string[];
}

export interface MessageDto {
  id: string;
  conversationId: string;
  senderRole: SenderRole;
  senderName?: string | null;
  body: string;
  sentAt: string;
  deliveryStatus?: DeliveryStatus | null;
  confidence?: number | null;
}

/**
 * Opaque keyset-pagination page, mirroring backend CursorPagedResult<T>
 * (Features/Conversations/Dtos/CursorPagedResult.cs). `nextCursor` is null when there is no more
 * data — pass it back as the next request's `cursor` query param to fetch the following page.
 */
export interface CursorPagedResult<T> {
  items: T[];
  nextCursor: string | null;
}

/** Query filters accepted by GetConversationsQuery, as BFF/REST query params. */
export interface ConversationListFilters {
  state?: ConversationState;
  channel?: Channel;
  assignedToMe?: boolean;
  cursor?: string;
  pageSize?: number;
}
