'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { ArrowLeft, Pencil, Check } from 'lucide-react';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/shared/ui/table';
import { Button } from '@/shared/ui/button';
import { Badge } from '@/shared/ui/badge';
import { Input } from '@/shared/ui/input';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/shared/ui/tabs';
import { cn } from '@/shared/lib/cn';

// ─── Types ────────────────────────────────────────────────────────────────────

type DocStatus = 'Ready' | 'Processing' | 'Failed';

interface Chunk {
  index: number;
  preview: string;
  tokens: number;
  score: number;
}

interface DocVersion {
  version: number;
  createdAt: string;
  model: string;
}

// ─── Mock Data ────────────────────────────────────────────────────────────────

const MOCK_DOC_DETAIL = {
  id: '1',
  title: 'Product Catalog 2026',
  status: 'Processing' as DocStatus,
  currentStep: 2,
  versions: [
    { version: 3, createdAt: '2026-06-30T09:00:00.000Z', model: 'text-embedding-3-large' },
    { version: 2, createdAt: '2026-06-28T10:00:00.000Z', model: 'text-embedding-3-large' },
    { version: 1, createdAt: '2026-06-25T08:00:00.000Z', model: 'text-embedding-ada-002' },
  ] as DocVersion[],
};

const MOCK_CHUNKS: Chunk[] = Array.from({ length: 12 }, (_, i) => ({
  index: i + 1,
  preview: [
    'Our return policy allows customers to return unused items within 30 days of purchase…',
    'The Jamdani saree collection features hand-woven patterns from traditional Bangladeshi…',
    'Delivery times vary by region: Dhaka 1-2 days, Chittagong 2-3 days, other areas 3-5…',
    'bKash payment is accepted for all orders above ৳500. For larger amounts, bank transfer…',
    'Warranty coverage includes manufacturing defects for 12 months from purchase date…',
    'আমাদের পণ্যগুলো সর্বোচ্চ মানের কাঁচামাল দিয়ে তৈরি। গুণমান নিশ্চিত করতে…',
  ][i % 6] + ` (chunk ${i + 1})`,
  tokens: 180 + (i * 23) % 120,
  score: parseFloat((0.72 + (i * 0.03) % 0.25).toFixed(2)),
}));

// ─── Helpers ──────────────────────────────────────────────────────────────────

function formatDate(iso: string): string {
  return new Intl.DateTimeFormat('en-GB', {
    day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit',
  }).format(new Date(iso));
}

function StatusBadge({ status }: { status: DocStatus }) {
  const cls = cn(
    'font-medium',
    status === 'Ready'      && 'text-chatConfidence-high border-chatConfidence-high/40',
    status === 'Processing' && 'text-chatConfidence-medium border-chatConfidence-medium/40',
    status === 'Failed'     && 'text-destructive border-destructive/40',
  );
  return <Badge variant="outline" className={cls}>{status}</Badge>;
}

function ScoreBadge({ score }: { score: number }) {
  const cls = cn(
    'font-mono text-xs font-medium',
    score >= 0.8 && 'text-chatConfidence-high border-chatConfidence-high/40',
    score >= 0.6 && score < 0.8 && 'text-chatConfidence-medium border-chatConfidence-medium/40',
    score < 0.6  && 'text-destructive border-destructive/40',
  );
  return <Badge variant="outline" className={cls}>{score.toFixed(2)}</Badge>;
}

// ─── Processing Steps ─────────────────────────────────────────────────────────

const STEPS = ['upload', 'extract', 'chunk', 'embed', 'index'] as const;
type StepKey = typeof STEPS[number];

function ProcessingSteps({ currentStep }: { currentStep: number }) {
  const t = useTranslations('chat');
  return (
    <div className="rounded-lg border border-border bg-card p-4">
      <p className="mb-4 text-xs font-medium uppercase tracking-wide text-muted-foreground">Processing</p>
      <div className="flex items-center">
        {STEPS.map((step: StepKey, idx) => {
          const isDone    = idx < currentStep;
          const isCurrent = idx === currentStep;
          const isPending = idx > currentStep;
          return (
            <div key={step} className="flex flex-1 items-center last:flex-none">
              <div className="flex flex-col items-center gap-1.5">
                <div
                  className={cn(
                    'flex h-8 w-8 items-center justify-center rounded-full border-2 transition-colors',
                    isDone    && 'border-primary bg-primary text-primary-foreground',
                    isCurrent && 'border-primary bg-primary/10 text-primary',
                    isPending && 'border-muted-foreground/30 bg-background text-muted-foreground/50',
                  )}
                  aria-label={`Step ${idx + 1}: ${t(`knowledge.detail.processingSteps.${step}`)}`}
                >
                  {isDone ? (
                    <Check className="h-3.5 w-3.5" />
                  ) : (
                    <span className={cn('h-2 w-2 rounded-full', isCurrent && 'animate-pulse bg-primary', isPending && 'bg-muted-foreground/30')} />
                  )}
                </div>
                <span className={cn('hidden text-center text-[10px] font-medium sm:block', isDone && 'text-primary', isCurrent && 'text-foreground', isPending && 'text-muted-foreground/50')}>
                  {t(`knowledge.detail.processingSteps.${step}`)}
                </span>
              </div>
              {idx < STEPS.length - 1 && (
                <div className={cn('h-0.5 flex-1 mx-1 rounded-full transition-colors', isDone ? 'bg-primary' : 'bg-muted-foreground/20')} />
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}

// ─── Chunks Tab ───────────────────────────────────────────────────────────────

function ChunksTab({ status }: { status: DocStatus }) {
  const t = useTranslations('chat');
  const [expanded, setExpanded] = useState<Record<number, boolean>>({});
  if (status !== 'Ready') {
    return <div className="py-12 text-center text-sm text-muted-foreground">Chunks will appear here once processing completes.</div>;
  }
  return (
    <div className="overflow-hidden rounded-lg border border-border">
      <Table>
        <TableHeader>
          <TableRow className="bg-muted/50">
            <TableHead className="w-12">{t('knowledge.detail.chunksTable.index')}</TableHead>
            <TableHead>{t('knowledge.detail.chunksTable.preview')}</TableHead>
            <TableHead className="w-20 text-right">{t('knowledge.detail.chunksTable.tokens')}</TableHead>
            <TableHead className="w-20 text-right">{t('knowledge.detail.chunksTable.score')}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {MOCK_CHUNKS.map(chunk => {
            const isExpanded = expanded[chunk.index] ?? false;
            const preview = isExpanded ? chunk.preview : chunk.preview.slice(0, 40) + (chunk.preview.length > 40 ? '…' : '');
            return (
              <TableRow key={chunk.index}>
                <TableCell className="tabular-nums text-sm text-muted-foreground">{chunk.index}</TableCell>
                <TableCell>
                  <div className="space-y-1">
                    <p className="text-sm leading-relaxed text-foreground">{preview}</p>
                    {chunk.preview.length > 40 && (
                      <button type="button" className="text-xs text-primary hover:underline focus-visible:outline-none focus-visible:underline"
                        onClick={() => setExpanded(prev => ({ ...prev, [chunk.index]: !isExpanded }))}>
                        {isExpanded ? 'Collapse' : 'Expand'}
                      </button>
                    )}
                  </div>
                </TableCell>
                <TableCell className="text-right tabular-nums text-sm text-muted-foreground">{chunk.tokens}</TableCell>
                <TableCell className="text-right"><ScoreBadge score={chunk.score} /></TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </div>
  );
}

// ─── Version History Tab ──────────────────────────────────────────────────────

function VersionHistoryTab() {
  const t = useTranslations('chat');
  const [restored, setRestored] = useState<Record<number, boolean>>({});
  function handleRestore(version: number) {
    setRestored(prev => ({ ...prev, [version]: true }));
    setTimeout(() => setRestored(prev => ({ ...prev, [version]: false })), 2000);
  }
  return (
    <div className="overflow-hidden rounded-lg border border-border">
      <Table>
        <TableHeader>
          <TableRow className="bg-muted/50">
            <TableHead className="w-24">{t('knowledge.detail.historyTable.version')}</TableHead>
            <TableHead>{t('knowledge.detail.historyTable.createdAt')}</TableHead>
            <TableHead>{t('knowledge.detail.historyTable.model')}</TableHead>
            <TableHead className="text-right">{t('knowledge.detail.historyTable.actions')}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {MOCK_DOC_DETAIL.versions.map(v => (
            <TableRow key={v.version}>
              <TableCell className="text-sm font-medium">v{v.version}</TableCell>
              <TableCell className="text-sm text-muted-foreground">{formatDate(v.createdAt)}</TableCell>
              <TableCell><span className="font-mono text-xs text-muted-foreground">{v.model}</span></TableCell>
              <TableCell className="text-right">
                <div className="flex items-center justify-end gap-2">
                  {restored[v.version] && <span className="text-xs text-chatConfidence-high">Restored to v{v.version}</span>}
                  <Button variant="outline" size="sm" className="h-7 text-xs" onClick={() => handleRestore(v.version)} disabled={restored[v.version]}>
                    {t('knowledge.actions.restore')}
                  </Button>
                </div>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}

// ─── Inline Editable Title ────────────────────────────────────────────────────

function EditableTitle({ initial }: { initial: string }) {
  const [editing, setEditing] = useState(false);
  const [title, setTitle]     = useState(initial);
  const [draft, setDraft]     = useState(initial);

  function commit() {
    const trimmed = draft.trim();
    if (trimmed) setTitle(trimmed);
    setEditing(false);
  }

  if (editing) {
    return (
      <Input autoFocus value={draft} onChange={e => setDraft(e.target.value)}
        onBlur={commit}
        onKeyDown={e => { if (e.key === 'Enter') commit(); if (e.key === 'Escape') setEditing(false); }}
        className="h-auto rounded-none border-0 border-b border-border bg-transparent px-0 py-0.5 text-2xl font-bold tracking-tight focus-visible:ring-0"
        aria-label="Edit document title" />
    );
  }

  return (
    <button type="button" className="group flex items-center gap-2 text-left focus-visible:outline-none" onClick={() => { setDraft(title); setEditing(true); }} aria-label="Click to edit title">
      <h1 className="text-2xl font-bold tracking-tight text-foreground">{title}</h1>
      <Pencil className="h-4 w-4 shrink-0 text-muted-foreground opacity-0 transition-opacity group-hover:opacity-100 group-focus-visible:opacity-100" />
    </button>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export default function KnowledgeDetailPage() {
  const t      = useTranslations('chat');
  const router = useRouter();
  const doc    = MOCK_DOC_DETAIL;

  return (
    <div className="flex flex-col gap-6 p-4 md:p-6">
      <button type="button" onClick={() => router.push('/dashboard/chat/settings/knowledge')}
        className="flex w-fit items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:underline"
        aria-label="Back to Knowledge Base">
        <ArrowLeft className="h-4 w-4" />
        {t('knowledge.title')}
      </button>

      <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:gap-4">
        <EditableTitle initial={doc.title} />
        <StatusBadge status={doc.status} />
      </div>

      {doc.status === 'Processing' && <ProcessingSteps currentStep={doc.currentStep} />}

      <Tabs defaultValue="chunks">
        <TabsList>
          <TabsTrigger value="chunks">{t('knowledge.detail.tabs.chunks')}</TabsTrigger>
          <TabsTrigger value="history">{t('knowledge.detail.tabs.history')}</TabsTrigger>
        </TabsList>
        <TabsContent value="chunks" className="mt-4"><ChunksTab status={doc.status} /></TabsContent>
        <TabsContent value="history" className="mt-4"><VersionHistoryTab /></TabsContent>
      </Tabs>
    </div>
  );
}
