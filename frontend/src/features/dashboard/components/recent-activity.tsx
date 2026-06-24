import { getTranslations } from 'next-intl/server';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/card';
import { Avatar, AvatarFallback } from '@/shared/ui/avatar';
import { cn } from '@/shared/lib/cn';
import { channelDot, type Channel } from '../model/channels';

export interface ActivityItem {
  name: string;
  handle: string;
  channel: Channel;
  /** Compact relative time, e.g. "2m" / "1h" (demo data — S7). */
  time: string;
}

/** Recent activity feed — presentational; real events arrive via React Query later (S7). */
export async function RecentActivity({ items }: { items: readonly ActivityItem[] }) {
  const t = await getTranslations('dashboard.panels');

  return (
    <Card className="flex h-full flex-col">
      <CardHeader className="flex-row items-center justify-between space-y-0 pb-3">
        <CardTitle className="text-sm">{t('activity')}</CardTitle>
        <a
          href="#activity"
          className="rounded text-xs font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          {t('viewAll')}
        </a>
      </CardHeader>
      <CardContent className="p-0">
        <ul className="divide-y divide-border">
          {items.map((item) => (
            <li key={`${item.channel}-${item.handle}`} className="flex items-center gap-3 px-4 py-2.5">
              <Avatar className="h-9 w-9">
                <AvatarFallback className="text-xs">{item.name.charAt(0)}</AvatarFallback>
              </Avatar>
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium">{item.name}</p>
                <p className="truncate text-xs text-muted-foreground">{item.handle}</p>
              </div>
              <div className="flex shrink-0 flex-col items-end gap-1">
                <span className="text-xs text-muted-foreground">{item.time}</span>
                <span className="inline-flex items-center gap-1.5 text-xs text-muted-foreground">
                  <span className={cn('h-1.5 w-1.5 rounded-full', channelDot[item.channel])} aria-hidden />
                  {item.channel}
                </span>
              </div>
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}
