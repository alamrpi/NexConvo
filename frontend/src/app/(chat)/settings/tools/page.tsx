'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { Plus, Trash2, ChevronDown, ChevronUp, AlertTriangle } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { Badge } from '@/shared/ui/badge';
import { Switch } from '@/shared/ui/switch';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/shared/ui/tabs';
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/shared/ui/card';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/shared/ui/dialog';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/shared/ui/table';
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
}

type Transport = 'stdio' | 'sse';
type ServerStatus = 'connected' | 'error';

interface CustomServer {
  id: string;
  name: string;
  transport: Transport;
  url?: string;
  command?: string;
  status: ServerStatus;
}

type TestStatus = 'idle' | 'testing' | 'passed' | 'failed';

// ── Mock data ─────────────────────────────────────────────────────────────────

const BUILT_IN_GROUPS_INIT: ToolGroup[] = [
  {
    id: 'crm',
    name: 'CRM Tools',
    description: 'Access and update customer records',
    enabled: true,
    tools: [
      {
        id: 'crm_read_contact',
        name: 'Read Contact',
        description: 'Look up a customer by phone or email',
        requiresConfirmation: false,
      },
      {
        id: 'crm_update_contact',
        name: 'Update Contact',
        description: 'Update customer details',
        requiresConfirmation: true,
      },
      {
        id: 'crm_create_lead',
        name: 'Create Lead',
        description: 'Create a new lead from conversation',
        requiresConfirmation: true,
      },
    ],
  },
  {
    id: 'orders',
    name: 'Order Management',
    description: 'Query and manage customer orders',
    enabled: true,
    tools: [
      {
        id: 'orders_track',
        name: 'Track Order',
        description: 'Get real-time tracking status',
        requiresConfirmation: false,
      },
      {
        id: 'orders_cancel',
        name: 'Cancel Order',
        description: 'Cancel an order (irreversible)',
        requiresConfirmation: true,
      },
      {
        id: 'orders_refund',
        name: 'Initiate Refund',
        description: 'Start a refund process',
        requiresConfirmation: true,
      },
    ],
  },
  {
    id: 'calendar',
    name: 'Calendar & Scheduling',
    description: 'Book appointments and check availability',
    enabled: false,
    tools: [
      {
        id: 'cal_check',
        name: 'Check Availability',
        description: 'Check agent availability slots',
        requiresConfirmation: false,
      },
      {
        id: 'cal_book',
        name: 'Book Appointment',
        description: 'Create a calendar appointment',
        requiresConfirmation: true,
      },
    ],
  },
];

const MOCK_CUSTOM_SERVERS_INIT: CustomServer[] = [
  {
    id: 'srv-1',
    name: 'Inventory MCP',
    transport: 'sse',
    url: 'https://inventory.dhaka-retail.co/mcp',
    status: 'connected',
  },
  {
    id: 'srv-2',
    name: 'Payments MCP',
    transport: 'stdio',
    command: 'node /opt/payments-mcp/index.js',
    status: 'error',
  },
];

// ── Built-in group card ───────────────────────────────────────────────────────

interface GroupCardProps {
  group: ToolGroup;
  onToggleEnabled: (groupId: string) => void;
  onToggleConfirmation: (groupId: string, toolId: string) => void;
}

function GroupCard({ group, onToggleEnabled, onToggleConfirmation }: GroupCardProps) {
  const t = useTranslations('chat.tools');
  const [expanded, setExpanded] = useState(false);

  return (
    <Card className="overflow-hidden">
      <CardHeader className="pb-3">
        <div className="flex items-start justify-between gap-4">
          <div className="min-w-0 flex-1">
            <CardTitle className="text-base">{group.name}</CardTitle>
            <CardDescription className="mt-0.5 text-sm">{group.description}</CardDescription>
          </div>
          <div className="flex shrink-0 items-center gap-3">
            <Switch
              checked={group.enabled}
              onCheckedChange={() => onToggleEnabled(group.id)}
              aria-label={`Enable ${group.name}`}
            />
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => setExpanded((v) => !v)}
              className="min-h-[44px] gap-1.5"
              aria-expanded={expanded}
            >
              {t('builtIn.configure')}
              {expanded ? (
                <ChevronUp className="h-3.5 w-3.5" />
              ) : (
                <ChevronDown className="h-3.5 w-3.5" />
              )}
            </Button>
          </div>
        </div>
      </CardHeader>

      {expanded && (
        <CardContent className="pt-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('builtIn.columns.tool')}</TableHead>
                <TableHead className="hidden sm:table-cell">
                  {t('builtIn.columns.description')}
                </TableHead>
                <TableHead className="text-right">{t('builtIn.columns.confirmation')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {group.tools.map((tool) => (
                <TableRow key={tool.id}>
                  <TableCell className="font-medium">
                    <div className="flex items-center gap-2">
                      {tool.name}
                      {tool.requiresConfirmation && (
                        <AlertTriangle className="h-3.5 w-3.5 text-yellow-500 dark:text-yellow-400" />
                      )}
                    </div>
                    <p className="mt-0.5 text-xs text-muted-foreground sm:hidden">
                      {tool.description}
                    </p>
                  </TableCell>
                  <TableCell className="hidden text-sm text-muted-foreground sm:table-cell">
                    {tool.description}
                  </TableCell>
                  <TableCell className="text-right">
                    <Switch
                      checked={tool.requiresConfirmation}
                      onCheckedChange={() => onToggleConfirmation(group.id, tool.id)}
                      aria-label={`Require confirmation for ${tool.name}`}
                    />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      )}
    </Card>
  );
}

// ── Custom server row ─────────────────────────────────────────────────────────

interface ServerRowProps {
  server: CustomServer;
  onRemove: (id: string) => void;
}

function ServerRow({ server, onRemove }: ServerRowProps) {
  const t = useTranslations('chat.tools');
  const [expanded, setExpanded] = useState(false);

  return (
    <div className="rounded-md border border-border bg-card">
      <button
        type="button"
        onClick={() => setExpanded((v) => !v)}
        className="flex w-full items-center justify-between gap-3 p-4 text-left"
        aria-expanded={expanded}
      >
        <div className="flex min-w-0 items-center gap-3">
          <span
            aria-label={t(`custom.status.${server.status}`)}
            className={cn(
              'h-2 w-2 shrink-0 rounded-full',
              server.status === 'connected' ? 'bg-green-500' : 'bg-destructive',
            )}
          />
          <span className="truncate text-sm font-medium text-foreground">{server.name}</span>
          <Badge variant="outline" className="hidden shrink-0 font-mono text-xs sm:inline-flex">
            {server.transport}
          </Badge>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          <Button
            type="button"
            variant="ghost"
            size="sm"
            aria-label={`Remove ${server.name}`}
            onClick={(e) => {
              e.stopPropagation();
              onRemove(server.id);
            }}
            className="h-8 w-8 p-0 text-muted-foreground hover:text-destructive"
          >
            <Trash2 className="h-4 w-4" />
          </Button>
          {expanded ? (
            <ChevronUp className="h-4 w-4 text-muted-foreground" />
          ) : (
            <ChevronDown className="h-4 w-4 text-muted-foreground" />
          )}
        </div>
      </button>

      {expanded && (
        <div className="border-t border-border px-4 pb-4 pt-3">
          {server.transport === 'sse' && server.url && (
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground">URL</p>
              <code className="block rounded bg-muted px-2 py-1 text-xs text-foreground">
                {server.url}
              </code>
            </div>
          )}
          {server.transport === 'stdio' && server.command && (
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground">Command</p>
              <code className="block rounded bg-muted px-2 py-1 text-xs text-foreground">
                {server.command}
              </code>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

// ── Add Server Dialog ─────────────────────────────────────────────────────────

interface AddServerDialogProps {
  open: boolean;
  onClose: () => void;
  onAdd: (server: Omit<CustomServer, 'id' | 'status'>) => void;
}

function AddServerDialog({ open, onClose, onAdd }: AddServerDialogProps) {
  const t = useTranslations('chat.tools.custom.dialog');

  const [name, setName] = useState('');
  const [transport, setTransport] = useState<Transport>('sse');
  const [url, setUrl] = useState('');
  const [command, setCommand] = useState('');
  const [oauthEnabled, setOauthEnabled] = useState(false);
  const [oauthTokenUrl, setOauthTokenUrl] = useState('');
  const [oauthClientId, setOauthClientId] = useState('');
  const [testStatus, setTestStatus] = useState<TestStatus>('idle');

  function resetForm() {
    setName('');
    setTransport('sse');
    setUrl('');
    setCommand('');
    setOauthEnabled(false);
    setOauthTokenUrl('');
    setOauthClientId('');
    setTestStatus('idle');
  }

  function handleClose() {
    resetForm();
    onClose();
  }

  function handleTestConnection() {
    setTestStatus('testing');
    setTimeout(() => {
      setTestStatus(Math.random() > 0.2 ? 'passed' : 'failed');
    }, 1500);
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
          {/* Name */}
          <div className="space-y-1.5">
            <Label htmlFor="server-name">{t('nameLabel')}</Label>
            <Input
              id="server-name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="My MCP Server"
            />
          </div>

          {/* Transport */}
          <div className="space-y-1.5">
            <Label>{t('transportLabel')}</Label>
            <div className="flex gap-2">
              {(['sse', 'stdio'] as Transport[]).map((tr) => (
                <button
                  key={tr}
                  type="button"
                  onClick={() => setTransport(tr)}
                  className={cn(
                    'min-h-[44px] rounded-md border px-4 py-2 text-sm font-medium transition-colors',
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

          {/* URL or command */}
          {transport === 'sse' ? (
            <div className="space-y-1.5">
              <Label htmlFor="server-url">{t('urlLabel')}</Label>
              <Input
                id="server-url"
                value={url}
                onChange={(e) => setUrl(e.target.value)}
                placeholder="https://your-mcp-server.com/mcp"
              />
            </div>
          ) : (
            <div className="space-y-1.5">
              <Label htmlFor="server-command">{t('commandLabel')}</Label>
              <Input
                id="server-command"
                value={command}
                onChange={(e) => setCommand(e.target.value)}
                placeholder="node /path/to/server.js"
                className="font-mono text-sm"
              />
            </div>
          )}

          {/* OAuth */}
          <div className="space-y-3 rounded-md border border-border p-3">
            <div className="flex items-center justify-between">
              <Label htmlFor="oauth-toggle">{t('oauthLabel')}</Label>
              <Switch
                id="oauth-toggle"
                checked={oauthEnabled}
                onCheckedChange={setOauthEnabled}
              />
            </div>
            {oauthEnabled && (
              <div className="space-y-3">
                <div className="space-y-1.5">
                  <Label htmlFor="oauth-token-url">Token URL</Label>
                  <Input
                    id="oauth-token-url"
                    value={oauthTokenUrl}
                    onChange={(e) => setOauthTokenUrl(e.target.value)}
                    placeholder="https://auth.example.com/token"
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="oauth-client-id">Client ID</Label>
                  <Input
                    id="oauth-client-id"
                    value={oauthClientId}
                    onChange={(e) => setOauthClientId(e.target.value)}
                    placeholder="client_abc123"
                  />
                </div>
              </div>
            )}
          </div>

          {/* Test connection */}
          <div className="flex items-center gap-3">
            <Button
              type="button"
              variant="outline"
              onClick={handleTestConnection}
              disabled={testStatus === 'testing'}
              className="min-h-[44px]"
            >
              {testStatus === 'testing' ? t('testing') : t('testConnection')}
            </Button>
            {testStatus === 'passed' && (
              <span className="text-sm text-green-600 dark:text-green-400">
                ✓ Connected
              </span>
            )}
            {testStatus === 'failed' && (
              <span className="text-sm text-destructive">✗ Failed</span>
            )}
          </div>
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={handleClose} className="min-h-[44px]">
            {t('cancel')}
          </Button>
          <Button
            type="button"
            onClick={handleAdd}
            disabled={!name.trim()}
            className="min-h-[44px]"
          >
            {t('add')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ── Page ─────────────────────────────────────────────────────────────────────

export default function ToolsSettingsPage() {
  const t = useTranslations('chat.tools');

  const [groups, setGroups] = useState<ToolGroup[]>(BUILT_IN_GROUPS_INIT);
  const [servers, setServers] = useState<CustomServer[]>(MOCK_CUSTOM_SERVERS_INIT);
  const [dialogOpen, setDialogOpen] = useState(false);

  function toggleGroupEnabled(groupId: string) {
    setGroups((prev) =>
      prev.map((g) => (g.id === groupId ? { ...g, enabled: !g.enabled } : g)),
    );
  }

  function toggleToolConfirmation(groupId: string, toolId: string) {
    setGroups((prev) =>
      prev.map((g) =>
        g.id === groupId
          ? {
              ...g,
              tools: g.tools.map((tool) =>
                tool.id === toolId
                  ? { ...tool, requiresConfirmation: !tool.requiresConfirmation }
                  : tool,
              ),
            }
          : g,
      ),
    );
  }

  function removeServer(id: string) {
    setServers((prev) => prev.filter((s) => s.id !== id));
  }

  function addServer(server: Omit<CustomServer, 'id' | 'status'>) {
    const newServer: CustomServer = {
      ...server,
      id: `srv-${Date.now()}`,
      status: 'connected',
    };
    setServers((prev) => [...prev, newServer]);
  }

  return (
    <div className="mx-auto max-w-2xl space-y-6 p-4 pb-16 sm:p-6">
      <h1 className="text-2xl font-bold tracking-tight text-foreground">{t('title')}</h1>

      <Tabs defaultValue="builtIn">
        <TabsList className="mb-6">
          <TabsTrigger value="builtIn">{t('tabs.builtIn')}</TabsTrigger>
          <TabsTrigger value="custom">{t('tabs.custom')}</TabsTrigger>
        </TabsList>

        {/* ── Built-in tab ─────────────────────────────────────────────────── */}
        <TabsContent value="builtIn" className="space-y-4">
          {groups.map((group) => (
            <GroupCard
              key={group.id}
              group={group}
              onToggleEnabled={toggleGroupEnabled}
              onToggleConfirmation={toggleToolConfirmation}
            />
          ))}
        </TabsContent>

        {/* ── Custom servers tab ───────────────────────────────────────────── */}
        <TabsContent value="custom" className="space-y-4">
          <div className="flex justify-end">
            <Button
              type="button"
              onClick={() => setDialogOpen(true)}
              className="min-h-[44px] gap-2"
            >
              <Plus className="h-4 w-4" />
              {t('custom.addServer')}
            </Button>
          </div>

          {servers.length === 0 ? (
            <div className="rounded-md border border-dashed border-border py-12 text-center">
              <p className="text-sm text-muted-foreground">No custom MCP servers configured.</p>
            </div>
          ) : (
            <div className="space-y-2">
              {servers.map((server) => (
                <ServerRow key={server.id} server={server} onRemove={removeServer} />
              ))}
            </div>
          )}
        </TabsContent>
      </Tabs>

      <AddServerDialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        onAdd={addServer}
      />
    </div>
  );
}
