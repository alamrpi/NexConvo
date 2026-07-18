'use client';

import { useState, useEffect, useRef, useCallback, useMemo, useSyncExternalStore } from 'react';
import { useTranslations } from 'next-intl';
import { useQueryClient } from '@tanstack/react-query';
import {
  Bot,
  ChevronLeft,
  Menu,
  Tag,
  Download,
  X,
  StickyNote,
  Send,
  Paperclip,
  Smile,
  Search,
  Info,
} from 'lucide-react';

import type { Channel, Conversation, ConversationState, Message } from '@/features/chat/mock-data';

import type { ConversationSummaryDto, MessageDto } from '@/features/inbox/model/inbox.types';
import { useConversations } from '@/features/inbox/api/use-conversations';
import { useConversationMessages } from '@/features/inbox/api/use-conversation-messages';
import { useSendReply } from '@/features/inbox/api/use-send-reply';
import { useTakeOver } from '@/features/inbox/api/use-take-over';
import { useResolve } from '@/features/inbox/api/use-resolve';
import { useReopen } from '@/features/inbox/api/use-reopen';
import { useChatHub } from '@/features/inbox/api/use-chat-hub';
import { useSessionStore } from '@/features/auth/model/session.store';

import { ConversationListItem } from '@/shared/ui/chat/conversation-list-item';
import { MessageBubble } from '@/shared/ui/chat/message-bubble';
import { StateBanner } from '@/shared/ui/chat/state-banner';
import { ChannelBadge } from '@/shared/ui/chat/channel-badge';
import { StateChip } from '@/shared/ui/chat/state-chip';

import { Tabs, TabsList, TabsTrigger } from '@/shared/ui/tabs';
import { Textarea } from '@/shared/ui/textarea';
import { Button } from '@/shared/ui/button';
import { Separator } from '@/shared/ui/separator';
import { Drawer, DrawerContent, DrawerTitle } from '@/shared/ui/drawer';

import { cn } from '@/shared/lib/cn';

// ─── Types ────────────────────────────────────────────────────────────────────

type TabId = 'all' | 'ai' | 'pending' | 'mine' | 'resolved';

// ─── Helpers ─────────────────────────────────────────────────────────────────

const ALL_CHANNELS: Channel[] = ['whatsapp', 'facebook', 'instagram', 'telegram', 'web'];
const LIST_PAGE_SIZE = 25;

/** Maps a UI tab to the server-side filters GetConversationsQuery understands (S7: server owns
 * filtering/sorting/pagination — the client no longer filters the full mock array in memory). */
function filtersForTab(tab: TabId): { state?: ConversationState; assignedToMe?: boolean } {
  switch (tab) {
    case 'ai':       return { state: 'AiHandling' };
    case 'pending':  return { state: 'PendingHuman' };
    case 'mine':     return { assignedToMe: true };
    case 'resolved': return { state: 'Resolved' };
    default:         return {};
  }
}

// ─── Channel filter pill ──────────────────────────────────────────────────────

const CHANNEL_COLOR: Record<Channel, string> = {
  whatsapp:  'bg-chatChannel-whatsapp',
  facebook:  'bg-chatChannel-facebook',
  instagram: 'bg-chatChannel-instagram',
  telegram:  'bg-chatChannel-telegram',
  web:       'bg-chatChannel-web',
};
const CHANNEL_LABEL: Record<Channel, string> = {
  whatsapp: 'W', facebook: 'F', instagram: 'I', telegram: 'T', web: '⊕',
};

function ChannelPill({ channel, active, onToggle }: {
  channel: Channel; active: boolean; onToggle: (c: Channel) => void;
}) {
  return (
    <button
      type="button"
      aria-label={`Filter ${channel}`}
      aria-pressed={active}
      onClick={() => onToggle(channel)}
      className={cn(
        'flex h-6 w-6 items-center justify-center rounded-full text-[0.5rem] font-bold text-white',
        'transition-all duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-1',
        CHANNEL_COLOR[channel],
        active ? 'scale-110 opacity-100 ring-2 ring-ring ring-offset-1' : 'opacity-35 hover:opacity-65 hover:scale-105',
      )}
    >
      {CHANNEL_LABEL[channel]}
    </button>
  );
}

// ─── Left panel — conversation list ──────────────────────────────────────────

function ConversationListPanel({
  selectedId,
  onSelect,
}: {
  selectedId: string | null;
  onSelect: (conv: ConversationSummaryDto) => void;
}) {
  const t = useTranslations('chat');
  const [tab, setTab]                     = useState<TabId>('all');
  const [search, setSearch]               = useState('');
  const [channelFilter, setChannelFilter] = useState<Set<Channel>>(new Set());
  const sentinelRef                       = useRef<HTMLDivElement>(null);
  const searchRef                         = useRef<HTMLInputElement>(null);

  const toggleChannel = useCallback((ch: Channel) => {
    setChannelFilter((prev) => {
      const next = new Set(prev);
      if (next.has(ch)) next.delete(ch); else next.add(ch);
      return next;
    });
  }, []);

  // Server-side filters for the active tab; channel narrows further only when exactly one
  // channel is toggled (the backend filter is single-valued — multi-channel narrowing stays
  // client-side below, same as free-text search).
  const singleChannel = channelFilter.size === 1 ? [...channelFilter][0] : undefined;
  const baseFilters = useMemo(
    () => ({ ...filtersForTab(tab), channel: singleChannel, pageSize: LIST_PAGE_SIZE }),
    [tab, singleChannel],
  );

  const [cursor, setCursor] = useState<string | undefined>(undefined);
  const [accumulated, setAccumulated] = useState<ConversationSummaryDto[]>([]);

  // Reset pagination whenever the filter set changes (new tab/channel = fresh first page).
  const filterKey = JSON.stringify(baseFilters);
  const prevFilterKeyRef = useRef(filterKey);
  useEffect(() => {
    if (prevFilterKeyRef.current !== filterKey) {
      prevFilterKeyRef.current = filterKey;
      setCursor(undefined);
      setAccumulated([]);
    }
  }, [filterKey]);

  const pageQuery = useConversations(baseFilters, cursor);

  // Append each fetched page's items into the accumulator (keyed by page identity, not just
  // length, so refetches of the same page replace rather than duplicate).
  useEffect(() => {
    const page = pageQuery.data;
    if (!page) return;
    setAccumulated((prev) => (cursor === undefined ? page.items : [...prev, ...page.items]));
    // Only re-run when a *new* page's data arrives (cursor changes) or the first page reloads.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pageQuery.data, cursor]);

  const isLoading = pageQuery.isLoading && accumulated.length === 0;
  const isError = pageQuery.isError;
  const hasNextPage = Boolean(pageQuery.data?.nextCursor);
  const isFetchingNextPage = pageQuery.isFetching && cursor !== undefined;

  const fetchNextPage = useCallback(() => {
    const next = pageQuery.data?.nextCursor;
    if (next) setCursor(next ?? undefined);
  }, [pageQuery.data?.nextCursor]);

  const filtered = accumulated.filter((c) => {
    const q = search.toLowerCase();
    const matchSearch =
      !q ||
      (c.contactName?.toLowerCase().includes(q) ?? false) ||
      c.lastMessagePreview.toLowerCase().includes(q);
    const matchChannel = channelFilter.size <= 1 || channelFilter.has(c.channel);
    return matchSearch && matchChannel;
  });

  useEffect(() => {
    const el = sentinelRef.current;
    if (!el || !hasNextPage) return;
    const observer = new IntersectionObserver(
      (entries) => { if (entries[0]?.isIntersecting) fetchNextPage(); },
      { threshold: 0.1 },
    );
    observer.observe(el);
    return () => observer.disconnect();
  }, [hasNextPage, fetchNextPage]);

  const TABS: { id: TabId; labelKey: string }[] = [
    { id: 'all',      labelKey: 'inbox.tabs.all' },
    { id: 'ai',       labelKey: 'inbox.tabs.ai' },
    { id: 'pending',  labelKey: 'inbox.tabs.pending' },
    { id: 'mine',     labelKey: 'inbox.tabs.mine' },
    { id: 'resolved', labelKey: 'inbox.tabs.resolved' },
  ];

  return (
    // h-full + flex-col: header/tabs/search are shrink-0, list is flex-1 overflow-y-auto
    <div className="flex h-full flex-col bg-card">

      {/* ── Header ── */}
      <div className="flex shrink-0 items-center justify-between px-4 pt-4 pb-3">
        <h1 className="text-sm font-semibold tracking-tight text-foreground">{t('inbox.title')}</h1>
        <span className="rounded-full bg-primary/10 px-2 py-0.5 text-[0.625rem] font-semibold text-primary">
          {filtered.length}
        </span>
      </div>

      {/* ── Tabs ── */}
      <div className="shrink-0 px-2 pb-1">
        <Tabs value={tab} onValueChange={(v) => setTab(v as TabId)}>
          <TabsList className="h-auto w-full gap-0 bg-transparent p-0">
            {TABS.map(({ id, labelKey }) => (
              <TabsTrigger
                key={id}
                value={id}
                className={cn(
                  'relative h-8 flex-1 gap-1 rounded-none border-b-2 border-transparent px-1.5 text-[0.6875rem] font-medium transition-colors',
                  'data-[state=active]:border-primary data-[state=active]:bg-transparent data-[state=active]:text-primary',
                  'text-muted-foreground hover:text-foreground',
                )}
              >
                {t(labelKey as Parameters<typeof t>[0])}
              </TabsTrigger>
            ))}
          </TabsList>
        </Tabs>
      </div>

      <Separator />

      {/* ── Search ── */}
      <div className="shrink-0 px-3 py-2.5">
        <div className="relative">
          <Search className="absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground/60" aria-hidden />
          <input
            ref={searchRef}
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder={t('inbox.search')}
            aria-label={t('inbox.search')}
            className={cn(
              'h-8 w-full rounded-md border border-border bg-background/60 pl-8 pr-3 text-xs text-foreground placeholder:text-muted-foreground/50',
              'transition-colors focus:border-ring focus:outline-none focus:ring-1 focus:ring-ring',
            )}
          />
          {search && (
            <button
              type="button"
              aria-label="Clear search"
              onClick={() => { setSearch(''); searchRef.current?.focus(); }}
              className="absolute right-2 top-1/2 -translate-y-1/2 text-muted-foreground/60 hover:text-foreground"
            >
              <X className="h-3 w-3" />
            </button>
          )}
        </div>
      </div>

      {/* ── Channel filter ── */}
      <div
        role="group"
        aria-label={t('inbox.filterChannel')}
        className="flex shrink-0 items-center gap-2 px-3 pb-2.5"
      >
        {ALL_CHANNELS.map((ch) => (
          <ChannelPill key={ch} channel={ch} active={channelFilter.has(ch)} onToggle={toggleChannel} />
        ))}
        {channelFilter.size > 0 && (
          <button
            type="button"
            aria-label="Clear filters"
            onClick={() => setChannelFilter(new Set())}
            className="ml-auto flex items-center gap-1 rounded text-[0.625rem] text-muted-foreground transition-colors hover:text-foreground"
          >
            <X className="h-3 w-3" /> Clear
          </button>
        )}
      </div>

      <Separator />

      {/* ── Scrollable list ── flex-1 + overflow-y-auto keeps this section scrollable only */}
      <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain">
        {isLoading ? (
          <div className="flex items-center justify-center py-16">
            <div className="h-1 w-1 animate-bounce rounded-full bg-muted-foreground/40 [animation-delay:-0.3s]" />
            <div className="mx-1 h-1 w-1 animate-bounce rounded-full bg-muted-foreground/40 [animation-delay:-0.15s]" />
            <div className="h-1 w-1 animate-bounce rounded-full bg-muted-foreground/40" />
          </div>
        ) : isError ? (
          <div className="flex flex-col items-center gap-2 py-16 text-center">
            <p className="text-sm font-medium text-destructive">Couldn&apos;t load conversations</p>
            <p className="text-xs text-muted-foreground">Please try again shortly.</p>
          </div>
        ) : filtered.length === 0 ? (
          <div className="flex flex-col items-center gap-3 py-16 text-center">
            <div className="flex h-12 w-12 items-center justify-center rounded-full bg-muted">
              <Bot className="h-6 w-6 text-muted-foreground/40" aria-hidden />
            </div>
            <div>
              <p className="text-sm font-medium text-foreground">{t('inbox.empty')}</p>
              <p className="mt-0.5 text-xs text-muted-foreground">{t('inbox.emptyHint')}</p>
            </div>
          </div>
        ) : (
          <div className="flex flex-col gap-px py-1">
            {filtered.map((conv) => (
              <ConversationListItem
                key={conv.id}
                conv={toUiConversation(conv)}
                isSelected={selectedId === conv.id}
                onClick={() => onSelect(conv)}
              />
            ))}
            {(hasNextPage || isFetchingNextPage) && (
              <div ref={sentinelRef} className="flex items-center justify-center py-4">
                <div className="h-1 w-1 animate-bounce rounded-full bg-muted-foreground/40 [animation-delay:-0.3s]" />
                <div className="mx-1 h-1 w-1 animate-bounce rounded-full bg-muted-foreground/40 [animation-delay:-0.15s]" />
                <div className="h-1 w-1 animate-bounce rounded-full bg-muted-foreground/40" />
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

/** Adapts a `ConversationSummaryDto` to the `Conversation` shape the shared `shared/ui/chat/*`
 * components expect: an empty `messages` array (the list item component never reads it), and
 * `null` optional fields normalized to `undefined` (the DTOs mirror the backend, which uses
 * `null`; the mock-data-derived `Conversation`/`Message` types the shared components were built
 * against use `undefined` — the two are semantically identical here, so this is a type-only
 * adapter, not a behavior change). */
function toUiConversation(dto: ConversationSummaryDto): Conversation {
  return {
    ...dto,
    contactName: dto.contactName ?? undefined,
    assignedAgentName: dto.assignedAgentName ?? undefined,
    slaExpiresAt: dto.slaExpiresAt ?? undefined,
    messages: [],
  };
}

/** Same null->undefined normalization as `toUiConversation`, for a single message. */
function toUiMessage(dto: MessageDto): Message {
  return {
    ...dto,
    senderName: dto.senderName ?? undefined,
    deliveryStatus: dto.deliveryStatus ?? undefined,
    confidence: dto.confidence ?? undefined,
  };
}

// ─── Right info sidebar ───────────────────────────────────────────────────────

function ConversationSidebar({
  conv,
  messages,
}: {
  conv: ConversationSummaryDto;
  messages: MessageDto[];
}) {
  const t = useTranslations('chat');
  const systemMessages = messages.filter((m) => m.senderRole === 'System');

  return (
    <aside className="flex h-full w-60 shrink-0 flex-col border-l border-border bg-card">
      <div className="shrink-0 border-b border-border px-4 py-3">
        <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Details</p>
      </div>
      {/* scrollable content */}
      <div className="min-h-0 flex-1 overflow-y-auto">
        <div className="space-y-5 p-4">
          <section>
            <p className="mb-2 text-[0.625rem] font-semibold uppercase tracking-wide text-muted-foreground">
              {t('conversation.contactInfo')}
            </p>
            <div className="space-y-1.5">
              <div className="flex items-center gap-2">
                <ChannelBadge channel={conv.channel} size="sm" />
                <span className="truncate text-xs text-foreground">{conv.contactHandle}</span>
              </div>
              {conv.contactName && (
                <p className="text-xs text-muted-foreground">{conv.contactName}</p>
              )}
            </div>
          </section>

          <Separator />

          <section>
            <p className="mb-2 text-[0.625rem] font-semibold uppercase tracking-wide text-muted-foreground">
              {t('conversation.metadata')}
            </p>
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <span className="text-xs text-muted-foreground">Status</span>
                <StateChip state={conv.state} />
              </div>
              {conv.tags.length > 0 && (
                <div>
                  <span className="text-xs text-muted-foreground">Tags</span>
                  <div className="mt-1.5 flex flex-wrap gap-1">
                    {conv.tags.map((tag) => (
                      <span key={tag} className="rounded-full bg-secondary px-2 py-0.5 text-[0.6rem] font-medium text-secondary-foreground">
                        {tag}
                      </span>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </section>

          {systemMessages.length > 0 && (
            <>
              <Separator />
              <section>
                <p className="mb-2 text-[0.625rem] font-semibold uppercase tracking-wide text-muted-foreground">
                  {t('conversation.escalationLog')}
                </p>
                <div className="space-y-2">
                  {systemMessages.map((m) => (
                    <p key={m.id} className="border-l-2 border-primary/30 pl-2 text-[0.6875rem] text-muted-foreground">
                      {m.body}
                    </p>
                  ))}
                </div>
              </section>
            </>
          )}

          <Separator />

          <section>
            <p className="mb-2 text-[0.625rem] font-semibold uppercase tracking-wide text-muted-foreground">Actions</p>
            <div className="flex flex-col gap-0.5">
              {[
                { icon: Tag,        label: t('conversation.addTag') },
                { icon: StickyNote, label: t('conversation.addNote') },
                { icon: Download,   label: t('conversation.export') },
              ].map(({ icon: Icon, label }) => (
                <button
                  key={label}
                  type="button"
                  className="flex items-center gap-2 rounded-md px-2 py-1.5 text-xs text-muted-foreground transition-colors hover:bg-accent hover:text-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                >
                  <Icon className="h-3.5 w-3.5 shrink-0" aria-hidden />
                  {label}
                </button>
              ))}
            </div>
          </section>
        </div>
      </div>
    </aside>
  );
}

// ─── Message composer ─────────────────────────────────────────────────────────

function MessageComposer({
  onSend,
  onResolve,
  isSending,
}: {
  onSend: (text: string) => void;
  onResolve: () => void;
  isSending: boolean;
}) {
  const [draft, setDraft] = useState('');
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  function handleSend() {
    const text = draft.trim();
    if (!text) return;
    onSend(text);
    setDraft('');
    textareaRef.current?.focus();
  }

  function handleKey(e: React.KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); handleSend(); }
  }

  // Auto-grow textarea up to ~5 lines
  function handleChange(e: React.ChangeEvent<HTMLTextAreaElement>) {
    setDraft(e.target.value);
    const el = e.target;
    el.style.height = 'auto';
    el.style.height = `${Math.min(el.scrollHeight, 140)}px`;
  }

  return (
    <div className="shrink-0 border-t border-border bg-card">
      {/* Toolbar top */}
      <div className="flex items-center gap-0.5 border-b border-border/50 px-3 py-1.5">
        <button type="button" aria-label="Attach file" className="rounded p-1.5 text-muted-foreground/70 transition-colors hover:bg-accent hover:text-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring">
          <Paperclip className="h-4 w-4" />
        </button>
        <button type="button" aria-label="Emoji" className="rounded p-1.5 text-muted-foreground/70 transition-colors hover:bg-accent hover:text-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring">
          <Smile className="h-4 w-4" />
        </button>
      </div>

      {/* Textarea */}
      <Textarea
        ref={textareaRef}
        value={draft}
        onChange={handleChange}
        onKeyDown={handleKey}
        placeholder="Type a reply… (Enter to send · Shift+Enter for new line)"
        rows={2}
        className="min-h-0 resize-none rounded-none border-0 bg-transparent px-4 py-3 text-sm shadow-none placeholder:text-muted-foreground/40 focus-visible:ring-0"
        aria-label="Compose message"
        style={{ height: '72px' }}
      />

      {/* Action bar */}
      <div className="flex items-center justify-between px-3 py-2">
        <span className="text-[0.625rem] text-muted-foreground/50">
          {draft.length > 0 ? `${draft.length} chars` : 'Enter to send'}
        </span>
        <div className="flex items-center gap-2">
          <Button
            size="sm"
            variant="ghost"
            onClick={onResolve}
            className="h-7 gap-1.5 text-xs text-muted-foreground hover:text-foreground"
          >
            Resolve
          </Button>
          <Button
            size="sm"
            onClick={handleSend}
            disabled={!draft.trim() || isSending}
            className="h-7 gap-1.5 text-xs"
          >
            <Send className="h-3 w-3" />
            Send
          </Button>
        </div>
      </div>
    </div>
  );
}

/**
 * Reads the freshest cached `ConversationSummaryDto` for `conversationId` across every
 * `['inbox', 'conversations', ...]` list-page query in the cache, falling back to `initial` (the
 * summary the user actually clicked) when no cached page contains it yet. This is what keeps the
 * open conversation's header/StateBanner/SlaCountdown in sync with `useChatHub`'s cache patches
 * (assigned/resolved/reopened/handoff) without a dedicated get-one-conversation endpoint.
 */
function useLiveConversationSummary(
  conversationId: string,
  initial: ConversationSummaryDto,
): ConversationSummaryDto {
  const queryClient = useQueryClient();

  const subscribe = useCallback(
    (onStoreChange: () => void) => queryClient.getQueryCache().subscribe(onStoreChange),
    [queryClient],
  );

  const getSnapshot = useCallback((): ConversationSummaryDto => {
    const queries = queryClient.getQueriesData<{ items: ConversationSummaryDto[] }>({
      queryKey: ['inbox', 'conversations'],
      exact: false,
    });
    for (const [, data] of queries) {
      const match = data?.items.find((c) => c.id === conversationId);
      if (match) return match;
    }
    return initial;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [queryClient, conversationId]);

  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
}

// ─── Conversation view ────────────────────────────────────────────────────────

function ConversationView({
  conv: initialConv,
  onBack,
  onToggleInfo,
  infoOpen,
}: {
  conv: ConversationSummaryDto;
  onBack: () => void;
  onToggleInfo: () => void;
  infoOpen: boolean;
}) {
  const t = useTranslations('chat');
  const bottomRef = useRef<HTMLDivElement>(null);
  const currentAgentName = useSessionStore((s) => s.user?.fullName);
  const conv = useLiveConversationSummary(initialConv.id, initialConv);

  const messagesQuery = useConversationMessages(conv.id);
  const messages = useMemo(() => messagesQuery.data?.items ?? [], [messagesQuery.data]);

  const sendReply = useSendReply(conv.id);
  const takeOver = useTakeOver();
  const resolve = useResolve();
  const reopen = useReopen();

  const isMyConv = conv.assignedAgentName === currentAgentName;
  const showComposer = conv.state === 'HumanHandling' && isMyConv;

  // Scroll to bottom on new messages or conversation switch
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [conv.id, messages.length]);

  return (
    // Three-row grid: header (fixed) | messages (flex-1 scroll) | footer (fixed)
    <div className="flex min-h-0 min-w-0 flex-1 flex-col">

      {/* ── Header — never scrolls ── */}
      <header className="flex shrink-0 items-center gap-2 border-b border-border bg-card px-3 py-2.5">
        <Button
          variant="ghost"
          size="icon"
          className="h-8 w-8 shrink-0 md:hidden"
          onClick={onBack}
          aria-label={t('conversation.back')}
        >
          <ChevronLeft className="h-4 w-4" />
        </Button>

        {/* Avatar */}
        <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-primary/15 text-xs font-bold text-primary">
          {(conv.contactName ?? conv.contactHandle).slice(0, 2).toUpperCase()}
        </div>

        <div className="flex min-w-0 flex-1 flex-col">
          <span className="truncate text-sm font-semibold leading-tight text-foreground">
            {conv.contactName ?? conv.contactHandle}
          </span>
          <div className="flex items-center gap-1.5">
            <ChannelBadge channel={conv.channel} size="sm" />
            <StateChip state={conv.state} />
          </div>
        </div>

        <div className="flex shrink-0 items-center gap-1">
          <Button
            variant="ghost"
            size="icon"
            className="h-8 w-8"
            aria-label={infoOpen ? 'Hide details' : 'Show details'}
            onClick={onToggleInfo}
          >
            <Info className={cn('h-4 w-4 transition-colors', infoOpen && 'text-primary')} />
          </Button>
        </div>
      </header>

      {/* ── Body row: messages + optional sidebar ── */}
      <div className="flex min-h-0 flex-1 overflow-hidden">

        {/* Messages — only this div scrolls */}
        <div className="flex min-w-0 flex-1 flex-col overflow-hidden">
          <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain px-4 py-5">
            <div className="flex flex-col gap-3">
              {messages.map((msg) => (
                <MessageBubble
                  key={msg.id}
                  message={toUiMessage(msg)}
                  isCurrentAgent={msg.senderRole === 'Agent' && msg.senderName === currentAgentName}
                />
              ))}
              {/* Live incoming/outgoing messages arrive via useChatHub, which patches the
                  same React Query cache this list reads from — no separate stream handling here. */}
              <div ref={bottomRef} aria-hidden className="h-1" />
            </div>
          </div>

          {/* ── Footer — never scrolls ── */}
          {showComposer ? (
            <MessageComposer
              onSend={(text) => sendReply.mutate(text)}
              onResolve={() => resolve.mutate(conv.id)}
              isSending={sendReply.isPending}
            />
          ) : (
            <StateBanner
              state={conv.state}
              assignedAgentName={conv.assignedAgentName ?? undefined}
              slaExpiresAt={conv.slaExpiresAt ?? undefined}
              isCurrentAgentAssigned={isMyConv}
              onTakeOver={() => takeOver.mutate(conv.id)}
              onAccept={() => takeOver.mutate(conv.id)}
              onResolve={() => resolve.mutate(conv.id)}
              onReopen={() => reopen.mutate(conv.id)}
            />
          )}
        </div>

        {/* Info sidebar (desktop only) */}
        {infoOpen && (
          <div className="hidden lg:flex">
            <ConversationSidebar conv={conv} messages={messages} />
          </div>
        )}
      </div>
    </div>
  );
}

// ─── Empty state ──────────────────────────────────────────────────────────────

function EmptyPrompt() {
  const t = useTranslations('chat');
  return (
    <div className="flex flex-1 flex-col items-center justify-center gap-4 text-center">
      <div className="flex h-16 w-16 items-center justify-center rounded-full bg-primary/8">
        <Bot className="h-8 w-8 text-primary/50" aria-hidden />
      </div>
      <div>
        <p className="text-sm font-medium text-foreground">{t('inbox.selectPrompt')}</p>
        <p className="mt-1 text-xs text-muted-foreground">{t('inbox.noConversation')}</p>
      </div>
    </div>
  );
}

// ─── Page root ────────────────────────────────────────────────────────────────

export default function InboxPage() {
  const t = useTranslations('chat');

  // The selected conversation is held as the summary the user clicked (from whichever tab/filter
  // it was visible in), not re-looked-up from a separate unfiltered query — the list panel's own
  // query is cursor-paged and filtered per-tab, so there is no single "all conversations" query
  // that is guaranteed to contain every selectable row.
  const [selectedConv, setSelectedConv] = useState<ConversationSummaryDto | null>(null);
  const [drawerOpen, setDrawerOpen]     = useState(false);
  const [infoOpen, setInfoOpen]         = useState(false);

  // One SignalR connection for the whole inbox: joins the tenant agent-dashboard group on mount,
  // and the selected conversation's group whenever the selection changes (see use-chat-hub.ts).
  useChatHub(selectedConv?.id ?? null);

  const handleSelect = useCallback((conv: ConversationSummaryDto) => {
    setSelectedConv(conv);
    setDrawerOpen(false);
    setInfoOpen(false);
  }, []);

  return (
    // Fill the <main> which is overflow-hidden — no outer page scroll
    <div className="flex h-full overflow-hidden bg-muted/30">

      {/* ── Desktop list panel ── */}
      <div className="hidden w-72 shrink-0 border-r border-border md:flex md:flex-col xl:w-80">
        <ConversationListPanel selectedId={selectedConv?.id ?? null} onSelect={handleSelect} />
      </div>

      {/* ── Mobile: FAB → drawer ── */}
      <button
        type="button"
        aria-label="Open conversations"
        onClick={() => setDrawerOpen(true)}
        className={cn(
          'fixed bottom-5 right-5 z-40 flex h-12 w-12 items-center justify-center rounded-full bg-primary text-primary-foreground shadow-lg transition-transform active:scale-95 md:hidden',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
        )}
      >
        <Menu className="h-5 w-5" />
      </button>

      <Drawer open={drawerOpen} onOpenChange={setDrawerOpen} direction="left" shouldScaleBackground={false}>
        <DrawerContent className="fixed inset-y-0 left-0 z-50 m-0 h-full w-[82vw] max-w-xs rounded-none border-r border-border bg-card">
          <DrawerTitle className="sr-only">{t('inbox.title')}</DrawerTitle>
          <div className="flex h-full flex-col">
            <div className="flex shrink-0 items-center justify-between border-b border-border px-4 py-2.5">
              <span className="text-sm font-semibold">{t('inbox.title')}</span>
              <button
                type="button"
                aria-label="Close"
                onClick={() => setDrawerOpen(false)}
                className="rounded-md p-1 text-muted-foreground hover:text-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
            <div className="min-h-0 flex-1">
              <ConversationListPanel selectedId={selectedConv?.id ?? null} onSelect={handleSelect} />
            </div>
          </div>
        </DrawerContent>
      </Drawer>

      {/* ── Right area: conversation or empty ── */}
      <div
        className={cn(
          'flex min-w-0 flex-1 flex-col overflow-hidden',
          selectedConv ? 'flex' : 'hidden md:flex',
        )}
      >
        {selectedConv ? (
          <ConversationView
            key={selectedConv.id}
            conv={selectedConv}
            onBack={() => setSelectedConv(null)}
            onToggleInfo={() => setInfoOpen((o) => !o)}
            infoOpen={infoOpen}
          />
        ) : (
          <EmptyPrompt />
        )}
      </div>

      {/* Mobile: full-screen list when nothing selected */}
      <div
        className={cn(
          'flex min-w-0 flex-1 flex-col overflow-hidden md:hidden',
          selectedConv ? 'hidden' : 'flex',
        )}
      >
        <ConversationListPanel selectedId={selectedConv?.id ?? null} onSelect={handleSelect} />
      </div>
    </div>
  );
}
