'use client';

import * as React from 'react';
import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { ArrowLeft, ArrowRight, CheckCircle2, Loader2, XCircle } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { useVerifyEmail } from '../api/use-password-reset';

/** Calls verify once on mount (StrictMode-safe via a ref guard) and renders the outcome. */
export function VerifyEmailStatus({ token }: { token: string }) {
  const t = useTranslations('verifyEmail');
  const verify = useVerifyEmail();
  const started = React.useRef(false);

  React.useEffect(() => {
    if (!token || started.current) {
      return;
    }
    started.current = true;
    verify.mutate(token);
  }, [token, verify]);

  if (!token) {
    return <Status icon="error" title={t('missingToken')} />;
  }
  if (verify.isPending || verify.isIdle) {
    return <Status icon="loading" title={t('verifying')} />;
  }
  if (verify.isError) {
    return (
      <Status icon="error" title={t('failed')}>
        <Button asChild variant="outline">
          <Link href="/login">
            <ArrowLeft aria-hidden="true" />
            {t('backToLogin')}
          </Link>
        </Button>
      </Status>
    );
  }
  return (
    <Status icon="success" title={t('success')}>
      <Button asChild>
        <Link href="/dashboard">
          {t('goToDashboard')}
          <ArrowRight aria-hidden="true" />
        </Link>
      </Button>
    </Status>
  );
}

function Status({
  icon,
  title,
  children,
}: {
  icon: 'loading' | 'success' | 'error';
  title: string;
  children?: React.ReactNode;
}) {
  const Icon = icon === 'loading' ? Loader2 : icon === 'success' ? CheckCircle2 : XCircle;
  const tone = icon === 'error' ? 'text-destructive' : 'text-primary';
  return (
    <div role="status" className="flex flex-col items-center gap-4 text-center">
      <Icon className={`h-10 w-10 ${tone} ${icon === 'loading' ? 'animate-spin' : ''}`} aria-hidden="true" />
      <p className="text-lg font-medium">{title}</p>
      {children}
    </div>
  );
}
