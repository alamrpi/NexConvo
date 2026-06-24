'use client';

import { useTranslations } from 'next-intl';
import { cn } from '@/shared/lib/cn';
import { PRESETS, type Preset } from '../../model/permission-catalog';

/**
 * Segmented None / Read-only / Standard / Full quick-set (S27 — clear interactive states).
 * Stateless: it just emits the chosen preset. Disabled for read-only (system) roles.
 */
export function PresetButtons({
  onSelect,
  disabled,
  size = 'sm',
}: {
  onSelect: (preset: Preset) => void;
  disabled?: boolean;
  size?: 'sm' | 'xs';
}) {
  const t = useTranslations('settings.roles.presets');

  return (
    <div
      role="group"
      className={cn(
        'inline-flex items-center rounded-md border border-border bg-muted/40 p-0.5',
        disabled && 'opacity-50',
      )}
    >
      {PRESETS.map((preset) => (
        <button
          key={preset}
          type="button"
          disabled={disabled}
          onClick={() => onSelect(preset)}
          className={cn(
            'rounded-[5px] font-medium text-muted-foreground transition-colors',
            'hover:bg-background hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
            'disabled:pointer-events-none',
            size === 'sm' ? 'px-2.5 py-1 text-xs' : 'px-2 py-0.5 text-[0.6875rem]',
          )}
        >
          {t(preset)}
        </button>
      ))}
    </div>
  );
}
