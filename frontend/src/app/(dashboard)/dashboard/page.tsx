import type { Metadata } from 'next';
import { getLocale, getTranslations } from 'next-intl/server';
import { DollarSign, FolderKanban, MessageSquare, Users } from 'lucide-react';
import { Card } from '@/shared/ui/card';
import { StatCard } from '@/features/dashboard/components/stat-card';
import { PeriodToggle } from '@/features/dashboard/components/period-toggle';
import { TrendChart } from '@/features/dashboard/components/trend-chart';
import { RecentActivity, type ActivityItem } from '@/features/dashboard/components/recent-activity';
import { ChannelMix, type ChannelStat } from '@/features/dashboard/components/channel-mix';
import { PipelineOverview, type PipelineStage } from '@/features/dashboard/components/pipeline-overview';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('nav.dashboard');
  return { title: t('overview') };
}

// Static demo data (S7: real values arrive via React Query when CRM data is wired).
const stats = [
  { key: 'openConversations', value: '128', delta: '+12%', trend: 'up', icon: MessageSquare, series: [88, 96, 92, 110, 104, 120, 128] },
  { key: 'newLeads', value: '342', delta: '+8%', trend: 'up', icon: Users, series: [280, 300, 290, 312, 330, 321, 342] },
  { key: 'activeProjects', value: '27', delta: '+3%', trend: 'up', icon: FolderKanban, series: [22, 23, 24, 24, 25, 26, 27] },
  { key: 'revenue', value: '৳ 8.4L', delta: '-2%', trend: 'down', icon: DollarSign, series: [91, 89, 90, 87, 86, 85, 84] },
] as const;

const conversationsSeries = [88, 96, 92, 110, 104, 120, 128];
const revenueSeries = [62, 68, 65, 74, 71, 80, 84];

const activity: readonly ActivityItem[] = [
  { name: 'Rafiul Islam', handle: '+8801712 345678', channel: 'WhatsApp', time: '2m' },
  { name: 'Sadia Karim', handle: 'sadia@northwind.co', channel: 'Email', time: '14m' },
  { name: 'Tanvir Ahmed', handle: '@tanvir_ah', channel: 'Instagram', time: '38m' },
  { name: 'Nabila Haque', handle: '+8801912 998877', channel: 'Voice', time: '1h' },
  { name: 'Imran Chowdhury', handle: 'imran@globex.io', channel: 'Email', time: '2h' },
];

const channelStats: readonly ChannelStat[] = [
  { channel: 'WhatsApp', count: 412 },
  { channel: 'Email', count: 236 },
  { channel: 'Instagram', count: 184 },
  { channel: 'Voice', count: 96 },
];

const pipelineStages: readonly PipelineStage[] = [
  { key: 'new', count: 64 },
  { key: 'qualified', count: 38 },
  { key: 'proposal', count: 21 },
  { key: 'won', count: 12 },
];

export default async function DashboardPage() {
  const [t, ts, locale] = await Promise.all([
    getTranslations('dashboard'),
    getTranslations('dashboard.stats'),
    getLocale(),
  ]);

  // Locale-aware weekday labels for the trend chart x-axis (S12).
  const weekday = new Intl.DateTimeFormat(locale, { weekday: 'short' });
  const categories = Array.from({ length: 7 }, (_, i) => weekday.format(new Date(2024, 0, i + 1)));

  return (
    <div className="mx-auto max-w-7xl space-y-5">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <div className="space-y-1">
          <h1 className="text-2xl font-bold tracking-tight sm:text-3xl">{t('greeting')}</h1>
          <p className="text-sm text-muted-foreground">{t('subtitle')}</p>
        </div>
        <PeriodToggle />
      </header>

      <section className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {stats.map((s) => (
          <StatCard
            key={s.key}
            label={ts(s.key)}
            value={s.value}
            delta={s.delta}
            deltaLabel={ts('vsLastWeek')}
            trend={s.trend}
            icon={s.icon}
            series={[...s.series]}
          />
        ))}
      </section>

      <section className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        <Card className="p-5 lg:col-span-2">
          <TrendChart
            conversations={conversationsSeries}
            revenue={revenueSeries}
            categories={categories}
          />
        </Card>
        <div className="lg:col-span-1">
          <RecentActivity items={activity} />
        </div>
      </section>

      <section className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <ChannelMix items={channelStats} />
        <PipelineOverview stages={pipelineStages} />
      </section>
    </div>
  );
}
