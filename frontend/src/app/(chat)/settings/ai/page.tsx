'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { Eye, EyeOff, ChevronUp, ChevronDown, X, ExternalLink } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Textarea } from '@/shared/ui/textarea';
import { Label } from '@/shared/ui/label';
import { Separator } from '@/shared/ui/separator';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/shared/ui/select';
import { Slider } from '@/shared/ui/slider';
import { Switch } from '@/shared/ui/switch';
import { cn } from '@/shared/lib/cn';

// ── Constants ─────────────────────────────────────────────────────────────────

const PROVIDERS = ['OpenAI', 'Anthropic', 'Gemini', 'OpenRouter', 'DeepSeek'] as const;
type Provider = (typeof PROVIDERS)[number];

const PRESET_TEMPLATES = {
  retail:
    'You are a helpful customer service AI for a retail business. You assist customers with orders, returns, products, and general inquiries. Always be polite and concise. If you cannot help, offer to connect to a human agent.',
  ecommerce:
    'You are an AI shopping assistant. Help customers find products, track orders, and resolve issues. Greet customers warmly and use their name when known.',
  support:
    'You are a technical support assistant. Diagnose issues systematically, provide clear step-by-step instructions, and escalate complex problems to human agents.',
} as const;
type TemplateKey = keyof typeof PRESET_TEMPLATES;

type PiiMode = 'none' | 'pseudonymise' | 'redact';
type Sensitivity = 'low' | 'medium' | 'high';
type TestStatus = 'idle' | 'testing' | 'passed' | 'failed';
type SaveStatus = 'idle' | 'saving' | 'saved';

// ── Section heading ───────────────────────────────────────────────────────────

function SectionHeading({ children }: { children: React.ReactNode }) {
  return (
    <h2 className="text-sm font-semibold uppercase tracking-wider text-muted-foreground">
      {children}
    </h2>
  );
}

// ── Tag input component ───────────────────────────────────────────────────────

interface TagInputProps {
  tags: string[];
  onAdd: (tag: string) => void;
  onRemove: (tag: string) => void;
  placeholder: string;
}

function TagInput({ tags, onAdd, onRemove, placeholder }: TagInputProps) {
  const [draft, setDraft] = useState('');

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key !== 'Enter') return;
    e.preventDefault();
    const trimmed = draft.trim();
    if (trimmed && !tags.includes(trimmed)) {
      onAdd(trimmed);
      setDraft('');
    }
  }

  return (
    <div className="space-y-2">
      <Input
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        onKeyDown={handleKeyDown}
        placeholder={placeholder}
      />
      {tags.length > 0 && (
        <div className="flex flex-wrap gap-2">
          {tags.map((phrase) => (
            <span
              key={phrase}
              className="inline-flex items-center gap-1 rounded-full bg-secondary px-2.5 py-1 text-xs text-secondary-foreground"
            >
              {phrase}
              <button
                type="button"
                aria-label={`Remove ${phrase}`}
                onClick={() => onRemove(phrase)}
                className="flex h-4 w-4 items-center justify-center rounded-full hover:bg-muted"
              >
                <X className="h-2.5 w-2.5" />
              </button>
            </span>
          ))}
        </div>
      )}
    </div>
  );
}

// ── Page ─────────────────────────────────────────────────────────────────────

export default function AiSettingsPage() {
  const t = useTranslations('chat.aiSettings');

  // Section 1 — Provider & Model
  const [provider, setProvider] = useState<Provider>('OpenRouter');
  const [modelId, setModelId] = useState('gpt-4o-mini');
  const [apiKey, setApiKey] = useState('');
  const [showKey, setShowKey] = useState(false);
  const [testStatus, setTestStatus] = useState<TestStatus>('idle');
  const [fallbackProviders, setFallbackProviders] = useState<Provider[]>([
    'OpenRouter',
    'Anthropic',
    'OpenAI',
  ]);

  // Section 2 — System Prompt
  const [systemPrompt, setSystemPrompt] = useState<string>(PRESET_TEMPLATES.retail);

  // Section 3 — Handoff
  const [confidence, setConfidence] = useState(65);
  const [sentimentEnabled, setSentimentEnabled] = useState(true);
  const [sensitivity, setSensitivity] = useState<Sensitivity>('medium');
  const [triggerPhrases, setTriggerPhrases] = useState<string[]>([
    'speak to a manager',
    'human agent',
    'escalate',
  ]);
  const [maxUnanswered, setMaxUnanswered] = useState(3);

  // Section 4 — PII
  const [piiMode, setPiiMode] = useState<PiiMode>('pseudonymise');

  // Section 5 — Retention
  const [retentionDays, setRetentionDays] = useState(90);

  // Save
  const [saveStatus, setSaveStatus] = useState<SaveStatus>('idle');

  // ── Handlers ────────────────────────────────────────────────────────────────

  function handleTestConnection() {
    setTestStatus('testing');
    setTimeout(() => {
      setTestStatus(Math.random() > 0.2 ? 'passed' : 'failed');
    }, 1500);
  }

  function moveUp(index: number) {
    if (index === 0) return;
    setFallbackProviders((prev) => {
      const next = [...prev];
      next.splice(index - 1, 2, next[index] as (typeof next)[number], next[index - 1] as (typeof next)[number]);
      return next;
    });
  }

  function moveDown(index: number) {
    setFallbackProviders((prev) => {
      if (index >= prev.length - 1) return prev;
      const next = [...prev];
      next.splice(index, 2, next[index + 1] as (typeof next)[number], next[index] as (typeof next)[number]);
      return next;
    });
  }

  function handleSave() {
    setSaveStatus('saving');
    setTimeout(() => setSaveStatus('saved'), 1000);
  }

  const tokenEstimate = Math.ceil(systemPrompt.length / 4);
  const testPromptUrl = `/playground?prompt=${encodeURIComponent(systemPrompt)}`;

  // ── PII options ──────────────────────────────────────────────────────────────

  const piiOptions: { value: PiiMode; label: string; description: string }[] = [
    {
      value: 'none',
      label: t('pii.none').split(' — ')[0] ?? 'None',
      description: 'Send raw text to LLM (not recommended for customer PII)',
    },
    {
      value: 'pseudonymise',
      label: t('pii.pseudonymise').split(' — ')[0] ?? 'Pseudonymise',
      description: 'Replace names, emails, phone numbers with tokens before sending to LLM',
    },
    {
      value: 'redact',
      label: t('pii.redact').split(' — ')[0] ?? 'Redact',
      description: 'Remove PII entirely (may reduce response quality)',
    },
  ];

  // ── Render ──────────────────────────────────────────────────────────────────

  return (
    <div className="mx-auto max-w-2xl space-y-8 p-4 pb-24 sm:p-6">
      <h1 className="text-2xl font-bold tracking-tight text-foreground">{t('title')}</h1>

      {/* ── Section 1: Provider & Model ──────────────────────────────────────── */}
      <section className="space-y-4">
        <SectionHeading>{t('sections.provider')}</SectionHeading>

        <div className="space-y-1.5">
          <Label htmlFor="provider-select">{t('provider.providerLabel')}</Label>
          <Select value={provider} onValueChange={(v) => setProvider(v as Provider)}>
            <SelectTrigger id="provider-select" className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {PROVIDERS.map((p) => (
                <SelectItem key={p} value={p}>
                  {p}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="model-id">{t('provider.modelLabel')}</Label>
          <Input
            id="model-id"
            value={modelId}
            onChange={(e) => setModelId(e.target.value)}
            placeholder="gpt-4o-mini"
          />
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="api-key">{t('provider.apiKeyLabel')}</Label>
          <div className="relative">
            <Input
              id="api-key"
              type={showKey ? 'text' : 'password'}
              value={apiKey}
              onChange={(e) => setApiKey(e.target.value)}
              className="pr-10"
              placeholder="sk-…"
            />
            <button
              type="button"
              aria-label={showKey ? 'Hide API key' : 'Show API key'}
              onClick={() => setShowKey((v) => !v)}
              className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
            >
              {showKey ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
            </button>
          </div>
        </div>

        <div className="flex items-center gap-3">
          <Button
            type="button"
            variant="outline"
            onClick={handleTestConnection}
            disabled={testStatus === 'testing'}
            className="min-h-[44px]"
          >
            {testStatus === 'testing' ? t('provider.testing') : t('provider.testConnection')}
          </Button>
          {testStatus === 'passed' && (
            <span className="text-sm text-green-600 dark:text-green-400">
              {t('provider.testPassed')}
            </span>
          )}
          {testStatus === 'failed' && (
            <span className="text-sm text-destructive">{t('provider.testFailed')}</span>
          )}
        </div>

        {/* Fallback providers */}
        <div className="space-y-2">
          <Label>{t('provider.fallbackLabel')}</Label>
          <div className="space-y-1">
            {fallbackProviders.map((fp, i) => (
              <div
                key={fp}
                className="flex items-center justify-between rounded-md border border-border bg-card px-3 py-2"
              >
                <span className="text-sm text-foreground">{fp}</span>
                <div className="flex gap-1">
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    aria-label={t('provider.moveUp')}
                    onClick={() => moveUp(i)}
                    disabled={i === 0}
                    className="h-8 w-8 p-0"
                  >
                    <ChevronUp className="h-4 w-4" />
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    aria-label={t('provider.moveDown')}
                    onClick={() => moveDown(i)}
                    disabled={i === fallbackProviders.length - 1}
                    className="h-8 w-8 p-0"
                  >
                    <ChevronDown className="h-4 w-4" />
                  </Button>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      <Separator />

      {/* ── Section 2: System Prompt ─────────────────────────────────────────── */}
      <section className="space-y-4">
        <SectionHeading>{t('sections.systemPrompt')}</SectionHeading>

        <div className="space-y-1.5">
          <Label htmlFor="template-select">{t('systemPrompt.templateLabel')}</Label>
          <Select
            onValueChange={(v) => setSystemPrompt(PRESET_TEMPLATES[v as TemplateKey])}
          >
            <SelectTrigger id="template-select" className="w-full">
              <SelectValue placeholder={t('systemPrompt.templateLabel')} />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="retail">Retail</SelectItem>
              <SelectItem value="ecommerce">E-Commerce</SelectItem>
              <SelectItem value="support">Support</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="system-prompt">{t('systemPrompt.label')}</Label>
          <Textarea
            id="system-prompt"
            rows={8}
            value={systemPrompt}
            onChange={(e) => setSystemPrompt(e.target.value)}
            className="resize-y font-mono text-sm"
          />
          <p className="text-xs text-muted-foreground">
            {t('systemPrompt.tokenEstimate', { count: tokenEstimate })}
          </p>
        </div>

        <a
          href={testPromptUrl}
          target="_blank"
          rel="noopener noreferrer"
          className="inline-flex items-center gap-1 text-sm text-primary hover:underline"
        >
          {t('systemPrompt.testLink')}
          <ExternalLink className="h-3.5 w-3.5" />
        </a>
      </section>

      <Separator />

      {/* ── Section 3: Handoff Thresholds ────────────────────────────────────── */}
      <section className="space-y-6">
        <SectionHeading>{t('sections.handoff')}</SectionHeading>

        {/* Confidence slider */}
        <div className="space-y-3">
          <Label>
            {t('handoff.confidenceLabel')} {confidence}%
          </Label>
          <Slider
            min={0}
            max={100}
            step={1}
            value={[confidence]}
            onValueChange={([v]) => { if (v !== undefined) setConfidence(v); }}
            aria-label="Confidence threshold"
          />
        </div>

        {/* Sentiment toggle */}
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <Label htmlFor="sentiment-toggle">{t('handoff.sentimentLabel')}</Label>
            <Switch
              id="sentiment-toggle"
              checked={sentimentEnabled}
              onCheckedChange={setSentimentEnabled}
            />
          </div>

          {sentimentEnabled && (
            <div className="space-y-1.5">
              <p className="text-sm text-muted-foreground">{t('handoff.sentimentSensitivity')}</p>
              <div className="flex gap-2">
                {(['low', 'medium', 'high'] as Sensitivity[]).map((s) => (
                  <button
                    key={s}
                    type="button"
                    onClick={() => setSensitivity(s)}
                    className={cn(
                      'min-h-[44px] rounded-md border px-4 py-2 text-sm font-medium transition-colors',
                      sensitivity === s
                        ? 'border-primary bg-primary text-primary-foreground'
                        : 'border-border bg-background text-foreground hover:bg-muted',
                    )}
                  >
                    {t(`handoff.${s}`)}
                  </button>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Trigger phrases */}
        <div className="space-y-1.5">
          <Label>{t('handoff.triggerPhrasesLabel')}</Label>
          <TagInput
            tags={triggerPhrases}
            onAdd={(tag) => setTriggerPhrases((prev) => [...prev, tag])}
            onRemove={(tag) => setTriggerPhrases((prev) => prev.filter((p) => p !== tag))}
            placeholder={t('handoff.triggerPlaceholder')}
          />
        </div>

        {/* Max unanswered */}
        <div className="space-y-1.5">
          <Label htmlFor="max-unanswered">{t('handoff.maxUnansweredLabel')}</Label>
          <Input
            id="max-unanswered"
            type="number"
            min={1}
            max={10}
            value={maxUnanswered}
            onChange={(e) => setMaxUnanswered(Number(e.target.value))}
            className="w-24"
          />
        </div>
      </section>

      <Separator />

      {/* ── Section 4: PII Masking ───────────────────────────────────────────── */}
      <section className="space-y-4">
        <SectionHeading>{t('sections.pii')}</SectionHeading>
        <fieldset className="space-y-3">
          <legend className="sr-only">{t('pii.label')}</legend>
          {piiOptions.map(({ value, label, description }) => (
            <label
              key={value}
              className={cn(
                'flex cursor-pointer gap-3 rounded-md border p-4 transition-colors',
                piiMode === value
                  ? 'border-primary bg-primary/5'
                  : 'border-border hover:bg-muted/50',
              )}
            >
              <input
                type="radio"
                name="pii-mode"
                value={value}
                checked={piiMode === value}
                onChange={() => setPiiMode(value)}
                className="mt-0.5 h-4 w-4 accent-primary"
              />
              <div className="space-y-0.5">
                <span className="block text-sm font-medium text-foreground">{label}</span>
                <span className="block text-xs text-muted-foreground">{description}</span>
              </div>
            </label>
          ))}
        </fieldset>
      </section>

      <Separator />

      {/* ── Section 5: Data Retention ────────────────────────────────────────── */}
      <section className="space-y-4">
        <SectionHeading>{t('sections.retention')}</SectionHeading>

        <div className="space-y-1.5">
          <Label htmlFor="retention-days">{t('retention.label')}</Label>
          <Input
            id="retention-days"
            type="number"
            min={7}
            max={730}
            value={retentionDays}
            onChange={(e) => setRetentionDays(Number(e.target.value))}
            className="w-32"
          />
          <p className="text-xs text-muted-foreground">
            After this period, message content is permanently deleted. Metadata is retained for
            analytics.
          </p>
        </div>
      </section>

      {/* ── Save ────────────────────────────────────────────────────────────── */}
      <div className="flex items-center gap-4 pt-2">
        <Button
          type="button"
          onClick={handleSave}
          disabled={saveStatus === 'saving'}
          className="min-h-[44px] px-8"
        >
          {saveStatus === 'saving' ? t('saving') : t('save')}
        </Button>
        {saveStatus === 'saved' && (
          <span className="text-sm text-muted-foreground">{t('saved')}</span>
        )}
      </div>
    </div>
  );
}
