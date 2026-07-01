import { cn } from '@/shared/lib/cn';
import type { Channel } from '@/features/chat/mock-data';

const CHANNEL_CONFIG: Record<
  Channel,
  { label: string; letter: string; bgClass: string; textClass: string }
> = {
  whatsapp: { label: 'WhatsApp', letter: 'W', bgClass: 'bg-chatChannel-whatsapp', textClass: 'text-white' },
  facebook: { label: 'Facebook', letter: 'F', bgClass: 'bg-chatChannel-facebook', textClass: 'text-white' },
  instagram: { label: 'Instagram', letter: 'I', bgClass: 'bg-chatChannel-instagram', textClass: 'text-white' },
  telegram: { label: 'Telegram', letter: 'T', bgClass: 'bg-chatChannel-telegram', textClass: 'text-white' },
  web: { label: 'Web', letter: 'W', bgClass: 'bg-chatChannel-web', textClass: 'text-white' },
};

interface ChannelBadgeProps {
  channel: Channel;
  size?: 'sm' | 'md';
  className?: string;
}

export function ChannelBadge({ channel, size = 'md', className }: ChannelBadgeProps) {
  const config = CHANNEL_CONFIG[channel];
  const sizeClass = size === 'sm' ? 'h-4 w-4 text-[0.5rem]' : 'h-5 w-5 text-[0.625rem]';

  return (
    <span
      role="img"
      aria-label={config.label}
      title={config.label}
      className={cn(
        'inline-flex items-center justify-center rounded-full font-bold leading-none',
        config.bgClass,
        config.textClass,
        sizeClass,
        className,
      )}
    >
      {config.letter}
    </span>
  );
}
