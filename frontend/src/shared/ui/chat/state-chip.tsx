import { cn } from '@/shared/lib/cn';
import type { ConversationState } from '@/features/chat/mock-data';

const STATE_CONFIG: Record<
  ConversationState,
  { label: string; bgClass: string; textClass: string; pulse?: boolean }
> = {
  AiHandling: { label: 'AI', bgClass: 'bg-chatState-ai/15', textClass: 'text-chatState-ai' },
  PendingHuman: {
    label: 'Pending',
    bgClass: 'bg-chatState-pending/15',
    textClass: 'text-chatState-pending',
    pulse: true,
  },
  HumanHandling: { label: 'Human', bgClass: 'bg-chatState-human/15', textClass: 'text-chatState-human' },
  Resolved: { label: 'Resolved', bgClass: 'bg-chatState-resolved/15', textClass: 'text-chatState-resolved' },
  Closed: { label: 'Closed', bgClass: 'bg-chatState-closed/15', textClass: 'text-chatState-closed' },
};

interface StateChipProps {
  state: ConversationState;
  className?: string;
}

export function StateChip({ state, className }: StateChipProps) {
  const config = STATE_CONFIG[state];

  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium',
        config.bgClass,
        config.textClass,
        className,
      )}
    >
      {config.pulse && (
        <span
          aria-hidden
          className="h-1.5 w-1.5 animate-pulse rounded-full bg-chatState-pending"
        />
      )}
      {config.label}
    </span>
  );
}
