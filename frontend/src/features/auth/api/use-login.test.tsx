import { beforeEach, describe, expect, it } from 'vitest';
import { renderHook } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/tests/msw/server';
import { createWrapper } from '@/tests/react-query';
import { useLogin } from './use-login';
import { useSessionStore } from '../model/session.store';
import type { CurrentUser } from '../model/auth.types';

const credentials = { tenantSlug: 'acme', email: 'o@acme.test', password: 'password123' };

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

beforeEach(() => useSessionStore.setState({ user: null }));

describe('useLogin', () => {
  it('authenticates and hydrates the session on a normal login', async () => {
    server.use(http.post('/api/bff/auth/login', () => HttpResponse.json(user)));
    const { result } = renderHook(() => useLogin(), { wrapper: createWrapper() });

    const outcome = await result.current.mutateAsync(credentials);

    expect(outcome).toEqual({ kind: 'authenticated', user });
    expect(useSessionStore.getState().user).toEqual(user);
  });

  it('returns twoFactorRequired without hydrating the session', async () => {
    server.use(http.post('/api/bff/auth/login', () => HttpResponse.json({ twoFactorRequired: true })));
    const { result } = renderHook(() => useLogin(), { wrapper: createWrapper() });

    const outcome = await result.current.mutateAsync(credentials);

    expect(outcome).toEqual({ kind: 'twoFactorRequired' });
    expect(useSessionStore.getState().user).toBeNull();
  });

  it('maps a 401 to invalidCredentials', async () => {
    server.use(http.post('/api/bff/auth/login', () => HttpResponse.json({ title: 'Login failed' }, { status: 401 })));
    const { result } = renderHook(() => useLogin(), { wrapper: createWrapper() });

    await expect(result.current.mutateAsync(credentials)).rejects.toMatchObject({ code: 'invalidCredentials' });
  });
});
