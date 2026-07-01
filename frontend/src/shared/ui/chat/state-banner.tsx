'use client';

import { Bot, Clock, User, CheckCircle, XCircle } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { Button } from '@/shared/ui/button';
import { SlaCountdown } from './sla-countdown';
import type { ConversationState } from '@/features/chat/mock-data';

interface StateBannerProps {
  state: ConversationState;
  assignedAgentName?: string;
  slaExpiresAt?: string;
  isCurrentAgentAssigned?: boolean;
  onTakeOver?: () => void;
  onAccept?: () => void;
  onResolve?: () => void;
  onReopen?: () => void;
  className?: string;
}

/**
 * Context-sensitive bottom banner for the Conversation View — renders one of 5 variants
 * based on the current conversation state.
 * // TODO: connect SignalR for live state transitions
 */
export function StateBanner({
  state,
  assignedAgentName,
  slaExpiresAt,
  isCurrentAgentAssigned = false,
  onTakeOver,
  onAccept,
  onResolve,
  onReopen,
  className,
}: StateBannerProps) {
  return (
    <div
      className={cn(
        'flex items-center gap-3 border-t border-border bg-card px-4 py-3',
        className,
      )}
    >
      {state === 'AiHandling' && (
        <>
          <Bot className="h-4 w-4 shrink-0 text-chatState-ai" aria-hidden />
          <span className="flex-1 text-sm text-muted-foreground">
            AI is handling this conversation
          </span>
          <Button
            size="sm"
            variant="outline"
            onClick={onTakeOver}
            className="shrink-0"
          >
            Take Over
          </Button>
        </>
      )}

      {state === 'PendingHuman' && (
        <>
          <Clock className="h-4 w-4 shrink-0 text-chatState-pending" aria-hidden />
          <span className="flex-1 text-sm text-muted-foreground">
            Waiting for an agent
            {slaExpiresAt && (
              <>
                {' · SLA '}
                <SlaCountdown slaExpiresAt={slaExpiresAt} className="inline" />
              </>
            )}
          </span>
          <Button
            size="sm"
            onClick={onAccept}
            className="shrink-0 bg-chatState-pending text-white hover:bg-chatState-pending/90"
          >
            Accept
          </Button>
        </>
      )}

      {state === 'HumanHandling' && isCurrentAgentAssigned && (
        <>
          <User className="h-4 w-4 shrink-0 text-chatState-human" aria-hidden />
          <span className="flex-1 text-sm text-muted-foreground">You are handling this conversation</span>
          <Button
            size="sm"
            variant="outline"
            onClick={onResolve}
            className="shrink-0 border-chatState-human text-chatState-human hover:bg-chatState-human/10"
          >
            Resolve
          </Button>
        </>
      )}

      {state === 'HumanHandling' && !isCurrentAgentAssigned && (
        <>
          <User className="h-4 w-4 shrink-0 text-chatState-human" aria-hidden />
          <span className="flex-1 text-sm text-muted-foreground">
            Assigned to <strong>{assignedAgentName ?? 'another agent'}</strong>
          </span>
        </>
      )}

      {state === 'Resolved' && (
        <>
          <CheckCircle className="h-4 w-4 shrink-0 text-chatState-resolved" aria-hidden />
          <span className="flex-1 text-sm text-muted-foreground">Resolved</span>
          <Button size="sm" variant="ghost" onClick={onReopen} className="shrink-0 text-muted-foreground">
            Reopen
          </Button>
        </>
      )}

      {state === 'Closed' && (
        <>
          <XCircle className="h-4 w-4 shrink-0 text-chatState-closed" aria-hidden />
          <span className="flex-1 text-sm text-muted-foreground">Closed</span>
        </>
      )}
    </div>
  );
}
