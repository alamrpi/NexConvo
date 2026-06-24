import type { ReactNode } from 'react';
import { getTranslations } from 'next-intl/server';
import { SettingsNav } from '@/features/settings/components/settings-nav';

/**
 * Settings shell: a left sub-nav + content panel (mobile-first — the nav becomes a
 * horizontal scroller below md). Auth is already guarded by the (dashboard) layout.
 */
export default async function SettingsLayout({ children }: { children: ReactNode }) {
  const t = await getTranslations('settings');

  return (
    <div className="mx-auto grid max-w-6xl grid-cols-1 gap-x-8 gap-y-4 md:grid-cols-[200px_minmax(0,1fr)]">
      <aside className="min-w-0 md:space-y-4">
        <div className="mb-3 md:mb-0">
          <h1 className="text-base font-semibold tracking-tight">{t('title')}</h1>
          <p className="text-xs text-muted-foreground">{t('subtitle')}</p>
        </div>
        <SettingsNav />
      </aside>
      <div className="min-w-0">{children}</div>
    </div>
  );
}
