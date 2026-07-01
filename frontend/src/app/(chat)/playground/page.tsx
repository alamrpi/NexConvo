'use client';

import { useState, useRef, useEffect, useCallback } from 'react';
import {
  Shield,
  ChevronDown,
  ChevronUp,
  ChevronRight,
  ChevronLeft,
  Copy,
  Check,
  Trash2,
  RefreshCw,
  GitCompare,
  Bug,
} from 'lucide-react';
import { Badge } from '@/shared/ui/badge';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Textarea } from '@/shared/ui/textarea';
import { Switch } from '@/shared/ui/switch';
import { Slider } from '@/shared/ui/slider';
import { Label } from '@/shared/ui/label';
import { Separator } from '@/shared/ui/separator';
import { ScrollArea } from '@/shared/ui/scroll-area';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/shared/ui/tabs';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/shared/ui/table';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/ui/select';
import { cn } from '@/shared/lib/cn';

// ─── Types ────────────────────────────────────────────────────────────────────

interface DebugChunk {
  rank: number;
  document: string;
  preview: string;
  fullText: string;
  score: number;
}

interface DebugData {
  chunks: DebugChunk[];
  confidence: number;
  confidenceBand: 'High' | 'Medium' | 'Low';
  promptTokens: number;
  completionTokens: number;
  totalTokens: number;
  promptPreview: string;
  rawResponse: string;
  piiDetected: { token: string; type: string }[];
  timing: { embed: number; retrieval: number; llm: number; total: number };
}

interface PlaygroundMessage {
  id: string;
  role: 'user' | 'ai' | 'system';
  body: string;
  isStreaming?: boolean;
  debugData?: DebugData;
  sentAt: string;
}

// ─── Scenario data ────────────────────────────────────────────────────────────

const SCENARIOS: Record<string, PlaygroundMessage[]> = {
  angryRefund: [
    {
      id: 's1-1',
      role: 'user',
      body: "I ordered this 3 weeks ago and still haven't received a refund! This is absolutely ridiculous.",
      sentAt: '2026-06-30T09:00:00.000Z',
    },
    {
      id: 's1-2',
      role: 'ai',
      body: 'I sincerely apologize for the delay and your frustration. Let me look into your refund status immediately. Could you provide your order number so I can investigate right away?',
      sentAt: '2026-06-30T09:00:05.000Z',
      debugData: {
        chunks: [
          {
            rank: 1,
            document: 'Return Policy FAQ',
            preview: 'Refunds are processed within 5–7 business',
            fullText:
              'Refunds are processed within 5–7 business days after the item is received and inspected. Bank transfers may take an additional 2–3 days.',
            score: 0.91,
          },
        ],
        confidence: 0.52,
        confidenceBand: 'Medium',
        promptTokens: 420,
        completionTokens: 52,
        totalTokens: 472,
        promptPreview:
          '[System]: You are a customer service AI...\n[User]: I ordered this 3 weeks ago...',
        rawResponse:
          '{"id":"chatcmpl-abc","choices":[{"message":{"content":"I sincerely apologize..."}}]}',
        piiDetected: [],
        timing: { embed: 42, retrieval: 88, llm: 1240, total: 1370 },
      },
    },
  ],
  bengaliInquiry: [
    {
      id: 's2-1',
      role: 'user',
      body: 'আপনাদের কাছে কি লাল রঙের জামদানি শাড়ি আছে? দাম কত?',
      sentAt: '2026-06-30T09:00:00.000Z',
    },
    {
      id: 's2-2',
      role: 'ai',
      body: 'হ্যাঁ, আমাদের কাছে লাল রঙের জামদানি শাড়ি আছে (SKU: JAM-041)। দাম ৳৩,৫০০ থেকে শুরু। আপনি কি এখন অর্ডার করতে চান?',
      sentAt: '2026-06-30T09:00:05.000Z',
      debugData: {
        chunks: [
          {
            rank: 1,
            document: 'Product Catalog 2026',
            preview: 'জামদানি শাড়ি বিভিন্ন রঙে পাওয়া যায়।',
            fullText:
              'জামদানি শাড়ি বিভিন্ন রঙে পাওয়া যায়। SKU: JAM-040 (নীল), JAM-041 (লাল), JAM-042 (সবুজ)। মূল্য ৳৩,৫০০ – ৳৫,০০০।',
            score: 0.88,
          },
        ],
        confidence: 0.82,
        confidenceBand: 'High',
        promptTokens: 385,
        completionTokens: 48,
        totalTokens: 433,
        promptPreview:
          '[System]: You are a customer service AI...\n[User]: আপনাদের কাছে কি লাল...',
        rawResponse: '{"choices":[{"message":{"content":"হ্যাঁ, আমাদের কাছে..."}}]}',
        piiDetected: [],
        timing: { embed: 38, retrieval: 74, llm: 980, total: 1092 },
      },
    },
  ],
  outOfScope: [
    {
      id: 's3-1',
      role: 'user',
      body: "What's the weather like in Dhaka today?",
      sentAt: '2026-06-30T09:00:00.000Z',
    },
    {
      id: 's3-2',
      role: 'ai',
      body: "I'm sorry, I can only help with questions about our products, orders, and services. For weather information, please check a weather service. Is there anything else I can help you with?",
      sentAt: '2026-06-30T09:00:05.000Z',
      debugData: {
        chunks: [],
        confidence: 0.28,
        confidenceBand: 'Low',
        promptTokens: 310,
        completionTokens: 38,
        totalTokens: 348,
        promptPreview:
          "[System]: You are a customer service AI...\n[User]: What's the weather...",
        rawResponse:
          '{"choices":[{"message":{"content":"I\'m sorry, I can only help..."}}]}',
        piiDetected: [],
        timing: { embed: 35, retrieval: 45, llm: 820, total: 900 },
      },
    },
  ],
  appointment: [
    {
      id: 's4-1',
      role: 'user',
      body: 'I want to book an appointment with your showroom for this Saturday.',
      sentAt: '2026-06-30T09:00:00.000Z',
    },
    {
      id: 's4-2',
      role: 'system',
      body: 'AI calling tool: check_availability(date="2026-07-05", location="Dhaka Showroom")',
      sentAt: '2026-06-30T09:00:03.000Z',
    },
    {
      id: 's4-3',
      role: 'ai',
      body: 'I found available slots for this Saturday at the Dhaka showroom: 10:00 AM, 2:00 PM, or 4:00 PM. Which time works best for you?',
      sentAt: '2026-06-30T09:00:07.000Z',
      debugData: {
        chunks: [
          {
            rank: 1,
            document: 'Showroom Schedule',
            preview: 'Dhaka showroom open Sat 9AM–6PM…',
            fullText: 'Dhaka showroom is open Saturday 9AM–6PM. Appointment slots every 2 hours.',
            score: 0.79,
          },
        ],
        confidence: 0.78,
        confidenceBand: 'Medium',
        promptTokens: 520,
        completionTokens: 55,
        totalTokens: 575,
        promptPreview:
          '[System]: You are a customer service AI with calendar access...\n[Tool Result]: {"slots":["10:00","14:00","16:00"]}',
        rawResponse:
          '{"choices":[{"message":{"content":"I found available slots..."}}]}',
        piiDetected: [],
        timing: { embed: 45, retrieval: 92, llm: 1520, total: 1657 },
      },
    },
  ],
  multiTurn: [
    {
      id: 's5-1',
      role: 'user',
      body: "What's your best seller this month?",
      sentAt: '2026-06-30T09:00:00.000Z',
    },
    {
      id: 's5-2',
      role: 'ai',
      body: 'This month our best seller is the Eid Collection Kurta Set (SKU: EID-2026-K). It comes in 8 colors and has sold over 500 units. Would you like to know more?',
      sentAt: '2026-06-30T09:00:05.000Z',
      debugData: {
        chunks: [
          {
            rank: 1,
            document: 'Product Catalog 2026',
            preview: 'Eid Collection features premium cotton…',
            fullText:
              'Eid Collection Kurta Set: premium cotton, available in 8 colors. SKU: EID-2026-K. Price: ৳1,800.',
            score: 0.92,
          },
          {
            rank: 2,
            document: 'Monthly Sales Report',
            preview: 'Top sellers: EID-2026-K (522 units)…',
            fullText:
              'Top sellers June 2026: 1. EID-2026-K 522 units, 2. JAM-041 344 units, 3. LEH-88 281 units',
            score: 0.85,
          },
        ],
        confidence: 0.89,
        confidenceBand: 'High',
        promptTokens: 610,
        completionTokens: 58,
        totalTokens: 668,
        promptPreview:
          "[System]: You are a customer service AI...\n[User]: What's your best seller...",
        rawResponse:
          '{"choices":[{"message":{"content":"This month our best seller..."}}]}',
        piiDetected: [],
        timing: { embed: 41, retrieval: 86, llm: 1180, total: 1307 },
      },
    },
    {
      id: 's5-3',
      role: 'user',
      body: 'How much does it cost and can I get a discount?',
      sentAt: '2026-06-30T09:01:00.000Z',
    },
    {
      id: 's5-4',
      role: 'ai',
      body: 'The Eid Collection Kurta Set is ৳1,800. For orders of 3 or more, you get a 10% discount automatically. I can also apply a first-time buyer code WELCOME10 for an extra 10% off. Would you like to place an order?',
      sentAt: '2026-06-30T09:01:05.000Z',
      debugData: {
        chunks: [
          {
            rank: 1,
            document: 'Discount Policy',
            preview: 'Bulk discount: 3+ items get 10% off…',
            fullText:
              'Bulk discount: 3 or more items of the same product get 10% off. First-time buyer code: WELCOME10 for 10% off.',
            score: 0.87,
          },
        ],
        confidence: 0.91,
        confidenceBand: 'High',
        promptTokens: 720,
        completionTokens: 68,
        totalTokens: 788,
        promptPreview:
          '[System]: You are a customer service AI...\n[History]: 2 turns\n[User]: How much does it cost...',
        rawResponse:
          '{"choices":[{"message":{"content":"The Eid Collection Kurta Set..."}}]}',
        piiDetected: [{ token: 'WELCOME10', type: 'PROMO_CODE' }],
        timing: { embed: 44, retrieval: 89, llm: 1350, total: 1483 },
      },
    },
  ],
};

// ─── Mock AI response generator ───────────────────────────────────────────────

function generateMockAiResponse(userMessage: string): { body: string; debugData: DebugData } {
  const isBengali = /[ঀ-৿]/.test(userMessage);
  const confidence = parseFloat((0.55 + Math.random() * 0.4).toFixed(2));
  const band: 'High' | 'Medium' | 'Low' =
    confidence >= 0.8 ? 'High' : confidence >= 0.6 ? 'Medium' : 'Low';
  const embed = 30 + Math.round(Math.random() * 30);
  const retrieval = 60 + Math.round(Math.random() * 60);
  const llm = 800 + Math.round(Math.random() * 800);
  return {
    body: isBengali
      ? 'ধন্যবাদ আপনার প্রশ্নের জন্য। আমি আপনার সমস্যা সমাধান করতে পারব। আপনার অর্ডার নম্বরটি দিন।'
      : "Thank you for reaching out. I'm here to help. Could you provide more details about your inquiry?",
    debugData: {
      chunks: [
        {
          rank: 1,
          document: 'Product Catalog 2026',
          preview: 'Our products are available in…',
          fullText:
            'Our products are available in a wide range of categories including clothing, accessories, and home goods.',
          score: parseFloat((0.65 + Math.random() * 0.3).toFixed(2)),
        },
      ],
      confidence,
      confidenceBand: band,
      promptTokens: 300 + Math.round(Math.random() * 200),
      completionTokens: 40 + Math.round(Math.random() * 60),
      totalTokens: 340 + Math.round(Math.random() * 260),
      promptPreview: `[System]: You are a customer service AI...\n[User]: ${userMessage.slice(0, 80)}`,
      rawResponse: JSON.stringify({
        id: 'chatcmpl-' + Math.random().toString(36).slice(2),
        choices: [{ message: { content: 'AI response here' } }],
      }),
      piiDetected: [],
      timing: { embed, retrieval, llm, total: embed + retrieval + llm },
    },
  };
}

// ─── Sub-components ───────────────────────────────────────────────────────────

function getConfidenceColorClass(band: 'High' | 'Medium' | 'Low'): string {
  return band === 'High'
    ? 'text-chatConfidence-high'
    : band === 'Medium'
      ? 'text-chatConfidence-medium'
      : 'text-chatConfidence-low';
}

function ScoreBadge({ score }: { score: number }) {
  const cls =
    score >= 0.8
      ? 'bg-chatConfidence-high/10 text-chatConfidence-high border-chatConfidence-high/20'
      : score >= 0.6
        ? 'bg-chatConfidence-medium/10 text-chatConfidence-medium border-chatConfidence-medium/20'
        : 'bg-destructive/10 text-destructive border-destructive/20';
  return (
    <span className={cn('inline-flex items-center rounded-md border px-1.5 py-0.5 text-xs font-medium tabular-nums', cls)}>
      {score.toFixed(2)}
    </span>
  );
}

function ChunksTab({ debugData }: { debugData: DebugData | null }) {
  const [expanded, setExpanded] = useState<number | null>(null);

  if (!debugData || debugData.chunks.length === 0) {
    return (
      <div className="flex items-center justify-center py-12 text-sm text-muted-foreground">
        No chunks retrieved
      </div>
    );
  }

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead className="w-10">#</TableHead>
          <TableHead>Document</TableHead>
          <TableHead>Preview</TableHead>
          <TableHead className="w-16 text-right">Score</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {debugData.chunks.map((chunk) => (
          <>
            <TableRow key={chunk.rank} className="cursor-pointer hover:bg-muted/40">
              <TableCell className="font-mono text-xs text-muted-foreground">{chunk.rank}</TableCell>
              <TableCell className="text-xs font-medium">{chunk.document}</TableCell>
              <TableCell className="text-xs text-muted-foreground">
                {expanded === chunk.rank ? (
                  <span>{chunk.fullText}</span>
                ) : (
                  <>
                    {chunk.preview.slice(0, 40)}
                    {chunk.preview.length > 40 || chunk.fullText !== chunk.preview ? (
                      <button
                        className="ml-1 text-primary underline-offset-2 hover:underline focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                        onClick={() => setExpanded(expanded === chunk.rank ? null : chunk.rank)}
                        aria-label={expanded === chunk.rank ? 'Collapse chunk text' : 'Expand chunk text'}
                      >
                        {expanded === chunk.rank ? '[collapse]' : '[...]'}
                      </button>
                    ) : null}
                  </>
                )}
                {expanded === chunk.rank && (
                  <button
                    className="ml-1 text-primary underline-offset-2 hover:underline focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                    onClick={() => setExpanded(null)}
                    aria-label="Collapse chunk text"
                  >
                    [collapse]
                  </button>
                )}
              </TableCell>
              <TableCell className="text-right">
                <ScoreBadge score={chunk.score} />
              </TableCell>
            </TableRow>
          </>
        ))}
      </TableBody>
    </Table>
  );
}

function ConfidenceTab({ debugData }: { debugData: DebugData | null }) {
  if (!debugData) {
    return (
      <div className="flex items-center justify-center py-12 text-sm text-muted-foreground">
        No data yet — send a message first
      </div>
    );
  }

  const colorClass = getConfidenceColorClass(debugData.confidenceBand);

  return (
    <div className="space-y-4 p-1">
      <div className="flex flex-col items-center gap-2 py-4">
        <span className={cn('text-5xl font-bold tabular-nums', colorClass)}>
          {(debugData.confidence * 100).toFixed(0)}%
        </span>
        <Badge
          variant="outline"
          className={cn(
            'text-xs',
            debugData.confidenceBand === 'High' && 'border-chatConfidence-high/30 text-chatConfidence-high',
            debugData.confidenceBand === 'Medium' && 'border-chatConfidence-medium/30 text-chatConfidence-medium',
            debugData.confidenceBand === 'Low' && 'border-destructive/30 text-destructive',
          )}
        >
          {debugData.confidenceBand}
        </Badge>
      </div>
      <Separator />
      <div className="space-y-2 text-xs text-muted-foreground">
        <p className="font-mono">Score = retrieval_score × 0.6 + groundedness × 0.4</p>
        <ul className="space-y-1">
          <li>
            <span className="font-medium text-chatConfidence-high">High (≥0.8):</span> answer is well-grounded in retrieved context
          </li>
          <li>
            <span className="font-medium text-chatConfidence-medium">Medium (0.6–0.8):</span> partially grounded; some inference
          </li>
          <li>
            <span className="font-medium text-destructive">Low (&lt;0.6):</span> AI may be guessing — consider improving the knowledge base
          </li>
        </ul>
      </div>
    </div>
  );
}

function PromptTab({ debugData }: { debugData: DebugData | null }) {
  if (!debugData) {
    return (
      <div className="flex items-center justify-center py-12 text-sm text-muted-foreground">
        No data yet
      </div>
    );
  }

  return (
    <div className="space-y-3 p-1">
      <pre className="max-h-80 overflow-auto rounded-md bg-muted p-3 text-xs font-mono leading-relaxed whitespace-pre-wrap">
        {debugData.promptPreview}
      </pre>
      <p className="text-xs text-muted-foreground tabular-nums">
        Prompt tokens: <span className="font-medium text-foreground">{debugData.promptTokens.toLocaleString()}</span>
      </p>
    </div>
  );
}

function ResponseTab({ debugData }: { debugData: DebugData | null }) {
  if (!debugData) {
    return (
      <div className="flex items-center justify-center py-12 text-sm text-muted-foreground">
        No data yet
      </div>
    );
  }

  let prettyJson = debugData.rawResponse;
  try {
    prettyJson = JSON.stringify(JSON.parse(debugData.rawResponse), null, 2);
  } catch {
    // keep raw
  }

  return (
    <div className="space-y-3 p-1">
      <pre className="max-h-56 overflow-auto rounded-md bg-muted p-3 text-xs font-mono leading-relaxed whitespace-pre-wrap">
        {prettyJson}
      </pre>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Type</TableHead>
            <TableHead className="text-right">Tokens</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <TableRow>
            <TableCell className="text-xs">Prompt</TableCell>
            <TableCell className="text-right text-xs tabular-nums">{debugData.promptTokens.toLocaleString()}</TableCell>
          </TableRow>
          <TableRow>
            <TableCell className="text-xs">Completion</TableCell>
            <TableCell className="text-right text-xs tabular-nums">{debugData.completionTokens.toLocaleString()}</TableCell>
          </TableRow>
          <TableRow>
            <TableCell className="text-xs font-semibold">Total</TableCell>
            <TableCell className="text-right text-xs font-semibold tabular-nums">{debugData.totalTokens.toLocaleString()}</TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </div>
  );
}

function PiiTab({ debugData }: { debugData: DebugData | null }) {
  if (!debugData || debugData.piiDetected.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center gap-2 py-12 text-muted-foreground">
        <Shield className="h-8 w-8 opacity-40" aria-hidden />
        <span className="text-sm">No PII detected</span>
      </div>
    );
  }

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Token</TableHead>
          <TableHead>Type</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {debugData.piiDetected.map((item, i) => (
          <TableRow key={i}>
            <TableCell className="font-mono text-xs">{item.token}</TableCell>
            <TableCell>
              <Badge variant="secondary" className="text-xs">{item.type}</Badge>
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

function PerformanceTab({ debugData }: { debugData: DebugData | null }) {
  if (!debugData) {
    return (
      <div className="flex items-center justify-center py-12 text-sm text-muted-foreground">
        No data yet
      </div>
    );
  }

  const t = debugData.timing;
  const stages: { label: string; key: keyof typeof t; color: string }[] = [
    { label: 'Embedding', key: 'embed', color: 'bg-primary' },
    { label: 'Retrieval', key: 'retrieval', color: 'bg-chatState-ai' },
    { label: 'LLM', key: 'llm', color: 'bg-chatConfidence-medium' },
  ];

  return (
    <div className="space-y-3 p-1">
      {stages.map(({ label, key, color }) => {
        const ms = t[key];
        const pct = ((ms / t.total) * 100).toFixed(0);
        return (
          <div key={key} className="flex items-center gap-2">
            <span className="w-20 shrink-0 text-xs text-muted-foreground">{label}</span>
            <div className="flex-1 overflow-hidden rounded-full bg-muted h-2">
              <div
                className={cn('h-full rounded-full transition-all', color)}
                style={{ width: `${pct}%` }}
                aria-label={`${label}: ${ms}ms`}
              />
            </div>
            <span className="w-14 shrink-0 text-right text-xs tabular-nums text-muted-foreground">
              {ms.toLocaleString()}ms
            </span>
          </div>
        );
      })}
      <Separator />
      <div className="flex items-center gap-2">
        <span className="w-20 shrink-0 text-xs font-semibold">Total</span>
        <div className="flex-1" />
        <span className="w-14 shrink-0 text-right text-xs font-semibold tabular-nums">
          {t.total.toLocaleString()}ms
        </span>
      </div>
    </div>
  );
}

function DebugPanel({
  debugData,
  open,
  onToggle,
}: {
  debugData: DebugData | null;
  open: boolean;
  onToggle: () => void;
}) {
  return (
    <div
      className={cn(
        'flex flex-col border-l border-border bg-card transition-all duration-200',
        open ? 'w-full md:w-[40%]' : 'w-0 overflow-hidden border-l-0',
      )}
      aria-label="Debug panel"
    >
      {open && (
        <>
          <div className="flex items-center justify-between border-b border-border px-3 py-2">
            <div className="flex items-center gap-1.5">
              <Bug className="h-3.5 w-3.5 text-muted-foreground" aria-hidden />
              <span className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">Debug</span>
            </div>
            <Button
              variant="ghost"
              size="sm"
              className="h-6 w-6 p-0"
              onClick={onToggle}
              aria-label="Hide debug panel"
            >
              <ChevronRight className="h-3.5 w-3.5" aria-hidden />
            </Button>
          </div>
          <ScrollArea className="flex-1">
            <div className="p-2">
              <Tabs defaultValue="chunks">
                <TabsList className="w-full grid grid-cols-3 h-auto mb-3">
                  <TabsTrigger value="chunks" className="text-xs py-1">Chunks</TabsTrigger>
                  <TabsTrigger value="confidence" className="text-xs py-1">Confidence</TabsTrigger>
                  <TabsTrigger value="prompt" className="text-xs py-1">Prompt</TabsTrigger>
                </TabsList>
                <TabsList className="w-full grid grid-cols-3 h-auto mb-3">
                  <TabsTrigger value="response" className="text-xs py-1">Response</TabsTrigger>
                  <TabsTrigger value="pii" className="text-xs py-1">PII</TabsTrigger>
                  <TabsTrigger value="performance" className="text-xs py-1">Perf</TabsTrigger>
                </TabsList>
                <TabsContent value="chunks">
                  <ChunksTab debugData={debugData} />
                </TabsContent>
                <TabsContent value="confidence">
                  <ConfidenceTab debugData={debugData} />
                </TabsContent>
                <TabsContent value="prompt">
                  <PromptTab debugData={debugData} />
                </TabsContent>
                <TabsContent value="response">
                  <ResponseTab debugData={debugData} />
                </TabsContent>
                <TabsContent value="pii">
                  <PiiTab debugData={debugData} />
                </TabsContent>
                <TabsContent value="performance">
                  <PerformanceTab debugData={debugData} />
                </TabsContent>
              </Tabs>
            </div>
          </ScrollArea>
        </>
      )}
    </div>
  );
}

function MessageRow({ msg }: { msg: PlaygroundMessage }) {
  if (msg.role === 'system') {
    return (
      <div className="flex justify-center py-1">
        <span className="rounded-full bg-muted px-3 py-1 text-xs text-muted-foreground">
          {msg.body}
        </span>
      </div>
    );
  }

  if (msg.role === 'user') {
    return (
      <div className="flex justify-end gap-2">
        <div className="max-w-[70%] space-y-0.5">
          <div className="flex justify-end">
            <span className="text-xs font-medium text-muted-foreground">You (test)</span>
          </div>
          <div className="rounded-2xl rounded-tr-sm bg-muted px-3 py-2 text-sm leading-relaxed text-foreground">
            {msg.body}
          </div>
          <div className="flex justify-end">
            <time
              dateTime={msg.sentAt}
              className="text-[0.625rem] text-muted-foreground/60 tabular-nums"
              suppressHydrationWarning
            >
              {new Date(msg.sentAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
            </time>
          </div>
        </div>
        <div
          aria-hidden
          className="mt-1 h-6 w-6 shrink-0 rounded-full bg-muted-foreground/30 text-[0.5rem] flex items-center justify-center font-bold text-background"
        >
          U
        </div>
      </div>
    );
  }

  // AI message
  const confidence = msg.debugData?.confidence;
  const band = msg.debugData?.confidenceBand;
  const colorClass = band ? getConfidenceColorClass(band) : undefined;

  return (
    <div className="flex gap-2">
      <div
        aria-hidden
        className="mt-1 h-6 w-6 shrink-0 rounded-full bg-chatState-ai text-[0.5rem] flex items-center justify-center font-bold text-background"
      >
        AI
      </div>
      <div className="max-w-[70%] space-y-0.5">
        <span className="text-xs font-medium text-chatState-ai">AI Assistant</span>
        <div className="rounded-2xl rounded-tl-sm border border-chatState-ai/20 bg-chatState-ai/5 px-3 py-2 text-sm leading-relaxed text-foreground">
          {msg.isStreaming ? (
            <span className="animate-pulse text-muted-foreground">▌</span>
          ) : (
            msg.body
          )}
        </div>
        <div className="flex items-center gap-2">
          <time
            dateTime={msg.sentAt}
            className="text-[0.625rem] text-muted-foreground/60 tabular-nums"
            suppressHydrationWarning
          >
            {new Date(msg.sentAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
          </time>
          {confidence != null && colorClass && (
            <span className={cn('text-[0.625rem] font-medium tabular-nums', colorClass)}>
              {(confidence * 100).toFixed(0)}% conf
            </span>
          )}
        </div>
      </div>
    </div>
  );
}

function ComparePaneConfig({
  paneIndex,
  provider,
  setProvider,
  model,
  setModel,
}: {
  paneIndex: number;
  provider: string;
  setProvider: (v: string) => void;
  model: string;
  setModel: (v: string) => void;
}) {
  return (
    <div className="flex flex-col gap-1.5 border-b border-border bg-muted/30 px-3 py-2">
      <span className="text-[0.625rem] font-semibold uppercase tracking-wide text-muted-foreground">
        Pane {paneIndex + 1}
      </span>
      <div className="flex flex-wrap gap-2">
        <Select value={provider} onValueChange={setProvider}>
          <SelectTrigger className="h-7 w-36 text-xs" aria-label="Provider">
            <SelectValue placeholder="Provider" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="openrouter">OpenRouter</SelectItem>
            <SelectItem value="anthropic">Anthropic</SelectItem>
            <SelectItem value="openai">OpenAI</SelectItem>
          </SelectContent>
        </Select>
        <Input
          className="h-7 w-44 text-xs"
          placeholder="Model name"
          value={model}
          onChange={(e) => setModel(e.target.value)}
          aria-label="Model name"
        />
      </div>
    </div>
  );
}

// ─── Main page ────────────────────────────────────────────────────────────────

export default function PlaygroundPage() {
  const [messages, setMessages] = useState<PlaygroundMessage[]>([]);
  const [compareLeftMessages, setCompareLeftMessages] = useState<PlaygroundMessage[]>([]);
  const [compareRightMessages, setCompareRightMessages] = useState<PlaygroundMessage[]>([]);
  const [sessionId, setSessionId] = useState(() => Math.random().toString(36).slice(2, 10));
  const [currentDebugData, setCurrentDebugData] = useState<DebugData | null>(null);
  const [inputText, setInputText] = useState('');
  const [compareMode, setCompareMode] = useState(false);
  const [debugOpen, setDebugOpen] = useState(true);
  const [controlsOpen, setControlsOpen] = useState(false);
  const [copied, setCopied] = useState(false);

  // Controls state
  const [sysPromptOn, setSysPromptOn] = useState(false);
  const [sysPrompt, setSysPrompt] = useState('');
  const [confidenceOverrideOn, setConfidenceOverrideOn] = useState(false);
  const [confidenceThreshold, setConfidenceThreshold] = useState(60);
  const [knowledgeVersion, setKnowledgeVersion] = useState('current');
  const [mcpDryRun, setMcpDryRun] = useState(false);

  // Compare pane configs
  const [leftProvider, setLeftProvider] = useState('openrouter');
  const [leftModel, setLeftModel] = useState('anthropic/claude-3-haiku');
  const [rightProvider, setRightProvider] = useState('openai');
  const [rightModel, setRightModel] = useState('gpt-4o-mini');

  const scrollRef = useRef<HTMLDivElement>(null);
  const leftScrollRef = useRef<HTMLDivElement>(null);
  const rightScrollRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const scrollToBottom = useCallback((ref: React.RefObject<HTMLDivElement | null>) => {
    ref.current?.scrollTo({ top: ref.current.scrollHeight, behavior: 'smooth' });
  }, []);

  useEffect(() => {
    scrollToBottom(scrollRef);
  }, [messages, scrollToBottom]);

  useEffect(() => {
    scrollToBottom(leftScrollRef);
    scrollToBottom(rightScrollRef);
  }, [compareLeftMessages, compareRightMessages, scrollToBottom]);

  function newSession() {
    setMessages([]);
    setCompareLeftMessages([]);
    setCompareRightMessages([]);
    setCurrentDebugData(null);
    setSessionId(Math.random().toString(36).slice(2, 10));
    inputRef.current?.focus();
  }

  function shareSession() {
    const url = `${window.location.href.split('?')[0]}?session=${sessionId}`;
    navigator.clipboard.writeText(url).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    });
  }

  function loadScenario(key: string) {
    const scenario = SCENARIOS[key];
    if (!scenario) return;
    setMessages(scenario);
    const lastAiMsg = [...scenario].reverse().find((m) => m.role === 'ai');
    if (lastAiMsg?.debugData) setCurrentDebugData(lastAiMsg.debugData);
  }

  const sendMessage = useCallback(() => {
    const body = inputText.trim();
    if (!body) return;
    setInputText('');

    const userMsg: PlaygroundMessage = {
      id: crypto.randomUUID(),
      role: 'user',
      body,
      sentAt: new Date().toISOString(),
    };

    const streamId = crypto.randomUUID();
    const streamMsg: PlaygroundMessage = {
      id: streamId,
      role: 'ai',
      body: '',
      isStreaming: true,
      sentAt: new Date().toISOString(),
    };

    if (compareMode) {
      setCompareLeftMessages((prev) => [...prev, userMsg, streamMsg]);
      setCompareRightMessages((prev) => [
        ...prev,
        userMsg,
        { ...streamMsg, id: crypto.randomUUID() },
      ]);

      setTimeout(() => {
        const { body: aiBody, debugData } = generateMockAiResponse(body);
        const finalId = crypto.randomUUID();
        const finalMsg: PlaygroundMessage = {
          id: finalId,
          role: 'ai',
          body: aiBody,
          debugData,
          sentAt: new Date().toISOString(),
        };
        setCompareLeftMessages((prev) => prev.map((m) => (m.isStreaming ? finalMsg : m)));
        setCurrentDebugData(debugData);
      }, 800);

      setTimeout(() => {
        const { body: aiBody2, debugData: debugData2 } = generateMockAiResponse(body);
        const finalMsg2: PlaygroundMessage = {
          id: crypto.randomUUID(),
          role: 'ai',
          body: aiBody2,
          debugData: debugData2,
          sentAt: new Date().toISOString(),
        };
        setCompareRightMessages((prev) => prev.map((m) => (m.isStreaming ? finalMsg2 : m)));
      }, 1000);
    } else {
      setMessages((prev) => [...prev, userMsg, streamMsg]);

      setTimeout(() => {
        const { body: aiBody, debugData } = generateMockAiResponse(body);
        const finalMsg: PlaygroundMessage = {
          id: streamId,
          role: 'ai',
          body: aiBody,
          debugData,
          sentAt: new Date().toISOString(),
        };
        setMessages((prev) => prev.map((m) => (m.id === streamId ? finalMsg : m)));
        setCurrentDebugData(debugData);
      }, 800 + 1200);
    }

    inputRef.current?.focus();
  }, [inputText, compareMode]);

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      sendMessage();
    }
  }

  function renderChatList(msgs: PlaygroundMessage[]) {
    return (
      <div className="flex flex-col gap-3 p-4">
        {msgs.length === 0 && (
          <div className="flex flex-col items-center justify-center gap-2 py-16 text-center text-muted-foreground">
            <span className="text-sm">No messages yet</span>
            <span className="text-xs">Type a message below or load a scenario from Controls</span>
          </div>
        )}
        {msgs.map((msg) => (
          <MessageRow key={msg.id} msg={msg} />
        ))}
      </div>
    );
  }

  return (
    <div className="flex h-full flex-col overflow-hidden bg-background">
      {/* Header bar */}
      <header className="flex shrink-0 items-center justify-between gap-3 border-b border-border px-4 py-2.5">
        <div className="flex items-center gap-2 min-w-0">
          <span className="text-sm font-semibold text-foreground truncate">Test Playground</span>
          <Badge variant="outline" className="shrink-0 text-xs">Sandbox</Badge>
        </div>
        <div className="flex items-center gap-1.5 shrink-0">
          <Button
            variant="ghost"
            size="sm"
            className="h-7 gap-1.5 px-2.5 text-xs"
            onClick={newSession}
            aria-label="Start new session"
          >
            <RefreshCw className="h-3.5 w-3.5" aria-hidden />
            <span className="hidden sm:inline">New Session</span>
          </Button>
          <Button
            variant="ghost"
            size="sm"
            className="h-7 gap-1.5 px-2.5 text-xs"
            onClick={shareSession}
            aria-label="Copy session share link"
          >
            {copied ? (
              <Check className="h-3.5 w-3.5 text-chatConfidence-high" aria-hidden />
            ) : (
              <Copy className="h-3.5 w-3.5" aria-hidden />
            )}
            <span className="hidden sm:inline">{copied ? 'Copied!' : 'Share Session'}</span>
          </Button>
          <Button
            variant={compareMode ? 'default' : 'ghost'}
            size="sm"
            className="h-7 gap-1.5 px-2.5 text-xs"
            onClick={() => setCompareMode((v) => !v)}
            aria-label={compareMode ? 'Disable compare mode' : 'Enable compare mode'}
            aria-pressed={compareMode}
          >
            <GitCompare className="h-3.5 w-3.5" aria-hidden />
            <span className="hidden sm:inline">Compare Mode</span>
          </Button>
          {!debugOpen && (
            <Button
              variant="outline"
              size="sm"
              className="h-7 gap-1.5 px-2.5 text-xs"
              onClick={() => setDebugOpen(true)}
              aria-label="Show debug panel"
            >
              <Bug className="h-3.5 w-3.5" aria-hidden />
              <ChevronLeft className="h-3 w-3" aria-hidden />
            </Button>
          )}
        </div>
      </header>

      {/* Controls bar */}
      <div className="shrink-0 border-b border-border bg-muted/20">
        <button
          className="flex w-full items-center justify-between px-4 py-2 text-xs font-medium text-muted-foreground hover:text-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
          onClick={() => setControlsOpen((v) => !v)}
          aria-expanded={controlsOpen}
          aria-controls="controls-panel"
        >
          <span>Controls</span>
          {controlsOpen ? (
            <ChevronUp className="h-3.5 w-3.5" aria-hidden />
          ) : (
            <ChevronDown className="h-3.5 w-3.5" aria-hidden />
          )}
        </button>

        {controlsOpen && (
          <div
            id="controls-panel"
            className="flex flex-col gap-4 px-4 pb-4 md:flex-row md:flex-wrap md:items-start"
          >
            {/* System prompt override */}
            <div className="flex flex-col gap-2">
              <div className="flex items-center gap-2">
                <Switch
                  id="sys-prompt-switch"
                  checked={sysPromptOn}
                  onCheckedChange={setSysPromptOn}
                  aria-label="Enable system prompt override"
                />
                <Label htmlFor="sys-prompt-switch" className="text-xs cursor-pointer">
                  System prompt override
                </Label>
              </div>
              {sysPromptOn && (
                <Textarea
                  rows={3}
                  className="w-full min-w-64 text-xs"
                  placeholder="Override system prompt…"
                  value={sysPrompt}
                  onChange={(e) => setSysPrompt(e.target.value)}
                  aria-label="System prompt override text"
                />
              )}
            </div>

            {/* Confidence threshold */}
            <div className="flex flex-col gap-2">
              <div className="flex items-center gap-2">
                <Switch
                  id="conf-switch"
                  checked={confidenceOverrideOn}
                  onCheckedChange={setConfidenceOverrideOn}
                  aria-label="Enable confidence threshold override"
                />
                <Label htmlFor="conf-switch" className="text-xs cursor-pointer">
                  Confidence threshold override
                </Label>
              </div>
              {confidenceOverrideOn && (
                <div className="flex items-center gap-3 min-w-48">
                  <Slider
                    min={0}
                    max={100}
                    step={1}
                    value={[confidenceThreshold]}
                    onValueChange={([v]) => { if (v !== undefined) setConfidenceThreshold(v); }}
                    aria-label={`Confidence threshold: ${confidenceThreshold}%`}
                    className="flex-1"
                  />
                  <span className="w-9 text-right text-xs tabular-nums text-muted-foreground">
                    {confidenceThreshold}%
                  </span>
                </div>
              )}
            </div>

            {/* Knowledge version */}
            <div className="flex flex-col gap-1.5">
              <Label className="text-xs text-muted-foreground">Knowledge version</Label>
              <Select value={knowledgeVersion} onValueChange={setKnowledgeVersion}>
                <SelectTrigger className="h-8 w-36 text-xs" aria-label="Knowledge version">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="current">Current</SelectItem>
                  <SelectItem value="v3">v3</SelectItem>
                  <SelectItem value="v2">v2</SelectItem>
                  <SelectItem value="v1">v1</SelectItem>
                </SelectContent>
              </Select>
            </div>

            {/* MCP dry-run */}
            <div className="flex items-center gap-2 pt-4 md:pt-5">
              <Switch
                id="mcp-switch"
                checked={mcpDryRun}
                onCheckedChange={setMcpDryRun}
                aria-label="Enable MCP dry-run mode"
              />
              <Label htmlFor="mcp-switch" className="text-xs cursor-pointer">
                MCP dry-run
              </Label>
            </div>

            {/* Scenario library */}
            <div className="flex flex-col gap-1.5">
              <Label className="text-xs text-muted-foreground">Scenario library</Label>
              <Select onValueChange={loadScenario}>
                <SelectTrigger className="h-8 w-72 text-xs" aria-label="Load scenario">
                  <SelectValue placeholder="Load a scenario →" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="angryRefund">Angry customer requesting refund</SelectItem>
                  <SelectItem value="bengaliInquiry">Product inquiry in Bengali</SelectItem>
                  <SelectItem value="outOfScope">Out-of-scope question</SelectItem>
                  <SelectItem value="appointment">Appointment booking via MCP tool</SelectItem>
                  <SelectItem value="multiTurn">Multi-turn conversation</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
        )}
      </div>

      {/* Body: chat + debug */}
      <div className="flex min-h-0 flex-1">
        {/* Chat area */}
        <div className="flex min-w-0 flex-1 flex-col">
          {compareMode ? (
            /* Compare mode: two panes */
            <div className="flex min-h-0 flex-1 gap-0">
              {/* Left pane */}
              <div className="flex flex-1 min-w-0 flex-col border-r border-border">
                <ComparePaneConfig
                  paneIndex={0}
                  provider={leftProvider}
                  setProvider={setLeftProvider}
                  model={leftModel}
                  setModel={setLeftModel}
                />
                <div
                  ref={leftScrollRef}
                  className="flex-1 overflow-y-auto"
                  aria-label="Left pane chat messages"
                >
                  {renderChatList(compareLeftMessages)}
                </div>
              </div>

              {/* Right pane */}
              <div className="flex flex-1 min-w-0 flex-col">
                <ComparePaneConfig
                  paneIndex={1}
                  provider={rightProvider}
                  setProvider={setRightProvider}
                  model={rightModel}
                  setModel={setRightModel}
                />
                <div
                  ref={rightScrollRef}
                  className="flex-1 overflow-y-auto"
                  aria-label="Right pane chat messages"
                >
                  {renderChatList(compareRightMessages)}
                </div>
              </div>
            </div>
          ) : (
            /* Standard mode */
            <div
              ref={scrollRef}
              className="flex-1 overflow-y-auto"
              aria-label="Chat messages"
              aria-live="polite"
              aria-relevant="additions"
            >
              {renderChatList(messages)}
            </div>
          )}

          {/* Input bar */}
          <div className="shrink-0 border-t border-border bg-background p-3">
            <div className="flex items-center gap-2">
              <Button
                variant="ghost"
                size="sm"
                className="h-8 shrink-0 gap-1.5 px-2 text-xs text-muted-foreground hover:text-destructive"
                onClick={() => {
                  setMessages([]);
                  setCompareLeftMessages([]);
                  setCompareRightMessages([]);
                  setCurrentDebugData(null);
                }}
                aria-label="Clear conversation"
              >
                <Trash2 className="h-3.5 w-3.5" aria-hidden />
                <span className="hidden sm:inline">Clear</span>
              </Button>
              <Input
                ref={inputRef}
                className="flex-1 h-8 text-sm"
                placeholder="Type a message…"
                value={inputText}
                onChange={(e) => setInputText(e.target.value)}
                onKeyDown={handleKeyDown}
                aria-label="Message input"
              />
              <Button
                size="sm"
                className="h-8 shrink-0 px-4 text-xs"
                onClick={sendMessage}
                disabled={!inputText.trim()}
                aria-label="Send message"
              >
                Send
              </Button>
            </div>
          </div>
        </div>

        {/* Debug panel */}
        <DebugPanel
          debugData={currentDebugData}
          open={debugOpen}
          onToggle={() => setDebugOpen(false)}
        />
      </div>
    </div>
  );
}
