import 'server-only';

import axios, { type AxiosError, type AxiosInstance } from 'axios';
import { serverEnv } from '@/shared/lib/env';
import type { AuthTokens } from '@/features/auth/model/auth.types';
import type { SessionTokens } from './session';

// Module augmentation keeps the retry flag typed — never `any` (frontend standard S2).
declare module 'axios' {
  export interface InternalAxiosRequestConfig {
    _retried?: boolean;
  }
}

/** Thrown when the refresh token is dead — the BFF turns this into a 401 + cleared cookies. */
export class SessionExpiredError extends Error {
  constructor() {
    super('Session expired');
    this.name = 'SessionExpiredError';
  }
}

type OnRefreshed = (tokens: AuthTokens) => void;

/**
 * Request-scoped, tenant-isolated API client for the BFF (frontend standard S4).
 *
 * - Injects the current access token on every outbound call to the gateway.
 * - On a 401, silently refreshes ONCE (single-flight — concurrent calls share one
 *   refresh), rotates the in-memory tokens, retries the original request, and reports
 *   the rotated tokens via `onRefreshed` so the route handler can re-set the cookies.
 * - If refresh fails, throws `SessionExpiredError`.
 */
export function createServerApiClient(
  tokens: SessionTokens,
  onRefreshed: OnRefreshed,
  correlationId?: string,
): AxiosInstance {
  const { API_GATEWAY_URL } = serverEnv();
  let accessToken = tokens.accessToken;
  let refreshToken = tokens.refreshToken;
  let refreshing: Promise<void> | null = null;

  const client = axios.create({
    baseURL: API_GATEWAY_URL,
    timeout: 10_000,
    // Forward the browser's correlation id so the trace threads through the gateway + service logs.
    headers: correlationId ? { 'x-correlation-id': correlationId } : undefined,
  });

  client.interceptors.request.use((config) => {
    config.headers.Authorization = `Bearer ${accessToken}`;
    return config;
  });

  client.interceptors.response.use(
    (response) => response,
    async (error: AxiosError) => {
      const original = error.config;
      if (error.response?.status !== 401 || !original || original._retried) {
        throw error;
      }
      original._retried = true;

      // Single-flight: the first 401 starts the refresh; concurrent 401s await the same promise.
      refreshing ??= refreshTokens(API_GATEWAY_URL, refreshToken, correlationId)
        .then((rotated) => {
          accessToken = rotated.accessToken;
          refreshToken = rotated.refreshToken;
          onRefreshed(rotated);
        })
        .finally(() => {
          refreshing = null;
        });

      await refreshing;
      original.headers.Authorization = `Bearer ${accessToken}`;
      return client(original);
    },
  );

  return client;
}

/** Bare call to the gateway refresh endpoint — must NOT go through the client interceptor. */
async function refreshTokens(
  gatewayUrl: string,
  refreshToken: string,
  correlationId?: string,
): Promise<AuthTokens> {
  try {
    const { data } = await axios.post<AuthTokens>(
      `${gatewayUrl}/api/v1/auth/refresh`,
      { refreshToken },
      correlationId ? { headers: { 'x-correlation-id': correlationId } } : undefined,
    );
    return data;
  } catch {
    throw new SessionExpiredError();
  }
}
