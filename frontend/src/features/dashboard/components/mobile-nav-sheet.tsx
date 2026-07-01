'use client';

import * as React from 'react';
import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { Menu } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from '@/shared/ui/sheet';
import { Logo } from '@/shared/ui/logo';
import { NavLinks } from './nav-links';

/**
 * Mobile navigation: hamburger trigger → slide-out sheet (S24). Closes on selection.
 * Only rendered on mobile/tablet via `lg:hidden` on the trigger.
 */
export function MobileNavSheet() {
  const t = useTranslations('dashboard');
  const tc = useTranslations('common');
  const [open, setOpen] = React.useState(false);

  return (
    <Sheet open={open} onOpenChange={setOpen}>
      <SheetTrigger asChild>
        <Button variant="ghost" size="icon" className="lg:hidden" aria-label={t('openMenu')}>
          <Menu className="h-5 w-5" />
        </Button>
      </SheetTrigger>
      <SheetContent side="left" className="w-72 p-0">
        <SheetHeader className="flex h-16 flex-row items-center border-b border-border px-6 text-left">
          <SheetTitle asChild>
            <Link href="/dashboard" onClick={() => setOpen(false)}>
              <Logo label={tc('appName')} />
            </Link>
          </SheetTitle>
          <SheetDescription className="sr-only">{t('subtitle')}</SheetDescription>
        </SheetHeader>
        <div className="p-4">
          <NavLinks onNavigate={() => setOpen(false)} />
        </div>
      </SheetContent>
    </Sheet>
  );
}
