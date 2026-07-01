'use client';

import * as React from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import {
  Inbox,
  BarChart2,
  FlaskConical,
  Settings2,
  Zap,
  BookOpen,
  Bot,
  Wrench,
  ChevronDown,
  Menu,
  X,
} from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { Button } from '@/shared/ui/button';

interface NavLabels {
  inbox: string;
  analytics: string;
  playground: string;
  settings: string;
  channels: string;
  knowledge: string;
  aiConfig: string;
  tools: string;
  openMenu: string;
  settingsSubmenu: string;
}

const TOP_LINKS = [
  { href: '/inbox', icon: Inbox, labelKey: 'inbox' as const },
  { href: '/analytics', icon: BarChart2, labelKey: 'analytics' as const },
  { href: '/playground', icon: FlaskConical, labelKey: 'playground' as const },
];

const SETTINGS_LINKS = [
  { href: '/settings/channels', icon: Zap, labelKey: 'channels' as const },
  { href: '/settings/knowledge', icon: BookOpen, labelKey: 'knowledge' as const },
  { href: '/settings/ai', icon: Bot, labelKey: 'aiConfig' as const },
  { href: '/settings/tools', icon: Wrench, labelKey: 'tools' as const },
];

export function ChatNav({ navLabels }: { navLabels: NavLabels }) {
  const pathname = usePathname();
  const settingsActive = pathname.startsWith('/settings');
  const [settingsOpen, setSettingsOpen] = React.useState(settingsActive);
  const [mobileOpen, setMobileOpen] = React.useState(false);

  const navContent = (
    <nav aria-label="Chat navigation" className="flex flex-col gap-1 p-3">
      {TOP_LINKS.map(({ href, icon: Icon, labelKey }) => {
        const active = pathname === href || (href !== '/inbox' && pathname.startsWith(href));
        return (
          <Link
            key={href}
            href={href}
            aria-current={active ? 'page' : undefined}
            onClick={() => setMobileOpen(false)}
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
            {navLabels[labelKey]}
          </Link>
        );
      })}

      {/* Settings collapsible group */}
      <button
        type="button"
        aria-expanded={settingsOpen}
        aria-controls="chat-settings-submenu"
        onClick={() => setSettingsOpen((o) => !o)}
        className={cn(
          'flex w-full items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
          settingsActive
            ? 'text-primary'
            : 'text-muted-foreground hover:bg-accent hover:text-foreground',
        )}
      >
        <Settings2 className="h-[1.125rem] w-[1.125rem] shrink-0" />
        <span className="flex-1 text-left">{navLabels.settings}</span>
        <ChevronDown
          className={cn(
            'h-4 w-4 shrink-0 transition-transform duration-200',
            settingsOpen && 'rotate-180',
          )}
          aria-hidden
        />
      </button>

      {settingsOpen && (
        <div id="chat-settings-submenu" className="ml-3 flex flex-col gap-1 border-l border-border pl-3">
          {SETTINGS_LINKS.map(({ href, icon: Icon, labelKey }) => {
            const active = pathname.startsWith(href);
            return (
              <Link
                key={href}
                href={href}
                aria-current={active ? 'page' : undefined}
                onClick={() => setMobileOpen(false)}
                className={cn(
                  'flex items-center gap-2.5 rounded-md px-2.5 py-1.5 text-sm transition-colors',
                  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                  active
                    ? 'bg-primary/10 font-medium text-primary'
                    : 'text-muted-foreground hover:bg-accent hover:text-foreground',
                )}
              >
                <Icon className="h-4 w-4 shrink-0" />
                {navLabels[labelKey]}
              </Link>
            );
          })}
        </div>
      )}
    </nav>
  );

  return (
    <>
      {/* Desktop sidebar */}
      <aside className="sticky top-0 hidden h-full w-56 shrink-0 flex-col border-r border-border bg-card lg:flex">
        <div className="flex-1 overflow-y-auto">{navContent}</div>
      </aside>

      {/* Mobile hamburger + overlay */}
      <div className="lg:hidden">
        <Button
          variant="ghost"
          size="icon"
          aria-label={navLabels.openMenu}
          aria-expanded={mobileOpen}
          onClick={() => setMobileOpen(true)}
          className="fixed bottom-4 left-4 z-40 rounded-full bg-primary text-primary-foreground shadow-lg hover:bg-primary/90"
        >
          <Menu className="h-5 w-5" />
        </Button>

        {mobileOpen && (
          <>
            <div
              aria-hidden
              className="fixed inset-0 z-40 bg-black/50"
              onClick={() => setMobileOpen(false)}
            />
            <aside className="fixed inset-y-0 left-0 z-50 w-64 bg-card shadow-xl">
              <div className="flex h-14 items-center justify-between border-b border-border px-4">
                <span className="font-semibold">{navLabels.settings}</span>
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label="Close menu"
                  onClick={() => setMobileOpen(false)}
                >
                  <X className="h-5 w-5" />
                </Button>
              </div>
              <div className="overflow-y-auto py-2">{navContent}</div>
            </aside>
          </>
        )}
      </div>
    </>
  );
}
