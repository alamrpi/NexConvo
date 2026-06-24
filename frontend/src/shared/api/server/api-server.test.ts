// @vitest-environment node
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

// Neutralize the server-only guard so the module can load under the test runtime.
vi.mock('server-only', () => ({}));

import { delay, http, HttpResponse } from 'msw';
import { server } from '@/tests/msw/server';
import { createServerApiClient, SessionExpiredError } from './api-server';

const GATEWAY = 'http://gateway:8080';

describe('createServerApiClient (silent refresh interceptor)', () => {
  beforeEach(() => {
    process.env.API_GATEWAY_URL = GATEWAY;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('refreshes once on 401, rotates tokens, and retries — even under concurrency', async () => {
    const onRefreshed = vi.fn();
    const client = createServerApiClient({ accessToken: 'old', refreshToken: 'r1' }, onRefreshed);

    let refreshCalls = 0;
    server.use(
      http.post(`${GATEWAY}/api/v1/auth/refresh`, async () => {
        refreshCalls += 1;
        // Small delay so two concurrent 401s overlap on the same in-flight refresh.
        await delay(20);
        return HttpResponse.json({ accessToken: 'new', expiresInSeconds: 900, refreshToken: 'r2' });
      }),
      // 401 while the stale token is presented; 200 once the rotated token is attached.
      http.get(`${GATEWAY}/widgets`, ({ request }) =>
        request.headers.get('authorization') === 'Bearer old'
          ? new HttpResponse(null, { status: 401 })
          : HttpResponse.json({ ok: true }),
      ),
    );

    const [a, b] = await Promise.all([client.get('/widgets'), client.get('/widgets')]);

    expect(a.data).toEqual({ ok: true });
    expect(b.data).toEqual({ ok: true });
    expect(refreshCalls).toBe(1); // single-flight
    expect(onRefreshed).toHaveBeenCalledWith({
      accessToken: 'new',
      expiresInSeconds: 900,
      refreshToken: 'r2',
    });
  });

  it('throws SessionExpiredError when the refresh token is dead', async () => {
    const client = createServerApiClient({ accessToken: 'old', refreshToken: 'bad' }, vi.fn());

    server.use(
      http.post(`${GATEWAY}/api/v1/auth/refresh`, () => new HttpResponse(null, { status: 401 })),
      http.get(`${GATEWAY}/widgets`, () => new HttpResponse(null, { status: 401 })),
    );

    await expect(client.get('/widgets')).rejects.toBeInstanceOf(SessionExpiredError);
  });

  it('does not retry on a non-401 error', async () => {
    const client = createServerApiClient({ accessToken: 'tok', refreshToken: 'r1' }, vi.fn());

    let refreshCalls = 0;
    server.use(
      http.post(`${GATEWAY}/api/v1/auth/refresh`, () => {
        refreshCalls += 1;
        return HttpResponse.json({ accessToken: 'new', expiresInSeconds: 900, refreshToken: 'r2' });
      }),
      http.get(`${GATEWAY}/widgets`, () => new HttpResponse(null, { status: 500 })),
    );

    await expect(client.get('/widgets')).rejects.toMatchObject({
      response: { status: 500 },
    });
    expect(refreshCalls).toBe(0);
  });
});
