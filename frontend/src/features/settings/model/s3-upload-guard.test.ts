import { describe, expect, it } from 'vitest';
import { getS3UploadGuard } from './s3-upload-guard';

describe('getS3UploadGuard', () => {
  it('allows uploads when the config is Healthy', () => {
    const guard = getS3UploadGuard({ isLoading: false, status: 'Healthy' });
    expect(guard).toEqual({ blocked: false, reason: null });
  });

  it('blocks uploads with an "unhealthy" reason for Degraded status', () => {
    const guard = getS3UploadGuard({ isLoading: false, status: 'Degraded' });
    expect(guard).toEqual({ blocked: true, reason: 'unhealthy' });
  });

  it('blocks uploads with an "unhealthy" reason for Failed status', () => {
    const guard = getS3UploadGuard({ isLoading: false, status: 'Failed' });
    expect(guard).toEqual({ blocked: true, reason: 'unhealthy' });
  });

  it('blocks uploads with a "not-configured" reason for Untested status', () => {
    const guard = getS3UploadGuard({ isLoading: false, status: 'Untested' });
    expect(guard).toEqual({ blocked: true, reason: 'not-configured' });
  });

  it('blocks uploads with a "not-configured" reason when there is no config yet', () => {
    const guard = getS3UploadGuard({ isLoading: false, status: null });
    expect(guard).toEqual({ blocked: true, reason: 'not-configured' });
  });

  it('does not block while the config query is still loading (avoids a banner flash)', () => {
    const guard = getS3UploadGuard({ isLoading: true, status: null });
    expect(guard).toEqual({ blocked: false, reason: null });
  });
});
