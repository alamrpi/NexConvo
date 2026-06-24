import { beforeEach, describe, expect, it } from 'vitest';
import { renderHook } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/tests/msw/server';
import { createWrapper } from '@/tests/react-query';
import {
  useConfirmTwoFactorEnrollment,
  useDisableTwoFactor,
  useStartTwoFactorEnrollment,
} from './use-two-factor';
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

describe('useStartTwoFactorEnrollment', () => {
  it('returns the secret + provisioning URI', async () => {
    server.use(
      http.post('/api/bff/account/2fa/start', () =>
        HttpResponse.json({ secret: 'JBSWY3DPEHPK3PXP', otpAuthUri: 'otpauth://totp/NexConvo:o@acme.test?secret=JBSWY3DPEHPK3PXP' }),
      ),
    );
    const { result } = renderHook(() => useStartTwoFactorEnrollment(), { wrapper: createWrapper() });

    const data = await result.current.mutateAsync();

    expect(data.secret).toBe('JBSWY3DPEHPK3PXP');
    expect(data.otpAuthUri).toContain('otpauth://');
  });
});

describe('useConfirmTwoFactorEnrollment', () => {
  it('returns backup codes and flips the session twoFactorEnabled on', async () => {
    server.use(
      http.post('/api/bff/account/2fa/confirm', () => HttpResponse.json({ backupCodes: ['AAAAA-BBBBB', 'CCCCC-DDDDD'] })),
    );
    const { result } = renderHook(() => useConfirmTwoFactorEnrollment(), { wrapper: createWrapper() });

    const data = await result.current.mutateAsync({ code: '123456' });

    expect(data.backupCodes).toHaveLength(2);
    expect(useSessionStore.getState().user?.twoFactorEnabled).toBe(true);
  });

  it('maps a wrong code (422) to codeInvalid and leaves 2FA off', async () => {
    server.use(http.post('/api/bff/account/2fa/confirm', () => HttpResponse.json({ code: 'codeInvalid' }, { status: 422 })));
    const { result } = renderHook(() => useConfirmTwoFactorEnrollment(), { wrapper: createWrapper() });

    await expect(result.current.mutateAsync({ code: '000000' })).rejects.toMatchObject({ code: 'codeInvalid' });
    expect(useSessionStore.getState().user?.twoFactorEnabled).toBe(false);
  });
});

describe('useDisableTwoFactor', () => {
  it('flips the session twoFactorEnabled off on success', async () => {
    useSessionStore.setState({ user: { ...user, twoFactorEnabled: true } });
    server.use(http.post('/api/bff/account/2fa/disable', () => new HttpResponse(null, { status: 204 })));
    const { result } = renderHook(() => useDisableTwoFactor(), { wrapper: createWrapper() });

    await result.current.mutateAsync({ password: 'password123' });

    expect(useSessionStore.getState().user?.twoFactorEnabled).toBe(false);
  });

  it('maps a wrong password (422) to invalidPassword', async () => {
    useSessionStore.setState({ user: { ...user, twoFactorEnabled: true } });
    server.use(http.post('/api/bff/account/2fa/disable', () => HttpResponse.json({ code: 'invalidPassword' }, { status: 422 })));
    const { result } = renderHook(() => useDisableTwoFactor(), { wrapper: createWrapper() });

    await expect(result.current.mutateAsync({ password: 'wrong' })).rejects.toMatchObject({ code: 'invalidPassword' });
    expect(useSessionStore.getState().user?.twoFactorEnabled).toBe(true);
  });
});
