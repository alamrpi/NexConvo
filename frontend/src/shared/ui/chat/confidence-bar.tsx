import { cn } from '@/shared/lib/cn';

interface ConfidenceBarProps {
  score: number;
  className?: string;
}

function confidenceClass(score: number) {
  if (score >= 0.8) return 'bg-chatConfidence-high';
  if (score >= 0.6) return 'bg-chatConfidence-medium';
  return 'bg-chatConfidence-low';
}

function confidenceLabelClass(score: number) {
  if (score >= 0.8) return 'text-chatConfidence-high';
  if (score >= 0.6) return 'text-chatConfidence-medium';
  return 'text-chatConfidence-low';
}

export function ConfidenceBar({ score, className }: ConfidenceBarProps) {
  const pct = Math.round(score * 100);

  return (
    <div className={cn('flex items-center gap-2', className)}>
      <div
        role="progressbar"
        aria-valuenow={pct}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-label={`AI confidence: ${pct}%`}
        className="h-1 w-16 overflow-hidden rounded-full bg-muted"
      >
        <div
          className={cn('h-full rounded-full transition-all', confidenceClass(score))}
          style={{ width: `${pct}%` }}
        />
      </div>
      <span className={cn('text-xs tabular-nums', confidenceLabelClass(score))}>{pct}%</span>
    </div>
  );
}
