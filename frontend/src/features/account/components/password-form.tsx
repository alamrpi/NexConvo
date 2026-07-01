'use client';

import * as React from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { Loader2, ShieldCheck } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Card, CardContent } from '@/shared/ui/card';
import { useLogout } from '@/features/auth/api/use-logout';
import { passwordChangeSchema, type PasswordChangeValues } from '../model/password.schema';
import { useChangePassword } from '../api/use-change-password';
import { Field } from './field';

/**
 * Change-password section. RHF + zod validates locally (UX, S10); the backend re-verifies the
 * current password and ends ALL sessions on success — so we sign the user out and send them to
 * the login page. A wrong current password surfaces inline (mapped from the BFF's 422), not as a
 * redirect (S15).
 */
export function PasswordForm() {
  const t = useTranslations('settings.password');
  const change = useChangePassword();
  const logout = useLogout();
  const [done, setDone] = React.useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<PasswordChangeValues>({
    resolver: zodResolver(passwordChangeSchema),
    mode: 'onTouched',
    defaultValues: { currentPassword: '', newPassword: '', confirmPassword: '' },
  });

  const errorText = (key?: string) => (key ? t(`errors.${key}`) : undefined);
  const busy = isSubmitting || change.isPending || done;

  // On success the server has revoked every session; sign out + redirect to login after a beat.
  React.useEffect(() => {
    if (!done) return;
    const timer = window.setTimeout(() => logout.mutate(), 1400);
    return () => window.clearTimeout(timer);
  }, [done, logout]);

  const onSubmit = handleSubmit(async ({ currentPassword, newPassword }) => {
    const ok = await change
      .mutateAsync({ currentPassword, newPassword })
      .then(() => true)
      .catch(() => false);
    if (ok) setDone(true);
  });

  return (
    <Card>
      <CardContent className="p-5">
        <form onSubmit={onSubmit} noValidate className="space-y-5">
          {done && (
            <p role="status" className="rounded-md border border-primary/30 bg-primary/5 px-3 py-2 text-sm">
              {t('changed')}
            </p>
          )}
          {change.isError && (
            <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {t(`errors.${change.error.code}`)}
            </p>
          )}

          <Field id="currentPassword" label={t('currentLabel')} error={errorText(errors.currentPassword?.message)}>
            <Input
              id="currentPassword"
              type="password"
              autoComplete="current-password"
              {...register('currentPassword')}
              aria-invalid={!!errors.currentPassword}
            />
          </Field>

          <div className="grid gap-4 sm:grid-cols-2">
            <Field id="newPassword" label={t('newLabel')} error={errorText(errors.newPassword?.message)}>
              <Input
                id="newPassword"
                type="password"
                autoComplete="new-password"
                {...register('newPassword')}
                aria-invalid={!!errors.newPassword}
              />
            </Field>
            <Field id="confirmPassword" label={t('confirmLabel')} error={errorText(errors.confirmPassword?.message)}>
              <Input
                id="confirmPassword"
                type="password"
                autoComplete="new-password"
                {...register('confirmPassword')}
                aria-invalid={!!errors.confirmPassword}
              />
            </Field>
          </div>

          <p className="text-xs text-muted-foreground">{t('rotateNote')}</p>

          <Button type="submit" size="sm" disabled={busy}>
            {busy ? (
              <>
                <Loader2 className="h-4 w-4 animate-spin" />
                {t('saving')}
              </>
            ) : (
              <>
                <ShieldCheck aria-hidden="true" />
                {t('save')}
              </>
            )}
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}
