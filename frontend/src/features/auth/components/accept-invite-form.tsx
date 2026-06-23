'use client';

import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { Loader2 } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { acceptInviteSchema, type AcceptInviteValues } from '../model/accept-invite.schema';
import { useAcceptInvite } from '../api/use-accept-invite';

export function AcceptInviteForm({ token }: { token: string }) {
  const t = useTranslations('acceptInvite');
  const router = useRouter();
  const accept = useAcceptInvite();
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<AcceptInviteValues>({
    resolver: zodResolver(acceptInviteSchema),
    mode: 'onTouched',
    defaultValues: { fullName: '', password: '' },
  });

  if (!token) {
    return <p role="alert" className="text-sm text-destructive">{t('missingToken')}</p>;
  }

  const onSubmit = handleSubmit(async (values) => {
    const user = await accept.mutateAsync({ ...values, token }).catch(() => null);
    if (user) {
      router.replace('/dashboard');
    }
  });
  const errorText = (key?: string) => (key ? t(`errors.${key}`) : undefined);
  const busy = isSubmitting || accept.isPending;

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-5">
      {accept.isError && (
        <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {t('failed')}
        </p>
      )}
      <div className="space-y-2">
        <Label htmlFor="fullName">{t('fullNameLabel')}</Label>
        <Input id="fullName" autoComplete="name" aria-invalid={!!errors.fullName} {...register('fullName')} />
        {errors.fullName && <p role="alert" className="text-sm text-destructive">{errorText(errors.fullName.message)}</p>}
      </div>
      <div className="space-y-2">
        <Label htmlFor="password">{t('passwordLabel')}</Label>
        <Input id="password" type="password" autoComplete="new-password" aria-invalid={!!errors.password} {...register('password')} />
        {errors.password && <p role="alert" className="text-sm text-destructive">{errorText(errors.password.message)}</p>}
      </div>
      <Button type="submit" size="lg" className="w-full" disabled={busy}>
        {busy ? (
          <>
            <Loader2 className="h-4 w-4 animate-spin" />
            {t('submitting')}
          </>
        ) : (
          t('submit')
        )}
      </Button>
    </form>
  );
}
