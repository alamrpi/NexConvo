import type { S3HealthStatus } from './s3-config.schema';

/**
 * Reason the knowledge-base upload control is blocked.
 *
 * - `unhealthy`: an S3 config exists but its last connection test came back Degraded/Failed.
 * - `not-configured`: there is no S3 config yet, or it has never been tested (Untested).
 */
export type S3UploadBlockReason = 'unhealthy' | 'not-configured';

export interface S3UploadGuard {
  blocked: boolean;
  reason: S3UploadBlockReason | null;
}

export interface S3UploadGuardInput {
  /** True while the `useS3Config` query has not yet resolved (initial load). */
  isLoading: boolean;
  /** `lastTestStatus` from the S3 config DTO, or `null` when no config exists yet. */
  status: S3HealthStatus | null;
}

/**
 * Frontend pre-flight guard (Task 12, re-scoped): the knowledge-base upload UI is disabled
 * unless the workspace's S3 connection is confirmed `Healthy`. This is a UX guard only — the
 * backend does not yet call S3 during upload (deferred to Phase 2's EnsureHealthy guard) — so
 * this purely steers users away from uploads that would silently go nowhere.
 *
 * While the S3 config query is loading, uploads are NOT blocked to avoid flashing the warning
 * banner before we know the real status (S27 — no jarring flicker).
 */
export function getS3UploadGuard({ isLoading, status }: S3UploadGuardInput): S3UploadGuard {
  if (isLoading) return { blocked: false, reason: null };
  if (status === 'Healthy') return { blocked: false, reason: null };
  if (status === 'Degraded' || status === 'Failed') return { blocked: true, reason: 'unhealthy' };
  // status === 'Untested' or null (no config saved yet)
  return { blocked: true, reason: 'not-configured' };
}
