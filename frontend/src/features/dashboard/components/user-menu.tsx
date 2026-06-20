'use client';

import { useTranslations } from 'next-intl';
import { ChevronDown, LogOut, Settings, User } from 'lucide-react';
import { Avatar, AvatarFallback } from '@/shared/ui/avatar';
import { Button } from '@/shared/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/shared/ui/dropdown-menu';
import { useSessionStore } from '@/features/auth/model/session.store';
import { useLogout } from '@/features/auth/api/use-logout';

/** Derive up-to-two uppercase initials from a full name, falling back to 'U'. */
function initialsOf(name: string): string {
  return (
    name
      .split(' ')
      .map((part) => part.charAt(0))
      .filter(Boolean)
      .slice(0, 2)
      .join('')
      .toUpperCase() || 'U'
  );
}

/**
 * Account dropdown. Identity comes from the hydrated session store (S7); Sign out runs
 * the logout mutation (revokes the refresh token, clears cookies, leaves the authed area).
 */
export function UserMenu() {
  const t = useTranslations('dashboard.user');
  const user = useSessionStore((s) => s.user);
  const logout = useLogout();

  const name = user?.fullName ?? '';
  const email = user?.email ?? '';
  const initials = initialsOf(name);

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" className="h-9 gap-2 px-1.5">
          <Avatar className="h-7 w-7">
            <AvatarFallback className="text-xs">{initials}</AvatarFallback>
          </Avatar>
          <ChevronDown className="hidden h-4 w-4 text-muted-foreground sm:block" />
          <span className="sr-only">{name}</span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-56">
        <DropdownMenuLabel className="font-normal">
          <div className="flex flex-col gap-0.5">
            <span className="text-sm font-medium">{name}</span>
            <span className="text-xs text-muted-foreground">{email}</span>
          </div>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem>
          <User className="h-4 w-4" />
          {t('profile')}
        </DropdownMenuItem>
        <DropdownMenuItem>
          <Settings className="h-4 w-4" />
          {t('settings')}
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem
          className="text-destructive focus:text-destructive"
          disabled={logout.isPending}
          onSelect={(event) => {
            event.preventDefault();
            logout.mutate();
          }}
        >
          <LogOut className="h-4 w-4" />
          {t('signOut')}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
