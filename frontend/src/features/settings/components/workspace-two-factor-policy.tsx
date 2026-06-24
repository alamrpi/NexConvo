'use client';

import * as React from 'react';
import { useTranslations } from 'next-intl';
import { Card, CardContent } from '@/shared/ui/card';
import { Switch } from '@/shared/ui/switch';
import { Skeleton } from '@/shared/ui/skeleton';
import { Button } from '@/shared/ui/button';
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
import { useSecuritySettings, useUpdateSecuritySettings } from '../api/use-security-settings';

/**
 * Workspace-wide 2FA policy toggle (settings:manage). When on, members must enable 2FA to
 * perform sensitive writes (the server enforces it — this toggle is UX, S9). Hidden for users
 * without settings:manage.
 */
export function WorkspaceTwoFactorPolicy() {
  const t = useTranslations('settings.security.workspacePolicy');
  const canManage = useSessionStore((s) => s.hasPermission('settings:manage'));
  const { data, isLoading, isError } = useSecuritySettings();
  const update = useUpdateSecuritySettings();
  const [confirmOpen, setConfirmOpen] = React.useState(false);

  if (!canManage) return null;
  if (isLoading) return <Skeleton className="h-20 w-full" />;
  if (isError || !data) {
    return (
      <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive">
        {t('loadError')}
      </p>
    );
  }

  // Enabling forces every member to enroll — confirm it; disabling is immediate.
  const onToggle = (value: boolean) => {
    if (value) setConfirmOpen(true);
    else update.mutate({ requireTwoFactor: false });
  };

  return (
    <Card>
      <CardContent className="flex items-center justify-between gap-4 p-5">
        <div className="min-w-0 space-y-1">
          <p className="text-sm font-medium">{t('title')}</p>
          <p className="max-w-prose text-xs text-muted-foreground">{t('description')}</p>
          {update.isError && <p role="alert" className="text-xs text-destructive">{t('error')}</p>}
        </div>
        <Switch
          checked={data.requireTwoFactor}
          disabled={update.isPending}
          onCheckedChange={onToggle}
          aria-label={t('title')}
        />
      </CardContent>

      <Dialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <DialogContent className="max-w-sm">
          <DialogHeader>
            <DialogTitle>{t('confirm.title')}</DialogTitle>
            <DialogDescription>{t('confirm.description')}</DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <DialogClose asChild>
              <Button type="button" variant="outline" size="sm">
                {t('confirm.cancel')}
              </Button>
            </DialogClose>
            <Button
              type="button"
              size="sm"
              onClick={() => {
                update.mutate({ requireTwoFactor: true });
                setConfirmOpen(false);
              }}
            >
              {t('confirm.enable')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
