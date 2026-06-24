'use client';

import { useTranslations } from 'next-intl';
import { ChevronRight, Folder } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import {
  MODULE_ICONS,
  countGranted,
  permissionLabelKey,
  type ModuleGroup,
  type Preset,
} from '../../model/permission-catalog';
import type { PermissionItem } from '../../model/roles.types';
import { PresetButtons } from './preset-buttons';
import { PermissionChip } from './permission-chip';

const CATEGORY_DOT: Record<string, string> = {
  Access: 'bg-sky-500',
  Manage: 'bg-violet-500',
  Operations: 'bg-amber-500',
};

/**
 * One expandable module card: header (icon, name, progress, per-module preset) + an
 * expanded body of ACCESS / MANAGE / OPERATIONS chip rows. Honors the search query (matches
 * module or permission labels) and renders read-only for system roles. Returns null when
 * nothing in the module matches the active search.
 */
export function RoleModuleCard({
  group,
  granted,
  readOnly,
  expanded,
  search,
  onToggleExpand,
  onTogglePermission,
  onModulePreset,
}: {
  group: ModuleGroup;
  granted: ReadonlySet<string>;
  readOnly: boolean;
  expanded: boolean;
  search: string;
  onToggleExpand: () => void;
  onTogglePermission: (key: string) => void;
  onModulePreset: (preset: Preset) => void;
}) {
  const tp = useTranslations('settings.roles.permissions');
  const tm = useTranslations('settings.roles.modules');
  const tc = useTranslations('settings.roles.categories');

  const Icon = MODULE_ICONS[group.module] ?? Folder;
  const moduleLabel = tm(group.module);
  const q = search.trim().toLowerCase();
  const moduleMatches = q === '' || moduleLabel.toLowerCase().includes(q);

  const matches = (item: PermissionItem) =>
    moduleMatches || tp(permissionLabelKey(item.key)).toLowerCase().includes(q);

  const visibleCategories = group.categories
    .map((c) => ({ category: c.category, items: c.items.filter(matches) }))
    .filter((c) => c.items.length > 0);

  if (q !== '' && visibleCategories.length === 0) return null;

  const allItems = group.categories.flatMap((c) => c.items);
  const grantedCount = countGranted(allItems, granted);
  const isOpen = expanded || q !== '';

  return (
    <div
      className={cn(
        'overflow-hidden rounded-lg border border-border border-l-2 bg-card shadow-sm transition-colors',
        isOpen ? 'border-l-primary' : 'border-l-border',
      )}
    >
      <div className="flex flex-col gap-3 p-3 sm:flex-row sm:items-center">
        <button
          type="button"
          aria-expanded={isOpen}
          onClick={onToggleExpand}
          className="flex min-w-0 flex-1 items-center gap-2.5 text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring rounded-md"
        >
          <ChevronRight className={cn('h-4 w-4 shrink-0 text-muted-foreground transition-transform', isOpen && 'rotate-90')} aria-hidden="true" />
          <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-md bg-primary/10 text-primary">
            <Icon className="h-4 w-4" aria-hidden="true" />
          </span>
          <span className="truncate text-sm font-medium">{moduleLabel}</span>
        </button>

        <div className="flex items-center gap-3 sm:ml-auto">
          <ProgressPill granted={grantedCount} total={group.total} />
          <PresetButtons size="xs" disabled={readOnly} onSelect={onModulePreset} />
        </div>
      </div>

      {isOpen && (
        <div className="space-y-3 border-t border-border bg-muted/20 p-3">
          {visibleCategories.map(({ category, items }) => (
            <div key={category} className="flex flex-col gap-2 sm:flex-row sm:items-start">
              <div className="flex w-32 shrink-0 items-center gap-1.5 pt-1">
                <span className={cn('h-1.5 w-1.5 rounded-full', CATEGORY_DOT[category])} aria-hidden="true" />
                <span className="text-[0.6875rem] font-semibold uppercase tracking-wide text-muted-foreground">
                  {tc(category)}
                </span>
              </div>
              <div className="flex flex-wrap gap-2">
                {items.map((item) => (
                  <PermissionChip
                    key={item.key}
                    permissionKey={item.key}
                    granted={granted.has(item.key)}
                    readOnly={readOnly}
                    onToggle={() => onTogglePermission(item.key)}
                  />
                ))}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );

  function ProgressPill({ granted: g, total }: { granted: number; total: number }) {
    const pct = total === 0 ? 0 : Math.round((g / total) * 100);
    return (
      <div className="flex items-center gap-2">
        <div className="hidden h-1.5 w-20 overflow-hidden rounded-full bg-muted sm:block">
          <div className="h-full rounded-full bg-success transition-all" style={{ width: `${pct}%` }} />
        </div>
        <span
          className={cn(
            'rounded-full border px-2 py-0.5 text-xs font-medium',
            g === total ? 'border-success/30 text-success' : 'border-border text-muted-foreground',
          )}
        >
          {g}/{total}
        </span>
      </div>
    );
  }
}
