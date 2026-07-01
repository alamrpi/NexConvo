import { TrendingDown, TrendingUp } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/card';
import type { LucideIcon } from 'lucide-react';

interface KpiCardProps {
  title: string;
  value: string;
  trend: number;
  trendLabel: string;
  icon: LucideIcon;
  className?: string;
}

export function KpiCard({ title, value, trend, trendLabel, icon: Icon, className }: KpiCardProps) {
  const positive = trend >= 0;

  return (
    <Card className={cn('border border-border shadow-sm', className)}>
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
        <CardTitle className="text-sm font-medium text-muted-foreground">{title}</CardTitle>
        <Icon className="h-4 w-4 text-muted-foreground" aria-hidden />
      </CardHeader>
      <CardContent>
        <div className="text-2xl font-bold tabular-nums">{value}</div>
        <p
          className={cn(
            'mt-1 flex items-center gap-1 text-xs',
            positive ? 'text-chatConfidence-high' : 'text-chatConfidence-low',
          )}
        >
          {positive ? (
            <TrendingUp className="h-3.5 w-3.5" aria-hidden />
          ) : (
            <TrendingDown className="h-3.5 w-3.5" aria-hidden />
          )}
          <span>
            {positive ? '+' : ''}
            {trend}% {trendLabel}
          </span>
        </p>
      </CardContent>
    </Card>
  );
}
