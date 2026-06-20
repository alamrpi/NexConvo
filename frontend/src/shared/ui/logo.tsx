import { cn } from '@/shared/lib/cn';

interface LogoProps {
  /** Brand name, passed in by the caller from the i18n layer (S12). */
  label: string;
  className?: string;
  /** Render only the mark, no wordmark (e.g. collapsed contexts). */
  iconOnly?: boolean;
}

/**
 * NexConvo brand mark — a single source of truth for the logo so it never gets
 * hand-rolled per surface (S18). Uses the primary token, restrained accent (S28).
 */
export function Logo({ label, className, iconOnly = false }: LogoProps) {
  return (
    <span className={cn('inline-flex items-center gap-2 font-semibold tracking-tight', className)}>
      <span
        aria-hidden
        className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary text-primary-foreground shadow-sm"
      >
        <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth={2.2}>
          <path d="M5 19V5l14 14V5" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      </span>
      {!iconOnly && <span className="text-lg">{label}</span>}
    </span>
  );
}
