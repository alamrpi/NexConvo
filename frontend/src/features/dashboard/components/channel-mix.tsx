import { getTranslations } from 'next-intl/server';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/card';
import { cn } from '@/shared/lib/cn';
import { channelDot, type Channel } from '../model/channels';

export interface ChannelStat {
  channel: Channel;
  count: number;
}

/** Channel volume breakdown — presentational demo data (S7). Bars are token-colored (S25). */
export async function ChannelMix({ items }: { items: readonly ChannelStat[] }) {
  const t = await getTranslations('dashboard.panels');
  const total = items.reduce((sum, item) => sum + item.count, 0) || 1;

  return (
    <Card className="flex h-full flex-col">
      <CardHeader className="pb-3">
        <CardTitle className="text-sm">{t('channelMix')}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3.5">
        {items.map((item) => {
          const pct = Math.round((item.count / total) * 100);
          return (
            <div key={item.channel} className="space-y-1.5">
              <div className="flex items-center justify-between text-xs">
                <span className="inline-flex items-center gap-2 font-medium">
                  <span className={cn('h-2 w-2 rounded-full', channelDot[item.channel])} aria-hidden />
                  {item.channel}
                </span>
                <span className="text-muted-foreground">
                  {item.count} · {pct}%
                </span>
              </div>
              <div
                className="h-2 w-full overflow-hidden rounded-full bg-muted"
                role="progressbar"
                aria-valuenow={pct}
                aria-valuemin={0}
                aria-valuemax={100}
                aria-label={item.channel}
              >
                <div
                  className={cn('h-full rounded-full', channelDot[item.channel])}
                  style={{ width: `${pct}%` }}
                />
              </div>
            </div>
          );
        })}
      </CardContent>
    </Card>
  );
}
