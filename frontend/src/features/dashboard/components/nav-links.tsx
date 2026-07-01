'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { ChevronDown } from 'lucide-react';
import { useState } from 'react';
import { cn } from '@/shared/lib/cn';
import { dashboardNav, navSections, type NavItem } from '@/shared/config/nav';

export function NavLinks({ onNavigate }: { onNavigate?: () => void }) {
  const pathname = usePathname();
  const t = useTranslations('nav.dashboard');

  const onChatSettings = pathname.startsWith('/dashboard/chat/settings');
  const onChatReports  = pathname.startsWith('/dashboard/chat/reports');

  const [settingsOpen, setSettingsOpen] = useState(onChatSettings);
  const [reportsOpen,  setReportsOpen]  = useState(onChatReports);

  function isActive(item: NavItem) {
    return item.href === '/dashboard'
      ? pathname === item.href
      : pathname.startsWith(item.href);
  }

  function renderDropdown(
    item: NavItem,
    open: boolean,
    toggle: () => void,
  ) {
    const active = isActive(item) || (item.children ?? []).some(c => isActive(c));
    const Icon = item.icon;
    // If no children defined or empty, render as plain link
    if (!item.children || item.children.length === 0) {
      return (
        <Link
          key={item.href}
          href={item.href}
          onClick={onNavigate}
          aria-current={isActive(item) ? 'page' : undefined}
          className={cn(
            'group relative flex items-center gap-2.5 rounded-md px-3 py-2 text-sm font-medium transition-colors',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
            isActive(item)
              ? 'bg-primary/10 text-primary'
              : 'text-muted-foreground hover:bg-accent hover:text-foreground',
          )}
        >
          {isActive(item) && <span aria-hidden className="absolute left-0 top-1/2 h-5 w-1 -translate-y-1/2 rounded-r-full bg-primary" />}
          <Icon className="h-4 w-4 shrink-0" />
          {t(item.labelKey)}
        </Link>
      );
    }

    return (
      <div key={item.href}>
        <button
          type="button"
          aria-expanded={open}
          onClick={toggle}
          className={cn(
            'group relative flex w-full items-center gap-2.5 rounded-md px-3 py-2 text-sm font-medium transition-colors',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
            active
              ? 'bg-primary/10 text-primary'
              : 'text-muted-foreground hover:bg-accent hover:text-foreground',
          )}
        >
          {active && <span aria-hidden className="absolute left-0 top-1/2 h-5 w-1 -translate-y-1/2 rounded-r-full bg-primary" />}
          <Icon className="h-4 w-4 shrink-0" />
          <span className="flex-1 text-left">{t(item.labelKey)}</span>
          <ChevronDown className={cn('h-3.5 w-3.5 shrink-0 transition-transform duration-200', open && 'rotate-180')} aria-hidden />
        </button>

        {open && (
          <div className="ml-3 mt-0.5 flex flex-col gap-0.5 border-l border-border pl-3">
            {item.children.map(child => {
              const childActive = isActive(child);
              const ChildIcon = child.icon;
              return (
                <Link
                  key={child.href}
                  href={child.href}
                  onClick={onNavigate}
                  aria-current={childActive ? 'page' : undefined}
                  className={cn(
                    'flex items-center gap-2 rounded-md px-2.5 py-1.5 text-sm transition-colors',
                    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                    childActive
                      ? 'bg-primary/10 font-medium text-primary'
                      : 'text-muted-foreground hover:bg-accent hover:text-foreground',
                  )}
                >
                  <ChildIcon className="h-3.5 w-3.5 shrink-0" />
                  {t(child.labelKey)}
                </Link>
              );
            })}
          </div>
        )}
      </div>
    );
  }

  function renderItem(item: NavItem) {
    // Dropdowns
    if (item.labelKey === 'chatSettings') {
      return renderDropdown(item, settingsOpen, () => setSettingsOpen(o => !o));
    }
    if (item.labelKey === 'chatReports') {
      return renderDropdown(item, reportsOpen, () => setReportsOpen(o => !o));
    }

    // Plain link
    const active = isActive(item);
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
        {active && <span aria-hidden className="absolute left-0 top-1/2 h-5 w-1 -translate-y-1/2 rounded-r-full bg-primary" />}
        <Icon className="h-[1.125rem] w-[1.125rem] shrink-0" />
        {t(item.labelKey)}
      </Link>
    );
  }

  return (
    <nav className="flex flex-col gap-5" aria-label="Dashboard">
      {navSections.map((section) => {
        const items = dashboardNav.filter(item => item.section === section);
        if (items.length === 0) return null;

        return (
          <div key={section} className="flex flex-col gap-1">
            <p className="px-3 pb-1 text-[0.6875rem] font-semibold uppercase tracking-wider text-muted-foreground">
              {t(`sections.${section}`)}
            </p>
            {items.map(item => renderItem(item))}
          </div>
        );
      })}
    </nav>
  );
}
