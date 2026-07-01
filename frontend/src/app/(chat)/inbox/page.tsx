'use client';

import { useState, useEffect, useRef, useCallback } from 'react';
import { useTranslations } from 'next-intl';
import {
  Bot,
  ChevronRight,
  ChevronLeft,
  Menu,
  Tag,
  Download,
  X,
  StickyNote,
} from 'lucide-react';

import {
  MOCK_CONVERSATIONS,
  MOCK_AGENT,
  type Conversation,
  type ConversationState,
  type Channel,
  type Message,
} from '@/features/chat/mock-data';

import { ConversationListItem } from '@/shared/ui/chat/conversation-list-item';
import { MessageBubble } from '@/shared/ui/chat/message-bubble';
import { StateBanner } from '@/shared/ui/chat/state-banner';
import { ChannelBadge } from '@/shared/ui/chat/channel-badge';
import { StateChip } from '@/shared/ui/chat/state-chip';

import { ScrollArea } from '@/shared/ui/scroll-area';
import { Tabs, TabsList, TabsTrigger } from '@/shared/ui/tabs';
import { Input } from '@/shared/ui/input';
import { Button } from '@/shared/ui/button';
import { Separator } from '@/shared/ui/separator';
import {
  Drawer,
  DrawerContent,
  DrawerTitle,
} from '@/shared/ui/drawer';

import { cn } from '@/shared/lib/cn';

// ─── Types ────────────────────────────────────────────────────────────────────

type TabId = 'all' | 'ai' | 'pending' | 'mine' | 'resolved';

interface ConvState {
  state: ConversationState;
  assignedAgentName?: string;
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

const ALL_CHANNELS: Channel[] = ['whatsapp', 'facebook', 'instagram', 'telegram', 'web'];

function filterByTab(convs: Conversation[], tab: TabId): Conversation[] {
  switch (tab) {
    case 'ai':
      return convs.filter((c) => c.state === 'AiHandling');
    case 'pending':
      return convs.filter((c) => c.state === 'PendingHuman');
    case 'mine':
      return convs.filter((c) => c.assignedAgentName === MOCK_AGENT.name);
    case 'resolved':
      return convs.filter((c) => c.state === 'Resolved' || c.state === 'Closed');
    default:
      return convs;
  }
}

function sortConvs(convs: Conversation[], tab: TabId): Conversation[] {
  if (tab === 'pending') {
    return [...convs].sort((a, b) => {
      if (!a.slaExpiresAt) return 1;
      if (!b.slaExpiresAt) return -1;
      return new Date(a.slaExpiresAt).getTime() - new Date(b.slaExpiresAt).getTime();
    });
  }
  return [...convs].sort(
    (a, b) => new Date(b.lastMessageAt).getTime() - new Date(a.lastMessageAt).getTime(),
  );
}

function tabCount(convs: Conversation[], tab: TabId): number {
  return filterByTab(convs, tab).length;
}

// ─── Channel filter toggle button ─────────────────────────────────────────────

interface ChannelToggleProps {
  channel: Channel;
  active: boolean;
  onToggle: (ch: Channel) => void;
}

const CHANNEL_LABEL: Record<Channel, string> = {
  whatsapp: 'W',
  facebook: 'F',
  instagram: 'I',
  telegram: 'T',
  web: '🌐',
};

const CHANNEL_BG: Record<Channel, string> = {
  whatsapp: 'bg-chatChannel-whatsapp',
  facebook: 'bg-chatChannel-facebook',
  instagram: 'bg-chatChannel-instagram',
  telegram: 'bg-chatChannel-telegram',
  web: 'bg-chatChannel-web',
};

function ChannelToggle({ channel, active, onToggle }: ChannelToggleProps) {
  return (
    <button
      type="button"
      aria-label={`Filter by ${channel}`}
      aria-pressed={active}
      onClick={() => onToggle(channel)}
      className={cn(
        'h-6 w-6 rounded-full text-[0.5rem] font-bold text-white transition-opacity focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
        CHANNEL_BG[channel],
        active ? 'opacity-100 ring-2 ring-ring ring-offset-1' : 'opacity-40 hover:opacity-70',
      )}
    >
      {CHANNEL_LABEL[channel]}
    </button>
  );
}

// ─── Streaming bubble ─────────────────────────────────────────────────────────

interface StreamingBubbleProps {
  message: Message;
}

function StreamingBubble({ message }: StreamingBubbleProps) {
  const words = message.body.split(' ');
  const [revealedCount, setRevealedCount] = useState(0);

  useEffect(() => {
    if (revealedCount >= words.length) return;
    const id = setInterval(() => {
      setRevealedCount((n) => {
        if (n >= words.length) {
          clearInterval(id);
          return n;
        }
        return n + 1;
      });
    }, 50);
    return () => clearInterval(id);
  }, [words.length, revealedCount]);

  const displayedText = words.slice(0, revealedCount).join(' ');
  const isComplete = revealedCount >= words.length;

  // Pass a synthetic message with streaming cleared once complete so MessageBubble renders normally
  const syntheticMessage: Message = {
    ...message,
    body: displayedText || '▌',
    isStreaming: !isComplete && revealedCount === 0,
  };

  return <MessageBubble message={syntheticMessage} isCurrentAgent={false} />;
}

// ─── Left panel — conversation list ──────────────────────────────────────────

interface ConversationListPanelProps {
  selectedId: string | null;
  onSelect: (id: string) => void;
}

function ConversationListPanel({ selectedId, onSelect }: ConversationListPanelProps) {
  const t = useTranslations('chat');
  const [tab, setTab] = useState<TabId>('all');
  const [search, setSearch] = useState('');
  const [channelFilter, setChannelFilter] = useState<Set<Channel>>(new Set());

  const toggleChannel = useCallback((ch: Channel) => {
    setChannelFilter((prev) => {
      const next = new Set(prev);
      if (next.has(ch)) next.delete(ch);
      else next.add(ch);
      return next;
    });
  }, []);

  const filtered = sortConvs(
    filterByTab(MOCK_CONVERSATIONS, tab).filter((c) => {
      const q = search.toLowerCase();
      const matchesSearch =
        !q ||
        (c.contactName?.toLowerCase().includes(q) ?? false) ||
        c.lastMessagePreview.toLowerCase().includes(q);
      const matchesChannel =
        channelFilter.size === 0 || channelFilter.has(c.channel);
      return matchesSearch && matchesChannel;
    }),
    tab,
  );

  const TABS: { id: TabId; labelKey: string }[] = [
    { id: 'all', labelKey: 'inbox.tabs.all' },
    { id: 'ai', labelKey: 'inbox.tabs.ai' },
    { id: 'pending', labelKey: 'inbox.tabs.pending' },
    { id: 'mine', labelKey: 'inbox.tabs.mine' },
    { id: 'resolved', labelKey: 'inbox.tabs.resolved' },
  ];

  return (
    <div className="flex h-full flex-col border-r border-border bg-card">
      {/* Header */}
      <div className="shrink-0 px-4 pt-4 pb-2">
        <h1 className="text-base font-semibold text-foreground">{t('inbox.title')}</h1>
      </div>

      {/* Tabs */}
      <div className="shrink-0 overflow-x-auto px-2 pb-1">
        <Tabs value={tab} onValueChange={(v) => setTab(v as TabId)}>
          <TabsList className="h-auto w-full gap-0.5 bg-transparent p-0">
            {TABS.map(({ id, labelKey }) => {
              const count = tabCount(MOCK_CONVERSATIONS, id);
              return (
                <TabsTrigger
                  key={id}
                  value={id}
                  className="h-7 gap-1 px-2 py-1 text-xs data-[state=active]:bg-background"
                >
                  {t(labelKey as Parameters<typeof t>[0])}
                  {count > 0 && (
                    <span
                      aria-label={`${count} conversations`}
                      className="rounded-full bg-muted px-1 text-[0.5rem] font-semibold text-muted-foreground data-[state=active]:bg-primary/10"
                    >
                      {count}
                    </span>
                  )}
                </TabsTrigger>
              );
            })}
          </TabsList>
        </Tabs>
      </div>

      <Separator />

      {/* Search */}
      <div className="shrink-0 px-3 py-2">
        <Input
          type="search"
          placeholder={t('inbox.search')}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="h-8 text-sm"
          aria-label={t('inbox.search')}
        />
      </div>

      {/* Channel filter */}
      <div
        role="group"
        aria-label={t('inbox.filterChannel')}
        className="flex shrink-0 items-center gap-2 px-3 pb-2"
      >
        {ALL_CHANNELS.map((ch) => (
          <ChannelToggle
            key={ch}
            channel={ch}
            active={channelFilter.has(ch)}
            onToggle={toggleChannel}
          />
        ))}
        {channelFilter.size > 0 && (
          <button
            type="button"
            aria-label="Clear channel filter"
            onClick={() => setChannelFilter(new Set())}
            className="ml-auto text-muted-foreground hover:text-foreground"
          >
            <X className="h-3.5 w-3.5" />
          </button>
        )}
      </div>

      <Separator />

      {/* List */}
      <ScrollArea className="flex-1">
        {filtered.length === 0 ? (
          <div className="flex flex-col items-center gap-2 py-12 text-center">
            <Bot className="h-8 w-8 text-muted-foreground/40" aria-hidden />
            <p className="text-sm text-muted-foreground">{t('inbox.empty')}</p>
            <p className="text-xs text-muted-foreground/60">{t('inbox.emptyHint')}</p>
          </div>
        ) : (
          <div className="flex flex-col gap-px p-2">
            {filtered.map((conv) => (
              <ConversationListItem
                key={conv.id}
                conv={conv}
                isSelected={selectedId === conv.id}
                onClick={() => onSelect(conv.id)}
              />
            ))}
          </div>
        )}
      </ScrollArea>
    </div>
  );
}

// ─── Right sidebar ─────────────────────────────────────────────────────────────

interface ConversationSidebarProps {
  conv: Conversation;
  convState: ConvState;
}

function ConversationSidebar({ conv, convState }: ConversationSidebarProps) {
  const t = useTranslations('chat');
  const escalationMessages = conv.messages.filter((m) => m.senderRole === 'System');

  return (
    <aside className="flex h-full w-64 shrink-0 flex-col border-l border-border bg-card">
      <ScrollArea className="flex-1">
        <div className="p-4 space-y-5">
          {/* Contact info */}
          <section aria-label={t('conversation.contactInfo')}>
            <h2 className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              {t('conversation.contactInfo')}
            </h2>
            <div className="space-y-1.5 text-sm">
              <div className="flex items-center gap-2">
                <ChannelBadge channel={conv.channel} size="sm" />
                <span className="text-foreground truncate">{conv.contactHandle}</span>
              </div>
              <p className="text-muted-foreground">{conv.contactName ?? conv.contactHandle}</p>
            </div>
          </section>

          <Separator />

          {/* Conversation metadata */}
          <section aria-label={t('conversation.metadata')}>
            <h2 className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              {t('conversation.metadata')}
            </h2>
            <div className="space-y-2 text-sm">
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">State</span>
                <StateChip state={convState.state} />
              </div>
              {conv.tags.length > 0 && (
                <div>
                  <span className="text-muted-foreground">Tags</span>
                  <div className="mt-1 flex flex-wrap gap-1">
                    {conv.tags.map((tag) => (
                      <span
                        key={tag}
                        className="rounded-full bg-secondary px-2 py-px text-[0.6rem] text-secondary-foreground"
                      >
                        {tag}
                      </span>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </section>

          {escalationMessages.length > 0 && (
            <>
              <Separator />
              {/* Escalation log */}
              <section aria-label={t('conversation.escalationLog')}>
                <h2 className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  {t('conversation.escalationLog')}
                </h2>
                <div className="space-y-1">
                  {escalationMessages.map((m) => (
                    <p key={m.id} className="text-xs text-muted-foreground border-l-2 border-border pl-2">
                      {m.body}
                    </p>
                  ))}
                </div>
              </section>
            </>
          )}

          <Separator />

          {/* Actions */}
          <section aria-label="Conversation actions">
            <h2 className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              Actions
            </h2>
            <div className="flex flex-col gap-1.5">
              <Button
                variant="ghost"
                size="sm"
                className="justify-start gap-2 text-sm"
                onClick={() => {
                  /* no-op */
                }}
              >
                <Tag className="h-3.5 w-3.5" aria-hidden />
                {t('conversation.addTag')}
              </Button>
              <Button
                variant="ghost"
                size="sm"
                className="justify-start gap-2 text-sm"
                onClick={() => {
                  /* no-op */
                }}
              >
                <StickyNote className="h-3.5 w-3.5" aria-hidden />
                {t('conversation.addNote')}
              </Button>
              <Button
                variant="ghost"
                size="sm"
                className="justify-start gap-2 text-sm"
                onClick={() => {
                  /* no-op */
                }}
              >
                <Download className="h-3.5 w-3.5" aria-hidden />
                {t('conversation.export')}
              </Button>
            </div>
          </section>
        </div>
      </ScrollArea>
    </aside>
  );
}

// ─── Conversation view (right panel) ──────────────────────────────────────────

interface ConversationViewProps {
  conv: Conversation;
  convState: ConvState;
  onConvStateChange: (update: Partial<ConvState>) => void;
  onBack: () => void;
}

function ConversationView({ conv, convState, onConvStateChange, onBack }: ConversationViewProps) {
  const t = useTranslations('chat');
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const bottomRef = useRef<HTMLDivElement>(null);

  // Auto-scroll to latest message
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [conv.id, conv.messages.length]);

  const isCurrentAgentAssigned = convState.assignedAgentName === MOCK_AGENT.name;
  const canResolveHere =
    convState.state === 'HumanHandling' && isCurrentAgentAssigned;

  return (
    <div className="flex min-w-0 flex-1 flex-col">
      {/* Header */}
      <header className="flex shrink-0 items-center gap-3 border-b border-border bg-card px-4 py-3">
        {/* Back button (mobile) */}
        <Button
          variant="ghost"
          size="icon"
          className="md:hidden"
          onClick={onBack}
          aria-label={t('conversation.back')}
        >
          <ChevronLeft className="h-4 w-4" />
        </Button>

        <div className="flex min-w-0 flex-1 items-center gap-2">
          <span className="truncate text-sm font-semibold text-foreground">
            {conv.contactName ?? conv.contactHandle}
          </span>
          <ChannelBadge channel={conv.channel} size="sm" />
          <StateChip state={convState.state} />
        </div>

        <div className="flex shrink-0 items-center gap-2">
          {canResolveHere && (
            <Button
              size="sm"
              variant="outline"
              onClick={() => onConvStateChange({ state: 'Resolved' })}
            >
              {t('conversation.resolve')}
            </Button>
          )}
          <Button
            variant="ghost"
            size="icon"
            aria-label={t('conversation.toggleSidebar')}
            onClick={() => setSidebarOpen((o) => !o)}
          >
            {sidebarOpen ? (
              <ChevronRight className="h-4 w-4" />
            ) : (
              <ChevronLeft className="h-4 w-4" />
            )}
          </Button>
        </div>
      </header>

      {/* Body: messages + optional sidebar */}
      <div className="flex min-h-0 flex-1">
        {/* Message thread */}
        <div className="flex min-w-0 flex-1 flex-col">
          <ScrollArea className="flex-1 px-4 py-4">
            <div className="flex flex-col gap-4">
              {conv.messages.map((msg) =>
                msg.isStreaming ? (
                  <StreamingBubble key={msg.id} message={msg} />
                ) : (
                  <MessageBubble
                    key={msg.id}
                    message={msg}
                    isCurrentAgent={
                      msg.senderRole === 'Agent' && msg.senderName === MOCK_AGENT.name
                    }
                  />
                ),
              )}
              {/* TODO: connect SignalR — append incoming messages here */}
              <div ref={bottomRef} aria-hidden />
            </div>
          </ScrollArea>

          {/* State banner */}
          <StateBanner
            state={convState.state}
            assignedAgentName={convState.assignedAgentName}
            slaExpiresAt={conv.slaExpiresAt}
            isCurrentAgentAssigned={isCurrentAgentAssigned}
            onTakeOver={() =>
              onConvStateChange({ state: 'HumanHandling', assignedAgentName: MOCK_AGENT.name })
            }
            onAccept={() =>
              onConvStateChange({ state: 'HumanHandling', assignedAgentName: MOCK_AGENT.name })
            }
            onResolve={() => onConvStateChange({ state: 'Resolved' })}
            onReopen={() =>
              onConvStateChange({ state: 'HumanHandling', assignedAgentName: MOCK_AGENT.name })
            }
          />
        </div>

        {/* Collapsible right sidebar (desktop) */}
        {sidebarOpen && (
          <div className="hidden lg:flex">
            <ConversationSidebar conv={conv} convState={convState} />
          </div>
        )}
      </div>
    </div>
  );
}

// ─── Empty state (no conversation selected) ────────────────────────────────────

function NoConversationSelected() {
  const t = useTranslations('chat');
  return (
    <div className="flex flex-1 flex-col items-center justify-center gap-3 text-center">
      <div className="flex h-14 w-14 items-center justify-center rounded-full bg-muted">
        <Bot className="h-7 w-7 text-muted-foreground/50" aria-hidden />
      </div>
      <p className="text-sm font-medium text-foreground">{t('inbox.selectPrompt')}</p>
      <p className="text-xs text-muted-foreground">{t('inbox.noConversation')}</p>
    </div>
  );
}

// ─── Page root ────────────────────────────────────────────────────────────────

export default function InboxPage() {
  const t = useTranslations('chat');

  // Selected conversation id
  const [selectedId, setSelectedId] = useState<string | null>(null);

  // Local state overrides per conversation (state transitions, assignment)
  const [convStateMap, setConvStateMap] = useState<Record<string, ConvState>>({});

  // Mobile drawer open
  const [drawerOpen, setDrawerOpen] = useState(false);

  const selectedConv = selectedId
    ? MOCK_CONVERSATIONS.find((c) => c.id === selectedId) ?? null
    : null;

  const getConvState = (conv: Conversation): ConvState =>
    convStateMap[conv.id] ?? {
      state: conv.state,
      assignedAgentName: conv.assignedAgentName,
    };

  const handleConvStateChange = useCallback(
    (id: string, update: Partial<ConvState>) => {
      setConvStateMap((prev) => ({
        ...prev,
        [id]: { ...(prev[id] ?? { state: 'AiHandling' }), ...update },
      }));
    },
    [],
  );

  const handleSelect = useCallback((id: string) => {
    setSelectedId(id);
    setDrawerOpen(false); // close drawer on mobile when conversation selected
  }, []);

  const handleBack = useCallback(() => {
    setSelectedId(null);
  }, []);

  return (
    // The layout's <main> is `overflow-auto`; we need full height here
    <div className="flex h-full overflow-hidden">
      {/* ── Desktop: fixed-width left panel ──────────────────────────── */}
      <div className="hidden w-80 shrink-0 md:flex md:flex-col">
        <ConversationListPanel selectedId={selectedId} onSelect={handleSelect} />
      </div>

      {/* ── Mobile: hamburger + drawer ────────────────────────────────── */}
      {/* Hamburger only shown when no conversation is selected on mobile */}
      {!selectedConv && (
        <button
          type="button"
          aria-label={t('nav.openMenu')}
          onClick={() => setDrawerOpen(true)}
          className={cn(
            'fixed bottom-6 right-6 z-40 flex h-12 w-12 items-center justify-center rounded-full bg-primary text-primary-foreground shadow-md md:hidden',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
          )}
        >
          <Menu className="h-5 w-5" />
        </button>
      )}

      {selectedConv && (
        <button
          type="button"
          aria-label="Open conversation list"
          onClick={() => setDrawerOpen(true)}
          className={cn(
            'fixed bottom-6 right-6 z-40 flex h-10 w-10 items-center justify-center rounded-full bg-card border border-border shadow-sm md:hidden',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
          )}
        >
          <Menu className="h-4 w-4 text-foreground" />
        </button>
      )}

      {/* vaul Drawer — left-side panel on mobile */}
      <Drawer
        open={drawerOpen}
        onOpenChange={setDrawerOpen}
        direction="left"
        shouldScaleBackground={false}
      >
        <DrawerContent
          className={cn(
            'fixed inset-y-0 left-0 z-50 m-0 h-full w-[85vw] max-w-xs rounded-none border-r border-border bg-card',
          )}
        >
          <DrawerTitle className="sr-only">{t('inbox.title')}</DrawerTitle>
          <div className="flex h-full flex-col">
            {/* Close button inside drawer */}
            <div className="flex items-center justify-end p-2">
              <button
                type="button"
                aria-label="Close menu"
                onClick={() => setDrawerOpen(false)}
                className="rounded-md p-1.5 text-muted-foreground hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
            <div className="min-h-0 flex-1">
              <ConversationListPanel selectedId={selectedId} onSelect={handleSelect} />
            </div>
          </div>
        </DrawerContent>
      </Drawer>

      {/* ── Right panel ───────────────────────────────────────────────── */}
      <div
        className={cn(
          'flex min-w-0 flex-1 flex-col',
          // On mobile: hide list panel area, show conversation or empty prompt
          selectedConv ? 'flex' : 'hidden md:flex',
        )}
      >
        {selectedConv ? (
          <ConversationView
            key={selectedConv.id}
            conv={selectedConv}
            convState={getConvState(selectedConv)}
            onConvStateChange={(update) => handleConvStateChange(selectedConv.id, update)}
            onBack={handleBack}
          />
        ) : (
          <NoConversationSelected />
        )}
      </div>

      {/* On mobile with no conversation selected, show the list full-screen */}
      <div
        className={cn(
          'flex min-w-0 flex-1 flex-col md:hidden',
          selectedConv ? 'hidden' : 'flex',
        )}
      >
        <ConversationListPanel selectedId={selectedId} onSelect={handleSelect} />
      </div>
    </div>
  );
}
