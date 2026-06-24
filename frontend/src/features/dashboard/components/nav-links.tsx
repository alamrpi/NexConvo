'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { cn } from '@/shared/lib/cn';
import { dashboardNav, navSections } from '@/shared/config/nav';

/**
 * Dashboard navigation list. Client leaf (uses `usePathname` for the active state).
 * Shared by the desktop sidebar and the mobile sheet — defined once as data (S5),
 * grouped into labelled sections so the current location is always obvious.
 */
export function NavLinks({ onNavigate }: { onNavigate?: () => void }) {
  const pathname = usePathname();
  const t = useTranslations('nav.dashboard');

  return (
    <nav className="flex flex-col gap-5" aria-label="Dashboard">
      {navSections.map((section) => {
        const items = dashboardNav.filter((item) => item.section === section);
        if (items.length === 0) return null;

        return (
          <div key={section} className="flex flex-col gap-1">
            <p className="px-3 pb-1 text-[0.6875rem] font-semibold uppercase tracking-wider text-muted-foreground">
              {t(`sections.${section}`)}
            </p>
            {items.map((item) => {
              const active =
                item.href === '/dashboard'
                  ? pathname === item.href
                  : pathname.startsWith(item.href);
              const Icon = item.icon;

              return (
                <Link
                  key={item.href}
                  href={item.href}
                  onClick={onNavigate}
                  aria-current={active ? 'page' : undefined}
                  className={cn(
                    'group relative flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors',
                    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                    active
                      ? 'bg-primary/10 text-primary'
                      : 'text-muted-foreground hover:bg-accent hover:text-foreground',
                  )}
                >
                  {active && (
                    <span
                      aria-hidden
                      className="absolute left-0 top-1/2 h-5 w-1 -translate-y-1/2 rounded-r-full bg-primary"
                    />
                  )}
                  <Icon className="h-[1.125rem] w-[1.125rem] shrink-0" />
                  {t(item.labelKey)}
                </Link>
              );
            })}
          </div>
        );
      })}
    </nav>
  );
}
