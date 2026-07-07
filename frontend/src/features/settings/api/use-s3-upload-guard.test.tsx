import { describe, expect, it } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/tests/msw/server';
import { createWrapper } from '@/tests/react-query';
import { useS3UploadGuard } from './use-s3-upload-guard';
import type { S3ConfigDto } from '../model/s3-config.types';

const baseConfig: S3ConfigDto = {
  id: 'cfg-1',
  bucketName: 'my-bucket',
  region: 'us-east-1',
  hasAccessKey: true,
  customEndpoint: null,
  pathPrefix: null,
  isActive: true,
  lastTestStatus: 'Healthy',
  lastTestedAt: '2026-07-01T00:00:00Z',
  lastTestError: null,
  lastTestLatencyMs: 120,
};

describe('useS3UploadGuard', () => {
  it('does not block uploads while the S3 config query is loading', () => {
    server.use(
      http.get('/api/bff/settings/s3-config', async () => {
        await new Promise(() => {}); // never resolves within the test
        return HttpResponse.json(baseConfig);
      }),
    );
    const { result } = renderHook(() => useS3UploadGuard(), { wrapper: createWrapper() });
    expect(result.current.blocked).toBe(false);
  });

  it('does not block uploads when the connection is Healthy', async () => {
    server.use(
      http.get('/api/bff/settings/s3-config', () => HttpResponse.json(baseConfig)),
    );
    const { result } = renderHook(() => useS3UploadGuard(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.blocked).toBe(false));
    expect(result.current.reason).toBeNull();
  });

  it('blocks uploads with reason "unhealthy" when the connection is Degraded', async () => {
    server.use(
      http.get('/api/bff/settings/s3-config', () =>
        HttpResponse.json({ ...baseConfig, lastTestStatus: 'Degraded' }),
      ),
    );
    const { result } = renderHook(() => useS3UploadGuard(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.blocked).toBe(true));
    expect(result.current.reason).toBe('unhealthy');
  });

  it('blocks uploads with reason "unhealthy" when the connection is Failed', async () => {
    server.use(
      http.get('/api/bff/settings/s3-config', () =>
        HttpResponse.json({ ...baseConfig, lastTestStatus: 'Failed' }),
      ),
    );
    const { result } = renderHook(() => useS3UploadGuard(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.blocked).toBe(true));
    expect(result.current.reason).toBe('unhealthy');
  });

  it('blocks uploads with reason "not-configured" when there is no config yet', async () => {
    server.use(
      http.get('/api/bff/settings/s3-config', () => HttpResponse.json(null)),
    );
    const { result } = renderHook(() => useS3UploadGuard(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.blocked).toBe(true));
    expect(result.current.reason).toBe('not-configured');
  });

  it('blocks uploads with reason "not-configured" when the connection is Untested', async () => {
    server.use(
      http.get('/api/bff/settings/s3-config', () =>
        HttpResponse.json({ ...baseConfig, lastTestStatus: 'Untested' }),
      ),
    );
    const { result } = renderHook(() => useS3UploadGuard(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.blocked).toBe(true));
    expect(result.current.reason).toBe('not-configured');
  });
});
