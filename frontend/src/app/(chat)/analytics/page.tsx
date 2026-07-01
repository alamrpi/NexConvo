'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import {
  BarChart,
  Bar,
  Cell,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer,
  PieChart,
  Pie,
  LineChart,
  Line,
} from 'recharts';
import {
  MessageSquare,
  Bot,
  Users,
  Clock,
  Star,
  DollarSign,
  ArrowUpDown,
  Download,
} from 'lucide-react';
import { MOCK_ANALYTICS } from '@/features/chat/mock-data';
import { KpiCard } from '@/shared/ui/chat/kpi-card';
import { ChannelBadge } from '@/shared/ui/chat/channel-badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/card';
import { Badge } from '@/shared/ui/badge';
import { Button } from '@/shared/ui/button';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/shared/ui/table';
import { Input } from '@/shared/ui/input';

// ─── Types ───────────────────────────────────────────────────────────────────

type DateRange = '7d' | '30d' | '90d' | 'custom';
type AgentSortCol = 'name' | 'handled' | 'resolved' | 'avgTimeMs' | 'csat';
type SortDir = 'asc' | 'desc';

// ─── Constants ───────────────────────────────────────────────────────────────

const PIE_COLORS = [
  'hsl(var(--chart-1))',
  'hsl(var(--chart-2))',
  'hsl(var(--chart-3))',
  'hsl(var(--chart-4))',
  'hsl(var(--chart-5))',
];

const CONFIDENCE_COLORS = [
  'var(--chat-confidence-high)',
  'var(--chat-confidence-medium)',
  'var(--chat-confidence-low)',
];

const TOOLTIP_STYLE = {
  background: 'hsl(var(--card))',
  border: '1px solid hsl(var(--border))',
  borderRadius: '0.5rem',
  color: 'hsl(var(--foreground))',
  fontSize: 12,
};

const KPI_ITEMS = [
  {
    title: 'Total conversations',
    value: MOCK_ANALYTICS.kpis.totalConversations.toLocaleString(),
    trend: MOCK_ANALYTICS.kpis.totalConversationsTrend,
    trendLabel: 'vs prev period',
    icon: MessageSquare,
  },
  {
    title: 'Containment rate',
    value: `${MOCK_ANALYTICS.kpis.containmentRate}%`,
    trend: MOCK_ANALYTICS.kpis.containmentRateTrend,
    trendLabel: 'vs prev period',
    icon: Bot,
  },
  {
    title: 'Handoff rate',
    value: `${MOCK_ANALYTICS.kpis.handoffRate}%`,
    trend: MOCK_ANALYTICS.kpis.handoffRateTrend,
    trendLabel: 'vs prev period',
    icon: Users,
  },
  {
    title: 'Avg. response time',
    value: `${(MOCK_ANALYTICS.kpis.avgResponseTimeMs / 1000).toFixed(1)}s`,
    trend: MOCK_ANALYTICS.kpis.avgResponseTimeTrend,
    trendLabel: 'vs prev period',
    icon: Clock,
  },
  {
    title: 'CSAT',
    value: `${MOCK_ANALYTICS.kpis.csat}/5`,
    trend: MOCK_ANALYTICS.kpis.csatTrend,
    trendLabel: 'vs prev period',
    icon: Star,
  },
  {
    title: 'Token cost',
    value: `$${MOCK_ANALYTICS.kpis.tokenCostUsd.toFixed(2)}`,
    trend: MOCK_ANALYTICS.kpis.tokenCostTrend,
    trendLabel: 'vs prev period',
    icon: DollarSign,
  },
] as const;

// ─── Helpers ─────────────────────────────────────────────────────────────────

function sliceDays<T>(arr: T[], days: number): T[] {
  return arr.slice(-days);
}

function formatAvgTime(ms: number): string {
  const m = Math.floor(ms / 60000);
  const s = Math.round((ms % 60000) / 1000);
  return `${m}m ${s}s`;
}

function exportAgentCsv(data: typeof MOCK_ANALYTICS.agentPerformance): void {
  const header = 'Agent,Handled,Resolved,Avg Time,CSAT';
  const rows = data.map(
    (a) =>
      `${a.name},${a.handled},${a.resolved},${Math.floor(a.avgTimeMs / 60000)}m,${a.csat}`,
  );
  const csv = [header, ...rows].join('\n');
  const blob = new Blob([csv], { type: 'text/csv' });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = 'agent-performance.csv';
  anchor.click();
  URL.revokeObjectURL(url);
}

// ─── Sub-components ──────────────────────────────────────────────────────────

function DateRangeSelector({
  value,
  onChange,
  customFrom,
  customTo,
  onCustomFromChange,
  onCustomToChange,
}: {
  value: DateRange;
  onChange: (v: DateRange) => void;
  customFrom: string;
  customTo: string;
  onCustomFromChange: (v: string) => void;
  onCustomToChange: (v: string) => void;
}) {
  const options: { label: string; value: DateRange }[] = [
    { label: 'Last 7d', value: '7d' },
    { label: 'Last 30d', value: '30d' },
    { label: 'Last 90d', value: '90d' },
    { label: 'Custom', value: 'custom' },
  ];

  return (
    <div className="flex flex-wrap items-center gap-3">
      <div
        className="inline-flex rounded-md border border-border"
        role="group"
        aria-label="Date range"
      >
        {options.map((opt, idx) => (
          <button
            key={opt.value}
            onClick={() => onChange(opt.value)}
            aria-pressed={value === opt.value}
            className={[
              'px-3 py-1.5 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
              idx === 0 ? 'rounded-l-md' : '',
              idx === options.length - 1 ? 'rounded-r-md' : '',
              idx > 0 ? 'border-l border-border' : '',
              value === opt.value
                ? 'bg-primary text-primary-foreground'
                : 'bg-background text-foreground hover:bg-muted',
            ]
              .filter(Boolean)
              .join(' ')}
          >
            {opt.label}
          </button>
        ))}
      </div>

      {value === 'custom' && (
        <div className="flex items-center gap-2">
          <Input
            type="date"
            value={customFrom}
            onChange={(e) => onCustomFromChange(e.target.value)}
            className="h-8 w-36 text-sm"
            aria-label="From date"
          />
          <span className="text-muted-foreground text-sm">to</span>
          <Input
            type="date"
            value={customTo}
            onChange={(e) => onCustomToChange(e.target.value)}
            className="h-8 w-36 text-sm"
            aria-label="To date"
          />
        </div>
      )}
    </div>
  );
}

function VolumeChart({
  data,
  rangeLabel,
}: {
  data: typeof MOCK_ANALYTICS.dailyVolume;
  rangeLabel: string;
}) {
  return (
    <Card className="border border-border shadow-sm">
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-semibold">Volume by channel</CardTitle>
        <p className="text-xs text-muted-foreground">{rangeLabel}</p>
      </CardHeader>
      <CardContent className="pt-0">
        <ResponsiveContainer width="100%" height={280}>
          <BarChart data={data} margin={{ top: 8, right: 8, left: -16, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" />
            <XAxis
              dataKey="date"
              tick={{ fontSize: 11 }}
              tickFormatter={(d: string) => d.slice(5)}
            />
            <YAxis tick={{ fontSize: 11 }} />
            <Tooltip contentStyle={TOOLTIP_STYLE} />
            <Legend wrapperStyle={{ fontSize: 11 }} />
            <Bar dataKey="whatsapp" stackId="a" fill="var(--chat-channel-whatsapp)" name="WhatsApp" />
            <Bar dataKey="facebook" stackId="a" fill="var(--chat-channel-facebook)" name="Facebook" />
            <Bar dataKey="instagram" stackId="a" fill="var(--chat-channel-instagram)" name="Instagram" />
            <Bar dataKey="telegram" stackId="a" fill="var(--chat-channel-telegram)" name="Telegram" />
            <Bar
              dataKey="web"
              stackId="a"
              fill="var(--chat-channel-web)"
              radius={[3, 3, 0, 0]}
              name="Web"
            />
          </BarChart>
        </ResponsiveContainer>
      </CardContent>
    </Card>
  );
}

function HandoffReasonsChart() {
  return (
    <Card className="border border-border shadow-sm">
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-semibold">Handoff reasons</CardTitle>
      </CardHeader>
      <CardContent className="pt-0">
        <ResponsiveContainer width="100%" height={280}>
          <PieChart>
            <Pie
              data={MOCK_ANALYTICS.handoffReasons}
              dataKey="count"
              nameKey="reason"
              cx="50%"
              cy="50%"
              outerRadius={80}
              innerRadius={40}
              paddingAngle={3}
            >
              {MOCK_ANALYTICS.handoffReasons.map((_, i) => (
                <Cell key={i} fill={PIE_COLORS[i % PIE_COLORS.length]} />
              ))}
            </Pie>
            <Tooltip contentStyle={TOOLTIP_STYLE} />
            <Legend wrapperStyle={{ fontSize: 11 }} />
          </PieChart>
        </ResponsiveContainer>
      </CardContent>
    </Card>
  );
}

function ConfidenceChart() {
  return (
    <Card className="border border-border shadow-sm">
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-semibold">Confidence distribution</CardTitle>
      </CardHeader>
      <CardContent className="pt-0">
        <ResponsiveContainer width="100%" height={240}>
          <BarChart
            data={MOCK_ANALYTICS.confidenceBands}
            margin={{ top: 8, right: 8, left: -16, bottom: 0 }}
          >
            <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" />
            <XAxis dataKey="band" tick={{ fontSize: 11 }} />
            <YAxis tick={{ fontSize: 11 }} />
            <Tooltip contentStyle={TOOLTIP_STYLE} />
            <Bar dataKey="count" radius={[4, 4, 0, 0]} name="Conversations">
              {MOCK_ANALYTICS.confidenceBands.map((_, i) => (
                <Cell key={i} fill={CONFIDENCE_COLORS[i % CONFIDENCE_COLORS.length]} />
              ))}
            </Bar>
          </BarChart>
        </ResponsiveContainer>
      </CardContent>
    </Card>
  );
}

function TokenCostChart({ data }: { data: typeof MOCK_ANALYTICS.dailyTokenCost }) {
  return (
    <Card className="border border-border shadow-sm">
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-semibold">Daily token cost</CardTitle>
      </CardHeader>
      <CardContent className="pt-0">
        <ResponsiveContainer width="100%" height={240}>
          <LineChart data={data} margin={{ top: 8, right: 8, left: -8, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" />
            <XAxis
              dataKey="date"
              tickFormatter={(d: string) => d.slice(5)}
              tick={{ fontSize: 11 }}
            />
            <YAxis tick={{ fontSize: 11 }} tickFormatter={(v: number) => `$${v}`} />
            <Line
              type="monotone"
              dataKey="cost"
              stroke="hsl(var(--primary))"
              strokeWidth={2}
              dot={false}
            />
            <Tooltip
              contentStyle={TOOLTIP_STYLE}
              formatter={(v) => [`$${Number(v).toFixed(2)}`, 'Cost']}
            />
          </LineChart>
        </ResponsiveContainer>
      </CardContent>
    </Card>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export default function AnalyticsPage() {
  const router = useRouter();

  const [dateRange, setDateRange] = useState<DateRange>('30d');
  const [customFrom, setCustomFrom] = useState('');
  const [customTo, setCustomTo] = useState('');
  const [sort, setSort] = useState<{ col: AgentSortCol; dir: SortDir }>({
    col: 'handled',
    dir: 'desc',
  });

  // ── Filtered data ──────────────────────────────────────────────────────────
  const days = dateRange === '7d' ? 7 : dateRange === '90d' ? 90 : 30;
  const filteredDailyVolume = useMemo(
    () => (dateRange === 'custom' ? MOCK_ANALYTICS.dailyVolume : sliceDays(MOCK_ANALYTICS.dailyVolume, days)),
    [dateRange, days],
  );
  const filteredDailyTokenCost = useMemo(
    () =>
      dateRange === 'custom'
        ? MOCK_ANALYTICS.dailyTokenCost
        : sliceDays(MOCK_ANALYTICS.dailyTokenCost, days),
    [dateRange, days],
  );

  const rangeLabel =
    dateRange === 'custom' && customFrom && customTo
      ? `${customFrom} – ${customTo}`
      : dateRange === '7d'
        ? 'Last 7 days'
        : dateRange === '90d'
          ? 'Last 90 days'
          : 'Last 30 days';

  // ── Sorted agents ──────────────────────────────────────────────────────────
  const sortedAgents = useMemo(() => {
    return [...MOCK_ANALYTICS.agentPerformance].sort((a, b) => {
      const av = a[sort.col];
      const bv = b[sort.col];
      if (typeof av === 'string' && typeof bv === 'string') {
        return sort.dir === 'asc' ? av.localeCompare(bv) : bv.localeCompare(av);
      }
      return sort.dir === 'asc' ? (av as number) - (bv as number) : (bv as number) - (av as number);
    });
  }, [sort]);

  function toggleSort(col: AgentSortCol) {
    setSort((prev) => ({
      col,
      dir: prev.col === col && prev.dir === 'desc' ? 'asc' : 'desc',
    }));
  }

  return (
    <div className="space-y-6 p-4 sm:p-6">
      {/* 1. Date range selector */}
      <DateRangeSelector
        value={dateRange}
        onChange={setDateRange}
        customFrom={customFrom}
        customTo={customTo}
        onCustomFromChange={setCustomFrom}
        onCustomToChange={setCustomTo}
      />

      {/* 2. KPI row */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
        {KPI_ITEMS.map((kpi) => (
          <KpiCard key={kpi.title} {...kpi} />
        ))}
      </div>

      {/* 3. Charts row — volume + handoff */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-5">
        <div className="lg:col-span-3">
          <VolumeChart data={filteredDailyVolume} rangeLabel={rangeLabel} />
        </div>
        <div className="lg:col-span-2">
          <HandoffReasonsChart />
        </div>
      </div>

      {/* 4. Third row — confidence + token cost */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <ConfidenceChart />
        <TokenCostChart data={filteredDailyTokenCost} />
      </div>

      {/* 5. Agent performance table */}
      <Card className="border border-border shadow-sm">
        <CardHeader className="flex flex-row items-center justify-between pb-2">
          <CardTitle className="text-sm font-semibold">Agent performance</CardTitle>
          <Button
            variant="outline"
            size="sm"
            onClick={() => exportAgentCsv(sortedAgents)}
            className="gap-1.5 text-xs"
          >
            <Download className="h-3.5 w-3.5" aria-hidden />
            Export CSV
          </Button>
        </CardHeader>
        <CardContent className="pt-0">
          <Table>
            <TableHeader>
              <TableRow>
                {(
                  [
                    { col: 'name', label: 'Agent' },
                    { col: 'handled', label: 'Handled' },
                    { col: 'resolved', label: 'Resolved' },
                    { col: 'avgTimeMs', label: 'Avg. Time' },
                    { col: 'csat', label: 'CSAT' },
                  ] as { col: AgentSortCol; label: string }[]
                ).map(({ col, label }) => (
                  <TableHead key={col}>
                    <button
                      onClick={() => toggleSort(col)}
                      className="inline-flex items-center gap-1 text-xs font-medium text-muted-foreground hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                      aria-label={`Sort by ${label}`}
                    >
                      {label}
                      <ArrowUpDown
                        className={[
                          'h-3 w-3 transition-opacity',
                          sort.col === col ? 'opacity-100' : 'opacity-40',
                        ].join(' ')}
                        aria-hidden
                      />
                    </button>
                  </TableHead>
                ))}
              </TableRow>
            </TableHeader>
            <TableBody>
              {sortedAgents.map((agent) => (
                <TableRow key={agent.id}>
                  <TableCell className="text-sm font-medium">{agent.name}</TableCell>
                  <TableCell className="text-sm tabular-nums">{agent.handled}</TableCell>
                  <TableCell className="text-sm tabular-nums">{agent.resolved}</TableCell>
                  <TableCell className="text-sm tabular-nums">
                    {formatAvgTime(agent.avgTimeMs)}
                  </TableCell>
                  <TableCell>
                    <span className="text-sm font-medium" style={{ color: 'var(--chat-confidence-high)' }}>
                      ★ {agent.csat}
                    </span>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {/* 6. Low-confidence conversations table */}
      <Card className="border border-border shadow-sm">
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-semibold">Low-confidence conversations</CardTitle>
        </CardHeader>
        <CardContent className="pt-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="text-xs text-muted-foreground">Contact</TableHead>
                <TableHead className="text-xs text-muted-foreground">Channel</TableHead>
                <TableHead className="text-xs text-muted-foreground">Confidence</TableHead>
                <TableHead className="text-xs text-muted-foreground">Topic</TableHead>
                <TableHead className="text-xs text-muted-foreground">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {MOCK_ANALYTICS.lowConfidenceConversations.map((row) => {
                const isLow = row.confidence < 0.6;
                return (
                  <TableRow key={row.id}>
                    <TableCell className="text-sm font-medium">{row.contactName}</TableCell>
                    <TableCell>
                      <ChannelBadge channel={row.channel} size="sm" />
                    </TableCell>
                    <TableCell>
                      <Badge
                        variant="outline"
                        className={[
                          'text-xs font-semibold tabular-nums',
                          isLow
                            ? 'border-red-200 text-chatConfidence-low'
                            : 'border-amber-200 text-chatConfidence-medium',
                        ].join(' ')}
                      >
                        {(row.confidence * 100).toFixed(0)}%
                      </Badge>
                    </TableCell>
                    <TableCell>
                      <span className="rounded bg-muted px-1.5 py-0.5 text-xs text-muted-foreground">
                        {row.topic}
                      </span>
                    </TableCell>
                    <TableCell>
                      <div className="flex flex-wrap gap-2">
                        <Button
                          variant="outline"
                          size="sm"
                          className="h-7 text-xs"
                          onClick={() => router.push(`/inbox?conv=${row.id}`)}
                        >
                          View
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          className="h-7 text-xs text-primary hover:text-primary"
                          onClick={() =>
                            router.push(`/settings/knowledge?search=${row.topic}`)
                          }
                        >
                          Find gap →
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}
