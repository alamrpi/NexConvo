'use client';

import * as React from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { KeyRound, Loader2 } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import {
  backupChallengeSchema,
  totpChallengeSchema,
  type ChallengeCodeValues,
} from '../model/two-factor-challenge.schema';
import { useVerifyTwoFactor } from '../api/use-verify-2fa';

/**
 * Login step 2: enter the authenticator code (or switch to a recovery code). The challenge
 * token is held server-side in an HttpOnly cookie — we only send the code. On success the
 * caller navigates to the dashboard; an expired challenge restarts login (S15).
 */
export function LoginTwoFactorStep({
  onVerified,
  onRestart,
}: {
  onVerified: () => void;
  onRestart: () => void;
}) {
  const t = useTranslations('auth.twoFactor');
  const verify = useVerifyTwoFactor();
  const [useBackup, setUseBackup] = React.useState(false);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<ChallengeCodeValues>({
    resolver: zodResolver(useBackup ? backupChallengeSchema : totpChallengeSchema),
    mode: 'onTouched',
    defaultValues: { code: '' },
  });

  const switchMode = (backup: boolean) => {
    setUseBackup(backup);
    reset({ code: '' });
    verify.reset();
  };

  const onSubmit = handleSubmit(async ({ code }) => {
    try {
      await verify.mutateAsync({ code });
      onVerified();
    } catch (error) {
      // An expired/missing challenge can't be recovered here — restart login.
      if (error instanceof Error && error.message === 'challengeExpired') onRestart();
    }
  });

  const formError =
    verify.isError && verify.error.code !== 'challengeExpired'
      ? t(`errors.${verify.error.code}`)
      : undefined;

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-5">
      <div className="space-y-1">
        <h2 className="text-lg font-semibold tracking-tight">{t('title')}</h2>
        <p className="text-sm text-muted-foreground">{useBackup ? t('backupSubtitle') : t('subtitle')}</p>
      </div>

      {formError && (
        <div role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {formError}
        </div>
      )}

      <div className="space-y-2">
        <Label htmlFor="code">{useBackup ? t('backupLabel') : t('codeLabel')}</Label>
        <Input
          id="code"
          inputMode={useBackup ? 'text' : 'numeric'}
          autoComplete="one-time-code"
          autoFocus
          placeholder={useBackup ? 'XXXXX-XXXXX' : '123456'}
          aria-invalid={!!errors.code}
          aria-describedby={errors.code ? 'code-error' : undefined}
          {...register('code')}
        />
        {errors.code && (
          <p id="code-error" role="alert" className="text-sm text-destructive">
            {t(`errors.${errors.code.message}`)}
          </p>
        )}
      </div>

      <Button type="submit" size="lg" className="w-full" disabled={verify.isPending}>
        {verify.isPending ? (
          <>
            <Loader2 className="h-4 w-4 animate-spin" />
            {t('verifying')}
          </>
        ) : (
          <>
            <KeyRound aria-hidden="true" />
            {t('verify')}
          </>
        )}
      </Button>

      <div className="flex items-center justify-between text-sm">
        <button
          type="button"
          onClick={() => switchMode(!useBackup)}
          className="font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          {useBackup ? t('useAuthenticator') : t('useBackup')}
        </button>
        <button
          type="button"
          onClick={onRestart}
          className="text-muted-foreground underline-offset-4 hover:text-foreground hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          {t('back')}
        </button>
      </div>
    </form>
  );
}
