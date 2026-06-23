'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { Mail, SlidersHorizontal, Users, CreditCard, type LucideIcon } from 'lucide-react';
import { cn } from '@/shared/lib/cn';

interface SettingsSection {
  key: string;
  href: string;
  icon: LucideIcon;
  enabled: boolean;
}

// Email ships first; the rest are placeholders for upcoming settings sections.
const sections: readonly SettingsSection[] = [
  { key: 'email', href: '/dashboard/settings/email', icon: Mail, enabled: true },
  { key: 'members', href: '/dashboard/settings/members', icon: Users, enabled: true },
  { key: 'general', href: '/dashboard/settings/general', icon: SlidersHorizontal, enabled: false },
  { key: 'billing', href: '/dashboard/settings/billing', icon: CreditCard, enabled: false },
] as const;

export function SettingsNav() {
  const pathname = usePathname();
  const t = useTranslations('settings.nav');

  return (
    <nav
      aria-label={t('label')}
      className="flex gap-1 overflow-x-auto pb-2 md:flex-col md:overflow-visible md:pb-0"
    >
      {sections.map((section) => {
        const Icon = section.icon;
        const isActive = pathname === section.href || pathname.startsWith(`${section.href}/`);
        const base =
          'flex shrink-0 items-center gap-2 rounded-md px-3 py-2 text-sm font-medium transition-colors';

        if (!section.enabled) {
          return (
            <span
              key={section.key}
              aria-disabled="true"
              className={cn(base, 'cursor-not-allowed text-muted-foreground/50')}
            >
              <Icon className="h-4 w-4" aria-hidden="true" />
              {t(section.key)}
              <span className="ml-auto hidden text-xs md:inline">{t('soon')}</span>
            </span>
          );
        }

        return (
          <Link
            key={section.key}
            href={section.href}
            aria-current={isActive ? 'page' : undefined}
            className={cn(
              base,
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
              isActive
                ? 'bg-muted text-foreground'
                : 'text-muted-foreground hover:bg-muted/60 hover:text-foreground',
            )}
          >
            <Icon className="h-4 w-4" aria-hidden="true" />
            {t(section.key)}
          </Link>
        );
      })}
    </nav>
  );
}
