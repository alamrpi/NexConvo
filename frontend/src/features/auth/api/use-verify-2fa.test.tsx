import { beforeEach, describe, expect, it } from 'vitest';
import { renderHook } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/tests/msw/server';
import { createWrapper } from '@/tests/react-query';
import { useVerifyTwoFactor } from './use-verify-2fa';
import { useSessionStore } from '../model/session.store';
import type { CurrentUser } from '../model/auth.types';

const user: CurrentUser = {
  userId: 'u-1',
  tenantId: 't-1',
  tenantSlug: 'acme',
  email: 'o@acme.test',
  fullName: 'Owner',
  emailVerified: true,
  twoFactorEnabled: true,
  workspaceRequiresTwoFactor: false,
  roles: ['Owner'],
  permissions: ['*'],
};

beforeEach(() => useSessionStore.setState({ user: null }));

describe('useVerifyTwoFactor', () => {
  it('hydrates the session when the code verifies', async () => {
    server.use(http.post('/api/bff/auth/2fa/verify', () => HttpResponse.json(user)));
    const { result } = renderHook(() => useVerifyTwoFactor(), { wrapper: createWrapper() });

    await result.current.mutateAsync({ code: '123456' });

    expect(useSessionStore.getState().user).toEqual(user);
  });

  it('maps a wrong code to invalidCode', async () => {
    server.use(http.post('/api/bff/auth/2fa/verify', () => HttpResponse.json({ title: 'Verification failed' }, { status: 401 })));
    const { result } = renderHook(() => useVerifyTwoFactor(), { wrapper: createWrapper() });

    await expect(result.current.mutateAsync({ code: '000000' })).rejects.toMatchObject({ code: 'invalidCode' });
  });

  it('maps a missing/expired challenge to challengeExpired', async () => {
    server.use(http.post('/api/bff/auth/2fa/verify', () => HttpResponse.json({ code: 'challengeExpired' }, { status: 401 })));
    const { result } = renderHook(() => useVerifyTwoFactor(), { wrapper: createWrapper() });

    await expect(result.current.mutateAsync({ code: '123456' })).rejects.toMatchObject({ code: 'challengeExpired' });
  });
});
