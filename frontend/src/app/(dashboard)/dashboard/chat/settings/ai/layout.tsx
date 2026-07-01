import type { ReactNode } from 'react';
import { getTranslations } from 'next-intl/server';
import { AiSettingsNav } from '@/features/chat/components/ai-settings-nav';

/**
 * AI config sub-shell: title + left sub-nav (Provider | System Prompt | Handoff | Privacy)
 * nested inside the chat settings layout.
 */
export default async function AiSettingsLayout({ children }: { children: ReactNode }) {
  const t = await getTranslations('chat.aiSettings');

  return (
    <div className="flex min-h-0 flex-1 flex-col overflow-y-auto">
      <div className="mx-auto w-full max-w-3xl px-4 py-5 sm:px-6">
        <div className="mb-4">
          <h1 className="text-lg font-semibold tracking-tight text-foreground">{t('title')}</h1>
          <p className="mt-0.5 text-sm text-muted-foreground">{t('subtitle')}</p>
        </div>

        <div className="grid grid-cols-1 gap-x-6 gap-y-3 sm:grid-cols-[148px_minmax(0,1fr)]">
          <AiSettingsNav />
          <div className="min-w-0">{children}</div>
        </div>
      </div>
    </div>
  );
}
