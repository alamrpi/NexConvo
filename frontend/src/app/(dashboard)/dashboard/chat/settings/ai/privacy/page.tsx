'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { Check } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { Separator } from '@/shared/ui/separator';
import { cn } from '@/shared/lib/cn';

type PiiMode    = 'none' | 'pseudonymise' | 'redact';
type SaveStatus = 'idle' | 'saving' | 'saved';

export default function PrivacyPage() {
  const t = useTranslations('chat.aiSettings.privacy');

  const [piiMode, setPiiMode]           = useState<PiiMode>('pseudonymise');
  const [retentionDays, setRetentionDays] = useState(90);
  const [saveStatus, setSaveStatus]     = useState<SaveStatus>('idle');

  function handleSave() {
    setSaveStatus('saving');
    setTimeout(() => setSaveStatus('saved'), 900);
  }

  const piiOptions: { value: PiiMode; label: string; description: string }[] = [
    { value: 'none',          label: t('piiNoneLabel'),          description: t('piiNoneDescription') },
    { value: 'pseudonymise',  label: t('piiPseudonymiseLabel'),  description: t('piiPseudonymiseDescription') },
    { value: 'redact',        label: t('piiRedactLabel'),        description: t('piiRedactDescription') },
  ];

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-base font-semibold text-foreground">{t('title')}</h2>
        <p className="mt-1 text-sm text-muted-foreground">{t('subtitle')}</p>
      </div>

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
                onChange={() => setPiiMode(value)}
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
            onChange={(e) => setRetentionDays(Number(e.target.value))}
            className="h-9 w-24 text-center tabular-nums"
            aria-label={t('retentionTitle')}
          />
          <span className="text-sm text-muted-foreground">{t('retentionDays')}</span>
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
