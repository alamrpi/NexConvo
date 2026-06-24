'use client';

import * as React from 'react';
import { useTranslations } from 'next-intl';
import { CheckCircle2, ChevronLeft, ChevronRight, Loader2 } from 'lucide-react';
import { Card, CardContent } from '@/shared/ui/card';
import { Badge } from '@/shared/ui/badge';
import { Button } from '@/shared/ui/button';
import { Skeleton } from '@/shared/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/shared/ui/table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/shared/ui/select';
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
import { useRoles } from '../api/use-roles';
import { useUsers } from '../api/use-users';
import { useChangeUserRole, useDeactivateUser, useReactivateUser } from '../api/use-user-mutations';
import type { UserListItem } from '../model/users.types';
import type { Role } from '../model/roles.types';

const PAGE_SIZE = 20;

/** Workspace users directory — role assignment + activation, paginated (S16). Gated `users:read`. */
export function UsersSection() {
  const t = useTranslations('settings.users');
  const canRead = useSessionStore((s) => s.hasPermission('users:read'));
  const [page, setPage] = React.useState(1);

  const usersQuery = useUsers(page, PAGE_SIZE);
  const rolesQuery = useRoles();

  if (!canRead) {
    return (
      <p role="status" className="rounded-md border border-border bg-muted/40 px-4 py-3 text-sm text-muted-foreground">
        {t('noPermission')}
      </p>
    );
  }

  if (usersQuery.isLoading || rolesQuery.isLoading) return <UsersSkeleton />;
  if (usersQuery.isError || rolesQuery.isError || !usersQuery.data) {
    return (
      <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive">
        {t('loadError')}
      </p>
    );
  }

  const { items, total } = usersQuery.data;
  const roles = rolesQuery.data ?? [];
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  return (
    <Card>
      <CardContent className="p-0">
        <Table>
          <TableHeader>
            <TableRow className="hover:bg-transparent">
              <TableHead>{t('columns.name')}</TableHead>
              <TableHead>{t('columns.role')}</TableHead>
              <TableHead>{t('columns.status')}</TableHead>
              <TableHead className="text-right">{t('columns.actions')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((user) => (
              <UserRow key={user.id} user={user} roles={roles} />
            ))}
          </TableBody>
        </Table>

        <div className="flex items-center justify-between gap-3 border-t border-border px-4 py-3 text-sm text-muted-foreground">
          <span>{t('total', { count: total })}</span>
          {totalPages > 1 && (
            <div className="flex items-center gap-2">
              <Button
                variant="outline"
                size="icon"
                className="h-8 w-8"
                disabled={page <= 1 || usersQuery.isFetching}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                aria-label={t('prevPage')}
              >
                <ChevronLeft className="h-4 w-4" />
              </Button>
              <span className="tabular-nums">{t('pageOf', { page, totalPages })}</span>
              <Button
                variant="outline"
                size="icon"
                className="h-8 w-8"
                disabled={page >= totalPages || usersQuery.isFetching}
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                aria-label={t('nextPage')}
              >
                <ChevronRight className="h-4 w-4" />
              </Button>
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

/** Skeleton that mirrors the table layout so there's no shift when data lands (S27). */
function UsersSkeleton() {
  return (
    <Card>
      <CardContent className="space-y-3 p-4">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="flex items-center gap-3">
            <Skeleton className="h-8 w-8 shrink-0 rounded-full" />
            <div className="min-w-0 flex-1 space-y-1.5">
              <Skeleton className="h-3.5 w-32 max-w-full" />
              <Skeleton className="h-3 w-44 max-w-full" />
            </div>
            <Skeleton className="h-8 w-[140px] shrink-0" />
            <Skeleton className="hidden h-5 w-16 shrink-0 sm:block" />
          </div>
        ))}
      </CardContent>
    </Card>
  );
}

function UserRow({ user, roles }: { user: UserListItem; roles: Role[] }) {
  const t = useTranslations('settings.users');
  const canManage = useSessionStore((s) => s.hasPermission('users:manage'));
  const currentUserId = useSessionStore((s) => s.user?.userId);
  const isSelf = currentUserId === user.id;

  const changeRole = useChangeUserRole();
  const deactivate = useDeactivateUser();
  const reactivate = useReactivateUser();
  const [confirmOpen, setConfirmOpen] = React.useState(false);

  const initials =
    user.fullName
      .split(/\s+/)
      .map((p) => p.charAt(0))
      .filter(Boolean)
      .slice(0, 2)
      .join('')
      .toUpperCase() || 'U';

  const onRoleChange = (roleId: string) => {
    if (roleId !== user.roleId) changeRole.mutate({ id: user.id, roleId });
  };

  const rowError =
    (changeRole.isError && changeRole.error.code) ||
    (deactivate.isError && deactivate.error.code) ||
    (reactivate.isError && reactivate.error.code) ||
    null;

  return (
    <>
      <TableRow>
        <TableCell>
          <div className="flex items-center gap-2.5">
            <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-primary/10 text-xs font-semibold text-primary">
              {initials}
            </span>
            <div className="min-w-0">
              <p className="truncate text-sm font-medium">{user.fullName}</p>
              <p className="truncate text-xs text-muted-foreground">{user.email}</p>
            </div>
          </div>
        </TableCell>

        <TableCell>
          {canManage && !isSelf && user.status === 'Active' ? (
            <Select value={user.roleId ?? undefined} onValueChange={onRoleChange} disabled={changeRole.isPending}>
              <SelectTrigger className="h-8 w-[140px]">
                <SelectValue placeholder={t('noRole')} />
              </SelectTrigger>
              <SelectContent>
                {roles.map((role) => (
                  <SelectItem key={role.id} value={role.id}>
                    {role.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          ) : (
            <span className="text-sm">{user.roleName ?? t('noRole')}</span>
          )}
          {rowError && <p role="alert" className="mt-1 text-xs text-destructive">{t(`errors.${rowError}`)}</p>}
        </TableCell>

        <TableCell>
          <div className="flex flex-wrap items-center gap-1.5">
            <Badge variant={user.status === 'Active' ? 'success' : 'secondary'}>
              {user.status === 'Active' ? t('status.active') : t('status.disabled')}
            </Badge>
            {user.emailVerified ? (
              <span className="inline-flex items-center gap-1 text-xs text-muted-foreground">
                <CheckCircle2 className="h-3.5 w-3.5 text-success" aria-hidden="true" />
                {t('verified')}
              </span>
            ) : (
              <Badge variant="warning">{t('unverified')}</Badge>
            )}
          </div>
        </TableCell>

        <TableCell className="text-right">
          {canManage && !isSelf && (
            user.status === 'Active' ? (
              <Button variant="outline" size="sm" onClick={() => setConfirmOpen(true)}>
                {t('deactivate')}
              </Button>
            ) : (
              <Button
                variant="outline"
                size="sm"
                disabled={reactivate.isPending}
                onClick={() => reactivate.mutate(user.id)}
              >
                {reactivate.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
                {t('reactivate')}
              </Button>
            )
          )}
        </TableCell>
      </TableRow>

      <Dialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <DialogContent className="max-w-sm">
          <DialogHeader>
            <DialogTitle>{t('deactivateDialog.title', { name: user.fullName })}</DialogTitle>
            <DialogDescription>{t('deactivateDialog.description')}</DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <DialogClose asChild>
              <Button type="button" variant="outline" size="sm">
                {t('deactivateDialog.cancel')}
              </Button>
            </DialogClose>
            <Button
              type="button"
              variant="destructive"
              size="sm"
              disabled={deactivate.isPending}
              onClick={async () => {
                const ok = await deactivate.mutateAsync(user.id).then(() => true).catch(() => false);
                if (ok) setConfirmOpen(false);
              }}
            >
              {deactivate.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
              {t('deactivateDialog.confirm')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
