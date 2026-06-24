'use client';

import * as React from 'react';
import { useTranslations } from 'next-intl';
import { Loader2, Search, Trash2, Wand2 } from 'lucide-react';
import { Card, CardContent } from '@/shared/ui/card';
import { Badge } from '@/shared/ui/badge';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Skeleton } from '@/shared/ui/skeleton';
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/ui/dialog';
import { useSessionStore } from '@/features/auth/model/session.store';
import {
  applyGlobalPreset,
  applyModulePreset,
  countGranted,
  groupCatalog,
} from '../../model/permission-catalog';
import { usePermissionCatalog, useRoles } from '../../api/use-roles';
import { useCreateRole, useDeleteRole, useUpdateRole } from '../../api/use-role-mutations';
import { RoleRail } from './role-rail';
import { RoleModuleCard } from './role-module-card';
import { PresetButtons } from './preset-buttons';
import { NewRoleDialog } from './new-role-dialog';

function setsEqual(a: ReadonlySet<string>, b: ReadonlySet<string>): boolean {
  return a.size === b.size && [...a].every((k) => b.has(k));
}

/**
 * The "Workspace Permissions" editor (S9 — system roles read-only; the server authorizes
 * every write). Fully data-driven from the backend catalog; the working set is a local
 * draft saved via PUT on Sync. Gated by `users:read` (view) / `roles:manage` (edit).
 */
export function RolesScreen() {
  const t = useTranslations('settings.roles');
  const canRead = useSessionStore((s) => s.hasPermission('users:read'));
  const canManage = useSessionStore((s) => s.hasPermission('roles:manage'));

  const rolesQuery = useRoles();
  const catalogQuery = usePermissionCatalog();
  const update = useUpdateRole();
  const create = useCreateRole();
  const remove = useDeleteRole();

  const [selectedId, setSelectedId] = React.useState<string | null>(null);
  const [draft, setDraft] = React.useState<Set<string>>(new Set());
  const [draftName, setDraftName] = React.useState('');
  const [expanded, setExpanded] = React.useState<Set<string>>(new Set());
  const [search, setSearch] = React.useState('');
  const [newOpen, setNewOpen] = React.useState(false);
  const [deleteOpen, setDeleteOpen] = React.useState(false);
  const [pendingSelectName, setPendingSelectName] = React.useState<string | null>(null);

  const roles = React.useMemo(() => rolesQuery.data ?? [], [rolesQuery.data]);
  const catalog = React.useMemo(() => catalogQuery.data ?? [], [catalogQuery.data]);
  const groups = React.useMemo(() => groupCatalog(catalog), [catalog]);
  const assignableKeys = React.useMemo(() => new Set(catalog.map((c) => c.key)), [catalog]);
  const total = assignableKeys.size;
  const selectedRole = roles.find((r) => r.id === selectedId) ?? null;

  // Default selection once data lands.
  React.useEffect(() => {
    const first = roles[0];
    if (first && !selectedId) setSelectedId(first.id);
  }, [roles, selectedId]);

  // After creating a role, select it when the refetched list contains it.
  React.useEffect(() => {
    if (!pendingSelectName) return;
    const created = roles.find((r) => r.name === pendingSelectName);
    if (created) {
      setSelectedId(created.id);
      setPendingSelectName(null);
    }
  }, [roles, pendingSelectName]);

  // Load the working draft when the selected role changes (or the catalog arrives).
  React.useEffect(() => {
    const role = roles.find((r) => r.id === selectedId);
    if (!role || catalog.length === 0) return;
    setDraftName(role.name);
    setDraft(role.grantsAll ? new Set(assignableKeys) : new Set(role.permissions.filter((k) => assignableKeys.has(k))));
    update.reset();
    // Intentionally keyed on selection + catalog only (not on roles refetch after save).
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedId, catalog]);

  if (!canRead) {
    return (
      <p role="status" className="rounded-md border border-border bg-muted/40 px-4 py-3 text-sm text-muted-foreground">
        {t('noPermission')}
      </p>
    );
  }

  if (rolesQuery.isLoading || catalogQuery.isLoading) return <RolesSkeleton />;
  if (rolesQuery.isError || catalogQuery.isError) {
    return (
      <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive">
        {t('loadError')}
      </p>
    );
  }

  const isEditable = !!selectedRole && canManage && !selectedRole.isSystem && !selectedRole.grantsAll;
  const readOnly = !isEditable;
  const grantedCount = countGranted(catalog, draft);
  const dirty =
    isEditable &&
    !!selectedRole &&
    (draftName.trim() !== selectedRole.name ||
      !setsEqual(draft, new Set(selectedRole.permissions.filter((k) => assignableKeys.has(k)))));

  const togglePermission = (key: string) => {
    if (readOnly) return;
    setDraft((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  };
  const toggleExpand = (module: string) =>
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(module)) next.delete(module);
      else next.add(module);
      return next;
    });

  const onSync = () => {
    if (!selectedRole || !dirty) return;
    update.mutate({ id: selectedRole.id, name: draftName.trim(), permissions: [...draft] });
  };

  const onCreate = async (name: string) => {
    // New roles start from the Read-only preset so they satisfy the ≥1-permission rule.
    const result = await create
      .mutateAsync({ name, permissions: [...applyGlobalPreset(catalog, 'read-only')] })
      .then(() => true)
      .catch(() => false);
    if (result) {
      setNewOpen(false);
      setPendingSelectName(name);
    }
  };

  const onDelete = async () => {
    if (!selectedRole) return;
    const ok = await remove
      .mutateAsync(selectedRole.id)
      .then(() => true)
      .catch(() => false);
    if (ok) {
      setDeleteOpen(false);
      setSelectedId(roles[0]?.id ?? null);
    }
  };

  return (
    <Card>
      <CardContent className="p-0">
        {/* Panel header */}
        <div className="flex flex-col gap-3 border-b border-border p-4 sm:flex-row sm:items-center sm:justify-between sm:p-5">
          <div>
            <h2 className="text-base font-semibold tracking-tight">{t('title')}</h2>
            <p className="text-sm text-muted-foreground">{t('subtitle')}</p>
          </div>
          {isEditable && (
            <Button size="sm" disabled={!dirty || update.isPending} onClick={onSync}>
              {update.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Wand2 aria-hidden="true" />}
              {t('sync')}
            </Button>
          )}
        </div>

        <div className="grid gap-5 p-4 sm:p-5 md:grid-cols-[220px_minmax(0,1fr)]">
          <RoleRail
            roles={roles}
            selectedId={selectedId}
            assignableKeys={assignableKeys}
            canManage={canManage}
            onSelect={setSelectedId}
            onNew={() => setNewOpen(true)}
          />

          {selectedRole ? (
            <div className="min-w-0 space-y-4">
              {/* Role detail header */}
              <div className="flex flex-col gap-3">
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0 space-y-1">
                    <div className="flex flex-wrap items-center gap-2">
                      {isEditable ? (
                        <Input
                          value={draftName}
                          onChange={(e) => setDraftName(e.target.value)}
                          maxLength={100}
                          aria-label={t('roleNameLabel')}
                          className="h-8 max-w-[15rem] text-base font-semibold"
                        />
                      ) : (
                        <h3 className="truncate text-base font-semibold">{selectedRole.name}</h3>
                      )}
                      {selectedRole.isSystem && <Badge variant="secondary">{t('system')}</Badge>}
                    </div>
                    <p className="text-xs text-muted-foreground">
                      {selectedRole.grantsAll
                        ? t('fullAccess')
                        : t('permissionsGranted', { count: grantedCount, total })}
                    </p>
                  </div>
                  {isEditable && (
                    <Button
                      variant="ghost"
                      size="icon"
                      onClick={() => setDeleteOpen(true)}
                      aria-label={t('delete')}
                    >
                      <Trash2 className="h-4 w-4 text-muted-foreground" />
                    </Button>
                  )}
                </div>

                <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                  <div className="flex items-center gap-2">
                    <span className="text-[0.6875rem] font-semibold uppercase tracking-wide text-muted-foreground">
                      {t('setAll')}
                    </span>
                    <PresetButtons disabled={readOnly} onSelect={(p) => setDraft(applyGlobalPreset(catalog, p))} />
                  </div>
                  <div className="relative sm:w-64">
                    <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                    <Input
                      value={search}
                      onChange={(e) => setSearch(e.target.value)}
                      placeholder={t('searchPlaceholder')}
                      aria-label={t('searchPlaceholder')}
                      className="pl-9"
                    />
                  </div>
                </div>
              </div>

              {update.isSuccess && !dirty && (
                <p role="status" className="rounded-md border border-primary/30 bg-primary/5 px-3 py-2 text-sm">
                  {t('saved')}
                </p>
              )}
              {update.isError && (
                <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
                  {t(`errors.${update.error.code}`)}
                </p>
              )}

              {/* Module cards */}
              <div className="space-y-3">
                {groups.map((group) => (
                  <RoleModuleCard
                    key={group.module}
                    group={group}
                    granted={draft}
                    readOnly={readOnly}
                    expanded={expanded.has(group.module)}
                    search={search}
                    onToggleExpand={() => toggleExpand(group.module)}
                    onTogglePermission={togglePermission}
                    onModulePreset={(p) =>
                      setDraft((prev) => applyModulePreset(prev, group.categories.flatMap((c) => c.items), p))
                    }
                  />
                ))}
              </div>
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">{t('selectPrompt')}</p>
          )}
        </div>
      </CardContent>

      <NewRoleDialog
        open={newOpen}
        onOpenChange={(v) => {
          setNewOpen(v);
          if (!v) create.reset();
        }}
        onCreate={onCreate}
        pending={create.isPending}
        error={create.isError ? create.error.code : undefined}
      />

      <Dialog open={deleteOpen} onOpenChange={setDeleteOpen}>
        <DialogContent className="max-w-sm">
          <DialogHeader>
            <DialogTitle>{t('deleteDialog.title', { name: selectedRole?.name ?? '' })}</DialogTitle>
            <DialogDescription>{t('deleteDialog.description')}</DialogDescription>
          </DialogHeader>
          {remove.isError && (
            <p role="alert" className="text-xs text-destructive">
              {t(`deleteDialog.errors.${remove.error.code}`)}
            </p>
          )}
          <DialogFooter>
            <DialogClose asChild>
              <Button type="button" variant="outline" size="sm">
                {t('deleteDialog.cancel')}
              </Button>
            </DialogClose>
            <Button type="button" variant="destructive" size="sm" disabled={remove.isPending} onClick={onDelete}>
              {remove.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
              {t('deleteDialog.confirm')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}

function RolesSkeleton() {
  return (
    <Card>
      <CardContent className="grid gap-5 p-5 md:grid-cols-[220px_minmax(0,1fr)]">
        <div className="space-y-2">
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-14 w-full" />
          ))}
        </div>
        <div className="space-y-3">
          <Skeleton className="h-8 w-48" />
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-16 w-full" />
          ))}
        </div>
      </CardContent>
    </Card>
  );
}
