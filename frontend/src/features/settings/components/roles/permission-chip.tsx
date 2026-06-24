'use client';

import { useTranslations } from 'next-intl';
import { Check } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { permissionLabelKey } from '../../model/permission-catalog';

/**
 * A single permission toggle chip (S11/S27). Granted = filled green with a check; off =
 * subtle outline. Read-only roles render the chip non-interactive (no toggle handler).
 */
export function PermissionChip({
  permissionKey,
  granted,
  readOnly,
  onToggle,
}: {
  permissionKey: string;
  granted: boolean;
  readOnly?: boolean;
  onToggle?: () => void;
}) {
  const t = useTranslations('settings.roles.permissions');
  const label = t(permissionLabelKey(permissionKey));

  return (
    <button
      type="button"
      role="switch"
      aria-checked={granted}
      aria-label={label}
      disabled={readOnly}
      onClick={onToggle}
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs font-medium transition-colors',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-1 focus-visible:ring-offset-background',
        granted
          ? 'border-success/30 bg-success/10 text-success'
          : 'border-border bg-background text-muted-foreground',
        readOnly ? 'cursor-default' : 'hover:border-ring/50',
      )}
    >
      <Check className={cn('h-3.5 w-3.5', !granted && 'opacity-0')} aria-hidden="true" />
      {label}
    </button>
  );
}
