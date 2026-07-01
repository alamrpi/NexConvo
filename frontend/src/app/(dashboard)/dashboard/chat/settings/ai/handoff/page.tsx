'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { X, Check } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { Separator } from '@/shared/ui/separator';
import { Slider } from '@/shared/ui/slider';
import { Switch } from '@/shared/ui/switch';
import { cn } from '@/shared/lib/cn';

type Sensitivity = 'low' | 'medium' | 'high';
type SaveStatus  = 'idle' | 'saving' | 'saved';

function TagInput({
  tags,
  onAdd,
  onRemove,
  placeholder,
}: {
  tags: string[];
  onAdd: (tag: string) => void;
  onRemove: (tag: string) => void;
  placeholder: string;
}) {
  const [draft, setDraft] = useState('');

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key !== 'Enter') return;
    e.preventDefault();
    const trimmed = draft.trim();
    if (trimmed && !tags.includes(trimmed)) { onAdd(trimmed); setDraft(''); }
  }

  return (
    <div className="space-y-2">
      <Input
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        onKeyDown={handleKeyDown}
        placeholder={placeholder}
        className="h-9"
      />
      {tags.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {tags.map((phrase) => (
            <span
              key={phrase}
              className="inline-flex items-center gap-1 rounded-md border border-border bg-muted px-2 py-0.5 text-xs text-foreground"
            >
              {phrase}
              <button
                type="button"
                aria-label={`Remove "${phrase}"`}
                onClick={() => onRemove(phrase)}
                className="ml-0.5 rounded hover:text-destructive focus-visible:outline-none"
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

export default function HandoffPage() {
  const t = useTranslations('chat.aiSettings.handoff');

  const [confidence, setConfidence]           = useState(65);
  const [sentimentEnabled, setSentimentEnabled] = useState(true);
  const [sensitivity, setSensitivity]         = useState<Sensitivity>('medium');
  const [triggerPhrases, setTriggerPhrases]   = useState<string[]>([
    'speak to a manager', 'human agent', 'escalate',
  ]);
  const [maxUnanswered, setMaxUnanswered]     = useState(3);
  const [saveStatus, setSaveStatus]           = useState<SaveStatus>('idle');

  function handleSave() {
    setSaveStatus('saving');
    setTimeout(() => setSaveStatus('saved'), 900);
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-base font-semibold text-foreground">{t('title')}</h2>
        <p className="mt-1 text-sm text-muted-foreground">{t('subtitle')}</p>
      </div>

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
          min={0} max={100} step={5}
          value={[confidence]}
          onValueChange={([v]) => { if (v !== undefined) setConfidence(v); }}
          aria-label={`Confidence threshold: ${confidence}%`}
        />
        <div className="flex justify-between text-[0.625rem] text-muted-foreground">
          <span>{t('confidenceAlways')}</span>
          <span>{t('confidenceNever')}</span>
        </div>
      </div>

      {/* Negative sentiment */}
      <div className="space-y-3 rounded-lg border border-border bg-card p-4">
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm font-medium text-foreground">{t('sentimentTitle')}</p>
            <p className="text-xs text-muted-foreground">{t('sentimentDescription')}</p>
          </div>
          <Switch
            checked={sentimentEnabled}
            onCheckedChange={setSentimentEnabled}
            aria-label={t('sentimentTitle')}
          />
        </div>
        {sentimentEnabled && (
          <>
            <Separator />
            <div className="space-y-1.5">
              <p className="text-xs text-muted-foreground">{t('sensitivityLabel')}</p>
              <div className="flex gap-2">
                {(['low', 'medium', 'high'] as Sensitivity[]).map((s) => (
                  <button
                    key={s}
                    type="button"
                    onClick={() => setSensitivity(s)}
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

      {/* Trigger phrases */}
      <div className="space-y-2">
        <Label>{t('triggerPhrasesLabel')}</Label>
        <p className="text-xs text-muted-foreground">{t('triggerPhrasesDescription')}</p>
        <TagInput
          tags={triggerPhrases}
          onAdd={(tag) => setTriggerPhrases((p) => [...p, tag])}
          onRemove={(tag) => setTriggerPhrases((p) => p.filter((x) => x !== tag))}
          placeholder={t('triggerPlaceholder')}
        />
      </div>

      {/* Max unanswered */}
      <div className="flex items-center justify-between rounded-lg border border-border bg-card px-4 py-3">
        <div>
          <p className="text-sm font-medium text-foreground">{t('maxUnansweredTitle')}</p>
          <p className="text-xs text-muted-foreground">{t('maxUnansweredDescription')}</p>
        </div>
        <div className="flex items-center gap-2">
          <button
            type="button"
            aria-label="Decrease"
            onClick={() => setMaxUnanswered((v) => Math.max(1, v - 1))}
            className="flex h-7 w-7 items-center justify-center rounded-md border border-border text-muted-foreground hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >−</button>
          <span className="w-6 text-center text-sm font-semibold tabular-nums">{maxUnanswered}</span>
          <button
            type="button"
            aria-label="Increase"
            onClick={() => setMaxUnanswered((v) => Math.min(10, v + 1))}
            className="flex h-7 w-7 items-center justify-center rounded-md border border-border text-muted-foreground hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >+</button>
        </div>
      </div>

      {/* Save */}
      <div className="flex items-center gap-3 pt-2">
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
