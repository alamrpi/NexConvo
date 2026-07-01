import axios from 'axios';

/**
 * Browser API client (frontend standard S4). Talks ONLY to the same-origin BFF —
 * it never sees a token. A 401 on a normal data call means the silent-refresh chain
 * failed server-side, so we restart auth. Auth endpoints (login/logout) surface their
 * own errors to the caller instead of redirecting.
 */
export const apiClient = axios.create({
  baseURL: '/api/bff',
  timeout: 15_000,
});

apiClient.interceptors.response.use(
  (response) => response,
  (error: unknown) => {
    if (axios.isAxiosError(error)) {
      const isAuthEndpoint = error.config?.url?.includes('/auth/') ?? false;
      if (error.response?.status === 401 && !isAuthEndpoint && typeof window !== 'undefined') {
        window.location.assign('/login');
      }
    }
    return Promise.reject(error);
  },
);
