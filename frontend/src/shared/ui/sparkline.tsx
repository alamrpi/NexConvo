import { cn } from '@/shared/lib/cn';

interface SparklineProps {
  /** The data series to plot. Needs at least two points to render. */
  data: number[];
  /** Tailwind text-color class — drives stroke + fill via `currentColor` (e.g. `text-chart-1`). */
  className?: string;
  width?: number;
  height?: number;
}

/**
 * Tiny inline-SVG trend line (S18 primitive). Deliberately hand-built rather than a
 * charting dependency: the data is static demo data today, this stays tokenized via
 * `currentColor` and adds no bundle weight (recharts/visx is the planned upgrade once
 * the Analytics module wires real, interactive series). Purely decorative — `aria-hidden`;
 * callers provide an accessible text summary (e.g. the delta badge) alongside.
 */
export function Sparkline({ data, className, width = 100, height = 32 }: SparklineProps) {
  if (data.length < 2) return null;

  const pad = 2;
  const max = Math.max(...data);
  const min = Math.min(...data);
  const range = max - min || 1;
  const stepX = width / (data.length - 1);

  const points = data.map((value, i) => {
    const x = i * stepX;
    const y = pad + (height - pad * 2) * (1 - (value - min) / range);
    return `${x.toFixed(2)},${y.toFixed(2)}`;
  });

  const line = `M${points.join(' L')}`;
  const area = `${line} L${width},${height} L0,${height} Z`;

  return (
    <svg
      aria-hidden
      viewBox={`0 0 ${width} ${height}`}
      preserveAspectRatio="none"
      className={cn('h-8 w-full overflow-visible', className)}
    >
      <path d={area} fill="currentColor" fillOpacity={0.12} stroke="none" />
      <path
        d={line}
        fill="none"
        stroke="currentColor"
        strokeWidth={2}
        strokeLinecap="round"
        strokeLinejoin="round"
        vectorEffect="non-scaling-stroke"
      />
    </svg>
  );
}
