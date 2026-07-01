'use client';

import { useState, useRef, useEffect, useCallback } from 'react';
import {
  Shield,
  ChevronDown,
  ChevronUp,
  ChevronRight,
  Copy,
  Check,
  Trash2,
  RefreshCw,
  GitCompare,
  Bug,
  Send,
  FlaskConical,
  Zap,
  Globe,
  BookOpen,
  AlertTriangle,
  Calendar,
  MessageSquare,
  X,
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
  streamedBody?: string; // partial text while streaming
  isStreaming?: boolean;
  debugData?: DebugData;
  sentAt: string;
}

// ─── Scenario data ────────────────────────────────────────────────────────────

interface ScenarioMeta {
  key: string;
  label: string;
  description: string;
  icon: React.ElementType;
  tag: string;
  tagColor: string;
}

const SCENARIO_META: ScenarioMeta[] = [
  {
    key: 'angryRefund',
    label: 'Angry Refund',
    description: 'Customer frustrated about a delayed refund.',
    icon: AlertTriangle,
    tag: 'Escalation',
    tagColor: 'text-destructive border-destructive/30 bg-destructive/5',
  },
  {
    key: 'bengaliInquiry',
    label: 'Bengali Product Query',
    description: 'জামদানি শাড়ি সম্পর্কে পণ্য অনুসন্ধান।',
    icon: Globe,
    tag: 'Bengali',
    tagColor: 'text-chatState-ai border-chatState-ai/30 bg-chatState-ai/5',
  },
  {
    key: 'outOfScope',
    label: 'Out-of-Scope',
    description: 'Question the AI should politely decline.',
    icon: X,
    tag: 'Low Conf',
    tagColor: 'text-chatConfidence-low border-chatConfidence-low/30 bg-chatConfidence-low/5',
  },
  {
    key: 'appointment',
    label: 'Appointment Booking',
    description: 'Booking via MCP tool call.',
    icon: Calendar,
    tag: 'Tool Use',
    tagColor: 'text-chatState-human border-chatState-human/30 bg-chatState-human/5',
  },
  {
    key: 'multiTurn',
    label: 'Multi-Turn',
    description: 'Product inquiry with discount negotiation.',
    icon: MessageSquare,
    tag: 'Multi-turn',
    tagColor: 'text-chatConfidence-medium border-chatConfidence-medium/30 bg-chatConfidence-medium/5',
  },
];

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
        promptPreview: '[System]: You are a customer service AI...\n[User]: I ordered this 3 weeks ago...',
        rawResponse: '{"id":"chatcmpl-abc","choices":[{"message":{"content":"I sincerely apologize..."}}]}',
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
        promptPreview: '[System]: You are a customer service AI...\n[User]: আপনাদের কাছে কি লাল...',
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
        promptPreview: "[System]: You are a customer service AI...\n[User]: What's the weather...",
        rawResponse: '{"choices":[{"message":{"content":"I\'m sorry, I can only help..."}}]}',
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
        rawResponse: '{"choices":[{"message":{"content":"I found available slots..."}}]}',
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
            fullText: 'Eid Collection Kurta Set: premium cotton, available in 8 colors. SKU: EID-2026-K. Price: ৳1,800.',
            score: 0.92,
          },
          {
            rank: 2,
            document: 'Monthly Sales Report',
            preview: 'Top sellers: EID-2026-K (522 units)…',
            fullText: 'Top sellers June 2026: 1. EID-2026-K 522 units, 2. JAM-041 344 units, 3. LEH-88 281 units',
            score: 0.85,
          },
        ],
        confidence: 0.89,
        confidenceBand: 'High',
        promptTokens: 610,
        completionTokens: 58,
        totalTokens: 668,
        promptPreview: "[System]: You are a customer service AI...\n[User]: What's your best seller...",
        rawResponse: '{"choices":[{"message":{"content":"This month our best seller..."}}]}',
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
            fullText: 'Bulk discount: 3 or more items of the same product get 10% off. First-time buyer code: WELCOME10 for 10% off.',
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
        rawResponse: '{"choices":[{"message":{"content":"The Eid Collection Kurta Set..."}}]}',
        piiDetected: [{ token: 'WELCOME10', type: 'PROMO_CODE' }],
        timing: { embed: 44, retrieval: 89, llm: 1350, total: 1483 },
      },
    },
  ],
};

// ─── Mock AI response generator ───────────────────────────────────────────────

let _responseCounter = 0;
function nextId(): string {
  _responseCounter += 1;
  return `mock-${_responseCounter}`;
}

function generateMockAiResponse(userMessage: string): { body: string; debugData: DebugData } {
  const isBengali = /[ঀ-৿]/.test(userMessage);
  const confidence = parseFloat((0.55 + (_responseCounter * 0.07 % 0.4)).toFixed(2));
  const band: 'High' | 'Medium' | 'Low' =
    confidence >= 0.8 ? 'High' : confidence >= 0.6 ? 'Medium' : 'Low';
  const embed = 30 + (_responseCounter * 11 % 30);
  const retrieval = 60 + (_responseCounter * 17 % 60);
  const llm = 800 + (_responseCounter * 113 % 800);
  return {
    body: isBengali
      ? 'ধন্যবাদ আপনার প্রশ্নের জন্য। আমি আপনার সমস্যা সমাধান করতে পারব। আপনার অর্ডার নম্বরটি দিন এবং আমি এখনই দেখছি।'
      : "Thank you for reaching out! I'm here to help. Could you provide more details about your inquiry so I can assist you better?",
    debugData: {
      chunks: [
        {
          rank: 1,
          document: 'Product Catalog 2026',
          preview: 'Our products are available in…',
          fullText:
            'Our products are available in a wide range of categories including clothing, accessories, and home goods.',
          score: parseFloat((0.65 + (_responseCounter * 0.05 % 0.3)).toFixed(2)),
        },
      ],
      confidence,
      confidenceBand: band,
      promptTokens: 300 + (_responseCounter * 37 % 200),
      completionTokens: 40 + (_responseCounter * 13 % 60),
      totalTokens: 340 + (_responseCounter * 50 % 260),
      promptPreview: `[System]: You are a customer service AI...\n[User]: ${userMessage.slice(0, 80)}`,
      rawResponse: JSON.stringify({
        id: `chatcmpl-${nextId()}`,
        choices: [{ message: { content: 'AI response here' } }],
      }),
      piiDetected: [],
      timing: { embed, retrieval, llm, total: embed + retrieval + llm },
    },
  };
}

// ─── Confidence helpers ───────────────────────────────────────────────────────

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

// ─── Debug sub-tabs ───────────────────────────────────────────────────────────

function ChunksTab({ debugData }: { debugData: DebugData | null }) {
  const [expanded, setExpanded] = useState<number | null>(null);

  if (!debugData || debugData.chunks.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center gap-2 py-10 text-center">
        <BookOpen className="h-7 w-7 text-muted-foreground/40" aria-hidden />
        <span className="text-sm text-muted-foreground">No chunks retrieved</span>
        <span className="text-xs text-muted-foreground/60">
          {debugData ? 'Question was out-of-scope or no knowledge matched.' : 'Send a message first.'}
        </span>
      </div>
    );
  }

  return (
    <div className="overflow-auto">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className="w-8">#</TableHead>
            <TableHead>Document</TableHead>
            <TableHead>Preview</TableHead>
            <TableHead className="w-14 text-right">Score</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {debugData.chunks.map((chunk) => (
            <TableRow key={chunk.rank}>
              <TableCell className="font-mono text-xs text-muted-foreground">{chunk.rank}</TableCell>
              <TableCell className="text-xs font-medium">{chunk.document}</TableCell>
              <TableCell className="text-xs text-muted-foreground">
                {expanded === chunk.rank ? (
                  <>
                    <span>{chunk.fullText}</span>
                    <button
                      className="ml-1 text-primary hover:underline underline-offset-2 focus-visible:outline-none"
                      onClick={() => setExpanded(null)}
                    >
                      [collapse]
                    </button>
                  </>
                ) : (
                  <>
                    {chunk.preview.slice(0, 45)}
                    {(chunk.preview.length > 45 || chunk.fullText !== chunk.preview) && (
                      <button
                        className="ml-1 text-primary hover:underline underline-offset-2 focus-visible:outline-none"
                        onClick={() => setExpanded(chunk.rank)}
                        aria-label="Expand chunk text"
                      >
                        [...]
                      </button>
                    )}
                  </>
                )}
              </TableCell>
              <TableCell className="text-right">
                <ScoreBadge score={chunk.score} />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}

function ConfidenceTab({ debugData }: { debugData: DebugData | null }) {
  if (!debugData) {
    return (
      <div className="flex items-center justify-center py-10 text-sm text-muted-foreground">
        Send a message to see confidence.
      </div>
    );
  }

  const colorClass = getConfidenceColorClass(debugData.confidenceBand);

  return (
    <div className="space-y-4 p-1">
      <div className="flex flex-col items-center gap-2 py-5">
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
          {debugData.confidenceBand} Confidence
        </Badge>
      </div>
      <Separator />
      <div className="space-y-2 text-xs text-muted-foreground">
        <p className="font-mono text-[0.6875rem]">score = retrieval × 0.6 + groundedness × 0.4</p>
        <div className="space-y-1.5">
          {[
            { label: 'High ≥ 0.80', color: 'bg-chatConfidence-high', desc: 'Well-grounded in context' },
            { label: 'Medium 0.60–0.79', color: 'bg-chatConfidence-medium', desc: 'Some inference involved' },
            { label: 'Low < 0.60', color: 'bg-chatConfidence-low', desc: 'AI may be guessing' },
          ].map(({ label, color, desc }) => (
            <div key={label} className="flex items-center gap-2">
              <div className={cn('h-2 w-2 shrink-0 rounded-full', color)} aria-hidden />
              <span className="font-medium text-foreground">{label}</span>
              <span>— {desc}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function PromptTab({ debugData }: { debugData: DebugData | null }) {
  if (!debugData) {
    return (
      <div className="flex items-center justify-center py-10 text-sm text-muted-foreground">
        No data yet
      </div>
    );
  }

  return (
    <div className="space-y-3 p-1">
      <pre className="max-h-72 overflow-auto rounded-md bg-muted p-3 text-xs font-mono leading-relaxed whitespace-pre-wrap break-words">
        {debugData.promptPreview}
      </pre>
      <p className="text-xs text-muted-foreground">
        Prompt tokens:{' '}
        <span className="font-semibold tabular-nums text-foreground">
          {debugData.promptTokens.toLocaleString()}
        </span>
      </p>
    </div>
  );
}

function ResponseTab({ debugData }: { debugData: DebugData | null }) {
  if (!debugData) {
    return (
      <div className="flex items-center justify-center py-10 text-sm text-muted-foreground">
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
      <pre className="max-h-48 overflow-auto rounded-md bg-muted p-3 text-xs font-mono leading-relaxed whitespace-pre-wrap break-words">
        {prettyJson}
      </pre>
      <div className="grid grid-cols-3 gap-2">
        {[
          { label: 'Prompt', value: debugData.promptTokens },
          { label: 'Completion', value: debugData.completionTokens },
          { label: 'Total', value: debugData.totalTokens },
        ].map(({ label, value }) => (
          <div key={label} className="rounded-md border border-border bg-muted/40 p-2 text-center">
            <div className="text-[0.625rem] text-muted-foreground">{label}</div>
            <div className="text-sm font-semibold tabular-nums">{value.toLocaleString()}</div>
          </div>
        ))}
      </div>
    </div>
  );
}

function PiiTab({ debugData }: { debugData: DebugData | null }) {
  if (!debugData || debugData.piiDetected.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center gap-2 py-10 text-center text-muted-foreground">
        <Shield className="h-8 w-8 opacity-30" aria-hidden />
        <span className="text-sm">No PII detected</span>
        {debugData && (
          <span className="text-xs text-muted-foreground/60">All tokens passed the PII filter cleanly.</span>
        )}
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
      <div className="flex items-center justify-center py-10 text-sm text-muted-foreground">
        No data yet
      </div>
    );
  }

  const t = debugData.timing;
  const stages = [
    { label: 'Embedding', key: 'embed' as const, color: 'bg-primary' },
    { label: 'Retrieval', key: 'retrieval' as const, color: 'bg-chatState-ai' },
    { label: 'LLM', key: 'llm' as const, color: 'bg-chatConfidence-medium' },
  ];

  return (
    <div className="space-y-4 p-1">
      <div className="space-y-2.5">
        {stages.map(({ label, key, color }) => {
          const ms = t[key];
          const pct = ((ms / t.total) * 100).toFixed(0);
          return (
            <div key={key}>
              <div className="mb-1 flex items-center justify-between text-xs">
                <span className="text-muted-foreground">{label}</span>
                <span className="tabular-nums text-foreground">{ms.toLocaleString()} ms</span>
              </div>
              <div className="h-1.5 overflow-hidden rounded-full bg-muted">
                <div
                  className={cn('h-full rounded-full transition-all duration-700', color)}
                  style={{ width: `${pct}%` }}
                  aria-label={`${label}: ${ms}ms (${pct}%)`}
                />
              </div>
            </div>
          );
        })}
      </div>
      <Separator />
      <div className="flex items-center justify-between text-xs font-semibold">
        <span>Total end-to-end</span>
        <span className="tabular-nums">{t.total.toLocaleString()} ms</span>
      </div>
      <div className={cn(
        'rounded-md border px-3 py-2 text-xs',
        t.total < 1000
          ? 'border-chatConfidence-high/30 bg-chatConfidence-high/5 text-chatConfidence-high'
          : t.total < 2000
            ? 'border-chatConfidence-medium/30 bg-chatConfidence-medium/5 text-chatConfidence-medium'
            : 'border-destructive/30 bg-destructive/5 text-destructive',
      )}>
        {t.total < 1000 ? '⚡ Excellent latency' : t.total < 2000 ? '✓ Acceptable latency' : '⚠ High latency — check LLM provider'}
      </div>
    </div>
  );
}

// ─── Debug panel ──────────────────────────────────────────────────────────────

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
        'flex shrink-0 flex-col border-l border-border bg-card transition-all duration-200',
        open ? 'w-[38%] min-w-[280px]' : 'w-0 overflow-hidden border-l-0',
      )}
      aria-label="Debug panel"
    >
      {open && (
        <>
          <div className="flex shrink-0 items-center justify-between border-b border-border px-3 py-2">
            <div className="flex items-center gap-1.5">
              <Bug className="h-3.5 w-3.5 text-muted-foreground" aria-hidden />
              <span className="text-[0.625rem] font-semibold uppercase tracking-widest text-muted-foreground">
                Debug Inspector
              </span>
              {debugData && (
                <span className={cn(
                  'ml-1 rounded-full px-1.5 py-0 text-[0.5625rem] font-semibold border',
                  debugData.confidenceBand === 'High'
                    ? 'border-chatConfidence-high/30 bg-chatConfidence-high/10 text-chatConfidence-high'
                    : debugData.confidenceBand === 'Medium'
                      ? 'border-chatConfidence-medium/30 bg-chatConfidence-medium/10 text-chatConfidence-medium'
                      : 'border-destructive/30 bg-destructive/10 text-destructive',
                )}>
                  {(debugData.confidence * 100).toFixed(0)}%
                </span>
              )}
            </div>
            <Button
              variant="ghost"
              size="sm"
              className="h-6 w-6 p-0 text-muted-foreground hover:text-foreground"
              onClick={onToggle}
              aria-label="Hide debug panel"
            >
              <ChevronRight className="h-3.5 w-3.5" aria-hidden />
            </Button>
          </div>

          <Tabs defaultValue="chunks" className="flex min-h-0 flex-1 flex-col">
            <div className="shrink-0 border-b border-border px-2 pt-2">
              <TabsList className="h-auto w-full grid grid-cols-6 gap-0 bg-transparent p-0">
                {(['chunks', 'confidence', 'prompt', 'response', 'pii', 'perf'] as const).map((tab) => (
                  <TabsTrigger
                    key={tab}
                    value={tab}
                    className={cn(
                      'h-7 rounded-none border-b-2 border-transparent px-1 text-[0.5625rem] font-medium uppercase tracking-wide transition-colors',
                      'text-muted-foreground hover:text-foreground',
                      'data-[state=active]:border-primary data-[state=active]:bg-transparent data-[state=active]:text-primary',
                    )}
                  >
                    {tab === 'confidence' ? 'Conf' : tab === 'perf' ? 'Perf' : tab.charAt(0).toUpperCase() + tab.slice(1)}
                  </TabsTrigger>
                ))}
              </TabsList>
            </div>

            <ScrollArea className="min-h-0 flex-1">
              <div className="p-2">
                <TabsContent value="chunks" className="mt-0">
                  <ChunksTab debugData={debugData} />
                </TabsContent>
                <TabsContent value="confidence" className="mt-0">
                  <ConfidenceTab debugData={debugData} />
                </TabsContent>
                <TabsContent value="prompt" className="mt-0">
                  <PromptTab debugData={debugData} />
                </TabsContent>
                <TabsContent value="response" className="mt-0">
                  <ResponseTab debugData={debugData} />
                </TabsContent>
                <TabsContent value="pii" className="mt-0">
                  <PiiTab debugData={debugData} />
                </TabsContent>
                <TabsContent value="perf" className="mt-0">
                  <PerformanceTab debugData={debugData} />
                </TabsContent>
              </div>
            </ScrollArea>
          </Tabs>
        </>
      )}
    </div>
  );
}

// ─── Message components ───────────────────────────────────────────────────────

function TypingDots() {
  return (
    <span className="inline-flex items-center gap-0.5" aria-label="AI is typing">
      {[0, 1, 2].map((i) => (
        <span
          key={i}
          className="h-1.5 w-1.5 rounded-full bg-chatState-ai/60 animate-bounce"
          style={{ animationDelay: `${i * 150}ms`, animationDuration: '900ms' }}
          aria-hidden
        />
      ))}
    </span>
  );
}

function MessageRow({
  msg,
  onSelectDebug,
  isDebugSelected,
}: {
  msg: PlaygroundMessage;
  onSelectDebug?: () => void;
  isDebugSelected?: boolean;
}) {
  if (msg.role === 'system') {
    return (
      <div className="flex justify-center py-1">
        <span className="inline-flex items-center gap-1.5 rounded-full border border-border bg-muted/60 px-3 py-1 text-xs text-muted-foreground">
          <Zap className="h-3 w-3 text-chatState-ai" aria-hidden />
          {msg.body}
        </span>
      </div>
    );
  }

  if (msg.role === 'user') {
    return (
      <div className="flex justify-end gap-2">
        <div className="max-w-[72%] space-y-0.5">
          <div className="rounded-2xl rounded-tr-sm bg-primary px-3.5 py-2.5 text-sm leading-relaxed text-primary-foreground shadow-sm">
            {msg.body}
          </div>
          <div className="flex justify-end">
            <time
              dateTime={msg.sentAt}
              className="text-[0.625rem] text-muted-foreground/50 tabular-nums"
              suppressHydrationWarning
            >
              {new Date(msg.sentAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
            </time>
          </div>
        </div>
      </div>
    );
  }

  // AI message
  const displayBody = msg.streamedBody ?? msg.body;
  const confidence = msg.debugData?.confidence;
  const band = msg.debugData?.confidenceBand;

  return (
    <div className="group flex gap-2.5">
      <div
        aria-hidden
        className="mt-1 flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-chatState-ai/15 text-[0.5rem] font-bold text-chatState-ai ring-1 ring-chatState-ai/20"
      >
        AI
      </div>
      <div className="min-w-0 flex-1 space-y-0.5">
        <div className="flex items-center gap-2">
          <span className="text-xs font-medium text-chatState-ai">AI Assistant</span>
          {band && (
            <span className={cn('text-[0.5625rem] tabular-nums', getConfidenceColorClass(band))}>
              {((confidence ?? 0) * 100).toFixed(0)}% conf
            </span>
          )}
        </div>
        <div
          className={cn(
            'inline-block max-w-[82%] rounded-2xl rounded-tl-sm border px-3.5 py-2.5 text-sm leading-relaxed shadow-sm transition-colors',
            isDebugSelected
              ? 'border-chatState-ai/40 bg-chatState-ai/8'
              : 'border-border bg-card hover:border-chatState-ai/20',
          )}
        >
          {msg.isStreaming ? (
            displayBody ? (
              <span>
                {displayBody}
                <span className="ml-0.5 inline-block h-3.5 w-0.5 animate-pulse bg-chatState-ai align-text-bottom" aria-hidden />
              </span>
            ) : (
              <TypingDots />
            )
          ) : (
            msg.body
          )}
        </div>
        <div className="flex items-center gap-2">
          <time
            dateTime={msg.sentAt}
            className="text-[0.625rem] text-muted-foreground/50 tabular-nums"
            suppressHydrationWarning
          >
            {new Date(msg.sentAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
          </time>
          {!msg.isStreaming && msg.debugData && onSelectDebug && (
            <button
              onClick={onSelectDebug}
              className={cn(
                'text-[0.5625rem] font-medium underline-offset-2 transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring rounded',
                isDebugSelected ? 'text-chatState-ai' : 'text-muted-foreground/50 hover:text-chatState-ai',
              )}
            >
              {isDebugSelected ? 'inspecting' : 'inspect →'}
            </button>
          )}
        </div>
      </div>
    </div>
  );
}

// ─── Compare pane config ──────────────────────────────────────────────────────

function ComparePaneConfig({
  paneLabel,
  provider,
  setProvider,
  model,
  setModel,
}: {
  paneLabel: string;
  provider: string;
  setProvider: (v: string) => void;
  model: string;
  setModel: (v: string) => void;
}) {
  return (
    <div className="flex shrink-0 items-center gap-2 border-b border-border bg-muted/30 px-3 py-2">
      <span className="text-[0.625rem] font-semibold uppercase tracking-widest text-muted-foreground">
        {paneLabel}
      </span>
      <Select value={provider} onValueChange={setProvider}>
        <SelectTrigger className="h-7 w-32 text-xs" aria-label={`${paneLabel} provider`}>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="openrouter">OpenRouter</SelectItem>
          <SelectItem value="anthropic">Anthropic</SelectItem>
          <SelectItem value="openai">OpenAI</SelectItem>
          <SelectItem value="gemini">Google Gemini</SelectItem>
        </SelectContent>
      </Select>
      <Input
        className="h-7 w-40 text-xs"
        placeholder="Model"
        value={model}
        onChange={(e) => setModel(e.target.value)}
        aria-label={`${paneLabel} model name`}
      />
    </div>
  );
}

// ─── Empty / welcome state ────────────────────────────────────────────────────

function EmptyState({ onLoadScenario }: { onLoadScenario: (key: string) => void }) {
  return (
    <div className="flex flex-col items-center gap-6 px-6 py-12">
      <div className="flex flex-col items-center gap-2 text-center">
        <FlaskConical className="h-10 w-10 text-muted-foreground/30" aria-hidden />
        <h2 className="text-sm font-semibold text-foreground">Test Playground</h2>
        <p className="max-w-xs text-xs text-muted-foreground">
          Send a message to test the AI chatbot live, or load a scenario below.
        </p>
      </div>

      <div className="w-full max-w-sm space-y-2">
        <p className="text-[0.625rem] font-semibold uppercase tracking-widest text-muted-foreground">
          Quick Scenarios
        </p>
        <div className="grid grid-cols-1 gap-1.5">
          {SCENARIO_META.map(({ key, label, description, icon: Icon, tag, tagColor }) => (
            <button
              key={key}
              onClick={() => onLoadScenario(key)}
              className="flex items-start gap-3 rounded-lg border border-border bg-card px-3 py-2.5 text-left transition-colors hover:border-primary/30 hover:bg-muted/40 focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
            >
              <Icon className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" aria-hidden />
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2">
                  <span className="text-xs font-medium text-foreground">{label}</span>
                  <span className={cn('rounded border px-1.5 py-0 text-[0.5rem] font-semibold', tagColor)}>
                    {tag}
                  </span>
                </div>
                <p className="mt-0.5 text-xs text-muted-foreground">{description}</p>
              </div>
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}

// ─── Chat thread ──────────────────────────────────────────────────────────────

function ChatThread({
  messages,
  scrollRef,
  selectedDebugId,
  onSelectDebug,
  onLoadScenario,
}: {
  messages: PlaygroundMessage[];
  scrollRef: React.RefObject<HTMLDivElement | null>;
  selectedDebugId: string | null;
  onSelectDebug: (id: string) => void;
  onLoadScenario: (key: string) => void;
}) {
  return (
    <div
      ref={scrollRef}
      className="min-h-0 flex-1 overflow-y-auto"
      aria-label="Chat messages"
      aria-live="polite"
      aria-relevant="additions"
    >
      {messages.length === 0 ? (
        <EmptyState onLoadScenario={onLoadScenario} />
      ) : (
        <div className="flex flex-col gap-4 p-4">
          {messages.map((msg) => (
            <MessageRow
              key={msg.id}
              msg={msg}
              onSelectDebug={msg.debugData ? () => onSelectDebug(msg.id) : undefined}
              isDebugSelected={selectedDebugId === msg.id}
            />
          ))}
        </div>
      )}
    </div>
  );
}

// ─── Main page ────────────────────────────────────────────────────────────────

export default function PlaygroundPage() {
  const [messages, setMessages] = useState<PlaygroundMessage[]>([]);
  const [compareLeftMessages, setCompareLeftMessages] = useState<PlaygroundMessage[]>([]);
  const [compareRightMessages, setCompareRightMessages] = useState<PlaygroundMessage[]>([]);
  const [selectedDebugId, setSelectedDebugId] = useState<string | null>(null);
  const [currentDebugData, setCurrentDebugData] = useState<DebugData | null>(null);
  const [inputText, setInputText] = useState('');
  const [compareMode, setCompareMode] = useState(false);
  const [debugOpen, setDebugOpen] = useState(true);
  const [controlsOpen, setControlsOpen] = useState(false);
  const [copied, setCopied] = useState(false);
  const [isStreaming, setIsStreaming] = useState(false);

  // Controls
  const [sysPromptOn, setSysPromptOn] = useState(false);
  const [sysPrompt, setSysPrompt] = useState('');
  const [confidenceOverrideOn, setConfidenceOverrideOn] = useState(false);
  const [confidenceThreshold, setConfidenceThreshold] = useState(60);
  const [knowledgeVersion, setKnowledgeVersion] = useState('current');
  const [mcpDryRun, setMcpDryRun] = useState(false);

  // Compare configs
  const [leftProvider, setLeftProvider] = useState('openrouter');
  const [leftModel, setLeftModel] = useState('anthropic/claude-3-haiku');
  const [rightProvider, setRightProvider] = useState('openai');
  const [rightModel, setRightModel] = useState('gpt-4o-mini');

  const scrollRef = useRef<HTMLDivElement>(null);
  const leftScrollRef = useRef<HTMLDivElement>(null);
  const rightScrollRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const streamTimersRef = useRef<ReturnType<typeof setTimeout>[]>([]);

  const scrollToBottom = useCallback((ref: React.RefObject<HTMLDivElement | null>) => {
    if (ref.current) ref.current.scrollTop = ref.current.scrollHeight;
  }, []);

  useEffect(() => {
    scrollToBottom(scrollRef);
  }, [messages, scrollToBottom]);

  useEffect(() => {
    scrollToBottom(leftScrollRef);
    scrollToBottom(rightScrollRef);
  }, [compareLeftMessages, compareRightMessages, scrollToBottom]);

  function clearTimers() {
    streamTimersRef.current.forEach(clearTimeout);
    streamTimersRef.current = [];
  }

  function newSession() {
    clearTimers();
    setMessages([]);
    setCompareLeftMessages([]);
    setCompareRightMessages([]);
    setCurrentDebugData(null);
    setSelectedDebugId(null);
    setIsStreaming(false);
    inputRef.current?.focus();
  }

  function shareSession() {
    navigator.clipboard.writeText(window.location.href).then(() => {
      setCopied(true);
      const t = setTimeout(() => setCopied(false), 2000);
      streamTimersRef.current.push(t);
    });
  }

  const loadScenario = useCallback((key: string) => {
    const scenario = SCENARIOS[key];
    if (!scenario) return;
    clearTimers();
    setMessages(scenario);
    setCompareLeftMessages([]);
    setCompareRightMessages([]);
    setIsStreaming(false);
    const lastAiMsg = [...scenario].reverse().find((m) => m.role === 'ai');
    if (lastAiMsg?.debugData) {
      setCurrentDebugData(lastAiMsg.debugData);
      setSelectedDebugId(lastAiMsg.id);
    }
  }, []);

  // Character-by-character streaming simulation
  function streamText(
    msgId: string,
    fullText: string,
    debugData: DebugData,
    setter: React.Dispatch<React.SetStateAction<PlaygroundMessage[]>>,
  ) {
    const chars = fullText.split('');
    const charsPerTick = 3; // characters per 30ms tick → ~100 chars/sec
    const tickMs = 30;

    setIsStreaming(true);

    chars.forEach((_, i) => {
      if (i % charsPerTick !== 0) return;
      const partial = fullText.slice(0, i + charsPerTick);
      const t = setTimeout(() => {
        setter((prev) =>
          prev.map((m) =>
            m.id === msgId
              ? { ...m, streamedBody: partial }
              : m,
          ),
        );
      }, i * (tickMs / charsPerTick));
      streamTimersRef.current.push(t);
    });

    // Finalize
    const totalMs = chars.length * (tickMs / charsPerTick) + 50;
    const finalTimer = setTimeout(() => {
      setter((prev) =>
        prev.map((m) =>
          m.id === msgId
            ? { ...m, body: fullText, streamedBody: undefined, isStreaming: false, debugData }
            : m,
        ),
      );
      setCurrentDebugData(debugData);
      setSelectedDebugId(msgId);
      setIsStreaming(false);
    }, totalMs);
    streamTimersRef.current.push(finalTimer);
  }

  const sendMessage = useCallback(() => {
    const body = inputText.trim();
    if (!body || isStreaming) return;
    setInputText('');

    const userMsg: PlaygroundMessage = {
      id: nextId(),
      role: 'user',
      body,
      sentAt: new Date().toISOString(),
    };

    const aiId = nextId();
    const aiStreamMsg: PlaygroundMessage = {
      id: aiId,
      role: 'ai',
      body: '',
      isStreaming: true,
      sentAt: new Date().toISOString(),
    };

    if (compareMode) {
      setCompareLeftMessages((prev) => [...prev, userMsg, aiStreamMsg]);
      const rightAiId = nextId();
      setCompareRightMessages((prev) => [
        ...prev,
        userMsg,
        { ...aiStreamMsg, id: rightAiId },
      ]);

      // Simulate retrieval delay then stream left
      const leftDelay = setTimeout(() => {
        const { body: aiBody, debugData } = generateMockAiResponse(body);
        streamText(aiId, aiBody, debugData, setCompareLeftMessages);
      }, 600);
      streamTimersRef.current.push(leftDelay);

      // Slightly different timing for right
      const rightDelay = setTimeout(() => {
        const { body: aiBody2, debugData: dd2 } = generateMockAiResponse(body + ' (alt)');
        streamText(rightAiId, aiBody2, dd2, setCompareRightMessages);
      }, 900);
      streamTimersRef.current.push(rightDelay);
    } else {
      setMessages((prev) => [...prev, userMsg, aiStreamMsg]);

      const delay = setTimeout(() => {
        const { body: aiBody, debugData } = generateMockAiResponse(body);
        streamText(aiId, aiBody, debugData, setMessages);
      }, 600);
      streamTimersRef.current.push(delay);
    }

    inputRef.current?.focus();
  }, [inputText, isStreaming, compareMode]);

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      sendMessage();
    }
  }

  const handleSelectDebug = useCallback(
    (id: string) => {
      const msg = messages.find((m) => m.id === id);
      if (msg?.debugData) {
        setSelectedDebugId(id);
        setCurrentDebugData(msg.debugData);
        if (!debugOpen) setDebugOpen(true);
      }
    },
    [messages, debugOpen],
  );

  const totalTokens = currentDebugData?.totalTokens ?? 0;
  const sessionMsgCount = messages.filter((m) => m.role !== 'system').length;

  return (
    <div className="flex h-full flex-col overflow-hidden bg-background">

      {/* ── Header ── */}
      <header className="flex shrink-0 items-center justify-between gap-3 border-b border-border bg-card px-4 py-2.5">
        <div className="flex min-w-0 items-center gap-2">
          <FlaskConical className="h-4 w-4 shrink-0 text-primary" aria-hidden />
          <span className="text-sm font-semibold text-foreground">Test Playground</span>
          <Badge variant="outline" className="shrink-0 text-xs text-muted-foreground">Sandbox</Badge>
          {sessionMsgCount > 0 && (
            <span className="text-xs text-muted-foreground tabular-nums">
              {sessionMsgCount} msg{sessionMsgCount !== 1 ? 's' : ''}
              {totalTokens > 0 && ` · ${totalTokens.toLocaleString()} tok`}
            </span>
          )}
        </div>

        <div className="flex shrink-0 items-center gap-1">
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
            aria-label="Copy session link"
          >
            {copied ? (
              <Check className="h-3.5 w-3.5 text-chatConfidence-high" aria-hidden />
            ) : (
              <Copy className="h-3.5 w-3.5" aria-hidden />
            )}
            <span className="hidden sm:inline">{copied ? 'Copied!' : 'Share'}</span>
          </Button>

          <Button
            variant={compareMode ? 'default' : 'ghost'}
            size="sm"
            className="h-7 gap-1.5 px-2.5 text-xs"
            onClick={() => { setCompareMode((v) => !v); newSession(); }}
            aria-label={compareMode ? 'Exit compare mode' : 'Enter compare mode'}
            aria-pressed={compareMode}
          >
            <GitCompare className="h-3.5 w-3.5" aria-hidden />
            <span className="hidden sm:inline">Compare</span>
          </Button>

          <Button
            variant={debugOpen ? 'secondary' : 'ghost'}
            size="sm"
            className="h-7 gap-1.5 px-2.5 text-xs"
            onClick={() => setDebugOpen((v) => !v)}
            aria-label={debugOpen ? 'Hide debug panel' : 'Show debug panel'}
            aria-pressed={debugOpen}
          >
            <Bug className="h-3.5 w-3.5" aria-hidden />
            <span className="hidden sm:inline">Debug</span>
          </Button>
        </div>
      </header>

      {/* ── Controls (collapsible) ── */}
      <div className="shrink-0 border-b border-border">
        <button
          className="flex w-full items-center justify-between px-4 py-2 text-xs font-medium text-muted-foreground transition-colors hover:bg-muted/30 hover:text-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
          onClick={() => setControlsOpen((v) => !v)}
          aria-expanded={controlsOpen}
        >
          <span className="flex items-center gap-1.5">
            <span className="font-semibold">Controls</span>
            {(sysPromptOn || confidenceOverrideOn || mcpDryRun || knowledgeVersion !== 'current') && (
              <span className="rounded-full bg-primary/15 px-1.5 text-[0.5625rem] font-semibold text-primary">
                overrides active
              </span>
            )}
          </span>
          {controlsOpen ? (
            <ChevronUp className="h-3.5 w-3.5" aria-hidden />
          ) : (
            <ChevronDown className="h-3.5 w-3.5" aria-hidden />
          )}
        </button>

        {controlsOpen && (
          <div className="flex flex-wrap items-start gap-x-6 gap-y-3 px-4 pb-4">
            {/* System prompt */}
            <div className="flex flex-col gap-1.5">
              <div className="flex items-center gap-2">
                <Switch
                  id="sys-prompt-switch"
                  checked={sysPromptOn}
                  onCheckedChange={setSysPromptOn}
                />
                <Label htmlFor="sys-prompt-switch" className="cursor-pointer text-xs">
                  System prompt override
                </Label>
              </div>
              {sysPromptOn && (
                <Textarea
                  rows={3}
                  className="w-72 text-xs"
                  placeholder="Override the default system prompt…"
                  value={sysPrompt}
                  onChange={(e) => setSysPrompt(e.target.value)}
                  aria-label="System prompt override"
                />
              )}
            </div>

            {/* Confidence threshold */}
            <div className="flex flex-col gap-1.5">
              <div className="flex items-center gap-2">
                <Switch
                  id="conf-switch"
                  checked={confidenceOverrideOn}
                  onCheckedChange={setConfidenceOverrideOn}
                />
                <Label htmlFor="conf-switch" className="cursor-pointer text-xs">
                  Confidence threshold
                </Label>
              </div>
              {confidenceOverrideOn && (
                <div className="flex items-center gap-3 w-48">
                  <Slider
                    min={0}
                    max={100}
                    step={5}
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
                <SelectTrigger className="h-8 w-32 text-xs">
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
            <div className="flex items-center gap-2 pt-5">
              <Switch id="mcp-switch" checked={mcpDryRun} onCheckedChange={setMcpDryRun} />
              <Label htmlFor="mcp-switch" className="cursor-pointer text-xs">
                MCP dry-run
              </Label>
            </div>

            {/* Scenario library */}
            <div className="flex flex-col gap-1.5">
              <Label className="text-xs text-muted-foreground">Load scenario</Label>
              <Select onValueChange={loadScenario}>
                <SelectTrigger className="h-8 w-64 text-xs">
                  <SelectValue placeholder="Choose a scenario →" />
                </SelectTrigger>
                <SelectContent>
                  {SCENARIO_META.map(({ key, label }) => (
                    <SelectItem key={key} value={key}>{label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
        )}
      </div>

      {/* ── Body ── */}
      <div className="flex min-h-0 flex-1 overflow-hidden">

        {/* Chat area */}
        <div className="flex min-w-0 flex-1 flex-col overflow-hidden">
          {compareMode ? (
            <div className="flex min-h-0 flex-1">
              {/* Left pane */}
              <div className="flex min-w-0 flex-1 flex-col border-r border-border">
                <ComparePaneConfig
                  paneLabel="Pane A"
                  provider={leftProvider}
                  setProvider={setLeftProvider}
                  model={leftModel}
                  setModel={setLeftModel}
                />
                <div
                  ref={leftScrollRef}
                  className="min-h-0 flex-1 overflow-y-auto"
                  aria-label="Left pane chat"
                >
                  {compareLeftMessages.length === 0 ? (
                    <EmptyState onLoadScenario={loadScenario} />
                  ) : (
                    <div className="flex flex-col gap-4 p-4">
                      {compareLeftMessages.map((msg) => (
                        <MessageRow
                          key={msg.id}
                          msg={msg}
                          onSelectDebug={msg.debugData ? () => handleSelectDebug(msg.id) : undefined}
                          isDebugSelected={selectedDebugId === msg.id}
                        />
                      ))}
                    </div>
                  )}
                </div>
              </div>

              {/* Right pane */}
              <div className="flex min-w-0 flex-1 flex-col">
                <ComparePaneConfig
                  paneLabel="Pane B"
                  provider={rightProvider}
                  setProvider={setRightProvider}
                  model={rightModel}
                  setModel={setRightModel}
                />
                <div
                  ref={rightScrollRef}
                  className="min-h-0 flex-1 overflow-y-auto"
                  aria-label="Right pane chat"
                >
                  {compareRightMessages.length === 0 ? (
                    <EmptyState onLoadScenario={loadScenario} />
                  ) : (
                    <div className="flex flex-col gap-4 p-4">
                      {compareRightMessages.map((msg) => (
                        <MessageRow
                          key={msg.id}
                          msg={msg}
                        />
                      ))}
                    </div>
                  )}
                </div>
              </div>
            </div>
          ) : (
            <ChatThread
              messages={messages}
              scrollRef={scrollRef}
              selectedDebugId={selectedDebugId}
              onSelectDebug={handleSelectDebug}
              onLoadScenario={loadScenario}
            />
          )}

          {/* ── Input bar ── */}
          <div className="shrink-0 border-t border-border bg-card px-3 py-2.5">
            <div className="flex items-center gap-2">
              <Button
                variant="ghost"
                size="sm"
                className="h-8 shrink-0 gap-1 px-2 text-xs text-muted-foreground hover:text-destructive"
                onClick={() => {
                  clearTimers();
                  setMessages([]);
                  setCompareLeftMessages([]);
                  setCompareRightMessages([]);
                  setCurrentDebugData(null);
                  setSelectedDebugId(null);
                  setIsStreaming(false);
                }}
                aria-label="Clear conversation"
                disabled={messages.length === 0 && compareLeftMessages.length === 0}
              >
                <Trash2 className="h-3.5 w-3.5" aria-hidden />
                <span className="hidden sm:inline">Clear</span>
              </Button>

              <Input
                ref={inputRef}
                className="h-8 flex-1 text-sm"
                placeholder={isStreaming ? 'AI is typing…' : 'Type a message… (Enter to send)'}
                value={inputText}
                onChange={(e) => setInputText(e.target.value)}
                onKeyDown={handleKeyDown}
                aria-label="Message input"
                disabled={isStreaming}
              />

              <Button
                size="sm"
                className="h-8 shrink-0 gap-1.5 px-4 text-xs"
                onClick={sendMessage}
                disabled={!inputText.trim() || isStreaming}
                aria-label="Send message"
              >
                <Send className="h-3.5 w-3.5" aria-hidden />
                <span>Send</span>
              </Button>
            </div>
          </div>
        </div>

        {/* ── Debug panel ── */}
        <DebugPanel
          debugData={currentDebugData}
          open={debugOpen}
          onToggle={() => setDebugOpen(false)}
        />
      </div>
    </div>
  );
}
