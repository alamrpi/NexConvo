'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { KeyRound, Loader2 } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { resetPasswordSchema, type ResetPasswordValues } from '../model/password-reset.schema';
import { useResetPassword } from '../api/use-password-reset';

export function ResetPasswordForm({ token }: { token: string }) {
  const t = useTranslations('resetPassword');
  const router = useRouter();
  const reset = useResetPassword();
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ResetPasswordValues>({
    resolver: zodResolver(resetPasswordSchema),
    mode: 'onTouched',
    defaultValues: { password: '', confirmPassword: '' },
  });

  if (!token) {
    return <p role="alert" className="text-sm text-destructive">{t('missingToken')}</p>;
  }

  const onSubmit = handleSubmit(async (values) => {
    const ok = await reset.mutateAsync({ token, newPassword: values.password }).then(() => true).catch(() => false);
    if (ok) {
      router.replace('/login?reset=1');
    }
  });
  const errorText = (key?: string) => (key ? t(`errors.${key}`) : undefined);
  const busy = isSubmitting || reset.isPending;

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-5">
      {reset.isError && (
        <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {t('failed')}
        </p>
      )}
      <div className="space-y-2">
        <Label htmlFor="password">{t('passwordLabel')}</Label>
        <Input id="password" type="password" autoComplete="new-password" aria-invalid={!!errors.password} {...register('password')} />
        {errors.password && <p role="alert" className="text-sm text-destructive">{errorText(errors.password.message)}</p>}
      </div>
      <div className="space-y-2">
        <Label htmlFor="confirmPassword">{t('confirmLabel')}</Label>
        <Input id="confirmPassword" type="password" autoComplete="new-password" aria-invalid={!!errors.confirmPassword} {...register('confirmPassword')} />
        {errors.confirmPassword && <p role="alert" className="text-sm text-destructive">{errorText(errors.confirmPassword.message)}</p>}
      </div>
      <Button type="submit" size="lg" className="w-full" disabled={busy}>
        {busy ? (
          <>
            <Loader2 className="h-4 w-4 animate-spin" />
            {t('submitting')}
          </>
        ) : (
          <>
            <KeyRound aria-hidden="true" />
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
