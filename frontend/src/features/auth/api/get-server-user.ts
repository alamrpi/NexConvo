import 'server-only';

import axios from 'axios';
import { serverEnv } from '@/shared/lib/env';
import { readSession } from '@/shared/api/server/session';
import { correlationHeaders } from '@/shared/api/server/correlation';
import type { CurrentUser } from '../model/auth.types';

/**
 * Server-side fetch of the current user for the protected (dashboard) layout, used to
 * hydrate the session store with no auth flash (S7). A React Server Component cannot
 * write cookies, so it does NOT refresh here — the middleware silently refreshes and
 * rotates the access cookie before this runs, so the token is fresh. Returns null on
 * any auth failure; the layout then redirects to /login.
 */
const E2E_MOCK_USER: CurrentUser = {
  userId: 'e2e-user-1',
  tenantId: 'tenant-e2e',
  tenantSlug: 'e2e',
  email: 'e2e@nexconvo.test',
  fullName: 'E2E Tester',
  emailVerified: true,
  twoFactorEnabled: false,
  workspaceRequiresTwoFactor: false,
  roles: ['admin'],
  permissions: ['*'],
};

export async function getServerUser(): Promise<CurrentUser | null> {
  if (process.env.NEXT_PUBLIC_E2E === 'true') return E2E_MOCK_USER;

  const session = await readSession();
  if (!session) return null;

  try {
    const { API_GATEWAY_URL } = serverEnv();
    const { data } = await axios.get<CurrentUser>(`${API_GATEWAY_URL}/api/v1/auth/me`, {
      headers: correlationHeaders(undefined, { Authorization: `Bearer ${session.accessToken}` }),
      timeout: 10_000,
    });
    return data;
  } catch {
    return null;
  }
}
