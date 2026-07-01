'use client';

import * as React from 'react';
import { useTranslations } from 'next-intl';
import { cn } from '@/shared/lib/cn';

type SeriesKey = 'conversations' | 'revenue';

interface TrendChartProps {
  /** Demo series — real values arrive via React Query once analytics is wired (S7). */
  conversations: number[];
  revenue: number[];
  /** X-axis category labels (e.g. weekday short names), already localized by the caller. */
  categories: string[];
}

const VIEW_W = 600;
const VIEW_H = 200;
const PAD_Y = 12;

function buildPaths(data: number[]) {
  const max = Math.max(...data);
  const min = Math.min(...data);
  const range = max - min || 1;
  const stepX = VIEW_W / (data.length - 1);

  const points = data.map((value, i) => {
    const x = i * stepX;
    const y = PAD_Y + (VIEW_H - PAD_Y * 2) * (1 - (value - min) / range);
    return `${x.toFixed(2)},${y.toFixed(2)}`;
  });

  const line = `M${points.join(' L')}`;
  return { line, area: `${line} L${VIEW_W},${VIEW_H} L0,${VIEW_H} Z` };
}

/**
 * Compact area chart for the dashboard. Client leaf (S6) — it owns only the tab
 * selection; the SVG is hand-built and fully tokenized (see Sparkline rationale).
 * Decorative SVG is `aria-hidden`; the heading + tab labels carry the meaning.
 */
export function TrendChart({ conversations, revenue, categories }: TrendChartProps) {
  const t = useTranslations('dashboard.trend');
  const gradientId = React.useId();
  const [active, setActive] = React.useState<SeriesKey>('conversations');

  const series: Record<SeriesKey, { data: number[]; color: string }> = {
    conversations: { data: conversations, color: 'text-chart-1' },
    revenue: { data: revenue, color: 'text-chart-4' },
  };
  const current = series[active];
  const { line, area } = buildPaths(current.data);

  const tabs: SeriesKey[] = ['conversations', 'revenue'];

  return (
    <div className="flex h-full flex-col">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h3 className="text-sm font-semibold tracking-tight">{t('title')}</h3>
        <div className="inline-flex rounded-md bg-muted p-0.5" role="tablist" aria-label={t('title')}>
          {tabs.map((key) => (
            <button
              key={key}
              type="button"
              role="tab"
              aria-selected={active === key}
              onClick={() => setActive(key)}
              className={cn(
                'rounded-[6px] px-3 py-1 text-xs font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                active === key
                  ? 'bg-card text-foreground shadow-sm'
                  : 'text-muted-foreground hover:text-foreground',
              )}
            >
              {t(key)}
            </button>
          ))}
        </div>
      </div>

      <div className={cn('mt-4 flex-1', current.color)}>
        <svg
          aria-hidden
          viewBox={`0 0 ${VIEW_W} ${VIEW_H}`}
          preserveAspectRatio="none"
          className="h-44 w-full overflow-visible"
        >
          <defs>
            <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="currentColor" stopOpacity={0.25} />
              <stop offset="100%" stopColor="currentColor" stopOpacity={0} />
            </linearGradient>
          </defs>
          {[0, 0.25, 0.5, 0.75, 1].map((g) => (
            <line
              key={g}
              x1={0}
              x2={VIEW_W}
              y1={PAD_Y + (VIEW_H - PAD_Y * 2) * g}
              y2={PAD_Y + (VIEW_H - PAD_Y * 2) * g}
              className="text-border"
              stroke="currentColor"
              strokeWidth={1}
              vectorEffect="non-scaling-stroke"
            />
          ))}
          <path d={area} fill={`url(#${gradientId})`} stroke="none" />
          <path
            d={line}
            fill="none"
            stroke="currentColor"
            strokeWidth={2.5}
            strokeLinecap="round"
            strokeLinejoin="round"
            vectorEffect="non-scaling-stroke"
          />
        </svg>
      </div>

      <div className="mt-3 flex justify-between text-xs text-muted-foreground">
        {categories.map((label) => (
          <span key={label}>{label}</span>
        ))}
      </div>
    </div>
  );
}
