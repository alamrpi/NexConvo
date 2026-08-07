'use client';

import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { CloudOff } from 'lucide-react';
import { useS3UploadGuard } from '../api/use-s3-upload-guard';

/**
 * Frontend pre-flight guard (Task 12, re-scoped) for the knowledge-base upload UI.
 *
 * S3 storage isn't wired into the upload/ingestion pipeline yet (the backend `EnsureHealthy`
 * guard is deferred to Phase 2), so this is a UX-only guard: it warns the user whenever the
 * workspace's S3 connection isn't confirmed `Healthy`, instead of letting them upload into a
 * pipeline that would silently do nothing useful with the bytes. The server still owns the
 * real enforcement once Phase 2 lands (S9) — this only steers the user away pre-flight.
 *
 * Renders nothing while the S3 config query is loading or once the connection is Healthy.
 */
export function S3UploadGuardBanner() {
  const t = useTranslations('chat');
  const guard = useS3UploadGuard();

  if (!guard.blocked) return null;

  return (
    <div
      role="alert"
      className="flex flex-wrap items-center gap-3 rounded-lg border border-warning/30 bg-warning/10 px-4 py-2.5 text-sm"
    >
      <CloudOff className="h-4 w-4 shrink-0 text-warning" aria-hidden="true" />
      <p className="min-w-0 flex-1 text-foreground">
        {guard.reason === 'not-configured'
          ? t('knowledge.s3NotConfigured')
          : t('knowledge.s3Unhealthy')}
      </p>
      <Link
        href="/dashboard/settings/s3"
        className="shrink-0 rounded-sm font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        {t('knowledge.s3UnhealthyCta')}
      </Link>
    </div>
  );
}
