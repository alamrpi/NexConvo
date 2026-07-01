'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { ExternalLink, Check } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Label } from '@/shared/ui/label';
import { Textarea } from '@/shared/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/shared/ui/select';

type SaveStatus = 'idle' | 'saving' | 'saved';

const PRESET_TEMPLATES: Record<string, string> = {
  retail:
    'You are a helpful customer service AI for a retail business. You assist customers with orders, returns, products, and general inquiries. Always be polite and concise. If you cannot help, offer to connect to a human agent.',
  ecommerce:
    'You are an AI shopping assistant. Help customers find products, track orders, and resolve issues. Greet customers warmly and use their name when known.',
  support:
    'You are a technical support assistant. Diagnose issues systematically, provide clear step-by-step instructions, and escalate complex problems to human agents.',
};

export default function SystemPromptPage() {
  const t = useTranslations('chat.aiSettings.systemPrompt');

  const [systemPrompt, setSystemPrompt] = useState<string>(PRESET_TEMPLATES['retail'] ?? '');
  const [saveStatus, setSaveStatus] = useState<SaveStatus>('idle');

  const tokenEstimate = Math.ceil(systemPrompt.length / 4);
  const testUrl = `/dashboard/chat/playground?prompt=${encodeURIComponent(systemPrompt)}`;

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

      {/* Template selector */}
      <div className="space-y-1.5">
        <Label htmlFor="template-select" className="text-sm">{t('templateLabel')}</Label>
        <Select
          onValueChange={(v) => {
            const tpl = PRESET_TEMPLATES[v];
            if (tpl) setSystemPrompt(tpl);
          }}
        >
          <SelectTrigger id="template-select" className="h-9 max-w-xs">
            <SelectValue placeholder={t('templatePlaceholder')} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="retail">{t('templates.retail')}</SelectItem>
            <SelectItem value="ecommerce">{t('templates.ecommerce')}</SelectItem>
            <SelectItem value="support">{t('templates.support')}</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {/* Prompt textarea */}
      <div className="space-y-1.5">
        <Label htmlFor="system-prompt">{t('promptLabel')}</Label>
        <Textarea
          id="system-prompt"
          rows={10}
          value={systemPrompt}
          onChange={(e) => setSystemPrompt(e.target.value)}
          className="resize-y font-mono text-sm"
          placeholder={t('promptPlaceholder')}
        />
        <div className="flex items-center justify-between">
          <p className="text-xs text-muted-foreground">
            {t('tokenEstimate', { count: tokenEstimate.toLocaleString() })}
          </p>
          <a
            href={testUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center gap-1 text-xs text-primary hover:underline underline-offset-2"
          >
            {t('testLink')}
            <ExternalLink className="h-3 w-3" aria-hidden />
          </a>
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
