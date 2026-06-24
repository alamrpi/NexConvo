'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Card, CardContent } from '@/shared/ui/card';
import { useSessionStore } from '@/features/auth/model/session.store';
import { profileSchema, type ProfileValues } from '../model/profile.schema';
import { useUpdateProfile } from '../api/use-update-profile';
import { Field } from './field';

/**
 * Profile section — edit the display name. The current values come from the hydrated
 * session store (S7), so there's no separate query/loading state. Client validation is UX
 * (S10); the Account service validates server-side. Email is shown read-only (changing it
 * is out of scope).
 */
export function ProfileForm() {
  const t = useTranslations('settings.account');
  const user = useSessionStore((s) => s.user);
  const update = useUpdateProfile();

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting, isDirty },
  } = useForm<ProfileValues>({
    resolver: zodResolver(profileSchema),
    mode: 'onTouched',
    values: { fullName: user?.fullName ?? '' },
  });

  const errorText = (key?: string) => (key ? t(`errors.${key}`) : undefined);
  const busy = isSubmitting || update.isPending;

  const onSubmit = handleSubmit(async (values) => {
    await update.mutateAsync(values).catch(() => null);
  });

  return (
    <Card>
      <CardContent className="p-5">
        <form onSubmit={onSubmit} noValidate className="space-y-5">
          {update.isSuccess && (
            <p role="status" className="rounded-md border border-primary/30 bg-primary/5 px-3 py-2 text-sm">
              {t('saved')}
            </p>
          )}
          {update.isError && (
            <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {t('saveError')}
            </p>
          )}

          <Field id="fullName" label={t('fullNameLabel')} error={errorText(errors.fullName?.message)}>
            <Input id="fullName" autoComplete="name" {...register('fullName')} aria-invalid={!!errors.fullName} />
          </Field>

          <Field id="email" label={t('emailLabel')}>
            <Input id="email" value={user?.email ?? ''} disabled readOnly />
          </Field>

          <Button type="submit" size="sm" disabled={busy || !isDirty}>
            {busy ? (
              <>
                <Loader2 className="h-4 w-4 animate-spin" />
                {t('saving')}
              </>
            ) : (
              <>
                <Save aria-hidden="true" />
                {t('save')}
              </>
            )}
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}
