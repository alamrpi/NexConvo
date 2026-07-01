import { TrendingDown, TrendingUp, type LucideIcon } from 'lucide-react';
import { Card, CardContent } from '@/shared/ui/card';
import { Badge } from '@/shared/ui/badge';

interface StatCardProps {
  label: string;
  value: string;
  delta: string;
  deltaLabel: string;
  trend: 'up' | 'down';
  icon: LucideIcon;
}

/** A single KPI tile — presentational; real values arrive via React Query later (S7). */
export function StatCard({ label, value, delta, deltaLabel, trend, icon: Icon }: StatCardProps) {
  const TrendIcon = trend === 'up' ? TrendingUp : TrendingDown;

  return (
    <Card>
      <CardContent className="p-5">
        <div className="flex items-center justify-between">
          <span className="text-sm font-medium text-muted-foreground">{label}</span>
          <Icon className="h-4 w-4 text-muted-foreground" />
        </div>
        <div className="mt-3 flex items-end justify-between gap-2">
          <span className="text-2xl font-bold tracking-tight">{value}</span>
          <Badge variant={trend === 'up' ? 'success' : 'destructive'}>
            <TrendIcon className="h-3 w-3" />
            {delta}
          </Badge>
        </div>
        <p className="mt-1 text-xs text-muted-foreground">{deltaLabel}</p>
      </CardContent>
    </Card>
  );
}
