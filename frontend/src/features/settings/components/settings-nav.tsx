'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useTranslations } from 'next-intl';
import {
  CreditCard,
  KeyRound,
  Mail,
  Shield,
  ShieldCheck,
  SlidersHorizontal,
  User,
  Users,
  type LucideIcon,
} from 'lucide-react';
import { cn } from '@/shared/lib/cn';

interface SettingsSection {
  key: string;
  href: string;
  icon: LucideIcon;
  enabled: boolean;
}

interface SettingsGroup {
  key: string;
  items: readonly SettingsSection[];
}

/**
 * Settings sections grouped by area so the list scales as more configuration lands
 * (group header resolves under `settings.nav.groups`). Email + Members ship first;
 * the rest are placeholders.
 */
const groups: readonly SettingsGroup[] = [
  {
    key: 'account',
    items: [
      { key: 'account', href: '/dashboard/settings/account', icon: User, enabled: true },
      { key: 'password', href: '/dashboard/settings/password', icon: KeyRound, enabled: true },
      { key: 'security', href: '/dashboard/settings/security', icon: Shield, enabled: true },
    ],
  },
  {
    key: 'workspace',
    items: [
      { key: 'general', href: '/dashboard/settings/general', icon: SlidersHorizontal, enabled: false },
      { key: 'members', href: '/dashboard/settings/members', icon: Users, enabled: true },
      { key: 'roles', href: '/dashboard/settings/roles', icon: ShieldCheck, enabled: true },
      { key: 'billing', href: '/dashboard/settings/billing', icon: CreditCard, enabled: false },
    ],
  },
  {
    key: 'communication',
    items: [{ key: 'email', href: '/dashboard/settings/email', icon: Mail, enabled: true }],
  },
];

const itemBase =
  'group relative flex shrink-0 items-center gap-2 rounded-md px-3 py-1.5 text-sm font-medium transition-colors';

export function SettingsNav() {
  const pathname = usePathname();
  const t = useTranslations('settings.nav');

  return (
    <nav
      aria-label={t('label')}
      className="flex gap-2 overflow-x-auto pb-1 md:flex-col md:gap-4 md:overflow-visible md:pb-0"
    >
      {groups.map((group) => (
        <div key={group.key} className="flex shrink-0 items-center gap-1 md:flex-col md:items-stretch md:gap-1">
          <p className="hidden px-3 pb-1 text-[0.6875rem] font-semibold uppercase tracking-wider text-muted-foreground md:block">
            {t(`groups.${group.key}`)}
          </p>

          {group.items.map((section) => {
            const Icon = section.icon;
            const isActive =
              pathname === section.href || pathname.startsWith(`${section.href}/`);

            if (!section.enabled) {
              return (
                <span
                  key={section.key}
                  aria-disabled="true"
                  className={cn(itemBase, 'cursor-not-allowed text-muted-foreground/50')}
                >
                  <Icon className="h-4 w-4" aria-hidden="true" />
                  {t(section.key)}
                  <span className="ml-auto hidden rounded-full bg-muted px-1.5 py-0.5 text-[0.625rem] font-medium text-muted-foreground md:inline">
                    {t('soon')}
                  </span>
                </span>
              );
            }

            return (
              <Link
                key={section.key}
                href={section.href}
                aria-current={isActive ? 'page' : undefined}
                className={cn(
                  itemBase,
                  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                  isActive
                    ? 'bg-primary/10 text-primary'
                    : 'text-muted-foreground hover:bg-accent hover:text-foreground',
                )}
              >
                {isActive && (
                  <span
                    aria-hidden
                    className="absolute left-0 top-1/2 hidden h-5 w-1 -translate-y-1/2 rounded-r-full bg-primary md:block"
                  />
                )}
                <Icon className="h-4 w-4" aria-hidden="true" />
                {t(section.key)}
              </Link>
            );
          })}
        </div>
      ))}
    </nav>
  );
}
