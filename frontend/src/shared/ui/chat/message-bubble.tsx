import { cn } from '@/shared/lib/cn';
import { FileText, ImageIcon, Mic, MapPin } from 'lucide-react';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/shared/ui/tooltip';
import { DeliveryStatusIcon } from './delivery-status';
import { ConfidenceBar } from './confidence-bar';
import type { Message } from '@/features/chat/mock-data';

interface MessageBubbleProps {
  message: Message;
  isCurrentAgent: boolean;
}

function AttachmentPreview({ message }: { message: Message }) {
  if (!message.attachments?.length) return null;

  return (
    <div className="mt-1.5 flex flex-col gap-1">
      {message.attachments.map((att, i) => (
        <div
          key={i}
          className="flex items-center gap-2 rounded-md border border-border bg-muted/40 px-3 py-2 text-sm"
        >
          {att.type === 'Image' && <ImageIcon className="h-4 w-4 shrink-0 text-muted-foreground" />}
          {att.type === 'Document' && <FileText className="h-4 w-4 shrink-0 text-muted-foreground" />}
          {att.type === 'Audio' && <Mic className="h-4 w-4 shrink-0 text-muted-foreground" />}
          {att.type === 'Location' && <MapPin className="h-4 w-4 shrink-0 text-muted-foreground" />}
          <span className="truncate text-xs text-muted-foreground">
            {att.caption ?? att.type}
            {att.fileSizeBytes != null && (
              <span className="ml-1 text-muted-foreground/60">
                ({Math.round(att.fileSizeBytes / 1024)} KB)
              </span>
            )}
          </span>
        </div>
      ))}
    </div>
  );
}

export function MessageBubble({ message, isCurrentAgent }: MessageBubbleProps) {
  const { senderRole } = message;

  // System event — full-width centered pill
  if (senderRole === 'System') {
    return (
      <div className="flex justify-center py-1">
        <span className="rounded-full bg-muted px-3 py-1 text-xs text-muted-foreground">
          {message.body}
        </span>
      </div>
    );
  }

  const isContact = senderRole === 'Contact';
  const isAgent = senderRole === 'Agent' && isCurrentAgent;
  const isOtherAgent = senderRole === 'Agent' && !isCurrentAgent;
  const isAi = senderRole === 'Ai';

  const alignRight = isAgent;

  return (
    <div className={cn('flex gap-2', alignRight ? 'flex-row-reverse' : 'flex-row')}>
      {/* Avatar dot */}
      <div
        aria-hidden
        className={cn(
          'mt-1 h-6 w-6 shrink-0 rounded-full text-[0.5rem] flex items-center justify-center font-bold text-white',
          isContact && 'bg-muted-foreground/40',
          isAi && 'bg-chatState-ai',
          isAgent && 'bg-primary',
          isOtherAgent && 'bg-chatState-human',
        )}
      >
        {isContact ? (message.senderName?.[0] ?? 'C') : isAi ? 'AI' : (message.senderName?.[0] ?? 'A')}
      </div>

      <div className={cn('flex max-w-[70%] flex-col gap-0.5', alignRight && 'items-end')}>
        {/* Sender name */}
        {message.senderName && (
          <span className="text-xs font-medium text-muted-foreground">{message.senderName}</span>
        )}
        {isAi && !message.senderName && (
          <span className="text-xs font-medium text-chatState-ai">AI Assistant</span>
        )}

        {/* Bubble */}
        <div
          className={cn(
            'rounded-2xl px-3 py-2 text-sm leading-relaxed font-message',
            isContact && 'rounded-tl-sm bg-muted text-foreground',
            isAi && 'rounded-tl-sm border border-chatState-ai/20 bg-chatState-ai/5 text-foreground',
            isAgent && 'rounded-tr-sm bg-primary text-primary-foreground',
            isOtherAgent && 'rounded-tl-sm bg-chatState-human/10 text-foreground',
          )}
        >
          {message.isStreaming ? (
            <span className="animate-pulse text-muted-foreground">▌</span>
          ) : (
            message.body
          )}
          <AttachmentPreview message={message} />
        </div>

        {/* AI confidence bar */}
        {isAi && message.confidence != null && (
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <span>
                  <ConfidenceBar score={message.confidence} />
                </span>
              </TooltipTrigger>
              <TooltipContent side="bottom" className="text-xs">
                AI confidence: {Math.round(message.confidence * 100)}%
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
        )}

        {/* Timestamp + delivery status */}
        <div className={cn('flex items-center gap-1', alignRight && 'flex-row-reverse')}>
          <time
            dateTime={message.sentAt}
            className="text-[0.625rem] text-muted-foreground/60"
            suppressHydrationWarning
          >
            {/* Convert in client component; SSR shows nothing (suppressHydrationWarning) */}
            {new Date(message.sentAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
          </time>
          {message.deliveryStatus && (
            <DeliveryStatusIcon status={message.deliveryStatus} className="text-[0.625rem]" />
          )}
        </div>
      </div>
    </div>
  );
}
