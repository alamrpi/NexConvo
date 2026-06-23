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
    <div className="space-y-6">
      <div className="space-y-1">
        <h1 className="text-3xl font-bold tracking-tight">{t('title')}</h1>
        <p className="text-sm text-muted-foreground">{t('subtitle')}</p>
      </div>

      <div className="grid gap-6 md:grid-cols-[200px_minmax(0,1fr)]">
        <aside>
          <SettingsNav />
        </aside>
        <div className="min-w-0">{children}</div>
      </div>
    </div>
  );
}
