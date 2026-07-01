'use client';

import * as React from 'react';
import { cn } from '@/shared/lib/cn';

interface SlaCountdownProps {
  slaExpiresAt: string;
  className?: string;
}

function formatRemaining(ms: number): string {
  if (ms <= 0) return '0:00';
  const totalSeconds = Math.floor(ms / 1000);
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, '0')}`;
}

function colorClass(ms: number): string {
  if (ms <= 0) return 'text-chatConfidence-low';
  if (ms <= 2 * 60 * 1000) return 'text-chatConfidence-low';
  if (ms <= 5 * 60 * 1000) return 'text-chatConfidence-medium';
  return 'text-chatConfidence-high';
}

/**
 * Client component — ticks down every second from slaExpiresAt.
 * // TODO: connect SignalR for server-authoritative SLA updates
 */
export function SlaCountdown({ slaExpiresAt, className }: SlaCountdownProps) {
  // null until after mount — avoids SSR/client time mismatch (hydration error)
  const [remaining, setRemaining] = React.useState<number | null>(null);

  React.useEffect(() => {
    const update = () => {
      setRemaining(new Date(slaExpiresAt).getTime() - Date.now());
    };
    update();
    const interval = setInterval(update, 1000);
    return () => clearInterval(interval);
  }, [slaExpiresAt]);

  if (remaining === null) return null;

  const expired = remaining <= 0;

  return (
    <span
      aria-label={expired ? 'SLA expired' : `SLA: ${formatRemaining(remaining)} remaining`}
      className={cn('font-mono text-xs tabular-nums', colorClass(remaining), className)}
    >
      {expired ? 'Expired' : formatRemaining(remaining)}
    </span>
  );
}
