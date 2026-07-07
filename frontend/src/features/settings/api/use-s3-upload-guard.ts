import { useS3Config } from './use-s3-config';
import { getS3UploadGuard, type S3UploadGuard } from '../model/s3-upload-guard';

/**
 * Composes the S3 config query with the pure status→guard mapping so consumers (the
 * knowledge-base upload UI) get a single `{ blocked, reason }` result without re-deriving the
 * loading/null handling themselves. See `getS3UploadGuard` for the guard semantics.
 */
export function useS3UploadGuard(): S3UploadGuard {
  const { data, isLoading } = useS3Config();
  return getS3UploadGuard({ isLoading, status: data?.lastTestStatus ?? null });
}
