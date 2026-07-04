'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { Check, Loader2 } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { Separator } from '@/shared/ui/separator';
import { Skeleton } from '@/shared/ui/skeleton';
import { cn } from '@/shared/lib/cn';
import type { PiiMaskingLevel } from '@/features/settings/model/chat-settings.types';
import { useChatSettings } from '@/features/settings/api/use-chat-settings';
import { useUpdateChatSettings, ChatSettingsError } from '@/features/settings/api/use-update-chat-settings';

// ── Loading skeleton ──────────────────────────────────────────────────────────────

function PrivacySkeleton() {
  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <Skeleton className="h-5 w-36" />
        <Skeleton className="h-3.5 w-64" />
      </div>
      <div className="space-y-2">
        {Array.from({ length: 3 }).map((_, i) => (
          <Skeleton key={i} className="h-16 w-full rounded-lg" />
        ))}
      </div>
      <Skeleton className="h-px w-full" />
      <div className="space-y-3">
        <Skeleton className="h-3.5 w-40" />
        <Skeleton className="h-9 w-32" />
      </div>
    </div>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────────

export default function PrivacyPage() {
  const t = useTranslations('chat.aiSettings.privacy');

  const { data: chatSettings, isLoading, isError } = useChatSettings();
  const update = useUpdateChatSettings();

  // Local state seeded from server data
  const [piiMode, setPiiMode] = useState<PiiMaskingLevel>('standard');
  const [retentionDays, setRetentionDays] = useState<string>('');
  const [saveStatus, setSaveStatus] = useState<'idle' | 'saving' | 'saved'>('idle');

  const [initialized, setInitialized] = useState(false);
  if (!initialized && chatSettings) {
    setPiiMode(chatSettings.piiMaskingLevel);
    setRetentionDays(chatSettings.dataRetentionDays?.toString() ?? '');
    setInitialized(true);
  }

  if (isLoading) return <PrivacySkeleton />;

  if (isError || !chatSettings) {
    return (
      <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive">
        Couldn&apos;t load privacy settings. Please try again.
      </p>
    );
  }

  async function handleSave() {
    if (!chatSettings) return;
    const parsedDays = retentionDays.trim() === '' ? null : parseInt(retentionDays, 10);
    setSaveStatus('saving');
    await update
      .mutateAsync({
        ...chatSettings,
        piiMaskingLevel: piiMode,
        dataRetentionDays: parsedDays,
      })
      .catch(() => undefined);
    setSaveStatus(update.isError ? 'idle' : 'saved');
  }

  const isConflict = update.error instanceof ChatSettingsError && update.error.code === 'conflict';

  // Map the schema's PiiMaskingLevel to the existing i18n keys
  // 'off' → piiNone, 'standard' → piiPseudonymise, 'strict' → piiRedact
  const piiOptions: { value: PiiMaskingLevel; label: string; description: string }[] = [
    { value: 'off', label: t('piiNoneLabel'), description: t('piiNoneDescription') },
    { value: 'standard', label: t('piiPseudonymiseLabel'), description: t('piiPseudonymiseDescription') },
    { value: 'strict', label: t('piiRedactLabel'), description: t('piiRedactDescription') },
  ];

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

      {/* PII handling */}
      <div className="space-y-3">
        <div>
          <Label className="text-sm font-medium">{t('piiTitle')}</Label>
          <p className="mt-0.5 text-xs text-muted-foreground">{t('piiDescription')}</p>
        </div>
        <fieldset className="space-y-2">
          <legend className="sr-only">{t('piiTitle')}</legend>
          {piiOptions.map(({ value, label, description }) => (
            <label
              key={value}
              className={cn(
                'flex cursor-pointer items-start gap-3 rounded-lg border px-4 py-3 transition-colors',
                'focus-within:ring-2 focus-within:ring-ring',
                piiMode === value
                  ? 'border-primary bg-primary/5'
                  : 'border-border bg-card hover:bg-muted/40',
              )}
            >
              <input
                type="radio"
                name="pii-mode"
                value={value}
                checked={piiMode === value}
                onChange={() => {
                  setPiiMode(value);
                  setSaveStatus('idle');
                }}
                className="mt-0.5 h-4 w-4 accent-primary"
              />
              <div>
                <span className="block text-sm font-medium text-foreground">{label}</span>
                <span className="block text-xs text-muted-foreground">{description}</span>
              </div>
            </label>
          ))}
        </fieldset>
      </div>

      <Separator />

      {/* Data retention */}
      <div className="space-y-3">
        <div>
          <Label className="text-sm font-medium">{t('retentionTitle')}</Label>
          <p className="mt-0.5 text-xs text-muted-foreground">{t('retentionDescription')}</p>
        </div>
        <div className="flex items-center gap-3">
          <Input
            type="number"
            min={7}
            max={730}
            value={retentionDays}
            onChange={(e) => {
              setRetentionDays(e.target.value);
              setSaveStatus('idle');
            }}
            placeholder="90"
            className="h-9 w-24 text-center tabular-nums"
            aria-label={t('retentionTitle')}
          />
          <span className="text-sm text-muted-foreground">{t('retentionDays')}</span>
          {retentionDays === '' && (
            <span className="text-xs text-muted-foreground">(platform default)</span>
          )}
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
