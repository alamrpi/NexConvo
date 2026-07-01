import { beforeEach, describe, expect, it } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/tests/msw/server';
import { createWrapper } from '@/tests/react-query';
import { useSecuritySettings, useUpdateSecuritySettings } from './use-security-settings';
import { useSessionStore } from '@/features/auth/model/session.store';
import type { CurrentUser } from '@/features/auth/model/auth.types';

const user: CurrentUser = {
  userId: 'u-1',
  tenantId: 't-1',
  tenantSlug: 'acme',
  email: 'o@acme.test',
  fullName: 'Owner',
  emailVerified: true,
  twoFactorEnabled: false,
  workspaceRequiresTwoFactor: false,
  roles: ['Owner'],
  permissions: ['*'],
};

beforeEach(() => useSessionStore.setState({ user: { ...user } }));

describe('useSecuritySettings', () => {
  it('reads the workspace 2FA requirement', async () => {
    server.use(http.get('/api/bff/settings/security', () => HttpResponse.json({ requireTwoFactor: true })));
    const { result } = renderHook(() => useSecuritySettings(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.data).toEqual({ requireTwoFactor: true }));
  });
});

describe('useUpdateSecuritySettings', () => {
  it('persists the toggle and reflects it in the session', async () => {
    server.use(http.put('/api/bff/settings/security', () => new HttpResponse(null, { status: 204 })));
    const { result } = renderHook(() => useUpdateSecuritySettings(), { wrapper: createWrapper() });

    await result.current.mutateAsync({ requireTwoFactor: true });

    expect(useSessionStore.getState().user?.workspaceRequiresTwoFactor).toBe(true);
  });
});
