'use client';

import { useTranslations } from 'next-intl';
import { Crown, Plus } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import type { Role } from '../../model/roles.types';
import { roleAccent } from './role-accent';

/** Left role rail — avatar + name + (Owner) "Full access" or a granted/total progress bar. */
export function RoleRail({
  roles,
  selectedId,
  assignableKeys,
  canManage,
  onSelect,
  onNew,
}: {
  roles: Role[];
  selectedId: string | null;
  assignableKeys: ReadonlySet<string>;
  canManage: boolean;
  onSelect: (id: string) => void;
  onNew: () => void;
}) {
  const t = useTranslations('settings.roles');
  const total = assignableKeys.size;

  return (
    <div className="space-y-1">
      <p className="px-2 pb-1 text-[0.6875rem] font-semibold uppercase tracking-wider text-muted-foreground">
        {t('rolesHeading')}
      </p>

      {roles.map((role) => {
        const accent = roleAccent(role);
        const selected = role.id === selectedId;
        const granted = role.permissions.filter((k) => assignableKeys.has(k)).length;
        const pct = total === 0 ? 0 : Math.round((granted / total) * 100);

        return (
          <button
            key={role.id}
            type="button"
            aria-current={selected ? 'true' : undefined}
            onClick={() => onSelect(role.id)}
            className={cn(
              'flex w-full items-center gap-3 rounded-lg border p-2.5 text-left transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
              selected ? 'border-primary/40 bg-primary/5' : 'border-transparent hover:bg-accent',
            )}
          >
            <span className={cn('flex h-9 w-9 shrink-0 items-center justify-center rounded-md text-xs font-semibold text-white', accent.className)}>
              {accent.isOwner ? <Crown className="h-4 w-4" aria-hidden="true" /> : accent.initials}
            </span>
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-medium">{role.name}</p>
              {role.grantsAll ? (
                <p className="text-xs text-muted-foreground">{t('fullAccess')}</p>
              ) : (
                <div className="mt-1 flex items-center gap-2">
                  <div className="h-1 flex-1 overflow-hidden rounded-full bg-muted">
                    <div className="h-full rounded-full bg-primary" style={{ width: `${pct}%` }} />
                  </div>
                  <span className="shrink-0 text-[0.6875rem] tabular-nums text-muted-foreground">{granted}/{total}</span>
                </div>
              )}
            </div>
          </button>
        );
      })}

      {canManage && (
        <button
          type="button"
          onClick={onNew}
          className="mt-1 flex w-full items-center gap-2 rounded-lg border border-dashed border-border p-2.5 text-sm text-muted-foreground transition-colors hover:border-ring/50 hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          <Plus className="h-4 w-4" aria-hidden="true" />
          {t('newRole')}
        </button>
      )}
    </div>
  );
}
