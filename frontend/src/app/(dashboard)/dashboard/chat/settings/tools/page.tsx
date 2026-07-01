'use client';

import { useState, useMemo } from 'react';
import { useTranslations } from 'next-intl';
import {
  Plus, Trash2, ChevronDown, ChevronUp, AlertTriangle,
  Users, ShoppingCart, CalendarDays, CheckCircle2, XCircle,
  Zap, Server, Search,
} from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { Badge } from '@/shared/ui/badge';
import { Switch } from '@/shared/ui/switch';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/shared/ui/tabs';
import { Separator } from '@/shared/ui/separator';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter,
} from '@/shared/ui/dialog';
import { cn } from '@/shared/lib/cn';

// ── Types ─────────────────────────────────────────────────────────────────────

interface Tool {
  id: string;
  name: string;
  description: string;
  requiresConfirmation: boolean;
}

interface ToolGroup {
  id: string;
  name: string;
  description: string;
  enabled: boolean;
  tools: Tool[];
  icon: React.ElementType;
  iconBg: string;
  iconColor: string;
}

type Transport = 'stdio' | 'sse';
type ServerStatus = 'connected' | 'error';
type TestStatus = 'idle' | 'testing' | 'passed' | 'failed';

interface CustomServer {
  id: string;
  name: string;
  transport: Transport;
  url?: string;
  command?: string;
  status: ServerStatus;
}

// ── Mock data ─────────────────────────────────────────────────────────────────

const BUILT_IN_GROUPS_INIT: ToolGroup[] = [
  {
    id: 'crm',
    name: 'CRM Tools',
    description: 'Access and update customer records, contacts, and leads from conversations.',
    enabled: true,
    icon: Users,
    iconBg: 'bg-blue-500/10',
    iconColor: 'text-blue-600 dark:text-blue-400',
    tools: [
      { id: 'crm_read_contact',   name: 'Read Contact',   description: 'Look up a customer by phone or email', requiresConfirmation: false },
      { id: 'crm_update_contact', name: 'Update Contact', description: 'Update customer details',              requiresConfirmation: true },
      { id: 'crm_create_lead',    name: 'Create Lead',    description: 'Create a new lead from conversation',  requiresConfirmation: true },
    ],
  },
  {
    id: 'orders',
    name: 'Order Management',
    description: 'Query order status, initiate returns, and manage fulfilment in real-time.',
    enabled: true,
    icon: ShoppingCart,
    iconBg: 'bg-emerald-500/10',
    iconColor: 'text-emerald-600 dark:text-emerald-400',
    tools: [
      { id: 'orders_track',  name: 'Track Order',     description: 'Get real-time tracking status',  requiresConfirmation: false },
      { id: 'orders_cancel', name: 'Cancel Order',    description: 'Cancel an order (irreversible)', requiresConfirmation: true },
      { id: 'orders_refund', name: 'Initiate Refund', description: 'Start a refund process',         requiresConfirmation: true },
    ],
  },
  {
    id: 'calendar',
    name: 'Calendar & Scheduling',
    description: 'Check agent availability and book appointments directly from the chat.',
    enabled: false,
    icon: CalendarDays,
    iconBg: 'bg-violet-500/10',
    iconColor: 'text-violet-600 dark:text-violet-400',
    tools: [
      { id: 'cal_check', name: 'Check Availability', description: 'Check agent availability slots', requiresConfirmation: false },
      { id: 'cal_book',  name: 'Book Appointment',   description: 'Create a calendar appointment',  requiresConfirmation: true },
    ],
  },
];

const MOCK_SERVERS_INIT: CustomServer[] = [
  { id: 'srv-1', name: 'Inventory MCP', transport: 'sse',   url: 'https://inventory.dhaka-retail.co/mcp',   status: 'connected' },
  { id: 'srv-2', name: 'Payments MCP',  transport: 'stdio', command: 'node /opt/payments-mcp/index.js', status: 'error' },
];

// ── Built-in group card ───────────────────────────────────────────────────────

function GroupCard({
  group,
  expanded,
  onExpand,
  onToggleEnabled,
  onToggleConfirmation,
}: {
  group: ToolGroup;
  expanded: boolean;
  onExpand: () => void;
  onToggleEnabled: (id: string) => void;
  onToggleConfirmation: (groupId: string, toolId: string) => void;
}) {
  const Icon = group.icon;

  return (
    <div
      className={cn(
        'flex flex-col rounded-xl border bg-card transition-all',
        group.enabled ? 'border-primary/30 ring-1 ring-primary/20' : 'border-border',
        !group.enabled && 'opacity-60',
      )}
    >
      {/* Card header */}
      <div className="flex items-start gap-3 p-4">
        <div className={cn('flex h-9 w-9 shrink-0 items-center justify-center rounded-lg', group.iconBg)}>
          <Icon className={cn('h-4.5 w-4.5', group.iconColor)} aria-hidden />
        </div>

        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-1.5 flex-wrap">
            <span className="text-sm font-semibold text-foreground">{group.name}</span>
            <Badge variant="secondary" className="text-[0.625rem] px-1.5 py-0 leading-4">
              {group.tools.length} tools
            </Badge>
          </div>
          <p className="mt-1 text-xs leading-relaxed text-muted-foreground">{group.description}</p>
        </div>

        <Switch
          checked={group.enabled}
          onCheckedChange={() => onToggleEnabled(group.id)}
          aria-label={`Enable ${group.name}`}
          className="shrink-0 mt-0.5"
        />
      </div>

      {/* Footer toggle */}
      <div className="border-t border-border px-4 py-2">
        <button
          type="button"
          onClick={onExpand}
          aria-expanded={expanded}
          className="inline-flex items-center gap-1 text-xs font-medium text-primary hover:underline underline-offset-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring rounded"
        >
          {expanded ? 'Hide tools' : 'Configure tools'}
          {expanded
            ? <ChevronUp className="h-3 w-3" aria-hidden />
            : <ChevronDown className="h-3 w-3" aria-hidden />}
        </button>
      </div>

      {/* Expanded tool list */}
      {expanded && (
        <div className="border-t border-border">
          {group.tools.map((tool, idx) => (
            <div key={tool.id}>
              {idx > 0 && <div className="h-px bg-border" />}
              <div className="flex items-center gap-3 px-4 py-2.5">
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-1.5">
                    <span className="text-xs font-medium text-foreground">{tool.name}</span>
                    {tool.requiresConfirmation && (
                      <AlertTriangle className="h-3 w-3 text-amber-500 shrink-0" aria-label="Requires confirmation" />
                    )}
                  </div>
                  <p className="text-[0.6875rem] text-muted-foreground">{tool.description}</p>
                </div>
                <div className="flex shrink-0 items-center gap-2">
                  <span className="text-[0.625rem] text-muted-foreground">Confirm</span>
                  <Switch
                    checked={tool.requiresConfirmation}
                    onCheckedChange={() => onToggleConfirmation(group.id, tool.id)}
                    aria-label={`Require confirmation for ${tool.name}`}
                  />
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

// ── Custom server row ─────────────────────────────────────────────────────────

function ServerRow({
  server,
  onRemove,
}: {
  server: CustomServer;
  onRemove: (id: string) => void;
}) {
  return (
    <div className="flex items-center gap-3 rounded-lg border border-border bg-card px-4 py-3">
      <span
        className={cn(
          'h-2 w-2 shrink-0 rounded-full',
          server.status === 'connected' ? 'bg-emerald-500' : 'bg-destructive',
        )}
        aria-label={server.status === 'connected' ? 'Connected' : 'Error'}
      />

      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2 flex-wrap">
          <span className="text-sm font-medium text-foreground">{server.name}</span>
          <Badge variant="outline" className="font-mono text-[0.625rem] px-1.5 py-0">
            {server.transport}
          </Badge>
          {server.status === 'error' && (
            <Badge variant="outline" className="text-[0.625rem] px-1.5 py-0 text-destructive border-destructive/30">
              Error
            </Badge>
          )}
        </div>
        <p className="mt-0.5 truncate font-mono text-xs text-muted-foreground">
          {server.transport === 'sse' ? server.url : server.command}
        </p>
      </div>

      <div className="flex shrink-0 items-center gap-1">
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="h-7 px-2 text-xs text-muted-foreground hover:text-foreground"
          aria-label={`Test ${server.name}`}
        >
          Test
        </Button>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="h-7 w-7 p-0 text-muted-foreground hover:text-destructive"
          aria-label={`Remove ${server.name}`}
          onClick={() => onRemove(server.id)}
        >
          <Trash2 className="h-3.5 w-3.5" />
        </Button>
      </div>
    </div>
  );
}

// ── Add Server Dialog ─────────────────────────────────────────────────────────

function AddServerDialog({
  open,
  onClose,
  onAdd,
}: {
  open: boolean;
  onClose: () => void;
  onAdd: (server: Omit<CustomServer, 'id' | 'status'>) => void;
}) {
  const t = useTranslations('chat.tools.custom.dialog');

  const [name, setName]             = useState('');
  const [transport, setTransport]   = useState<Transport>('sse');
  const [url, setUrl]               = useState('');
  const [command, setCommand]       = useState('');
  const [oauthEnabled, setOauthEnabled]   = useState(false);
  const [oauthTokenUrl, setOauthTokenUrl] = useState('');
  const [oauthClientId, setOauthClientId] = useState('');
  const [testStatus, setTestStatus] = useState<TestStatus>('idle');

  function resetForm() {
    setName(''); setTransport('sse'); setUrl(''); setCommand('');
    setOauthEnabled(false); setOauthTokenUrl(''); setOauthClientId('');
    setTestStatus('idle');
  }

  function handleClose() { resetForm(); onClose(); }

  function handleTestConnection() {
    setTestStatus('testing');
    setTimeout(() => setTestStatus(Math.random() > 0.2 ? 'passed' : 'failed'), 1500);
  }

  function handleAdd() {
    if (!name.trim()) return;
    const server: Omit<CustomServer, 'id' | 'status'> =
      transport === 'sse'
        ? { name: name.trim(), transport, url: url.trim() }
        : { name: name.trim(), transport, command: command.trim() };
    onAdd(server);
    handleClose();
  }

  return (
    <Dialog open={open} onOpenChange={(v) => !v && handleClose()}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{t('title')}</DialogTitle>
        </DialogHeader>

        <div className="space-y-4 py-2">
          <div className="space-y-1.5">
            <Label htmlFor="server-name">{t('nameLabel')}</Label>
            <Input id="server-name" value={name} onChange={(e) => setName(e.target.value)} placeholder="My MCP Server" />
          </div>

          <div className="space-y-1.5">
            <Label>{t('transportLabel')}</Label>
            <div className="flex gap-2">
              {(['sse', 'stdio'] as Transport[]).map((tr) => (
                <button
                  key={tr}
                  type="button"
                  onClick={() => setTransport(tr)}
                  className={cn(
                    'flex-1 rounded-md border px-4 py-2 text-sm font-medium transition-colors',
                    transport === tr
                      ? 'border-primary bg-primary text-primary-foreground'
                      : 'border-border bg-background text-foreground hover:bg-muted',
                  )}
                >
                  {t(tr)}
                </button>
              ))}
            </div>
          </div>

          {transport === 'sse' ? (
            <div className="space-y-1.5">
              <Label htmlFor="server-url">{t('urlLabel')}</Label>
              <Input id="server-url" value={url} onChange={(e) => setUrl(e.target.value)} placeholder="https://your-mcp-server.com/mcp" />
            </div>
          ) : (
            <div className="space-y-1.5">
              <Label htmlFor="server-command">{t('commandLabel')}</Label>
              <Input id="server-command" value={command} onChange={(e) => setCommand(e.target.value)} placeholder="node /path/to/server.js" className="font-mono text-sm" />
            </div>
          )}

          <div className="space-y-3 rounded-md border border-border p-3">
            <div className="flex items-center justify-between">
              <Label htmlFor="oauth-toggle">{t('oauthLabel')}</Label>
              <Switch id="oauth-toggle" checked={oauthEnabled} onCheckedChange={setOauthEnabled} />
            </div>
            {oauthEnabled && (
              <div className="space-y-3">
                <div className="space-y-1.5">
                  <Label htmlFor="oauth-token-url">Token URL</Label>
                  <Input id="oauth-token-url" value={oauthTokenUrl} onChange={(e) => setOauthTokenUrl(e.target.value)} placeholder="https://auth.example.com/token" />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="oauth-client-id">Client ID</Label>
                  <Input id="oauth-client-id" value={oauthClientId} onChange={(e) => setOauthClientId(e.target.value)} placeholder="client_abc123" />
                </div>
              </div>
            )}
          </div>

          <div className="flex items-center gap-3">
            <Button type="button" variant="outline" onClick={handleTestConnection} disabled={testStatus === 'testing'}>
              {testStatus === 'testing' ? t('testing') : t('testConnection')}
            </Button>
            {testStatus === 'passed' && (
              <span className="flex items-center gap-1.5 text-sm text-emerald-600 dark:text-emerald-400">
                <CheckCircle2 className="h-4 w-4" aria-hidden /> Connected
              </span>
            )}
            {testStatus === 'failed' && (
              <span className="flex items-center gap-1.5 text-sm text-destructive">
                <XCircle className="h-4 w-4" aria-hidden /> Failed
              </span>
            )}
          </div>
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={handleClose}>{t('cancel')}</Button>
          <Button type="button" onClick={handleAdd} disabled={!name.trim()}>{t('add')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ── Page ─────────────────────────────────────────────────────────────────────

export default function ToolsSettingsPage() {
  const t = useTranslations('chat.tools');

  const [groups, setGroups]           = useState<ToolGroup[]>(BUILT_IN_GROUPS_INIT);
  const [servers, setServers]         = useState<CustomServer[]>(MOCK_SERVERS_INIT);
  const [dialogOpen, setDialogOpen]   = useState(false);
  const [expandedId, setExpandedId]   = useState<string | null>(null);
  const [search, setSearch]           = useState('');

  function toggleExpand(groupId: string) {
    setExpandedId((prev) => (prev === groupId ? null : groupId));
  }

  function toggleGroupEnabled(groupId: string) {
    setGroups((prev) => prev.map((g) => g.id === groupId ? { ...g, enabled: !g.enabled } : g));
  }

  function toggleToolConfirmation(groupId: string, toolId: string) {
    setGroups((prev) =>
      prev.map((g) =>
        g.id === groupId
          ? { ...g, tools: g.tools.map((tool) => tool.id === toolId ? { ...tool, requiresConfirmation: !tool.requiresConfirmation } : tool) }
          : g,
      ),
    );
  }

  function removeServer(id: string) {
    setServers((prev) => prev.filter((s) => s.id !== id));
  }

  function addServer(server: Omit<CustomServer, 'id' | 'status'>) {
    setServers((prev) => [...prev, { ...server, id: `srv-${Date.now()}`, status: 'connected' }]);
  }

  const filteredGroups = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return groups;
    return groups
      .map((g) => {
        const groupMatch = g.name.toLowerCase().includes(q) || g.description.toLowerCase().includes(q);
        const matchedTools = g.tools.filter(
          (t) => t.name.toLowerCase().includes(q) || t.description.toLowerCase().includes(q),
        );
        if (groupMatch) return g;
        if (matchedTools.length > 0) return { ...g, tools: matchedTools };
        return null;
      })
      .filter((g): g is ToolGroup => g !== null);
  }, [groups, search]);

  const enabledCount = groups.filter((g) => g.enabled).length;

  return (
    <div className="flex-1 overflow-y-auto">
      <div className="mx-auto max-w-3xl px-4 py-5 sm:px-6">

        {/* Page header */}
        <div className="mb-5">
          <h1 className="text-lg font-semibold tracking-tight text-foreground">{t('title')}</h1>
          <p className="mt-0.5 text-sm text-muted-foreground">
            Give the AI access to your business tools and external MCP servers.
          </p>
        </div>

        <Tabs defaultValue="builtIn">
          <TabsList className="mb-4">
            <TabsTrigger value="builtIn" className="gap-1.5">
              <Zap className="h-3.5 w-3.5" aria-hidden />
              {t('tabs.builtIn')}
              <Badge variant="secondary" className="ml-1 text-[0.625rem] px-1.5 py-0">
                {enabledCount}/{groups.length}
              </Badge>
            </TabsTrigger>
            <TabsTrigger value="custom" className="gap-1.5">
              <Server className="h-3.5 w-3.5" aria-hidden />
              {t('tabs.custom')}
              {servers.length > 0 && (
                <Badge variant="secondary" className="ml-1 text-[0.625rem] px-1.5 py-0">
                  {servers.length}
                </Badge>
              )}
            </TabsTrigger>
          </TabsList>

          {/* ── Built-in tab ── */}
          <TabsContent value="builtIn">
            {/* Search */}
            <div className="relative mb-4">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground" aria-hidden />
              <Input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Search tools…"
                className="h-9 pl-8 text-sm"
                aria-label="Search built-in tools"
              />
            </div>

            {filteredGroups.length === 0 ? (
              <div className="flex flex-col items-center gap-2 py-12 text-center">
                <p className="text-sm font-medium text-foreground">No tools match &ldquo;{search}&rdquo;</p>
                <button
                  type="button"
                  onClick={() => setSearch('')}
                  className="text-xs text-primary hover:underline underline-offset-2"
                >
                  Clear search
                </button>
              </div>
            ) : (
              <div className="space-y-2.5">
                {filteredGroups.map((group) => (
                  <GroupCard
                    key={group.id}
                    group={group}
                    expanded={expandedId === group.id}
                    onExpand={() => toggleExpand(group.id)}
                    onToggleEnabled={toggleGroupEnabled}
                    onToggleConfirmation={toggleToolConfirmation}
                  />
                ))}
              </div>
            )}
          </TabsContent>

          {/* ── Custom servers tab ── */}
          <TabsContent value="custom">
            <div className="space-y-4">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-foreground">MCP Servers</p>
                  <p className="text-xs text-muted-foreground">
                    Connect external tool servers via the Model Context Protocol.
                  </p>
                </div>
                <Button
                  type="button"
                  size="sm"
                  onClick={() => setDialogOpen(true)}
                  className="gap-1.5"
                >
                  <Plus className="h-3.5 w-3.5" aria-hidden />
                  {t('custom.addServer')}
                </Button>
              </div>

              <Separator />

              {servers.length === 0 ? (
                <div className="flex flex-col items-center gap-3 rounded-xl border border-dashed border-border py-14 text-center">
                  <div className="flex h-12 w-12 items-center justify-center rounded-full bg-muted">
                    <Server className="h-6 w-6 text-muted-foreground" aria-hidden />
                  </div>
                  <div>
                    <p className="text-sm font-medium text-foreground">No MCP servers connected</p>
                    <p className="mt-0.5 text-xs text-muted-foreground">
                      Add a server to give the AI access to external tools.
                    </p>
                  </div>
                  <Button type="button" size="sm" onClick={() => setDialogOpen(true)} className="gap-1.5">
                    <Plus className="h-3.5 w-3.5" aria-hidden />
                    {t('custom.addServer')}
                  </Button>
                </div>
              ) : (
                <div className="space-y-2">
                  {servers.map((server) => (
                    <ServerRow key={server.id} server={server} onRemove={removeServer} />
                  ))}
                </div>
              )}
            </div>
          </TabsContent>
        </Tabs>

        <AddServerDialog
          open={dialogOpen}
          onClose={() => setDialogOpen(false)}
          onAdd={addServer}
        />
      </div>
    </div>
  );
}
