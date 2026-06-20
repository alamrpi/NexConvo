import { cn } from '@/shared/lib/cn';

/**
 * Loading placeholder that should match the shape of its final content to avoid
 * layout shift (frontend standard S27).
 */
function Skeleton({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={cn('animate-pulse rounded-md bg-muted', className)} {...props} />;
}

export { Skeleton };
