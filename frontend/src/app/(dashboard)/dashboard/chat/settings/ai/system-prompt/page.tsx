'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { Check, ExternalLink, Loader2 } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Label } from '@/shared/ui/label';
import { Textarea } from '@/shared/ui/textarea';
import { Skeleton } from '@/shared/ui/skeleton';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/shared/ui/select';
import { useChatSettings } from '@/features/settings/api/use-chat-settings';
import { useUpdateChatSettings, ChatSettingsError } from '@/features/settings/api/use-update-chat-settings';

// ── Preset templates ──────────────────────────────────────────────────────────────

const PRESET_TEMPLATES: Record<string, string> = {
  retail:
    'You are a helpful customer service AI for a retail business. You assist customers with orders, returns, products, and general inquiries. Always be polite and concise. If you cannot help, offer to connect to a human agent.',
  ecommerce:
    'You are an AI shopping assistant. Help customers find products, track orders, and resolve issues. Greet customers warmly and use their name when known.',
  support:
    'You are a technical support assistant. Diagnose issues systematically, provide clear step-by-step instructions, and escalate complex problems to human agents.',
};

// ── Loading skeleton ──────────────────────────────────────────────────────────────

function SystemPromptSkeleton() {
  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <Skeleton className="h-5 w-40" />
        <Skeleton className="h-3.5 w-72" />
      </div>
      <div className="space-y-1.5">
        <Skeleton className="h-3.5 w-32" />
        <Skeleton className="h-9 w-48" />
      </div>
      <div className="space-y-1.5">
        <Skeleton className="h-3.5 w-16" />
        <Skeleton className="h-48 w-full rounded-md" />
        <Skeleton className="h-3 w-24" />
      </div>
    </div>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────────

export default function SystemPromptPage() {
  const t = useTranslations('chat.aiSettings.systemPrompt');

  const { data: chatSettings, isLoading, isError } = useChatSettings();
  const update = useUpdateChatSettings();

  const [systemPrompt, setSystemPrompt] = useState<string>('');
  const [saveStatus, setSaveStatus] = useState<'idle' | 'saving' | 'saved'>('idle');

  const [initialized, setInitialized] = useState(false);
  if (!initialized && chatSettings) {
    setSystemPrompt(chatSettings.systemPromptOverride ?? PRESET_TEMPLATES['retail'] ?? '');
    setInitialized(true);
  }

  if (isLoading) return <SystemPromptSkeleton />;

  if (isError || !chatSettings) {
    return (
      <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive">
        Couldn&apos;t load system prompt. Please try again.
      </p>
    );
  }

  const tokenEstimate = Math.ceil(systemPrompt.length / 4);
  // Use btoa for base64 encoding so the playground can pre-fill the prompt
  const testUrl = `/dashboard/chat/playground?systemPrompt=${encodeURIComponent(btoa(systemPrompt))}`;

  async function handleSave() {
    if (!chatSettings) return;
    setSaveStatus('saving');
    await update
      .mutateAsync({
        ...chatSettings,
        systemPromptOverride: systemPrompt || null,
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

      {/* Template selector */}
      <div className="space-y-1.5">
        <Label htmlFor="template-select" className="text-sm">
          {t('templateLabel')}
        </Label>
        <Select
          onValueChange={(v) => {
            const tpl = PRESET_TEMPLATES[v];
            if (tpl) {
              setSystemPrompt(tpl);
              setSaveStatus('idle');
            }
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
          onChange={(e) => {
            setSystemPrompt(e.target.value);
            setSaveStatus('idle');
          }}
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
