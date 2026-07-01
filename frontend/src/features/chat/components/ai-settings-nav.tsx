'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { Zap, MessageSquare, Shield, Lock, type LucideIcon } from 'lucide-react';
import { cn } from '@/shared/lib/cn';

interface NavItem {
  key: string;
  href: string;
  icon: LucideIcon;
}

const BASE = '/dashboard/chat/settings/ai';

const NAV_ITEMS: NavItem[] = [
  { key: 'provider',     href: `${BASE}/provider`,      icon: Zap },
  { key: 'systemPrompt', href: `${BASE}/system-prompt`, icon: MessageSquare },
  { key: 'handoff',      href: `${BASE}/handoff`,       icon: Shield },
  { key: 'privacy',      href: `${BASE}/privacy`,       icon: Lock },
];

const itemBase =
  'group relative flex shrink-0 items-center gap-2 rounded-md px-3 py-1.5 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring';

export function AiSettingsNav() {
  const pathname = usePathname();
  const t = useTranslations('chat.aiSettings.nav');

  return (
    <nav
      aria-label="AI settings navigation"
      className="flex gap-1 overflow-x-auto pb-1 sm:flex-col sm:overflow-visible sm:pb-0"
    >
      {NAV_ITEMS.map(({ key, href, icon: Icon }) => {
        const isActive = pathname === href || pathname.startsWith(`${href}/`);
        return (
          <Link
            key={key}
            href={href}
            aria-current={isActive ? 'page' : undefined}
            className={cn(
              itemBase,
              isActive
                ? 'bg-primary/10 text-primary'
                : 'text-muted-foreground hover:bg-accent hover:text-foreground',
            )}
          >
            {isActive && (
              <span
                aria-hidden
                className="absolute left-0 top-1/2 hidden h-5 w-1 -translate-y-1/2 rounded-r-full bg-primary sm:block"
              />
            )}
            <Icon className="h-4 w-4 shrink-0" aria-hidden />
            {t(key)}
          </Link>
        );
      })}
    </nav>
  );
}
