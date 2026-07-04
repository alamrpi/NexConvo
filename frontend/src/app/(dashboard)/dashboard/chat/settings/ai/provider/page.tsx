'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { AlertCircle, Check, ChevronRight, ExternalLink, Loader2 } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { Badge } from '@/shared/ui/badge';
import { Skeleton } from '@/shared/ui/skeleton';
import { Separator } from '@/shared/ui/separator';
import { cn } from '@/shared/lib/cn';
import type { AiProviderType } from '@/features/settings/model/ai-settings.types';
import { useAiSettings } from '@/features/settings/api/use-ai-settings';
import { useChatSettings } from '@/features/settings/api/use-chat-settings';
import { useUpdateChatSettings, ChatSettingsError } from '@/features/settings/api/use-update-chat-settings';

// ── Provider card ─────────────────────────────────────────────────────────────────

function ProviderCard({
  provider,
  isActive,
  hasKey,
  selected,
  selectedModel,
  onSelect,
  onModelChange,
}: {
  provider: AiProviderType;
  isActive: boolean;
  hasKey: boolean;
  selected: boolean;
  selectedModel: string;
  onSelect: () => void;
  onModelChange: (model: string) => void;
}) {
  const t = useTranslations('chat.aiSettings.provider');

  return (
    <div
      className={cn(
        'rounded-lg border transition-colors',
        selected ? 'border-primary bg-primary/5' : 'border-border bg-card',
        (!isActive || !hasKey) && 'opacity-60',
      )}
    >
      <button
        type="button"
        onClick={onSelect}
        disabled={!isActive || !hasKey}
        aria-pressed={selected}
        className="flex w-full items-center gap-3 rounded-t-lg px-4 py-3 text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-1"
      >
        <div
          className={cn(
            'flex h-4 w-4 shrink-0 items-center justify-center rounded-full border-2 transition-colors',
            selected ? 'border-primary' : 'border-muted-foreground/30',
          )}
          aria-hidden
        >
          {selected && <div className="h-2 w-2 rounded-full bg-primary" />}
        </div>

        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <span className="text-sm font-medium text-foreground">{provider}</span>
            {!hasKey && (
              <Badge variant="outline" className="px-1.5 py-0 text-[0.625rem] text-muted-foreground">
                {t('notConfigured')}
              </Badge>
            )}
          </div>
          {selected && selectedModel && (
            <p className="mt-0.5 font-mono text-xs text-muted-foreground">{selectedModel}</p>
          )}
        </div>

        {selected && <Check className="h-4 w-4 shrink-0 text-primary" aria-hidden />}
      </button>

      {selected && isActive && hasKey && (
        <>
          <Separator />
          <div className="space-y-1.5 px-4 py-3">
            <Label htmlFor={`model-${provider}`} className="text-xs text-muted-foreground">
              {t('modelLabel')}
            </Label>
            <Input
              id={`model-${provider}`}
              value={selectedModel}
              onChange={(e) => onModelChange(e.target.value)}
              placeholder="e.g. openai/gpt-4o"
              className="h-9 font-mono text-sm"
            />
          </div>
        </>
      )}
    </div>
  );
}

// ── Loading skeleton ──────────────────────────────────────────────────────────────

function ProviderPageSkeleton() {
  return (
    <div className="space-y-5">
      <div className="space-y-2.5">
        <Skeleton className="h-3.5 w-28" />
        <Skeleton className="h-3 w-44" />
        {Array.from({ length: 3 }).map((_, i) => (
          <Skeleton key={i} className="h-12 w-full rounded-lg" />
        ))}
      </div>
      <Skeleton className="h-px w-full" />
      <div className="space-y-2.5">
        <Skeleton className="h-3.5 w-28" />
        <Skeleton className="h-12 w-full rounded-lg" />
      </div>
    </div>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────────

export default function AiProviderPage() {
  const t = useTranslations('chat.aiSettings.provider');

  const { data: globalProviders, isLoading: providersLoading, isError: providersError } =
    useAiSettings();
  const { data: chatSettings, isLoading: settingsLoading, isError: settingsError } =
    useChatSettings();
  const update = useUpdateChatSettings();

  const isLoading = providersLoading || settingsLoading;
  const isError = providersError || settingsError;

  // Local form state — mirror server state but allow user to edit without affecting the cache
  const [primaryProvider, setPrimaryProvider] = useState<AiProviderType | null>(null);
  const [primaryModel, setPrimaryModel] = useState('');
  const [fallbackProviders, setFallbackProviders] = useState<AiProviderType[]>([]);
  const [saveStatus, setSaveStatus] = useState<'idle' | 'saving' | 'saved'>('idle');

  // Seed local state once when both queries resolve, without overwriting mid-edit
  const [initialized, setInitialized] = useState(false);
  if (!initialized && chatSettings && globalProviders) {
    setPrimaryProvider(chatSettings.primaryProvider);
    setPrimaryModel(chatSettings.primaryModel);
    setFallbackProviders(chatSettings.fallbackProviders);
    setInitialized(true);
  }

  if (isLoading) return <ProviderPageSkeleton />;

  if (isError || !globalProviders || !chatSettings) {
    return (
      <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive">
        {t('noProvidersDescription')}
      </p>
    );
  }

  const activeProviders = globalProviders.filter((p) => p.isActive && p.hasApiKey);

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

  const currentPrimary = primaryProvider ?? activeProviders[0]?.provider ?? ('OpenAI' as AiProviderType);

  function handlePrimarySelect(provider: AiProviderType) {
    setPrimaryProvider(provider);
    setSaveStatus('idle');
    setFallbackProviders((prev) => prev.filter((p) => p !== provider));
  }

  function toggleFallback(provider: AiProviderType) {
    setSaveStatus('idle');
    setFallbackProviders((prev) =>
      prev.includes(provider) ? prev.filter((p) => p !== provider) : [...prev, provider],
    );
  }

  async function handleSave() {
    setSaveStatus('saving');
    await update
      .mutateAsync({
        primaryProvider: currentPrimary,
        primaryModel,
        fallbackProviders,
        systemPromptOverride: chatSettings?.systemPromptOverride ?? null,
        handoffConfidenceThreshold: chatSettings?.handoffConfidenceThreshold ?? 0.65,
        sentimentEscalationEnabled: chatSettings?.sentimentEscalationEnabled ?? true,
        sentimentSensitivity: chatSettings?.sentimentSensitivity ?? 'medium',
        triggerPhrases: chatSettings?.triggerPhrases ?? [],
        maxUnansweredMessages: chatSettings?.maxUnansweredMessages ?? 3,
        piiMaskingLevel: chatSettings?.piiMaskingLevel ?? 'standard',
        dataRetentionDays: chatSettings?.dataRetentionDays ?? null,
      })
      .catch(() => undefined);
    setSaveStatus(update.isError ? 'idle' : 'saved');
  }

  const isConflict = update.error instanceof ChatSettingsError && update.error.code === 'conflict';
  const fallbackOptions = globalProviders.filter(
    (p) => p.isActive && p.hasApiKey && p.provider !== currentPrimary,
  );

  return (
    <div className="space-y-5">
      {update.isError && (
        <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {isConflict
            ? 'Someone else changed these settings. Reload and try again.'
            : 'Couldn\'t save settings. Please try again.'}
        </p>
      )}

      {/* ── Primary provider ──────────────────────────────────────────────────── */}
      <section className="space-y-2.5">
        <div>
          <Label className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            {t('primaryLabel')}
          </Label>
          <p className="mt-0.5 text-xs text-muted-foreground">{t('primaryDescription')}</p>
        </div>
        <div className="space-y-2">
          {globalProviders.map((cfg) => (
            <ProviderCard
              key={cfg.provider}
              provider={cfg.provider}
              isActive={cfg.isActive}
              hasKey={cfg.hasApiKey}
              selected={currentPrimary === cfg.provider}
              selectedModel={currentPrimary === cfg.provider ? primaryModel : cfg.defaultModel}
              onSelect={() => handlePrimarySelect(cfg.provider)}
              onModelChange={(m) => {
                setPrimaryModel(m);
                setSaveStatus('idle');
              }}
            />
          ))}
        </div>
      </section>

      <Separator />

      {/* ── Fallback providers ────────────────────────────────────────────────── */}
      <section className="space-y-2.5">
        <div>
          <Label className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            {t('fallbackLabel')}{' '}
            <span className="normal-case font-normal text-muted-foreground/60">(optional)</span>
          </Label>
          <p className="mt-0.5 text-xs text-muted-foreground">{t('fallbackDescription')}</p>
        </div>
        {fallbackOptions.length === 0 ? (
          <p className="text-xs text-muted-foreground">{t('noFallback')}</p>
        ) : (
          <div className="space-y-2">
            {fallbackOptions.map((cfg) => {
              const isFallback = fallbackProviders.includes(cfg.provider);
              return (
                <button
                  key={cfg.provider}
                  type="button"
                  onClick={() => toggleFallback(cfg.provider)}
                  aria-pressed={isFallback}
                  className={cn(
                    'flex w-full items-center gap-3 rounded-lg border px-4 py-3 text-left transition-colors',
                    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-1',
                    isFallback
                      ? 'border-primary bg-primary/5'
                      : 'border-border bg-card hover:bg-muted/40',
                  )}
                >
                  <div
                    className={cn(
                      'flex h-4 w-4 shrink-0 items-center justify-center rounded border-2 transition-colors',
                      isFallback ? 'border-primary bg-primary' : 'border-muted-foreground/30',
                    )}
                    aria-hidden
                  >
                    {isFallback && <Check className="h-2.5 w-2.5 text-primary-foreground" />}
                  </div>
                  <span className="text-sm font-medium text-foreground">{cfg.provider}</span>
                </button>
              );
            })}
          </div>
        )}
      </section>

      {/* Link to global AI settings */}
      <a
        href="/dashboard/settings/ai"
        className="inline-flex items-center gap-1.5 text-xs text-muted-foreground transition-colors hover:text-foreground"
      >
        <ExternalLink className="h-3 w-3" aria-hidden />
        {t('manageKeys')}
        <ChevronRight className="h-3 w-3" aria-hidden />
      </a>

      {/* ── Save ──────────────────────────────────────────────────────────────── */}
      <div className="flex items-center gap-3 pt-1">
        <Button
          onClick={handleSave}
          disabled={saveStatus === 'saving' || update.isPending}
          className="px-6"
        >
          {saveStatus === 'saving' || update.isPending ? (
            <>
              <Loader2 className="mr-2 h-4 w-4 animate-spin" aria-hidden />
              {t('saving')}
            </>
          ) : (
            t('save')
          )}
        </Button>
        {saveStatus === 'saved' && !update.isError && (
          <span className="flex items-center gap-1.5 text-sm text-muted-foreground">
            <Check className="h-3.5 w-3.5 text-chatConfidence-high" aria-hidden />
            {t('saved')}
          </span>
        )}
      </div>
    </div>
  );
}
