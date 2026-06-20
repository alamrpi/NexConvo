import Link from 'next/link';
import { getTranslations } from 'next-intl/server';
import { Logo } from '@/shared/ui/logo';
import { NavLinks } from './nav-links';

/**
 * Fixed desktop sidebar (S24 — hidden on mobile, shown at lg). Server component;
 * the interactive nav list is the client leaf (S6).
 */
export async function AppSidebar() {
  const t = await getTranslations('common');

  return (
    <aside className="sticky top-0 hidden h-dvh w-64 shrink-0 flex-col border-r border-border bg-card lg:flex">
      <div className="flex h-16 items-center border-b border-border px-6">
        <Link href="/dashboard" aria-label={t('appName')}>
          <Logo label={t('appName')} />
        </Link>
      </div>
      <div className="flex-1 overflow-y-auto p-4">
        <NavLinks />
      </div>
      <div className="border-t border-border p-4 text-xs text-muted-foreground">{t('tagline')}</div>
    </aside>
  );
}
