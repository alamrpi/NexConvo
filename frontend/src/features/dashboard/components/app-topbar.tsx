import { getTranslations } from 'next-intl/server';
import { Bell, Plus, Search } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Separator } from '@/shared/ui/separator';
import { ThemeToggle } from '@/features/theming/components/theme-toggle';
import { LocaleToggle } from '@/features/theming/components/locale-toggle';
import { MobileNavSheet } from './mobile-nav-sheet';
import { UserMenu } from './user-menu';

/**
 * Sticky top header. Server component; the hamburger sheet, toggles, and user menu
 * are isolated client leaves (S6).
 */
export async function AppTopbar() {
  const t = await getTranslations('dashboard');

  return (
    <header className="sticky top-0 z-30 flex h-16 items-center gap-2 border-b border-border bg-background/80 px-4 backdrop-blur sm:px-6">
      <MobileNavSheet />

      <div className="relative hidden w-full max-w-md sm:block">
        <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          type="search"
          placeholder={t('searchPlaceholder')}
          aria-label={t('searchPlaceholder')}
          className="pl-9 pr-14"
        />
        <kbd
          aria-hidden
          className="pointer-events-none absolute right-2.5 top-1/2 hidden -translate-y-1/2 select-none items-center rounded border border-border bg-muted px-1.5 font-mono text-[0.625rem] font-medium text-muted-foreground md:inline-flex"
        >
          ⌘K
        </kbd>
      </div>

      <div className="ml-auto flex items-center gap-1">
        <Button size="sm" className="hidden gap-1.5 sm:inline-flex">
          <Plus className="h-4 w-4" />
          {t('newAction')}
        </Button>
        <Button variant="ghost" size="icon" aria-label={t('notifications')}>
          <Bell className="h-5 w-5" />
        </Button>
        <LocaleToggle />
        <ThemeToggle />
        <Separator orientation="vertical" className="mx-1 h-6" />
        <UserMenu />
      </div>
    </header>
  );
}
