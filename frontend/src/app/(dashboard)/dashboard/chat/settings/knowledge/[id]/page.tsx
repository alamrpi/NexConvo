'use client';

import { use, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { ArrowLeft, Pencil, Check, RefreshCw, Trash2 } from 'lucide-react';
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
import { Skeleton } from '@/shared/ui/skeleton';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/shared/ui/tabs';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from '@/shared/ui/dialog';
import { cn } from '@/shared/lib/cn';
import { useKnowledgeDocument } from '@/features/settings/api/use-knowledge-document';
import { useReEmbedKnowledgeDocument } from '@/features/settings/api/use-re-embed-knowledge-document';
import { useDeleteKnowledgeDocument } from '@/features/settings/api/use-delete-knowledge-document';
import type { IngestionStatus } from '@/features/settings/model/knowledge-document.types';

// ─── Helpers ──────────────────────────────────────────────────────────────────

function formatDate(iso: string): string {
  return new Intl.DateTimeFormat('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(iso));
}

function formatDateShort(iso: string): string {
  return new Intl.DateTimeFormat('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  }).format(new Date(iso));
}

// ─── Status Badge ─────────────────────────────────────────────────────────────

function StatusBadge({ status }: { status: IngestionStatus }) {
  const cls = cn(
    'font-medium gap-1',
    status === 'active'     && 'text-chatConfidence-high border-chatConfidence-high/40',
    status === 'pending'    && 'text-amber-600 border-amber-600/40 dark:text-amber-400 dark:border-amber-400/40',
    status === 'processing' && 'text-blue-600 border-blue-600/40 dark:text-blue-400 dark:border-blue-400/40',
    status === 'failed'     && 'text-destructive border-destructive/40',
    status === 'inactive'   && 'text-muted-foreground border-border',
  );
  return (
    <Badge variant="outline" className={cls}>
      {(status === 'pending' || status === 'processing') && (
        <span
          className={cn(
            'h-1.5 w-1.5 rounded-full animate-pulse',
            status === 'pending'    && 'bg-amber-600 dark:bg-amber-400',
            status === 'processing' && 'bg-blue-600 dark:bg-blue-400',
          )}
        />
      )}
      {status === 'active'     && 'Active'}
      {status === 'pending'    && 'Pending'}
      {status === 'processing' && 'Processing'}
      {status === 'failed'     && 'Failed'}
      {status === 'inactive'   && 'Inactive'}
    </Badge>
  );
}

// ─── Processing Steps ─────────────────────────────────────────────────────────

const STEPS = ['upload', 'extract', 'chunk', 'embed', 'index'] as const;
type StepKey = (typeof STEPS)[number];

function statusToStep(status: IngestionStatus): number {
  if (status === 'pending')    return 0;
  if (status === 'processing') return 2;
  if (status === 'active')     return 4;
  return -1;
}

function ProcessingSteps({ status }: { status: IngestionStatus }) {
  const t           = useTranslations('chat');
  const currentStep = statusToStep(status);
  if (currentStep < 0) return null;

  return (
    <div className="rounded-lg border border-border bg-card p-4">
      <p className="mb-4 text-xs font-medium uppercase tracking-wide text-muted-foreground">
        Processing
      </p>
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
                    <span
                      className={cn(
                        'h-2 w-2 rounded-full',
                        isCurrent && 'animate-pulse bg-primary',
                        isPending && 'bg-muted-foreground/30',
                      )}
                    />
                  )}
                </div>
                <span
                  className={cn(
                    'hidden text-center text-[10px] font-medium sm:block',
                    isDone    && 'text-primary',
                    isCurrent && 'text-foreground',
                    isPending && 'text-muted-foreground/50',
                  )}
                >
                  {t(`knowledge.detail.processingSteps.${step}`)}
                </span>
              </div>
              {idx < STEPS.length - 1 && (
                <div
                  className={cn(
                    'mx-1 h-0.5 flex-1 rounded-full transition-colors',
                    isDone ? 'bg-primary' : 'bg-muted-foreground/20',
                  )}
                />
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}

// ─── Chunks Tab ───────────────────────────────────────────────────────────────

function ChunksTab({
  chunks,
  status,
}: {
  chunks: Array<{ id: string; ordinal: number; contentPreview: string; tokenCount: number }>;
  status: IngestionStatus;
}) {
  const t        = useTranslations('chat');
  const [expanded, setExpanded] = useState<Record<string, boolean>>({});

  if (status !== 'active') {
    return (
      <div className="py-12 text-center text-sm text-muted-foreground">
        Chunks will appear here once processing completes.
      </div>
    );
  }

  return (
    <div className="overflow-hidden rounded-lg border border-border">
      <Table>
        <TableHeader>
          <TableRow className="bg-muted/50">
            <TableHead className="w-12">{t('knowledge.detail.chunksTable.index')}</TableHead>
            <TableHead>{t('knowledge.detail.chunksTable.preview')}</TableHead>
            <TableHead className="w-24 text-right">
              {t('knowledge.detail.chunksTable.tokens')}
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {chunks.map((chunk) => {
            const isExpanded = expanded[chunk.id] ?? false;
            const preview = isExpanded
              ? chunk.contentPreview
              : chunk.contentPreview.slice(0, 40) +
                (chunk.contentPreview.length > 40 ? '…' : '');
            return (
              <TableRow key={chunk.id}>
                <TableCell className="tabular-nums text-sm text-muted-foreground">
                  {chunk.ordinal}
                </TableCell>
                <TableCell>
                  <div className="space-y-1">
                    <p className="text-sm leading-relaxed text-foreground">{preview}</p>
                    {chunk.contentPreview.length > 40 && (
                      <button
                        type="button"
                        className="text-xs text-primary hover:underline focus-visible:outline-none focus-visible:underline"
                        onClick={() =>
                          setExpanded((prev) => ({ ...prev, [chunk.id]: !isExpanded }))
                        }
                      >
                        {isExpanded ? 'Collapse' : 'Expand'}
                      </button>
                    )}
                  </div>
                </TableCell>
                <TableCell className="text-right tabular-nums text-sm text-muted-foreground">
                  {chunk.tokenCount}
                </TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </div>
  );
}

// ─── Version History Tab ──────────────────────────────────────────────────────

function VersionHistoryTab({
  versions,
  docId,
}: {
  versions: Array<{ version: number; embeddingModel: string; chunkCount: number; createdAt: string }>;
  docId: string;
}) {
  const t = useTranslations('chat');
  const { mutate: reEmbed, isPending, variables: pendingId } = useReEmbedKnowledgeDocument();

  return (
    <div className="overflow-hidden rounded-lg border border-border">
      <Table>
        <TableHeader>
          <TableRow className="bg-muted/50">
            <TableHead className="w-24">{t('knowledge.detail.historyTable.version')}</TableHead>
            <TableHead>{t('knowledge.detail.historyTable.createdAt')}</TableHead>
            <TableHead>{t('knowledge.detail.historyTable.model')}</TableHead>
            <TableHead className="w-20 text-right">Chunks</TableHead>
            <TableHead className="text-right">{t('knowledge.detail.historyTable.actions')}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {versions.map((v) => (
            <TableRow key={v.version}>
              <TableCell className="text-sm font-medium">v{v.version}</TableCell>
              <TableCell className="text-sm text-muted-foreground">
                {formatDate(v.createdAt)}
              </TableCell>
              <TableCell>
                <span className="font-mono text-xs text-muted-foreground">
                  {v.embeddingModel}
                </span>
              </TableCell>
              <TableCell className="text-right tabular-nums text-sm text-muted-foreground">
                {v.chunkCount}
              </TableCell>
              <TableCell className="text-right">
                <Button
                  variant="outline"
                  size="sm"
                  className="h-7 text-xs"
                  disabled={isPending && pendingId === docId}
                  onClick={() => reEmbed(docId)}
                >
                  {isPending && pendingId === docId ? 'Restoring…' : t('knowledge.actions.restore')}
                </Button>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}

// ─── Inline Editable Title ────────────────────────────────────────────────────

interface EditableTitleProps {
  initial: string;
  onSave: (title: string) => void;
  isSaving: boolean;
}

function EditableTitle({ initial, onSave, isSaving }: EditableTitleProps) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft]     = useState(initial);

  function commit() {
    const trimmed = draft.trim();
    if (trimmed && trimmed !== initial) onSave(trimmed);
    setEditing(false);
  }

  if (editing) {
    return (
      <Input
        autoFocus
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        onBlur={commit}
        onKeyDown={(e) => {
          if (e.key === 'Enter') commit();
          if (e.key === 'Escape') setEditing(false);
        }}
        className="h-auto rounded-none border-0 border-b border-border bg-transparent px-0 py-0.5 text-2xl font-bold tracking-tight focus-visible:ring-0"
        aria-label="Edit document title"
      />
    );
  }

  return (
    <button
      type="button"
      className="group flex items-center gap-2 text-left focus-visible:outline-none"
      onClick={() => { setDraft(initial); setEditing(true); }}
      aria-label="Click to edit title"
      disabled={isSaving}
    >
      <h1 className="text-2xl font-bold tracking-tight text-foreground">
        {isSaving ? <span className="opacity-60">{initial}</span> : initial}
      </h1>
      {!isSaving && (
        <Pencil className="h-4 w-4 shrink-0 text-muted-foreground opacity-0 transition-opacity group-hover:opacity-100 group-focus-visible:opacity-100" />
      )}
      {isSaving && <RefreshCw className="h-4 w-4 animate-spin text-muted-foreground" />}
    </button>
  );
}

// ─── Detail Skeleton ──────────────────────────────────────────────────────────

function DetailSkeleton() {
  return (
    <div className="flex flex-col gap-6 p-4 md:p-6">
      <Skeleton className="h-4 w-32" />
      <div className="flex items-center gap-4">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-5 w-20 rounded-full" />
      </div>
      <div className="rounded-lg border border-border bg-card p-4">
        <Skeleton className="h-3 w-24 mb-4" />
        <Skeleton className="h-8 w-full" />
      </div>
      <div className="space-y-3">
        <Skeleton className="h-4 w-32" />
        {Array.from({ length: 5 }).map((_, i) => (
          <Skeleton key={i} className="h-10 w-full" />
        ))}
      </div>
    </div>
  );
}

// ─── Delete Confirm Dialog ────────────────────────────────────────────────────

interface DeleteConfirmDialogProps {
  open: boolean;
  title: string;
  onClose: () => void;
  onConfirm: () => void;
  isPending: boolean;
}

function DeleteConfirmDialog({
  open,
  title,
  onClose,
  onConfirm,
  isPending,
}: DeleteConfirmDialogProps) {
  const t = useTranslations('chat');
  return (
    <Dialog open={open} onOpenChange={(v) => { if (!v) onClose(); }}>
      <DialogContent className="max-w-sm">
        <DialogHeader>
          <DialogTitle>{t('knowledge.deleteConfirm')}</DialogTitle>
          <DialogDescription>{t('knowledge.deleteDescription')}</DialogDescription>
        </DialogHeader>
        <p className="truncate text-sm font-medium text-foreground">{title}</p>
        <DialogFooter className="gap-2">
          <Button variant="outline" onClick={onClose} disabled={isPending}>
            {t('knowledge.cancel')}
          </Button>
          <Button variant="destructive" onClick={onConfirm} disabled={isPending}>
            {isPending ? 'Deleting…' : t('knowledge.confirm')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export default function KnowledgeDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id }   = use(params);
  const router   = useRouter();
  const tl       = useTranslations('chat');

  const { data: doc, isLoading, isError } = useKnowledgeDocument(id);
  const { mutate: reEmbed, isPending: reEmbedPending } = useReEmbedKnowledgeDocument();
  const { mutate: deleteDoc, isPending: deletePending } = useDeleteKnowledgeDocument();

  const [deleteOpen, setDeleteOpen]     = useState(false);
  // Title save is a stub — in a real system this would call a PATCH mutation.
  // For now we track the locally-saved title; the server reflects it on the next refetch.
  const [localTitle, setLocalTitle]     = useState<string | null>(null);
  const [titleSaving, setTitleSaving]   = useState(false);

  function handleTitleSave(newTitle: string) {
    setTitleSaving(true);
    // Optimistic local update — replace with a real PATCH mutation when backend exposes it.
    setTimeout(() => {
      setLocalTitle(newTitle);
      setTitleSaving(false);
    }, 600);
  }

  function handleDelete() {
    deleteDoc(id, {
      onSuccess: () => router.push('/dashboard/chat/settings/knowledge'),
      onSettled: () => setDeleteOpen(false),
    });
  }

  if (isLoading) return <DetailSkeleton />;

  if (isError || !doc) {
    return (
      <div className="flex flex-col gap-4 p-4 md:p-6">
        <button
          type="button"
          onClick={() => router.push('/dashboard/chat/settings/knowledge')}
          className="flex w-fit items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:underline"
        >
          <ArrowLeft className="h-4 w-4" />
          {tl('knowledge.title')}
        </button>
        <p role="alert" className="text-sm text-destructive">
          Could not load this document. It may have been deleted.
        </p>
      </div>
    );
  }

  const displayTitle = localTitle ?? doc.title;

  return (
    <div className="flex flex-col gap-6 p-4 md:p-6">
      {/* ── Back link ── */}
      <button
        type="button"
        onClick={() => router.push('/dashboard/chat/settings/knowledge')}
        className="flex w-fit items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:underline"
        aria-label="Back to Knowledge Base"
      >
        <ArrowLeft className="h-4 w-4" />
        {tl('knowledge.title')}
      </button>

      {/* ── Title + actions ── */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div className="flex flex-wrap items-center gap-3">
          <EditableTitle
            initial={displayTitle}
            onSave={handleTitleSave}
            isSaving={titleSaving}
          />
          <StatusBadge status={doc.status} />
        </div>

        <div className="flex shrink-0 items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            disabled={reEmbedPending}
            onClick={() => reEmbed(id)}
          >
            <RefreshCw className={cn('h-4 w-4', reEmbedPending && 'animate-spin')} />
            {reEmbedPending ? tl('knowledge.reEmbedding') : tl('knowledge.actions.reEmbed')}
          </Button>
          <Button
            variant="destructive"
            size="sm"
            onClick={() => setDeleteOpen(true)}
          >
            <Trash2 className="h-4 w-4" />
            {tl('knowledge.actions.delete')}
          </Button>
        </div>
      </div>

      {/* ── Metadata bar ── */}
      <dl className="flex flex-wrap gap-x-6 gap-y-1 text-sm text-muted-foreground">
        <div className="flex gap-1">
          <dt className="font-medium text-foreground">Source:</dt>
          <dd className="capitalize">{doc.sourceType.replace('_', ' ')}</dd>
        </div>
        <div className="flex gap-1">
          <dt className="font-medium text-foreground">Version:</dt>
          <dd>v{doc.version}</dd>
        </div>
        <div className="flex gap-1">
          <dt className="font-medium text-foreground">Chunks:</dt>
          <dd>{doc.chunkCount.toLocaleString()}</dd>
        </div>
        <div className="flex gap-1">
          <dt className="font-medium text-foreground">Created:</dt>
          <dd>{formatDateShort(doc.createdAt)}</dd>
        </div>
        <div className="flex gap-1">
          <dt className="font-medium text-foreground">Model:</dt>
          <dd className="font-mono text-xs">{doc.embeddingModel}</dd>
        </div>
      </dl>

      {/* ── Processing steps (only when in-flight) ── */}
      {(doc.status === 'pending' || doc.status === 'processing') && (
        <ProcessingSteps status={doc.status} />
      )}

      {/* ── Failure reason ── */}
      {doc.status === 'failed' && doc.failureReason && (
        <div className="rounded-lg border border-destructive/40 bg-destructive/5 p-3">
          <p className="text-sm font-medium text-destructive">Ingestion failed</p>
          <p className="mt-1 text-sm text-destructive/80">{doc.failureReason}</p>
        </div>
      )}

      {/* ── Chunks + Version History tabs ── */}
      <Tabs defaultValue="chunks">
        <TabsList>
          <TabsTrigger value="chunks">
            {tl('knowledge.detail.tabs.chunks')}
          </TabsTrigger>
          <TabsTrigger value="history">
            {tl('knowledge.detail.tabs.history')}
          </TabsTrigger>
        </TabsList>
        <TabsContent value="chunks" className="mt-4">
          <ChunksTab chunks={doc.chunks} status={doc.status} />
        </TabsContent>
        <TabsContent value="history" className="mt-4">
          <VersionHistoryTab versions={doc.versionHistory} docId={id} />
        </TabsContent>
      </Tabs>

      {/* ── Delete confirmation ── */}
      <DeleteConfirmDialog
        open={deleteOpen}
        title={displayTitle}
        onClose={() => setDeleteOpen(false)}
        onConfirm={handleDelete}
        isPending={deletePending}
      />
    </div>
  );
}
