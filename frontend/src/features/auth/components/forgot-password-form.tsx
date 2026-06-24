'use client';

import Link from 'next/link';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { ArrowLeft, Loader2, Send } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { forgotPasswordSchema, type ForgotPasswordValues } from '../model/password-reset.schema';
import { useForgotPassword } from '../api/use-password-reset';

export function ForgotPasswordForm() {
  const t = useTranslations('forgotPassword');
  const forgot = useForgotPassword();
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ForgotPasswordValues>({
    resolver: zodResolver(forgotPasswordSchema),
    mode: 'onTouched',
    defaultValues: { tenantSlug: '', email: '' },
  });

  const onSubmit = handleSubmit(async (values) => {
    await forgot.mutateAsync(values).catch(() => null);
  });
  const errorText = (key?: string) => (key ? t(`errors.${key}`) : undefined);
  const busy = isSubmitting || forgot.isPending;

  // Always-success UX (no enumeration): once submitted, show the same confirmation.
  if (forgot.isSuccess) {
    return (
      <div role="status" className="space-y-4 text-sm">
        <p>{t('checkInbox')}</p>
        <Button asChild variant="outline">
          <Link href="/login">
            <ArrowLeft aria-hidden="true" />
            {t('backToLogin')}
          </Link>
        </Button>
      </div>
    );
  }

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-5">
      <div className="space-y-2">
        <Label htmlFor="tenantSlug">{t('tenantLabel')}</Label>
        <Input id="tenantSlug" autoComplete="organization" aria-invalid={!!errors.tenantSlug} {...register('tenantSlug')} />
        {errors.tenantSlug && <p role="alert" className="text-sm text-destructive">{errorText(errors.tenantSlug.message)}</p>}
      </div>
      <div className="space-y-2">
        <Label htmlFor="email">{t('emailLabel')}</Label>
        <Input id="email" type="email" autoComplete="email" aria-invalid={!!errors.email} {...register('email')} />
        {errors.email && <p role="alert" className="text-sm text-destructive">{errorText(errors.email.message)}</p>}
      </div>
      <Button type="submit" size="lg" className="w-full" disabled={busy}>
        {busy ? (
          <>
            <Loader2 className="h-4 w-4 animate-spin" />
            {t('submitting')}
          </>
        ) : (
          <>
            <Send aria-hidden="true" />
            {t('submit')}
          </>
        )}
      </Button>
      <p className="text-center text-sm text-muted-foreground">
        <Link href="/login" className="font-medium text-primary underline-offset-4 hover:underline">
          {t('backToLogin')}
        </Link>
      </p>
    </form>
  );
}
