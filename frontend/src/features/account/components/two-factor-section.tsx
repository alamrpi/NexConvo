'use client';

import * as React from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { Loader2, ShieldCheck, ShieldOff } from 'lucide-react';
import { Card, CardContent } from '@/shared/ui/card';
import { Badge } from '@/shared/ui/badge';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
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
import { disableTwoFactorSchema, type DisableTwoFactorValues } from '../model/two-factor.schema';
import { useDisableTwoFactor } from '../api/use-two-factor';
import { TwoFactorEnrollWizard } from './two-factor-enroll-wizard';

/**
 * Two-factor section. Reads the live `twoFactorEnabled` flag from the hydrated session
 * (S7) to show the current state, then offers enroll (wizard) or disable (password-gated
 * dialog). Disabling is a destructive, confirmed action (S27).
 */
export function TwoFactorSection() {
  const t = useTranslations('settings.security');
  const enabled = useSessionStore((s) => s.user?.twoFactorEnabled ?? false);
  const [enrollOpen, setEnrollOpen] = React.useState(false);
  const [disableOpen, setDisableOpen] = React.useState(false);

  return (
    <Card>
      <CardContent className="p-5">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="min-w-0 space-y-1">
            <div className="flex items-center gap-2">
              <p className="text-sm font-medium">{t('totpTitle')}</p>
              <Badge variant={enabled ? 'success' : 'secondary'}>
                {enabled ? t('status.on') : t('status.off')}
              </Badge>
            </div>
            <p className="max-w-prose text-xs text-muted-foreground">{t('totpDescription')}</p>
          </div>

          {enabled ? (
            <Button type="button" variant="outline" size="sm" onClick={() => setDisableOpen(true)}>
              <ShieldOff aria-hidden="true" />
              {t('disable')}
            </Button>
          ) : (
            <Button type="button" size="sm" onClick={() => setEnrollOpen(true)}>
              <ShieldCheck aria-hidden="true" />
              {t('enable')}
            </Button>
          )}
        </div>
      </CardContent>

      <TwoFactorEnrollWizard open={enrollOpen} onOpenChange={setEnrollOpen} />
      <DisableDialog open={disableOpen} onOpenChange={setDisableOpen} />
    </Card>
  );
}

function DisableDialog({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const t = useTranslations('settings.security.disableDialog');
  const disable = useDisableTwoFactor();

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<DisableTwoFactorValues>({
    resolver: zodResolver(disableTwoFactorSchema),
    mode: 'onTouched',
    defaultValues: { password: '' },
  });

  const handleOpenChange = (value: boolean) => {
    onOpenChange(value);
    if (!value) {
      reset();
      disable.reset();
    }
  };

  const onSubmit = handleSubmit(async ({ password }) => {
    const ok = await disable
      .mutateAsync({ password })
      .then(() => true)
      .catch(() => false);
    if (ok) handleOpenChange(false);
  });

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="max-w-sm">
        <DialogHeader>
          <DialogTitle>{t('title')}</DialogTitle>
          <DialogDescription>{t('description')}</DialogDescription>
        </DialogHeader>

        <form onSubmit={onSubmit} noValidate className="space-y-4">
          <div className="space-y-1.5">
            <label htmlFor="disable-password" className="text-xs font-medium">
              {t('passwordLabel')}
            </label>
            <Input
              id="disable-password"
              type="password"
              autoComplete="current-password"
              aria-invalid={!!errors.password}
              {...register('password')}
            />
            {errors.password && (
              <p role="alert" className="text-xs text-destructive">
                {t(`errors.${errors.password.message}`)}
              </p>
            )}
            {disable.isError && !errors.password && (
              <p role="alert" className="text-xs text-destructive">
                {t(`errors.${disable.error.code}`)}
              </p>
            )}
          </div>

          <DialogFooter>
            <DialogClose asChild>
              <Button type="button" variant="outline" size="sm">
                {t('cancel')}
              </Button>
            </DialogClose>
            <Button type="submit" variant="destructive" size="sm" disabled={disable.isPending}>
              {disable.isPending ? (
                <>
                  <Loader2 className="h-4 w-4 animate-spin" />
                  {t('disabling')}
                </>
              ) : (
                t('confirm')
              )}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
