'use client';

import * as React from 'react';
import { useTranslations } from 'next-intl';
import { cn } from '@/shared/lib/cn';

type Period = '7d' | '30d' | '90d';

const PERIODS: Period[] = ['7d', '30d', '90d'];

/**
 * Compact segmented control for the dashboard period. Client leaf (S6) — owns only
 * the selection; wiring it to real queried ranges lands with the Analytics module.
 */
export function PeriodToggle() {
  const t = useTranslations('dashboard.period');
  const [period, setPeriod] = React.useState<Period>('7d');

  return (
    <div
      className="inline-flex rounded-md border border-border bg-card p-0.5"
      role="group"
      aria-label={t('label')}
    >
      {PERIODS.map((value) => (
        <button
          key={value}
          type="button"
          aria-pressed={period === value}
          onClick={() => setPeriod(value)}
          className={cn(
            'rounded-[6px] px-3 py-1 text-xs font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
            period === value
              ? 'bg-primary/10 text-primary'
              : 'text-muted-foreground hover:text-foreground',
          )}
        >
          {t(value)}
        </button>
      ))}
    </div>
  );
}
