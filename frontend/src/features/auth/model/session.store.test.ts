import { beforeEach, describe, expect, it } from 'vitest';
import { useSessionStore } from './session.store';
import type { CurrentUser } from './auth.types';

const baseUser: CurrentUser = {
  userId: 'u-1',
  tenantId: 't-1',
  tenantSlug: 'dhaka-retail',
  email: 'alam@dhakaretail.co',
  fullName: 'Md. Alam Hossain',
  emailVerified: false,
  twoFactorEnabled: false,
  workspaceRequiresTwoFactor: false,
  roles: ['Agent'],
  permissions: ['leads:read'],
};

beforeEach(() => {
  useSessionStore.setState({ user: null });
});

describe('session store RBAC', () => {
  it('grants nothing when logged out', () => {
    const { hasPermission, hasRole } = useSessionStore.getState();
    expect(hasPermission('leads:read')).toBe(false);
    expect(hasRole('Agent')).toBe(false);
  });

  it('grants an explicitly held permission only', () => {
    useSessionStore.getState().setUser(baseUser);
    const { hasPermission } = useSessionStore.getState();
    expect(hasPermission('leads:read')).toBe(true);
    expect(hasPermission('leads:write')).toBe(false);
  });

  it('honors the "*" wildcard for any permission', () => {
    useSessionStore.getState().setUser({ ...baseUser, permissions: ['*'] });
    expect(useSessionStore.getState().hasPermission('tenant:manage')).toBe(true);
  });

  it('checks roles', () => {
    useSessionStore.getState().setUser(baseUser);
    const { hasRole } = useSessionStore.getState();
    expect(hasRole('Agent')).toBe(true);
    expect(hasRole('Owner')).toBe(false);
  });

  it('carries the account flags from /me (emailVerified, twoFactorEnabled)', () => {
    useSessionStore.getState().setUser({ ...baseUser, emailVerified: true, twoFactorEnabled: true });
    const { user } = useSessionStore.getState();
    expect(user?.emailVerified).toBe(true);
    expect(user?.twoFactorEnabled).toBe(true);
  });
});
