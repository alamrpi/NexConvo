'use client';

import { cn } from '@/shared/lib/cn';
import { ChannelBadge } from './channel-badge';
import { StateChip } from './state-chip';
import { SlaCountdown } from './sla-countdown';
import type { Conversation } from '@/features/chat/mock-data';

interface ConversationListItemProps {
  conv: Conversation;
  isSelected: boolean;
  onClick: () => void;
}

export function ConversationListItem({ conv, isSelected, onClick }: ConversationListItemProps) {
  const name = conv.contactName ?? conv.contactHandle;
  const initials = name
    .split(' ')
    .map((n) => n[0])
    .join('')
    .slice(0, 2)
    .toUpperCase();

  return (
    <button
      type="button"
      onClick={onClick}
      aria-current={isSelected ? 'true' : undefined}
      aria-label={`Conversation with ${name}`}
      className={cn(
        'flex w-full items-start gap-3 rounded-lg px-3 py-3 text-left transition-colors',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
        isSelected
          ? 'bg-primary/10'
          : 'hover:bg-accent',
      )}
    >
      {/* Avatar */}
      <div
        aria-hidden
        className="mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-muted font-medium text-sm text-foreground"
      >
        {initials}
      </div>

      <div className="min-w-0 flex-1">
        {/* Row 1: name + time */}
        <div className="flex items-baseline justify-between gap-1">
          <span className="truncate text-sm font-medium text-foreground">{name}</span>
          <time
            dateTime={conv.lastMessageAt}
            className="shrink-0 text-[0.625rem] tabular-nums text-muted-foreground"
            suppressHydrationWarning
          >
            {new Date(conv.lastMessageAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
          </time>
        </div>

        {/* Row 2: channel + state chip + SLA */}
        <div className="mt-0.5 flex items-center gap-1.5">
          <ChannelBadge channel={conv.channel} size="sm" />
          <StateChip state={conv.state} className="text-[0.6rem] px-1.5 py-px" />
          {conv.state === 'PendingHuman' && conv.slaExpiresAt && (
            <SlaCountdown slaExpiresAt={conv.slaExpiresAt} />
          )}
        </div>

        {/* Row 3: last message preview + unread badge */}
        <div className="mt-1 flex items-end justify-between gap-2">
          <p className="line-clamp-1 text-xs text-muted-foreground font-message">
            {conv.lastMessageFromAi && (
              <span className="mr-1 font-medium text-chatState-ai">AI:</span>
            )}
            {conv.lastMessagePreview}
          </p>
          {conv.unreadCount > 0 && (
            <span
              aria-label={`${conv.unreadCount} unread`}
              className="flex h-4 min-w-[1rem] shrink-0 items-center justify-center rounded-full bg-primary px-1 text-[0.5rem] font-bold text-primary-foreground"
            >
              {conv.unreadCount}
            </span>
          )}
        </div>

        {/* Tags */}
        {conv.tags.length > 0 && (
          <div className="mt-1.5 flex flex-wrap gap-1">
            {conv.tags.slice(0, 3).map((tag) => (
              <span
                key={tag}
                className="rounded-full bg-secondary px-1.5 py-px text-[0.5625rem] text-secondary-foreground"
              >
                {tag}
              </span>
            ))}
          </div>
        )}
      </div>
    </button>
  );
}
