'use client';

import Link from 'next/link';
import { Bell } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Separator } from '@/shared/ui/separator';
import { ThemeToggle } from '@/features/theming/components/theme-toggle';
import { UserMenu } from '@/features/dashboard/components/user-menu';
import { Logo } from '@/shared/ui/logo';
import { useTranslations } from 'next-intl';
import { MOCK_WORKSPACE } from '@/features/chat/mock-data';

/**
 * Top navigation bar for the chat section. Server-side strings are passed as props
 * from the server layout (S6); interactive controls are client leaves.
 */
export function ChatTopbar({
  notificationsLabel,
}: {
  notificationsLabel: string;
  toggleThemeLabel: string;
}) {
  const t = useTranslations('common');

  return (
    <header className="sticky top-0 z-30 flex h-14 shrink-0 items-center gap-3 border-b border-border bg-background/80 px-4 backdrop-blur sm:px-5">
      <Link
        href="/inbox"
        aria-label={t('appName')}
        className="flex shrink-0 items-center gap-2 rounded-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        <Logo label={t('appName')} />
      </Link>

      <span className="hidden text-sm font-medium text-muted-foreground sm:block">
        {MOCK_WORKSPACE.name}
      </span>

      <div className="ml-auto flex items-center gap-1">
        <Button variant="ghost" size="icon" aria-label={notificationsLabel}>
          <Bell className="h-5 w-5" />
        </Button>
        <ThemeToggle />
        <Separator orientation="vertical" className="mx-1 h-6" />
        <UserMenu />
      </div>
    </header>
  );
}
