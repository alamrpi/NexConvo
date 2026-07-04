'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import {
  Plus,
  Eye,
  RefreshCw,
  Trash2,
  FileText,
  Link,
  MessageSquare,
  File,
  BookOpen,
  Check,
} from 'lucide-react';
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
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from '@/shared/ui/dialog';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/shared/ui/tabs';
import { Input } from '@/shared/ui/input';
import { Textarea } from '@/shared/ui/textarea';
import { Label } from '@/shared/ui/label';
import { Skeleton } from '@/shared/ui/skeleton';
import { Checkbox } from '@/shared/ui/checkbox';
import { cn } from '@/shared/lib/cn';
import { useKnowledgeDocuments } from '@/features/settings/api/use-knowledge-documents';
import { useUploadKnowledgeDocument } from '@/features/settings/api/use-upload-knowledge-document';
import { useDeleteKnowledgeDocument } from '@/features/settings/api/use-delete-knowledge-document';
import { useReEmbedKnowledgeDocument } from '@/features/settings/api/use-re-embed-knowledge-document';
import type {
  KnowledgeDocumentDto,
  SourceType,
  IngestionStatus,
} from '@/features/settings/model/knowledge-document.types';

// ─── Helpers ──────────────────────────────────────────────────────────────────

function formatDate(iso: string): string {
  return new Intl.DateTimeFormat('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  }).format(new Date(iso));
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function sourceIcon(type: SourceType): React.ReactNode {
  const cls = 'h-3.5 w-3.5 shrink-0';
  switch (type) {
    case 'file':       return <File className={cls} />;
    case 'url':        return <Link className={cls} />;
    case 'text':       return <FileText className={cls} />;
    case 'faq_pairs':  return <FileText className={cls} />;
    case 'past_chats': return <MessageSquare className={cls} />;
  }
}

function sourceLabel(type: SourceType): string {
  const labels: Record<SourceType, string> = {
    file: 'File',
    url: 'URL',
    text: 'Text',
    faq_pairs: 'FAQ',
    past_chats: 'Chats',
  };
  return labels[type];
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

// ─── Inline Ingestion Progress ────────────────────────────────────────────────

const INGESTION_STEPS = ['Uploading', 'Extracting', 'Chunking', 'Embedding', 'Active'] as const;

function statusToStep(status: IngestionStatus): number {
  if (status === 'pending')    return 0;
  if (status === 'processing') return 2;
  if (status === 'active')     return 4;
  return -1;
}

function IngestionSteps({ status }: { status: IngestionStatus }) {
  const currentStep = statusToStep(status);
  if (currentStep < 0) return null;

  return (
    <div className="mt-1.5 flex items-center gap-1" aria-label="Ingestion progress">
      {INGESTION_STEPS.map((label, idx) => {
        const isDone    = idx < currentStep;
        const isCurrent = idx === currentStep;
        return (
          <div key={label} className="flex items-center gap-1">
            <div
              className={cn(
                'flex h-4 w-4 items-center justify-center rounded-full border text-[9px] font-bold transition-colors',
                isDone    && 'border-primary bg-primary text-primary-foreground',
                isCurrent && 'border-primary bg-primary/10 text-primary animate-pulse',
                !isDone && !isCurrent && 'border-border bg-muted text-muted-foreground/40',
              )}
              title={label}
            >
              {isDone ? <Check className="h-2.5 w-2.5" /> : <span>{idx + 1}</span>}
            </div>
            {idx < INGESTION_STEPS.length - 1 && (
              <div className={cn('h-px w-3 rounded', isDone ? 'bg-primary' : 'bg-border')} />
            )}
          </div>
        );
      })}
    </div>
  );
}

// ─── Table Skeleton ───────────────────────────────────────────────────────────

function TableSkeleton() {
  return (
    <div className="overflow-hidden rounded-lg border border-border">
      <Table>
        <TableHeader>
          <TableRow className="bg-muted/50">
            {[35, 10, 20, 8, 7, 12, 8].map((w, i) => (
              <TableHead key={i} style={{ width: `${w}%` }}>
                <Skeleton className="h-4 w-16" />
              </TableHead>
            ))}
          </TableRow>
        </TableHeader>
        <TableBody>
          {Array.from({ length: 4 }).map((_, i) => (
            <TableRow key={i}>
              <TableCell><Skeleton className="h-4 w-40" /></TableCell>
              <TableCell><Skeleton className="h-4 w-10" /></TableCell>
              <TableCell><Skeleton className="h-5 w-20 rounded-full" /></TableCell>
              <TableCell><Skeleton className="ml-auto h-4 w-8" /></TableCell>
              <TableCell><Skeleton className="ml-auto h-4 w-6" /></TableCell>
              <TableCell className="hidden md:table-cell"><Skeleton className="h-4 w-20" /></TableCell>
              <TableCell><Skeleton className="ml-auto h-4 w-16" /></TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}

// ─── Q&A Pair type ────────────────────────────────────────────────────────────

interface QAPair {
  id: string;
  question: string;
  answer: string;
}

// ─── Add Knowledge Dialog ─────────────────────────────────────────────────────

interface AddKnowledgeDialogProps {
  open: boolean;
  onClose: () => void;
}

function AddKnowledgeDialog({ open, onClose }: AddKnowledgeDialogProps) {
  const t = useTranslations('chat');
  const { mutate: upload, progressRef } = useUploadKnowledgeDocument();

  // Upload tab
  const [isDragging, setIsDragging]         = useState(false);
  const [uploadFile, setUploadFile]         = useState<File | null>(null);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [uploadDone, setUploadDone]         = useState(false);
  const [uploadError, setUploadError]       = useState<string | null>(null);
  const [uploadTitle, setUploadTitle]       = useState('');
  const [isUploading, setIsUploading]       = useState(false);

  // URL tab
  const [url, setUrl]           = useState('');
  const [urlTitle, setUrlTitle] = useState('');
  const [urlError, setUrlError] = useState<string | null>(null);

  // Text tab
  const [textMode, setTextMode]   = useState<'free' | 'qa'>('free');
  const [freeTitle, setFreeTitle] = useState('');
  const [freeText, setFreeText]   = useState('');
  const [qaPairs, setQaPairs]     = useState<QAPair[]>([{ id: '1', question: '', answer: '' }]);

  // Chat tab
  const [chatFrom, setChatFrom]         = useState('');
  const [chatTo, setChatTo]             = useState('');
  const [resolvedOnly, setResolvedOnly] = useState(false);

  const ALLOWED_EXT = ['.pdf', '.docx', '.txt', '.csv', '.md'];

  function validateFileType(file: File): boolean {
    const ext = file.name.substring(file.name.lastIndexOf('.')).toLowerCase();
    return ALLOWED_EXT.includes(ext);
  }

  function handleFileSelected(file: File) {
    if (!validateFileType(file)) {
      setUploadError('Unsupported file type. Use PDF, DOCX, TXT, CSV, or MD.');
      return;
    }
    setUploadError(null);
    setUploadFile(file);
    setUploadProgress(0);
    setUploadDone(false);
    const parts = file.name.split('.');
    parts.pop();
    setUploadTitle(parts.join('.'));
  }

  function handleDrop(e: React.DragEvent<HTMLDivElement>) {
    e.preventDefault();
    setIsDragging(false);
    const file = e.dataTransfer.files[0];
    if (file) handleFileSelected(file);
  }

  function handleFileInput(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (file) handleFileSelected(file);
  }

  function submitUpload() {
    if (!uploadFile || !uploadTitle.trim()) return;
    setIsUploading(true);
    progressRef.current = (pct) => {
      setUploadProgress(pct);
      if (pct >= 100) setUploadDone(true);
    };
    upload(
      { file: uploadFile, title: uploadTitle.trim(), sourceType: 'file' },
      {
        onSuccess: () => { handleClose(); },
        onError:   () => {
          setIsUploading(false);
          setUploadError('Upload failed. Please try again.');
        },
      },
    );
  }

  function validateUrl(value: string): boolean {
    try {
      return new URL(value).protocol === 'https:';
    } catch {
      return false;
    }
  }

  function submitUrl() {
    const trimmed = url.trim();
    if (!trimmed || !validateUrl(trimmed)) {
      setUrlError('URL must start with https://');
      return;
    }
    setUrlError(null);
    upload(
      { title: urlTitle.trim() || trimmed, sourceType: 'url', sourceUrl: trimmed },
      { onSuccess: handleClose },
    );
  }

  function submitText() {
    if (textMode === 'free') {
      if (!freeTitle.trim() || !freeText.trim()) return;
      upload(
        { title: freeTitle.trim(), sourceType: 'text', content: freeText.trim() },
        { onSuccess: handleClose },
      );
    } else {
      const valid = qaPairs.filter((p) => p.question.trim() && p.answer.trim());
      if (valid.length === 0) return;
      upload(
        {
          title: freeTitle.trim() || 'FAQ Document',
          sourceType: 'faq_pairs',
          faqPairs: JSON.stringify(
            valid.map(({ question, answer }) => ({ question, answer })),
          ),
        },
        { onSuccess: handleClose },
      );
    }
  }

  function submitChat() {
    if (!chatFrom || !chatTo) return;
    upload(
      {
        title: `Past Chats — ${chatFrom} to ${chatTo}`,
        sourceType: 'past_chats',
        dateFrom: chatFrom,
        dateTo: chatTo,
        content: resolvedOnly ? 'resolved_only=true' : undefined,
      },
      { onSuccess: handleClose },
    );
  }

  function addQaPair() {
    setQaPairs((p) => [...p, { id: String(Date.now()), question: '', answer: '' }]);
  }

  function removeQaPair(id: string) {
    setQaPairs((p) => p.filter((pair) => pair.id !== id));
  }

  function updateQaPair(id: string, field: 'question' | 'answer', value: string) {
    setQaPairs((p) =>
      p.map((pair) => (pair.id === id ? { ...pair, [field]: value } : pair)),
    );
  }

  function resetAll() {
    setUploadFile(null);
    setUploadProgress(0);
    setUploadDone(false);
    setUploadError(null);
    setUploadTitle('');
    setIsUploading(false);
    setUrl('');
    setUrlTitle('');
    setUrlError(null);
    setFreeTitle('');
    setFreeText('');
    setQaPairs([{ id: '1', question: '', answer: '' }]);
    setChatFrom('');
    setChatTo('');
    setResolvedOnly(false);
  }

  function handleClose() {
    onClose();
    resetAll();
  }

  const chatTitle = chatFrom && chatTo ? `Past Chats — ${chatFrom} to ${chatTo}` : '';

  return (
    <Dialog open={open} onOpenChange={(v) => { if (!v) handleClose(); }}>
      <DialogContent className="max-w-lg w-full">
        <DialogHeader>
          <DialogTitle>{t('knowledge.addDialog.title')}</DialogTitle>
        </DialogHeader>

        <Tabs defaultValue="upload">
          <TabsList className="grid w-full grid-cols-4">
            <TabsTrigger value="upload">{t('knowledge.addDialog.tabs.upload')}</TabsTrigger>
            <TabsTrigger value="url">{t('knowledge.addDialog.tabs.url')}</TabsTrigger>
            <TabsTrigger value="text">{t('knowledge.addDialog.tabs.text')}</TabsTrigger>
            <TabsTrigger value="chat">{t('knowledge.addDialog.tabs.chat')}</TabsTrigger>
          </TabsList>

          {/* ── Tab 1: Upload File ── */}
          <TabsContent value="upload" className="mt-4 space-y-3">
            <div
              onDragOver={(e) => { e.preventDefault(); setIsDragging(true); }}
              onDragLeave={() => setIsDragging(false)}
              onDrop={handleDrop}
              className={cn(
                'cursor-pointer rounded-lg border-2 border-dashed p-8 text-center transition-colors',
                isDragging
                  ? 'border-primary bg-primary/5'
                  : 'border-border hover:border-primary/50',
              )}
              onClick={() => document.getElementById('kb-file-input')?.click()}
              role="button"
              tabIndex={0}
              aria-label={t('knowledge.addDialog.upload.hint')}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                  e.preventDefault();
                  document.getElementById('kb-file-input')?.click();
                }
              }}
            >
              <p className="text-sm font-medium text-foreground">
                {t('knowledge.addDialog.upload.hint')}
              </p>
              <p className="mt-1 text-xs text-muted-foreground">
                {t('knowledge.addDialog.upload.types')}
              </p>
              <input
                id="kb-file-input"
                type="file"
                accept=".pdf,.docx,.txt,.csv,.md"
                className="sr-only"
                onChange={handleFileInput}
              />
            </div>

            {uploadError && (
              <p role="alert" className="text-sm text-destructive">{uploadError}</p>
            )}

            {uploadFile && !uploadError && (
              <div className="space-y-2">
                <div className="flex items-center justify-between gap-2">
                  <span className="truncate text-sm text-foreground">{uploadFile.name}</span>
                  <span className="shrink-0 text-xs text-muted-foreground">
                    {formatFileSize(uploadFile.size)}
                  </span>
                </div>
                <div
                  className="h-1.5 w-full overflow-hidden rounded-full bg-muted"
                  role="progressbar"
                  aria-valuenow={uploadProgress}
                  aria-valuemin={0}
                  aria-valuemax={100}
                  aria-label="Upload progress"
                >
                  <div
                    className="h-full bg-primary transition-all duration-150"
                    style={{ width: `${uploadProgress}%` }}
                  />
                </div>
                <p className="text-xs text-muted-foreground">
                  {uploadDone
                    ? 'Complete'
                    : isUploading
                    ? `${t('knowledge.addDialog.upload.uploading')} ${uploadProgress}%`
                    : 'Ready to upload'}
                </p>
              </div>
            )}

            {uploadFile && !uploadError && (
              <div className="space-y-1.5">
                <Label htmlFor="kb-upload-title">Title</Label>
                <Input
                  id="kb-upload-title"
                  value={uploadTitle}
                  onChange={(e) => setUploadTitle(e.target.value)}
                  placeholder="Document title"
                  disabled={isUploading}
                />
              </div>
            )}

            <DialogFooter>
              <Button
                disabled={!uploadFile || !uploadTitle.trim() || isUploading || !!uploadError}
                onClick={submitUpload}
              >
                {isUploading ? 'Uploading…' : 'Upload'}
              </Button>
            </DialogFooter>
          </TabsContent>

          {/* ── Tab 2: From URL ── */}
          <TabsContent value="url" className="mt-4 space-y-3">
            <div className="space-y-1.5">
              <Label htmlFor="kb-url">{t('knowledge.addDialog.url.label')}</Label>
              <Input
                id="kb-url"
                type="url"
                placeholder={t('knowledge.addDialog.url.placeholder')}
                value={url}
                onChange={(e) => { setUrl(e.target.value); setUrlError(null); }}
              />
              {urlError && (
                <p role="alert" className="text-xs text-destructive">{urlError}</p>
              )}
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="kb-url-title">Title</Label>
              <Input
                id="kb-url-title"
                placeholder="Page title"
                value={urlTitle}
                onChange={(e) => setUrlTitle(e.target.value)}
              />
            </div>
            <DialogFooter>
              <Button disabled={!url.trim()} onClick={submitUrl}>
                {t('knowledge.addDialog.url.fetch')}
              </Button>
            </DialogFooter>
          </TabsContent>

          {/* ── Tab 3: Write Text / FAQ ── */}
          <TabsContent value="text" className="mt-4 space-y-3">
            <fieldset className="flex gap-4">
              <legend className="sr-only">Content mode</legend>
              <label className="flex cursor-pointer items-center gap-2 text-sm">
                <input
                  type="radio"
                  name="text-mode"
                  value="free"
                  checked={textMode === 'free'}
                  onChange={() => setTextMode('free')}
                  className="accent-primary"
                />
                {t('knowledge.addDialog.text.freeText')}
              </label>
              <label className="flex cursor-pointer items-center gap-2 text-sm">
                <input
                  type="radio"
                  name="text-mode"
                  value="qa"
                  checked={textMode === 'qa'}
                  onChange={() => setTextMode('qa')}
                  className="accent-primary"
                />
                {t('knowledge.addDialog.text.qaBuilder')}
              </label>
            </fieldset>

            <div className="space-y-1.5">
              <Label htmlFor="kb-text-title">{t('knowledge.addDialog.text.titleLabel')}</Label>
              <Input
                id="kb-text-title"
                value={freeTitle}
                onChange={(e) => setFreeTitle(e.target.value)}
                placeholder="Document title"
              />
            </div>

            {textMode === 'free' ? (
              <div className="space-y-1.5">
                <Label htmlFor="kb-free-text">{t('knowledge.addDialog.text.label')}</Label>
                <Textarea
                  id="kb-free-text"
                  rows={6}
                  value={freeText}
                  onChange={(e) => setFreeText(e.target.value)}
                  placeholder="Paste or type your content here…"
                />
              </div>
            ) : (
              <div className="max-h-60 space-y-2 overflow-y-auto pr-1">
                {qaPairs.map((pair, idx) => (
                  <div
                    key={pair.id}
                    className="space-y-2 rounded-md border border-border p-3"
                  >
                    <div className="flex items-center justify-between">
                      <span className="text-xs font-medium text-muted-foreground">
                        Pair {idx + 1}
                      </span>
                      {qaPairs.length > 1 && (
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          className="h-6 w-6 p-0 text-muted-foreground hover:text-destructive"
                          aria-label={`Remove pair ${idx + 1}`}
                          onClick={() => removeQaPair(pair.id)}
                        >
                          ×
                        </Button>
                      )}
                    </div>
                    <Input
                      placeholder={t('knowledge.addDialog.text.question')}
                      value={pair.question}
                      onChange={(e) => updateQaPair(pair.id, 'question', e.target.value)}
                    />
                    <Textarea
                      rows={2}
                      placeholder={t('knowledge.addDialog.text.answer')}
                      value={pair.answer}
                      onChange={(e) => updateQaPair(pair.id, 'answer', e.target.value)}
                    />
                  </div>
                ))}
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={addQaPair}
                  className="w-full"
                >
                  {t('knowledge.addDialog.text.addPair')}
                </Button>
              </div>
            )}

            <DialogFooter>
              <Button
                disabled={
                  textMode === 'free'
                    ? !freeTitle.trim() || !freeText.trim()
                    : !qaPairs.some((p) => p.question.trim() && p.answer.trim())
                }
                onClick={submitText}
              >
                Save
              </Button>
            </DialogFooter>
          </TabsContent>

          {/* ── Tab 4: Import Past Chats ── */}
          <TabsContent value="chat" className="mt-4 space-y-3">
            <p className="text-sm text-muted-foreground">
              Import past conversations from your inbox as knowledge chunks. Select a date range to
              limit the import.
            </p>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label htmlFor="kb-chat-from">From</Label>
                <Input
                  id="kb-chat-from"
                  type="date"
                  value={chatFrom}
                  onChange={(e) => setChatFrom(e.target.value)}
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="kb-chat-to">To</Label>
                <Input
                  id="kb-chat-to"
                  type="date"
                  value={chatTo}
                  onChange={(e) => setChatTo(e.target.value)}
                />
              </div>
            </div>
            <div className="flex items-center gap-2">
              <Checkbox
                id="kb-chat-resolved"
                checked={resolvedOnly}
                onCheckedChange={(v) => setResolvedOnly(v === true)}
              />
              <Label
                htmlFor="kb-chat-resolved"
                className="cursor-pointer text-sm font-normal"
              >
                Only resolved conversations
              </Label>
            </div>
            {chatTitle && (
              <p className="text-xs text-muted-foreground">
                ~42 conversations estimated · Title:{' '}
                <span className="font-medium text-foreground">{chatTitle}</span>
              </p>
            )}
            <DialogFooter>
              <Button disabled={!chatFrom || !chatTo} onClick={submitChat}>
                Import
              </Button>
            </DialogFooter>
          </TabsContent>
        </Tabs>
      </DialogContent>
    </Dialog>
  );
}

// ─── Delete Confirm Dialog ────────────────────────────────────────────────────

interface DeleteDialogProps {
  doc: KnowledgeDocumentDto | null;
  onClose: () => void;
}

function DeleteDialog({ doc, onClose }: DeleteDialogProps) {
  const t = useTranslations('chat');
  const { mutate: deleteDoc, isPending } = useDeleteKnowledgeDocument();

  function handleConfirm() {
    if (!doc) return;
    deleteDoc(doc.id, { onSettled: onClose });
  }

  return (
    <Dialog open={doc !== null} onOpenChange={(v) => { if (!v) onClose(); }}>
      <DialogContent className="max-w-sm">
        <DialogHeader>
          <DialogTitle>{t('knowledge.deleteConfirm')}</DialogTitle>
          <DialogDescription>{t('knowledge.deleteDescription')}</DialogDescription>
        </DialogHeader>
        {doc && (
          <p className="truncate text-sm font-medium text-foreground">{doc.title}</p>
        )}
        <DialogFooter className="gap-2">
          <Button variant="outline" onClick={onClose} disabled={isPending}>
            {t('knowledge.cancel')}
          </Button>
          <Button variant="destructive" onClick={handleConfirm} disabled={isPending}>
            {isPending ? 'Deleting…' : t('knowledge.confirm')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export default function KnowledgePage() {
  const t      = useTranslations('chat');
  const router = useRouter();

  const [addOpen, setAddOpen]           = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<KnowledgeDocumentDto | null>(null);

  const { data, isLoading, isError }                            = useKnowledgeDocuments();
  const { mutate: reEmbed, variables: reEmbedId, isPending: reEmbedPending } =
    useReEmbedKnowledgeDocument();

  const docs = data?.items ?? [];

  function navigateToDoc(id: string) {
    router.push(`/dashboard/chat/settings/knowledge/${id}`);
  }

  return (
    <div className="flex-1 overflow-y-auto">
      <div className="flex flex-col gap-6 p-4 md:p-6">
        {/* ── Header ── */}
        <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <h1 className="text-2xl font-bold tracking-tight text-foreground">
              {t('knowledge.title')}
            </h1>
            <p className="mt-1 text-sm text-muted-foreground">
              Manage documents and data sources the AI uses to answer questions.
            </p>
          </div>
          <Button onClick={() => setAddOpen(true)} className="shrink-0 self-start">
            <Plus className="mr-2 h-4 w-4" />
            {t('knowledge.addKnowledge')}
          </Button>
        </div>

        {/* ── Error ── */}
        {isError && (
          <p role="alert" className="text-sm text-destructive">
            Could not load knowledge documents. Please refresh and try again.
          </p>
        )}

        {/* ── Loading skeleton ── */}
        {isLoading && <TableSkeleton />}

        {/* ── Empty state ── */}
        {!isLoading && !isError && docs.length === 0 && (
          <div className="flex flex-col items-center gap-4 rounded-lg border border-border py-16 text-center">
            <div className="flex h-14 w-14 items-center justify-center rounded-full bg-muted">
              <BookOpen className="h-7 w-7 text-muted-foreground" />
            </div>
            <div className="space-y-1">
              <p className="font-semibold text-foreground">No knowledge documents yet</p>
              <p className="max-w-xs text-sm text-muted-foreground">
                Add your first document to help your AI assistant answer questions.
              </p>
            </div>
            <Button onClick={() => setAddOpen(true)}>
              <Plus className="mr-2 h-4 w-4" />
              {t('knowledge.addKnowledge')}
            </Button>
          </div>
        )}

        {/* ── Table ── */}
        {!isLoading && !isError && docs.length > 0 && (
          <div className="overflow-hidden rounded-lg border border-border">
            <Table>
              <TableHeader>
                <TableRow className="bg-muted/50">
                  <TableHead className="w-[35%]">{t('knowledge.columns.title')}</TableHead>
                  <TableHead className="w-[10%]">{t('knowledge.columns.type')}</TableHead>
                  <TableHead className="w-[20%]">{t('knowledge.columns.status')}</TableHead>
                  <TableHead className="w-[8%] text-right">{t('knowledge.columns.chunks')}</TableHead>
                  <TableHead className="hidden w-[7%] text-right sm:table-cell">
                    Ver.
                  </TableHead>
                  <TableHead className="hidden w-[12%] md:table-cell">
                    {t('knowledge.columns.updatedAt')}
                  </TableHead>
                  <TableHead className="w-[8%] text-right">
                    {t('knowledge.columns.actions')}
                  </TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {docs.map((doc) => (
                  <TableRow
                    key={doc.id}
                    className="group cursor-pointer"
                    onClick={() => navigateToDoc(doc.id)}
                  >
                    <TableCell>
                      <div className="flex min-w-0 items-center gap-2">
                        <span className="shrink-0 text-muted-foreground">
                          {sourceIcon(doc.sourceType)}
                        </span>
                        <span className="truncate text-sm font-medium text-foreground">
                          {doc.title}
                        </span>
                      </div>
                    </TableCell>
                    <TableCell>
                      <span className="text-xs text-muted-foreground">
                        {sourceLabel(doc.sourceType)}
                      </span>
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={doc.status} />
                      {(doc.status === 'pending' || doc.status === 'processing') && (
                        <IngestionSteps status={doc.status} />
                      )}
                    </TableCell>
                    <TableCell className="text-right text-sm tabular-nums">
                      {doc.chunkCount > 0 ? doc.chunkCount.toLocaleString() : '—'}
                    </TableCell>
                    <TableCell className="hidden text-right text-sm tabular-nums sm:table-cell">
                      v{doc.version}
                    </TableCell>
                    <TableCell className="hidden text-sm text-muted-foreground md:table-cell">
                      {formatDate(doc.updatedAt)}
                    </TableCell>
                    <TableCell>
                      <div className="flex items-center justify-end gap-1">
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          className="h-7 w-7 p-0"
                          aria-label={`${t('knowledge.actions.view')} ${doc.title}`}
                          onClick={(e) => {
                            e.stopPropagation();
                            navigateToDoc(doc.id);
                          }}
                        >
                          <Eye className="h-3.5 w-3.5" />
                        </Button>
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          className="h-7 w-7 p-0"
                          aria-label={`${t('knowledge.actions.reEmbed')} ${doc.title}`}
                          disabled={reEmbedPending && reEmbedId === doc.id}
                          onClick={(e) => {
                            e.stopPropagation();
                            reEmbed(doc.id);
                          }}
                        >
                          <RefreshCw
                            className={cn(
                              'h-3.5 w-3.5',
                              reEmbedPending && reEmbedId === doc.id && 'animate-spin',
                            )}
                          />
                        </Button>
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          className="h-7 w-7 p-0 text-muted-foreground hover:text-destructive"
                          aria-label={`${t('knowledge.actions.delete')} ${doc.title}`}
                          onClick={(e) => {
                            e.stopPropagation();
                            setDeleteTarget(doc);
                          }}
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}

        {/* ── Dialogs ── */}
        <AddKnowledgeDialog open={addOpen} onClose={() => setAddOpen(false)} />
        <DeleteDialog doc={deleteTarget} onClose={() => setDeleteTarget(null)} />
      </div>
    </div>
  );
}
