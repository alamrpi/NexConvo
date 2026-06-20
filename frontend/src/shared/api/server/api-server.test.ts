// @vitest-environment node
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

// Neutralize the server-only guard so the module can load under the test runtime.
vi.mock('server-only', () => ({}));

import axios from 'axios';
import MockAdapter from 'axios-mock-adapter';
import { createServerApiClient, SessionExpiredError } from './api-server';

const GATEWAY = 'http://gateway:8080';

describe('createServerApiClient (silent refresh interceptor)', () => {
  let defaultMock: MockAdapter;

  beforeEach(() => {
    process.env.API_GATEWAY_URL = GATEWAY;
    defaultMock = new MockAdapter(axios);
  });

  afterEach(() => {
    defaultMock.restore();
    vi.restoreAllMocks();
  });

  it('refreshes once on 401, rotates tokens, and retries — even under concurrency', async () => {
    const onRefreshed = vi.fn();
    const client = createServerApiClient({ accessToken: 'old', refreshToken: 'r1' }, onRefreshed);
    const clientMock = new MockAdapter(client);

    let refreshCalls = 0;
    defaultMock.onPost(`${GATEWAY}/api/v1/auth/refresh`).reply(async () => {
      refreshCalls += 1;
      // Small delay so two concurrent 401s overlap on the same in-flight refresh.
      await new Promise((resolve) => setTimeout(resolve, 20));
      return [200, { accessToken: 'new', expiresInSeconds: 900, refreshToken: 'r2' }];
    });

    // 401 while the stale token is presented; 200 once the rotated token is attached.
    clientMock.onGet('/widgets').reply((config) =>
      config.headers?.Authorization === 'Bearer old' ? [401] : [200, { ok: true }],
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
    const clientMock = new MockAdapter(client);

    defaultMock.onPost(`${GATEWAY}/api/v1/auth/refresh`).reply(401);
    clientMock.onGet('/widgets').reply(401);

    await expect(client.get('/widgets')).rejects.toBeInstanceOf(SessionExpiredError);
  });

  it('does not retry on a non-401 error', async () => {
    const client = createServerApiClient({ accessToken: 'tok', refreshToken: 'r1' }, vi.fn());
    const clientMock = new MockAdapter(client);

    let refreshCalls = 0;
    defaultMock.onPost(`${GATEWAY}/api/v1/auth/refresh`).reply(() => {
      refreshCalls += 1;
      return [200, { accessToken: 'new', expiresInSeconds: 900, refreshToken: 'r2' }];
    });
    clientMock.onGet('/widgets').reply(500);

    await expect(client.get('/widgets')).rejects.toMatchObject({
      response: { status: 500 },
    });
    expect(refreshCalls).toBe(0);
  });
});
