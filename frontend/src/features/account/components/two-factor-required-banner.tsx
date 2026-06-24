'use client';

import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { ShieldAlert } from 'lucide-react';
import { useSessionStore } from '@/features/auth/model/session.store';

/**
 * Dashboard-level prompt shown when the workspace requires 2FA but the current user hasn't
 * enabled it. Their sensitive writes are server-gated until they do (S9 — the server enforces;
 * this banner is the nudge). Not dismissible — it's a required action.
 */
export function TwoFactorRequiredBanner() {
  const t = useTranslations('twoFactorBanner');
  const required = useSessionStore((s) => s.user?.workspaceRequiresTwoFactor ?? false);
  const enrolled = useSessionStore((s) => s.user?.twoFactorEnabled ?? true);

  if (!required || enrolled) return null;

  return (
    <div
      role="alert"
      className="flex flex-wrap items-center gap-3 border-b border-primary/30 bg-primary/10 px-4 py-2.5 text-sm sm:px-6"
    >
      <ShieldAlert className="h-4 w-4 shrink-0 text-primary" aria-hidden="true" />
      <p className="min-w-0 flex-1 text-foreground">{t('message')}</p>
      <Link
        href="/dashboard/settings/security"
        className="rounded-sm font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        {t('action')}
      </Link>
    </div>
  );
}
