'use client';

import * as React from 'react';
import { useLocale } from 'next-intl';
import { Badge } from '@/shared/ui/badge';
import type { ConnectionHealthStatus } from '../model/connection-health.schema';

const HEALTH_BADGE_VARIANT: Record<ConnectionHealthStatus, 'success' | 'warning' | 'destructive' | 'outline'> = {
  Healthy: 'success',
  Degraded: 'warning',
  Failed: 'destructive',
  Untested: 'outline',
};

/** Formats a past ISO timestamp as a locale-aware relative time, e.g. "5 minutes ago" (S12). */
export function useRelativeTime(iso: string | null): string | null {
  const locale = useLocale();
  return React.useMemo(() => {
    if (!iso) return null;
    const date = new Date(iso);
    if (Number.isNaN(date.getTime())) return null;

    const diffSeconds = Math.round((date.getTime() - Date.now()) / 1000);
    const rtf = new Intl.RelativeTimeFormat(locale, { numeric: 'auto' });
    const units: [Intl.RelativeTimeFormatUnit, number][] = [
      ['year', 60 * 60 * 24 * 365],
      ['month', 60 * 60 * 24 * 30],
      ['day', 60 * 60 * 24],
      ['hour', 60 * 60],
      ['minute', 60],
    ];
    for (const [unit, secondsInUnit] of units) {
      if (Math.abs(diffSeconds) >= secondsInUnit) {
        return rtf.format(Math.round(diffSeconds / secondsInUnit), unit);
      }
    }
    return rtf.format(diffSeconds, 'second');
  }, [iso, locale]);
}

export interface HealthBadgeProps {
  status: ConnectionHealthStatus;
  lastTestedAt: string | null;
  /** Resolves a health status to its display label, e.g. `t('healthHealthy')` (per-feature i18n keys — S12). */
  statusLabel: (status: ConnectionHealthStatus) => string;
  /** Resolves the "last tested" caption, e.g. `t('lastTested', { time })` / `t('lastTestedNever')`. */
  lastTestedLabel: (relative: string | null) => string;
}

/**
 * Shared connection-health badge used by every settings slice that probes an external provider
 * (S3 storage, AI providers, …). Status → variant/label mapping and the relative-time caption are
 * extracted here so features only need to supply their own i18n resolvers (S1 DRY, S12).
 */
export function HealthBadge({ status, lastTestedAt, statusLabel, lastTestedLabel }: HealthBadgeProps) {
  const relative = useRelativeTime(lastTestedAt);

  return (
    <div className="flex flex-wrap items-center gap-2">
      <Badge variant={HEALTH_BADGE_VARIANT[status]}>{statusLabel(status)}</Badge>
      <span className="text-xs text-muted-foreground">{lastTestedLabel(relative)}</span>
    </div>
  );
}
