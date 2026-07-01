'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { Plus, Eye, RefreshCw, Trash2, FileText, Link, MessageSquare, File, BookOpen } from 'lucide-react';
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
import { cn } from '@/shared/lib/cn';

// ─── Types ────────────────────────────────────────────────────────────────────

type DocStatus = 'Ready' | 'Processing' | 'Failed';
type DocType = 'PDF' | 'URL' | 'Text' | 'Chat';

interface KnowledgeDoc {
  id: string;
  title: string;
  type: DocType;
  status: DocStatus;
  chunks: number;
  updatedAt: string;
}

interface QAPair {
  id: string;
  question: string;
  answer: string;
}

// ─── Mock Data ────────────────────────────────────────────────────────────────

const MOCK_DOCS_RAW: KnowledgeDoc[] = [
  { id: '1', title: 'Product Catalog 2026', type: 'PDF' as DocType, status: 'Ready' as DocStatus, chunks: 142, updatedAt: '2026-06-28T10:00:00.000Z' },
  { id: '2', title: 'Return Policy FAQ', type: 'Text' as DocType, status: 'Ready' as DocStatus, chunks: 24, updatedAt: '2026-06-27T14:30:00.000Z' },
  { id: '3', title: 'Warranty Terms 2026', type: 'URL' as DocType, status: 'Processing' as DocStatus, chunks: 0, updatedAt: '2026-06-30T09:00:00.000Z' },
  { id: '4', title: 'Past Chat Import - June', type: 'Chat' as DocType, status: 'Ready' as DocStatus, chunks: 318, updatedAt: '2026-06-25T08:00:00.000Z' },
  { id: '5', title: 'Delivery SLA Guide', type: 'PDF' as DocType, status: 'Failed' as DocStatus, chunks: 0, updatedAt: '2026-06-29T16:00:00.000Z' },
];
const MOCK_DOCS: KnowledgeDoc[] = [...MOCK_DOCS_RAW].sort((a, b) => new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime());

// ─── Helpers ──────────────────────────────────────────────────────────────────

function formatDate(iso: string): string {
  return new Intl.DateTimeFormat('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  }).format(new Date(iso));
}

function typeIcon(type: DocType): React.ReactNode {
  const cls = 'h-3.5 w-3.5 shrink-0';
  switch (type) {
    case 'PDF':  return <File className={cls} />;
    case 'URL':  return <Link className={cls} />;
    case 'Text': return <FileText className={cls} />;
    case 'Chat': return <MessageSquare className={cls} />;
  }
}

function StatusBadge({ status }: { status: DocStatus }) {
  const cls = cn(
    'font-medium',
    status === 'Ready'      && 'text-chatConfidence-high border-chatConfidence-high/40',
    status === 'Processing' && 'text-chatConfidence-medium border-chatConfidence-medium/40',
    status === 'Failed'     && 'text-destructive border-destructive/40',
  );
  return (
    <Badge variant="outline" className={cls}>
      {status === 'Processing' && (
        <RefreshCw className="mr-1 h-3 w-3 animate-spin" />
      )}
      {status}
    </Badge>
  );
}

// ─── Add Knowledge Dialog ─────────────────────────────────────────────────────

function AddKnowledgeDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const t = useTranslations('chat');

  // Upload tab state
  const [isDragging, setIsDragging]         = useState(false);
  const [uploadFile, setUploadFile]         = useState<string | null>(null);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [uploadDone, setUploadDone]         = useState(false);
  const [uploadError, setUploadError]       = useState<string | null>(null);

  // URL tab state
  const [url, setUrl]               = useState('');
  const [urlLoading, setUrlLoading] = useState(false);
  const [urlReady, setUrlReady]     = useState(false);

  // Text tab state
  const [textMode, setTextMode] = useState<'free' | 'qa'>('free');
  const [freeTitle, setFreeTitle] = useState('');
  const [freeText, setFreeText]   = useState('');
  const [qaPairs, setQaPairs]     = useState<QAPair[]>([{ id: '1', question: '', answer: '' }]);

  // Chat tab state
  const [chatFrom, setChatFrom] = useState('');
  const [chatTo, setChatTo]     = useState('');

  const ALLOWED_TYPES = ['application/pdf', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 'text/plain', 'text/markdown'];
  const ALLOWED_EXT   = ['.pdf', '.docx', '.txt', '.md'];

  function validateFile(file: File): boolean {
    const ext = file.name.substring(file.name.lastIndexOf('.')).toLowerCase();
    return ALLOWED_TYPES.includes(file.type) || ALLOWED_EXT.includes(ext);
  }

  function startUpload(name: string) {
    setUploadFile(name);
    setUploadProgress(0);
    setUploadDone(false);
    setUploadError(null);
    const id = setInterval(() => {
      setUploadProgress(prev => {
        if (prev >= 100) { clearInterval(id); setUploadDone(true); return 100; }
        return prev + 5;
      });
    }, 150);
  }

  function handleDrop(e: React.DragEvent<HTMLDivElement>) {
    e.preventDefault();
    setIsDragging(false);
    const file = e.dataTransfer.files[0];
    if (!file) return;
    if (!validateFile(file)) { setUploadError('Unsupported file type. Use PDF, DOCX, TXT, or MD.'); return; }
    startUpload(file.name);
  }

  function handleFileInput(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    if (!validateFile(file)) { setUploadError('Unsupported file type. Use PDF, DOCX, TXT, or MD.'); return; }
    startUpload(file.name);
  }

  function fetchUrl() {
    if (!url.trim()) return;
    setUrlLoading(true);
    setUrlReady(false);
    setTimeout(() => { setUrlLoading(false); setUrlReady(true); }, 1500);
  }

  function addQaPair() {
    setQaPairs(p => [...p, { id: String(Date.now()), question: '', answer: '' }]);
  }

  function removeQaPair(id: string) {
    setQaPairs(p => p.filter(pair => pair.id !== id));
  }

  function updateQaPair(id: string, field: 'question' | 'answer', value: string) {
    setQaPairs(p => p.map(pair => pair.id === id ? { ...pair, [field]: value } : pair));
  }

  const uploadReady  = uploadDone;
  const urlSaveReady = urlReady && url.trim().length > 0;
  const textSaveReady = textMode === 'free'
    ? freeTitle.trim().length > 0 && freeText.trim().length > 0
    : qaPairs.some(p => p.question.trim().length > 0 && p.answer.trim().length > 0);
  const chatSaveReady = chatFrom.length > 0 && chatTo.length > 0;

  return (
    <Dialog open={open} onOpenChange={v => { if (!v) onClose(); }}>
      <DialogContent className="max-w-lg w-full">
        <DialogHeader>
          <DialogTitle>{t('knowledge.addDialog.title')}</DialogTitle>
        </DialogHeader>

        <Tabs defaultValue="upload">
          <TabsList className="w-full grid grid-cols-4">
            <TabsTrigger value="upload">{t('knowledge.addDialog.tabs.upload')}</TabsTrigger>
            <TabsTrigger value="url">{t('knowledge.addDialog.tabs.url')}</TabsTrigger>
            <TabsTrigger value="text">{t('knowledge.addDialog.tabs.text')}</TabsTrigger>
            <TabsTrigger value="chat">{t('knowledge.addDialog.tabs.chat')}</TabsTrigger>
          </TabsList>

          {/* ── Upload ── */}
          <TabsContent value="upload" className="mt-4 space-y-3">
            <div
              onDragOver={e => { e.preventDefault(); setIsDragging(true); }}
              onDragLeave={() => setIsDragging(false)}
              onDrop={handleDrop}
              className={cn(
                'rounded-lg border-2 border-dashed p-8 text-center transition-colors cursor-pointer',
                isDragging ? 'border-primary bg-primary/5' : 'border-border hover:border-primary/50',
              )}
              onClick={() => document.getElementById('kb-file-input')?.click()}
              role="button"
              tabIndex={0}
              aria-label="Drop files here or click to upload"
              onKeyDown={e => e.key === 'Enter' && document.getElementById('kb-file-input')?.click()}
            >
              <p className="text-sm font-medium text-foreground">{t('knowledge.addDialog.upload.hint')}</p>
              <p className="mt-1 text-xs text-muted-foreground">{t('knowledge.addDialog.upload.types')}</p>
              <input
                id="kb-file-input"
                type="file"
                accept=".pdf,.docx,.txt,.md"
                className="hidden"
                onChange={handleFileInput}
              />
            </div>

            {uploadError && (
              <p className="text-sm text-destructive">{uploadError}</p>
            )}

            {uploadFile && !uploadError && (
              <div className="space-y-1">
                <p className="text-sm text-foreground truncate">{uploadFile}</p>
                <div className="h-1.5 w-full rounded-full bg-muted overflow-hidden">
                  <div
                    className="h-full bg-primary transition-all duration-150"
                    style={{ width: `${uploadProgress}%` }}
                  />
                </div>
                <p className="text-xs text-muted-foreground">
                  {uploadDone ? 'Complete' : `${t('knowledge.addDialog.upload.uploading')} ${uploadProgress}%`}
                </p>
              </div>
            )}

            <DialogFooter>
              <Button disabled={!uploadReady} onClick={onClose}>Save</Button>
            </DialogFooter>
          </TabsContent>

          {/* ── URL ── */}
          <TabsContent value="url" className="mt-4 space-y-3">
            <Label htmlFor="kb-url">{t('knowledge.addDialog.url.label')}</Label>
            <div className="flex gap-2">
              <Input
                id="kb-url"
                type="url"
                placeholder={t('knowledge.addDialog.url.placeholder')}
                value={url}
                onChange={e => { setUrl(e.target.value); setUrlReady(false); }}
              />
              <Button variant="outline" onClick={fetchUrl} disabled={urlLoading || !url.trim()}>
                {urlLoading ? 'Fetching…' : t('knowledge.addDialog.url.fetch')}
              </Button>
            </div>
            {urlLoading && <p className="text-sm text-muted-foreground">Fetching content…</p>}
            {urlReady   && <p className="text-sm text-chatConfidence-high">Content ready (3,421 words)</p>}
            <DialogFooter>
              <Button disabled={!urlSaveReady} onClick={onClose}>Save</Button>
            </DialogFooter>
          </TabsContent>

          {/* ── Text ── */}
          <TabsContent value="text" className="mt-4 space-y-3">
            <div className="flex gap-4">
              <label className="flex items-center gap-2 cursor-pointer text-sm">
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
              <label className="flex items-center gap-2 cursor-pointer text-sm">
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
            </div>

            {textMode === 'free' ? (
              <div className="space-y-2">
                <Label htmlFor="kb-free-title">{t('knowledge.addDialog.text.titleLabel')}</Label>
                <Input
                  id="kb-free-title"
                  value={freeTitle}
                  onChange={e => setFreeTitle(e.target.value)}
                  placeholder="Document title"
                />
                <Label htmlFor="kb-free-text">{t('knowledge.addDialog.text.label')}</Label>
                <Textarea
                  id="kb-free-text"
                  rows={6}
                  value={freeText}
                  onChange={e => setFreeText(e.target.value)}
                  placeholder="Paste or type your content here…"
                />
              </div>
            ) : (
              <div className="space-y-3 max-h-64 overflow-y-auto pr-1">
                {qaPairs.map((pair, idx) => (
                  <div key={pair.id} className="rounded-md border border-border p-3 space-y-2">
                    <div className="flex items-center justify-between">
                      <span className="text-xs font-medium text-muted-foreground">Pair {idx + 1}</span>
                      {qaPairs.length > 1 && (
                        <Button
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
                      onChange={e => updateQaPair(pair.id, 'question', e.target.value)}
                    />
                    <Textarea
                      rows={2}
                      placeholder={t('knowledge.addDialog.text.answer')}
                      value={pair.answer}
                      onChange={e => updateQaPair(pair.id, 'answer', e.target.value)}
                    />
                  </div>
                ))}
                <Button variant="outline" size="sm" onClick={addQaPair} className="w-full">
                  {t('knowledge.addDialog.text.addPair')}
                </Button>
              </div>
            )}

            <DialogFooter>
              <Button disabled={!textSaveReady} onClick={onClose}>Save</Button>
            </DialogFooter>
          </TabsContent>

          {/* ── Chat Import ── */}
          <TabsContent value="chat" className="mt-4 space-y-3">
            <p className="text-sm text-muted-foreground">
              Import past conversations from your inbox as knowledge chunks. Select a date range to limit the import.
            </p>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1">
                <Label htmlFor="kb-chat-from">From</Label>
                <Input id="kb-chat-from" type="date" value={chatFrom} onChange={e => setChatFrom(e.target.value)} />
              </div>
              <div className="space-y-1">
                <Label htmlFor="kb-chat-to">To</Label>
                <Input id="kb-chat-to" type="date" value={chatTo} onChange={e => setChatTo(e.target.value)} />
              </div>
            </div>
            <DialogFooter>
              <Button disabled={!chatSaveReady} onClick={onClose}>Import</Button>
            </DialogFooter>
          </TabsContent>
        </Tabs>
      </DialogContent>
    </Dialog>
  );
}

// ─── Delete Confirm Dialog ────────────────────────────────────────────────────

function DeleteDialog({
  doc,
  onClose,
  onConfirm,
}: {
  doc: KnowledgeDoc | null;
  onClose: () => void;
  onConfirm: (id: string) => void;
}) {
  const t = useTranslations('chat');
  return (
    <Dialog open={doc !== null} onOpenChange={v => { if (!v) onClose(); }}>
      <DialogContent className="max-w-sm">
        <DialogHeader>
          <DialogTitle>{t('knowledge.deleteConfirm')}</DialogTitle>
          <DialogDescription>{t('knowledge.deleteDescription')}</DialogDescription>
        </DialogHeader>
        {doc && (
          <p className="text-sm font-medium text-foreground truncate">{doc.title}</p>
        )}
        <DialogFooter className="gap-2">
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button
            variant="destructive"
            onClick={() => { if (doc) { onConfirm(doc.id); onClose(); } }}
          >
            Delete
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export default function KnowledgePage() {
  const t = useTranslations('chat');
  const router = useRouter();

  const [docs, setDocs]                   = useState<KnowledgeDoc[]>(MOCK_DOCS);
  const [addOpen, setAddOpen]             = useState(false);
  const [deleteTarget, setDeleteTarget]   = useState<KnowledgeDoc | null>(null);
  const [reEmbedding, setReEmbedding]     = useState<Record<string, boolean>>({});

  function handleDelete(id: string) {
    setDocs(prev => prev.filter(d => d.id !== id));
  }

  function handleReEmbed(id: string) {
    setReEmbedding(prev => ({ ...prev, [id]: true }));
    setTimeout(() => setReEmbedding(prev => ({ ...prev, [id]: false })), 2000);
  }

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
          <Button
            onClick={() => setAddOpen(true)}
            className="shrink-0 self-start"
          >
            <Plus className="mr-2 h-4 w-4" />
            {t('knowledge.addKnowledge')}
          </Button>
        </div>

        {/* ── Table or empty state ── */}
        {docs.length === 0 ? (
          <div className="flex flex-col items-center gap-4 rounded-lg border border-border py-16 text-center">
            <div className="flex h-14 w-14 items-center justify-center rounded-full bg-muted">
              <BookOpen className="h-7 w-7 text-muted-foreground" />
            </div>
            <div className="space-y-1">
              <p className="font-semibold text-foreground">No knowledge sources yet</p>
              <p className="text-sm text-muted-foreground">
                Add your first document, URL, or text to power the AI assistant.
              </p>
            </div>
            <Button onClick={() => setAddOpen(true)}>
              <Plus className="mr-2 h-4 w-4" />
              Add your first knowledge source
            </Button>
          </div>
        ) : (
          <div className="rounded-lg border border-border overflow-hidden">
            <Table>
              <TableHeader>
                <TableRow className="bg-muted/50">
                  <TableHead className="w-[35%]">{t('knowledge.columns.title')}</TableHead>
                  <TableHead className="w-[10%]">{t('knowledge.columns.type')}</TableHead>
                  <TableHead className="w-[15%]">{t('knowledge.columns.status')}</TableHead>
                  <TableHead className="w-[10%] text-right">{t('knowledge.columns.chunks')}</TableHead>
                  <TableHead className="w-[15%] hidden md:table-cell">{t('knowledge.columns.updatedAt')}</TableHead>
                  <TableHead className="w-[15%] text-right">{t('knowledge.columns.actions')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {docs.map(doc => (
                  <TableRow
                    key={doc.id}
                    className="group cursor-pointer"
                    onClick={() => navigateToDoc(doc.id)}
                  >
                    <TableCell>
                      <div className="flex items-center gap-2 min-w-0">
                        <span className="text-muted-foreground shrink-0">{typeIcon(doc.type)}</span>
                        <span className="truncate text-sm font-medium text-foreground">{doc.title}</span>
                      </div>
                    </TableCell>
                    <TableCell>
                      <span className="text-xs text-muted-foreground">{doc.type}</span>
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={doc.status} />
                    </TableCell>
                    <TableCell className="text-right text-sm tabular-nums">
                      {doc.chunks > 0 ? doc.chunks.toLocaleString() : '—'}
                    </TableCell>
                    <TableCell className="hidden md:table-cell text-sm text-muted-foreground">
                      {formatDate(doc.updatedAt)}
                    </TableCell>
                    <TableCell>
                      <div className="flex items-center justify-end gap-1">
                        <Button
                          variant="ghost"
                          size="sm"
                          className="h-7 w-7 p-0"
                          aria-label={`${t('knowledge.actions.view')} ${doc.title}`}
                          onClick={(e) => { e.stopPropagation(); navigateToDoc(doc.id); }}
                        >
                          <Eye className="h-3.5 w-3.5" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          className="h-7 w-7 p-0"
                          aria-label={`${t('knowledge.actions.reEmbed')} ${doc.title}`}
                          title={reEmbedding[doc.id] ? t('knowledge.reEmbedding') : undefined}
                          onClick={(e) => { e.stopPropagation(); handleReEmbed(doc.id); }}
                          disabled={reEmbedding[doc.id]}
                        >
                          <RefreshCw className={cn('h-3.5 w-3.5', reEmbedding[doc.id] && 'animate-spin')} />
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          className="h-7 w-7 p-0 text-muted-foreground hover:text-destructive"
                          aria-label={`${t('knowledge.actions.delete')} ${doc.title}`}
                          onClick={(e) => { e.stopPropagation(); setDeleteTarget(doc); }}
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
        <DeleteDialog
          doc={deleteTarget}
          onClose={() => setDeleteTarget(null)}
          onConfirm={handleDelete}
        />
      </div>
    </div>
  );
}