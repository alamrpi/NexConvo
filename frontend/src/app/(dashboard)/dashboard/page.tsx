import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { DollarSign, FolderKanban, MessageSquare, Users } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/card';
import { Badge } from '@/shared/ui/badge';
import { Avatar, AvatarFallback } from '@/shared/ui/avatar';
import { StatCard } from '@/features/dashboard/components/stat-card';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('nav.dashboard');
  return { title: t('overview') };
}

// Static demo data (S7: real values arrive via React Query when CRM data is wired).
const stats = [
  { key: 'openConversations', value: '128', delta: '+12%', trend: 'up', icon: MessageSquare },
  { key: 'newLeads', value: '342', delta: '+8%', trend: 'up', icon: Users },
  { key: 'activeProjects', value: '27', delta: '+3%', trend: 'up', icon: FolderKanban },
  { key: 'revenue', value: '৳ 8.4L', delta: '-2%', trend: 'down', icon: DollarSign },
] as const;

const activity = [
  { name: 'Rafiul Islam', handle: '+8801712 345678', channel: 'WhatsApp' },
  { name: 'Sadia Karim', handle: 'sadia@northwind.co', channel: 'Email' },
  { name: 'Tanvir Ahmed', handle: '@tanvir_ah', channel: 'Instagram' },
  { name: 'Nabila Haque', handle: '+8801912 998877', channel: 'Voice' },
  { name: 'Imran Chowdhury', handle: 'imran@globex.io', channel: 'Email' },
] as const;

export default async function DashboardPage() {
  const [t, ts, tp] = await Promise.all([
    getTranslations('dashboard'),
    getTranslations('dashboard.stats'),
    getTranslations('dashboard.panels'),
  ]);

  return (
    <div className="mx-auto max-w-7xl space-y-8">
      <header className="space-y-1">
        <h1 className="text-3xl font-bold tracking-tight">{t('greeting')}</h1>
        <p className="text-muted-foreground">{t('subtitle')}</p>
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
          />
        ))}
      </section>

      <Card>
        <CardHeader className="flex-row items-center justify-between space-y-0">
          <CardTitle>{tp('activity')}</CardTitle>
          <a
            href="#activity"
            className="text-sm font-medium text-primary underline-offset-4 hover:underline"
          >
            {tp('viewAll')}
          </a>
        </CardHeader>
        <CardContent className="p-0">
          <ul className="divide-y divide-border">
            {activity.map((item) => (
              <li key={item.handle} className="flex items-center gap-3 px-6 py-3">
                <Avatar className="h-8 w-8">
                  <AvatarFallback className="text-xs">
                    {item.name.charAt(0)}
                  </AvatarFallback>
                </Avatar>
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium">{item.name}</p>
                  <p className="truncate text-xs text-muted-foreground">{item.handle}</p>
                </div>
                <Badge variant="outline">{item.channel}</Badge>
              </li>
            ))}
          </ul>
        </CardContent>
      </Card>
    </div>
  );
}
