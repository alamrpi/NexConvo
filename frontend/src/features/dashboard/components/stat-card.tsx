import { TrendingDown, TrendingUp, type LucideIcon } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { Card, CardContent } from '@/shared/ui/card';
import { Badge } from '@/shared/ui/badge';
import { Sparkline } from '@/shared/ui/sparkline';

interface StatCardProps {
  label: string;
  value: string;
  delta: string;
  deltaLabel: string;
  trend: 'up' | 'down';
  icon: LucideIcon;
  /** Demo trend series for the sparkline — real values arrive via React Query later (S7). */
  series: number[];
}

/** A single KPI tile — presentational; real values arrive via React Query later (S7). */
export function StatCard({ label, value, delta, deltaLabel, trend, icon: Icon, series }: StatCardProps) {
  const TrendIcon = trend === 'up' ? TrendingUp : TrendingDown;

  return (
    <Card className="hover:shadow-card">
      <CardContent className="flex flex-col gap-3 p-5">
        <div className="flex items-center justify-between">
          <span
            aria-hidden
            className="flex h-9 w-9 items-center justify-center rounded-lg bg-primary/10 text-primary"
          >
            <Icon className="h-[1.125rem] w-[1.125rem]" />
          </span>
          <Badge variant={trend === 'up' ? 'success' : 'destructive'}>
            <TrendIcon className="h-3 w-3" />
            {delta}
          </Badge>
        </div>

        <div className="space-y-0.5">
          <p className="text-2xl font-bold tracking-tight">{value}</p>
          <p className="text-sm text-muted-foreground">{label}</p>
        </div>

        <div className={cn(trend === 'up' ? 'text-primary' : 'text-muted-foreground')}>
          <Sparkline data={series} />
        </div>

        <p className="text-xs text-muted-foreground">{deltaLabel}</p>
      </CardContent>
    </Card>
  );
}
