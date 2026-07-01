'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { Bot, BookOpen, Plug, Wrench, type LucideIcon } from 'lucide-react';
import { cn } from '@/shared/lib/cn';

interface NavItem {
  key: string;
  href: string;
  icon: LucideIcon;
}

const NAV_ITEMS: NavItem[] = [
  { key: 'channels',  href: '/dashboard/chat/settings/channels',  icon: Plug },
  { key: 'knowledge', href: '/dashboard/chat/settings/knowledge',  icon: BookOpen },
  { key: 'ai',        href: '/dashboard/chat/settings/ai',         icon: Bot },
  { key: 'tools',     href: '/dashboard/chat/settings/tools',      icon: Wrench },
];

const itemBase =
  'group relative flex shrink-0 items-center gap-2 rounded-md px-3 py-1.5 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring';

export function ChatSettingsNav() {
  const pathname = usePathname();
  const t = useTranslations('chat.settings.nav');

  return (
    <nav
      aria-label={t('label')}
      className="flex gap-1 overflow-x-auto pb-1 md:flex-col md:overflow-visible md:pb-0"
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
                className="absolute left-0 top-1/2 hidden h-5 w-1 -translate-y-1/2 rounded-r-full bg-primary md:block"
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
