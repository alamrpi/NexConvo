'use client';

import * as React from 'react';
import { useTranslations } from 'next-intl';
import { Loader2, MailWarning, X } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { useSessionStore } from '@/features/auth/model/session.store';
import { useResendVerification } from '../api/use-resend-verification';

/**
 * Dashboard-level banner shown until the signed-in user verifies their email (soft
 * enforcement — sensitive writes are blocked server-side until then). Reads the live
 * `emailVerified` flag from the session (S7). Dismissal is session-only: it re-appears on
 * reload until the email is actually verified. Uses semantic `warning` tokens (S25).
 */
export function EmailVerificationBanner() {
  const t = useTranslations('verifyBanner');
  // Default to verified when there's no user, so the banner never flashes for a brief null session.
  const emailVerified = useSessionStore((s) => s.user?.emailVerified ?? true);
  const resend = useResendVerification();
  const [dismissed, setDismissed] = React.useState(false);

  if (emailVerified || dismissed) return null;

  return (
    <div
      role="status"
      className="flex flex-wrap items-center gap-3 border-b border-warning/30 bg-warning/10 px-4 py-2.5 text-sm sm:px-6"
    >
      <MailWarning className="h-4 w-4 shrink-0 text-warning" aria-hidden="true" />
      <p className="min-w-0 flex-1 text-foreground">{resend.isSuccess ? t('sent') : t('message')}</p>

      {!resend.isSuccess && (
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={resend.isPending}
          onClick={() => void resend.mutateAsync().catch(() => null)}
        >
          {resend.isPending ? (
            <>
              <Loader2 className="h-4 w-4 animate-spin" />
              {t('sending')}
            </>
          ) : (
            t('resend')
          )}
        </Button>
      )}

      <button
        type="button"
        onClick={() => setDismissed(true)}
        aria-label={t('dismiss')}
        className="rounded-sm p-1 text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        <X className="h-4 w-4" />
      </button>
    </div>
  );
}
