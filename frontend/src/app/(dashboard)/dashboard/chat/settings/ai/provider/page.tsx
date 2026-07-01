'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { ExternalLink, Check, AlertCircle, ChevronRight } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Label } from '@/shared/ui/label';
import { Badge } from '@/shared/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/shared/ui/select';
import { Separator } from '@/shared/ui/separator';
import { cn } from '@/shared/lib/cn';

// ── Types ──────────────────────────────────────────────────────────────────────

type Provider = 'OpenAI' | 'Anthropic' | 'Gemini' | 'OpenRouter' | 'DeepSeek';
type SaveStatus = 'idle' | 'saving' | 'saved';

interface ConfiguredProvider {
  provider: Provider;
  isActive: boolean;
  models: { id: string; label: string; contextWindow: string }[];
}

// ── Mock: comes from useAiSettings() in production ────────────────────────────

const CONFIGURED_PROVIDERS: ConfiguredProvider[] = [
  {
    provider: 'OpenRouter',
    isActive: true,
    models: [
      { id: 'openai/gpt-4o',             label: 'GPT-4o',             contextWindow: '128k' },
      { id: 'openai/gpt-4o-mini',        label: 'GPT-4o Mini',        contextWindow: '128k' },
      { id: 'anthropic/claude-3-5-sonnet', label: 'Claude 3.5 Sonnet', contextWindow: '200k' },
      { id: 'anthropic/claude-3-haiku',  label: 'Claude 3 Haiku',     contextWindow: '200k' },
      { id: 'google/gemini-2.0-flash',   label: 'Gemini 2.0 Flash',   contextWindow: '1M' },
      { id: 'deepseek/deepseek-chat',    label: 'DeepSeek Chat',      contextWindow: '64k' },
    ],
  },
  {
    provider: 'Anthropic',
    isActive: true,
    models: [
      { id: 'claude-opus-4-8',      label: 'Claude Opus 4.8',    contextWindow: '200k' },
      { id: 'claude-sonnet-4-6',    label: 'Claude Sonnet 4.6',  contextWindow: '200k' },
      { id: 'claude-haiku-4-5-20251001', label: 'Claude Haiku 4.5', contextWindow: '200k' },
    ],
  },
  {
    provider: 'OpenAI',
    isActive: false,
    models: [
      { id: 'gpt-4o',       label: 'GPT-4o',       contextWindow: '128k' },
      { id: 'gpt-4o-mini',  label: 'GPT-4o Mini',  contextWindow: '128k' },
      { id: 'o3-mini',      label: 'o3-mini',       contextWindow: '128k' },
    ],
  },
];

// ── ProviderCard ───────────────────────────────────────────────────────────────

function ProviderCard({
  config,
  selected,
  selectedModel,
  onSelect,
  onModelChange,
}: {
  config: ConfiguredProvider;
  selected: boolean;
  selectedModel: string;
  onSelect: () => void;
  onModelChange: (model: string) => void;
}) {
  const t = useTranslations('chat.aiSettings.provider');
  const currentModel = config.models.find((m) => m.id === selectedModel) ?? config.models[0];

  return (
    <div
      className={cn(
        'rounded-lg border transition-colors',
        selected ? 'border-primary bg-primary/5' : 'border-border bg-card',
        !config.isActive && 'opacity-60',
      )}
    >
      {/* Provider header row */}
      <button
        type="button"
        onClick={onSelect}
        disabled={!config.isActive}
        aria-pressed={selected}
        className="flex w-full items-center gap-3 px-4 py-3 text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-1 rounded-t-lg"
      >
        {/* Radio dot */}
        <div
          className={cn(
            'flex h-4 w-4 shrink-0 items-center justify-center rounded-full border-2 transition-colors',
            selected ? 'border-primary' : 'border-muted-foreground/30',
          )}
        >
          {selected && <div className="h-2 w-2 rounded-full bg-primary" />}
        </div>

        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <span className="text-sm font-medium text-foreground">{config.provider}</span>
            {!config.isActive && (
              <Badge variant="outline" className="text-[0.625rem] px-1.5 py-0 text-muted-foreground">
                {t('notConfigured')}
              </Badge>
            )}
          </div>
          {selected && currentModel && (
            <p className="mt-0.5 font-mono text-xs text-muted-foreground">{currentModel.id}</p>
          )}
        </div>

        {selected && <Check className="h-4 w-4 shrink-0 text-primary" aria-hidden />}
      </button>

      {/* Model selector — only shown when this provider is selected */}
      {selected && config.isActive && (
        <>
          <Separator />
          <div className="px-4 py-3 space-y-1.5">
            <Label className="text-xs text-muted-foreground">{t('modelLabel')}</Label>
            <Select value={selectedModel} onValueChange={onModelChange}>
              <SelectTrigger className="h-9">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {config.models.map((model) => (
                  <SelectItem key={model.id} value={model.id}>
                    <div className="flex items-center justify-between gap-8 w-full">
                      <span>{model.label}</span>
                      <span className="font-mono text-xs text-muted-foreground shrink-0">
                        {model.contextWindow} ctx
                      </span>
                    </div>
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </>
      )}
    </div>
  );
}

// ── Page ───────────────────────────────────────────────────────────────────────

export default function AiProviderPage() {
  const t = useTranslations('chat.aiSettings.provider');

  const [primaryProvider, setPrimaryProvider] = useState<Provider>('OpenRouter');
  const [primaryModel, setPrimaryModel] = useState('openai/gpt-4o');
  const [fallbackProvider, setFallbackProvider] = useState<Provider | 'none'>('Anthropic');
  const [fallbackModel, setFallbackModel] = useState('claude-sonnet-4-6');
  const [saveStatus, setSaveStatus] = useState<SaveStatus>('idle');

  const activeProviders = CONFIGURED_PROVIDERS.filter((p) => p.isActive);

  const fallbackOptions = CONFIGURED_PROVIDERS.filter(
    (p) => p.provider !== primaryProvider && p.isActive,
  );

  const fallbackConfig = CONFIGURED_PROVIDERS.find((p) => p.provider === fallbackProvider);

  function handlePrimarySelect(provider: Provider) {
    setPrimaryProvider(provider);
    const cfg = CONFIGURED_PROVIDERS.find((p) => p.provider === provider);
    if (cfg?.models[0]) setPrimaryModel(cfg.models[0].id);
    if (fallbackProvider === provider) setFallbackProvider('none');
  }

  function handleFallbackProviderChange(value: string) {
    setFallbackProvider(value as Provider | 'none');
    if (value !== 'none') {
      const cfg = CONFIGURED_PROVIDERS.find((p) => p.provider === value);
      if (cfg?.models[0]) setFallbackModel(cfg.models[0].id);
    }
  }

  function handleSave() {
    setSaveStatus('saving');
    setTimeout(() => setSaveStatus('saved'), 900);
  }

  if (activeProviders.length === 0) {
    return (
      <div className="flex items-start gap-3 rounded-lg border border-border bg-muted/40 px-4 py-4">
        <AlertCircle className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" aria-hidden />
        <div className="space-y-2 text-sm text-muted-foreground">
          <p className="font-medium text-foreground">{t('noProvidersTitle')}</p>
          <p>{t('noProvidersDescription')}</p>
          <a
            href="/dashboard/settings/ai"
            className="inline-flex items-center gap-1 font-medium text-primary hover:underline underline-offset-2"
          >
            {t('goToSettings')}
            <ChevronRight className="h-3 w-3" aria-hidden />
          </a>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-5">

      {/* ── Primary ─────────────────────────────────────────────────────────── */}
      <section className="space-y-2.5">
        <div>
          <Label className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            {t('primaryLabel')}
          </Label>
          <p className="mt-0.5 text-xs text-muted-foreground">{t('primaryDescription')}</p>
        </div>
        <div className="space-y-2">
          {CONFIGURED_PROVIDERS.map((cfg) => (
            <ProviderCard
              key={cfg.provider}
              config={cfg}
              selected={primaryProvider === cfg.provider}
              selectedModel={primaryProvider === cfg.provider ? primaryModel : (cfg.models[0]?.id ?? '')}
              onSelect={() => handlePrimarySelect(cfg.provider)}
              onModelChange={setPrimaryModel}
            />
          ))}
        </div>
      </section>

      <Separator />

      {/* ── Fallback ────────────────────────────────────────────────────────── */}
      <section className="space-y-2.5">
        <div>
          <Label className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            {t('fallbackLabel')}{' '}
            <span className="normal-case font-normal text-muted-foreground/60">(optional)</span>
          </Label>
          <p className="mt-0.5 text-xs text-muted-foreground">{t('fallbackDescription')}</p>
        </div>

        <div className="rounded-lg border border-border bg-card">
          {/* Provider select */}
          <div className="px-4 py-3 space-y-1.5">
            <Label className="text-xs text-muted-foreground">Provider</Label>
            <Select value={fallbackProvider} onValueChange={handleFallbackProviderChange}>
              <SelectTrigger className="h-9">
                <SelectValue placeholder={t('noFallback')} />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="none">{t('noFallback')}</SelectItem>
                {fallbackOptions.map((cfg) => (
                  <SelectItem key={cfg.provider} value={cfg.provider}>
                    {cfg.provider}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Model select — only if fallback provider chosen */}
          {fallbackProvider !== 'none' && fallbackConfig && (
            <>
              <Separator />
              <div className="px-4 py-3 space-y-1.5">
                <Label className="text-xs text-muted-foreground">{t('modelLabel')}</Label>
                <Select value={fallbackModel} onValueChange={setFallbackModel}>
                  <SelectTrigger className="h-9">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {fallbackConfig.models.map((model) => (
                      <SelectItem key={model.id} value={model.id}>
                        <div className="flex items-center justify-between gap-8 w-full">
                          <span>{model.label}</span>
                          <span className="font-mono text-xs text-muted-foreground shrink-0">
                            {model.contextWindow} ctx
                          </span>
                        </div>
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </>
          )}
        </div>
      </section>

      {/* ── Link to global settings ──────────────────────────────────────────── */}
      <a
        href="/dashboard/settings/ai"
        className="inline-flex items-center gap-1.5 text-xs text-muted-foreground hover:text-foreground transition-colors"
      >
        <ExternalLink className="h-3 w-3" aria-hidden />
        {t('manageKeys')}
        <ChevronRight className="h-3 w-3" aria-hidden />
      </a>

      {/* ── Save ────────────────────────────────────────────────────────────── */}
      <div className="flex items-center gap-3 pt-1">
        <Button onClick={handleSave} disabled={saveStatus === 'saving'} className="px-6">
          {saveStatus === 'saving' ? t('saving') : t('save')}
        </Button>
        {saveStatus === 'saved' && (
          <span className="flex items-center gap-1.5 text-sm text-muted-foreground">
            <Check className="h-3.5 w-3.5 text-chatConfidence-high" aria-hidden />
            {t('saved')}
          </span>
        )}
      </div>
    </div>
  );
}
