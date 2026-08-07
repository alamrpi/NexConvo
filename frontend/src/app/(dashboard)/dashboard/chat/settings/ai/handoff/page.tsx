'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { Check, Loader2, X } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { Skeleton } from '@/shared/ui/skeleton';
import { Separator } from '@/shared/ui/separator';
import { Slider } from '@/shared/ui/slider';
import { Switch } from '@/shared/ui/switch';
import { cn } from '@/shared/lib/cn';
import type { SentimentSensitivity } from '@/features/settings/model/chat-settings.types';
import { useChatSettings } from '@/features/settings/api/use-chat-settings';
import { useUpdateChatSettings, ChatSettingsError } from '@/features/settings/api/use-update-chat-settings';

// ── Tag input component ───────────────────────────────────────────────────────────

const DEFAULT_TRIGGER_PHRASES = [
  'আমি মানুষের সাথে কথা বলতে চাই',
  'speak to a manager',
  'human agent',
];

function TagInput({
  tags,
  onAdd,
  onRemove,
  placeholder,
  maxTags,
}: {
  tags: string[];
  onAdd: (tag: string) => void;
  onRemove: (tag: string) => void;
  placeholder: string;
  maxTags: number;
}) {
  const [draft, setDraft] = useState('');

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key !== 'Enter') return;
    e.preventDefault();
    const trimmed = draft.trim();
    if (trimmed && !tags.includes(trimmed) && tags.length < maxTags) {
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
        className="h-9"
        aria-label={placeholder}
        disabled={tags.length >= maxTags}
      />
      {tags.length >= maxTags && (
        <p className="text-xs text-muted-foreground">Maximum {maxTags} phrases.</p>
      )}
      {tags.length > 0 && (
        <div className="flex flex-wrap gap-1.5" role="list" aria-label="Trigger phrases">
          {tags.map((phrase) => (
            <span
              key={phrase}
              role="listitem"
              className="inline-flex items-center gap-1 rounded-md border border-border bg-muted px-2 py-0.5 text-xs text-foreground"
            >
              {phrase}
              <button
                type="button"
                aria-label={`Remove "${phrase}"`}
                onClick={() => onRemove(phrase)}
                className="ml-0.5 rounded hover:text-destructive focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              >
                <X className="h-3 w-3" aria-hidden />
              </button>
            </span>
          ))}
        </div>
      )}
    </div>
  );
}

// ── Loading skeleton ──────────────────────────────────────────────────────────────

function HandoffSkeleton() {
  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <Skeleton className="h-5 w-36" />
        <Skeleton className="h-3.5 w-64" />
      </div>
      {Array.from({ length: 3 }).map((_, i) => (
        <Skeleton key={i} className="h-24 w-full rounded-lg" />
      ))}
    </div>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────────

export default function HandoffPage() {
  const t = useTranslations('chat.aiSettings.handoff');

  const { data: chatSettings, isLoading, isError } = useChatSettings();
  const update = useUpdateChatSettings();

  // Local state mirrors server — seeded once, then owned by the user until save
  const [confidence, setConfidence] = useState(65);
  const [sentimentEnabled, setSentimentEnabled] = useState(true);
  const [sensitivity, setSensitivity] = useState<SentimentSensitivity>('medium');
  const [triggerPhrases, setTriggerPhrases] = useState<string[]>([]);
  const [maxUnanswered, setMaxUnanswered] = useState(3);
  const [saveStatus, setSaveStatus] = useState<'idle' | 'saving' | 'saved'>('idle');

  const [initialized, setInitialized] = useState(false);
  if (!initialized && chatSettings) {
    setConfidence(Math.round(chatSettings.handoffConfidenceThreshold * 100));
    setSentimentEnabled(chatSettings.sentimentEscalationEnabled);
    setSensitivity(chatSettings.sentimentSensitivity);
    setTriggerPhrases(
      chatSettings.triggerPhrases.length > 0
        ? chatSettings.triggerPhrases
        : DEFAULT_TRIGGER_PHRASES,
    );
    setMaxUnanswered(chatSettings.maxUnansweredMessages);
    setInitialized(true);
  }

  if (isLoading) return <HandoffSkeleton />;

  if (isError || !chatSettings) {
    return (
      <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive">
        Couldn&apos;t load handoff settings. Please try again.
      </p>
    );
  }

  async function handleSave() {
    if (!chatSettings) return;
    setSaveStatus('saving');
    await update
      .mutateAsync({
        ...chatSettings,
        handoffConfidenceThreshold: confidence / 100,
        sentimentEscalationEnabled: sentimentEnabled,
        sentimentSensitivity: sensitivity,
        triggerPhrases,
        maxUnansweredMessages: maxUnanswered,
      })
      .catch(() => undefined);
    setSaveStatus(update.isError ? 'idle' : 'saved');
  }

  const isConflict = update.error instanceof ChatSettingsError && update.error.code === 'conflict';

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-base font-semibold text-foreground">{t('title')}</h2>
        <p className="mt-1 text-sm text-muted-foreground">{t('subtitle')}</p>
      </div>

      {update.isError && (
        <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {isConflict
            ? 'Someone else changed these settings. Reload and try again.'
            : "Couldn't save settings. Please try again."}
        </p>
      )}

      {/* Confidence threshold */}
      <div className="space-y-3 rounded-lg border border-border bg-card p-4">
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm font-medium text-foreground">{t('confidenceTitle')}</p>
            <p className="text-xs text-muted-foreground">{t('confidenceDescription')}</p>
          </div>
          <span className="text-sm font-semibold tabular-nums text-foreground">{confidence}%</span>
        </div>
        <Slider
          min={0}
          max={100}
          step={1}
          value={[confidence]}
          onValueChange={([v]) => {
            if (v !== undefined) {
              setConfidence(v);
              setSaveStatus('idle');
            }
          }}
          aria-label={`Confidence threshold: ${confidence}%`}
        />
        <div className="flex justify-between text-[0.625rem] text-muted-foreground">
          <span>{t('confidenceAlways')}</span>
          <span>{t('confidenceNever')}</span>
        </div>
      </div>

      {/* Negative sentiment toggle + sensitivity */}
      <div className="space-y-3 rounded-lg border border-border bg-card p-4">
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm font-medium text-foreground">{t('sentimentTitle')}</p>
            <p className="text-xs text-muted-foreground">{t('sentimentDescription')}</p>
          </div>
          <Switch
            checked={sentimentEnabled}
            onCheckedChange={(v) => {
              setSentimentEnabled(v);
              setSaveStatus('idle');
            }}
            aria-label={t('sentimentTitle')}
          />
        </div>
        {sentimentEnabled && (
          <>
            <Separator />
            <div className="space-y-1.5">
              <p className="text-xs text-muted-foreground">{t('sensitivityLabel')}</p>
              <div className="flex gap-2" role="radiogroup" aria-label={t('sensitivityLabel')}>
                {(['low', 'medium', 'high'] as SentimentSensitivity[]).map((s) => (
                  <button
                    key={s}
                    type="button"
                    onClick={() => {
                      setSensitivity(s);
                      setSaveStatus('idle');
                    }}
                    aria-pressed={sensitivity === s}
                    className={cn(
                      'flex-1 rounded-md border px-3 py-1.5 text-xs font-medium capitalize transition-colors',
                      'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                      sensitivity === s
                        ? 'border-primary bg-primary text-primary-foreground'
                        : 'border-border bg-background text-foreground hover:bg-muted',
                    )}
                  >
                    {t(s)}
                  </button>
                ))}
              </div>
            </div>
          </>
        )}
      </div>

      {/* Trigger phrases — Bengali-first (S12): default phrases include Bengali */}
      <div className="space-y-2">
        <Label>{t('triggerPhrasesLabel')}</Label>
        <p className="text-xs text-muted-foreground">{t('triggerPhrasesDescription')}</p>
        <TagInput
          tags={triggerPhrases}
          onAdd={(tag) => {
            setTriggerPhrases((p) => [...p, tag]);
            setSaveStatus('idle');
          }}
          onRemove={(tag) => {
            setTriggerPhrases((p) => p.filter((x) => x !== tag));
            setSaveStatus('idle');
          }}
          placeholder={t('triggerPlaceholder')}
          maxTags={50}
        />
      </div>

      {/* Max unanswered counter */}
      <div className="flex items-center justify-between rounded-lg border border-border bg-card px-4 py-3">
        <div>
          <p className="text-sm font-medium text-foreground">{t('maxUnansweredTitle')}</p>
          <p className="text-xs text-muted-foreground">{t('maxUnansweredDescription')}</p>
        </div>
        <div className="flex items-center gap-2">
          <button
            type="button"
            aria-label="Decrease"
            onClick={() => {
              setMaxUnanswered((v) => Math.max(1, v - 1));
              setSaveStatus('idle');
            }}
            className="flex h-7 w-7 items-center justify-center rounded-md border border-border text-muted-foreground hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            −
          </button>
          <span className="w-6 text-center text-sm font-semibold tabular-nums">{maxUnanswered}</span>
          <button
            type="button"
            aria-label="Increase"
            onClick={() => {
              setMaxUnanswered((v) => Math.min(20, v + 1));
              setSaveStatus('idle');
            }}
            className="flex h-7 w-7 items-center justify-center rounded-md border border-border text-muted-foreground hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            +
          </button>
        </div>
      </div>

      {/* Save */}
      <div className="flex items-center gap-3 pt-2">
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
