import { describe, expect, it } from 'vitest';
import { renderHook } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/tests/msw/server';
import { createWrapper } from '@/tests/react-query';
import { useTestS3Connection } from './use-test-s3-connection';

describe('useTestS3Connection', () => {
  it('returns success: true with status/detail/latency on a healthy connection', async () => {
    server.use(
      http.post('/api/bff/settings/s3-config/test', () =>
        HttpResponse.json({
          success: true,
          status: 'Healthy',
          detail: 'Bucket reachable',
          errorMessage: null,
          latencyMs: 142,
        }),
      ),
    );

    const { result } = renderHook(() => useTestS3Connection(), { wrapper: createWrapper() });

    const response = await result.current.mutateAsync({
      bucketName: 'my-bucket',
      region: 'us-east-1',
    });

    expect(response).toEqual({
      success: true,
      status: 'Healthy',
      detail: 'Bucket reachable',
      errorMessage: null,
      latencyMs: 142,
    });
  });

  it('does not throw on a failed connection (HTTP 200 with success: false)', async () => {
    server.use(
      http.post('/api/bff/settings/s3-config/test', () =>
        HttpResponse.json({
          success: false,
          status: 'Failed',
          detail: null,
          errorMessage: 'Access denied',
          latencyMs: 88,
        }),
      ),
    );

    const { result } = renderHook(() => useTestS3Connection(), { wrapper: createWrapper() });

    await expect(
      result.current.mutateAsync({ bucketName: 'my-bucket', region: 'us-east-1' }),
    ).resolves.toMatchObject({
      success: false,
      status: 'Failed',
      errorMessage: 'Access denied',
    });
  });

  it('sends blank credential fields as undefined so the server tests with stored keys', async () => {
    let capturedBody: unknown;
    server.use(
      http.post('/api/bff/settings/s3-config/test', async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json({ success: true, status: 'Healthy' });
      }),
    );

    const { result } = renderHook(() => useTestS3Connection(), { wrapper: createWrapper() });

    await result.current.mutateAsync({
      bucketName: 'my-bucket',
      region: 'us-east-1',
      accessKeyId: '',
      secretAccessKey: '',
    });

    expect(capturedBody).toMatchObject({
      bucketName: 'my-bucket',
      region: 'us-east-1',
    });
  });
});
