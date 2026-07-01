import { AlertTriangle } from 'lucide-react';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/shared/ui/tooltip';
import { cn } from '@/shared/lib/cn';
import type { DeliveryStatus } from '@/features/chat/mock-data';

interface DeliveryStatusProps {
  status: DeliveryStatus;
  className?: string;
}

export function DeliveryStatusIcon({ status, className }: DeliveryStatusProps) {
  if (status === 'Sent') {
    return (
      <span aria-label="Sent" className={cn('text-muted-foreground/60', className)}>
        ✓
      </span>
    );
  }
  if (status === 'Delivered') {
    return (
      <span aria-label="Delivered" className={cn('text-muted-foreground/60', className)}>
        ✓✓
      </span>
    );
  }
  if (status === 'Read') {
    return (
      <span aria-label="Read" className={cn('text-blue-500', className)}>
        ✓✓
      </span>
    );
  }
  if (status === 'Failed' || status === 'PermanentlyFailed') {
    const label = status === 'Failed' ? 'Delivery failed' : 'Permanently failed — message not delivered';
    return (
      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>
            <span aria-label={label} className={cn('inline-flex cursor-default text-destructive', className)}>
              <AlertTriangle className="h-3.5 w-3.5" />
            </span>
          </TooltipTrigger>
          <TooltipContent side="top">{label}</TooltipContent>
        </Tooltip>
      </TooltipProvider>
    );
  }
  return null;
}
