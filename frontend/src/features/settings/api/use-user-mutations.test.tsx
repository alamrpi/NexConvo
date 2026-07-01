import { describe, expect, it } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/tests/msw/server';
import { createWrapper } from '@/tests/react-query';
import { useUsers } from './use-users';
import { useChangeUserRole, useDeactivateUser } from './use-user-mutations';

describe('useUsers', () => {
  it('returns a page of users from the BFF', async () => {
    server.use(
      http.get('/api/bff/users', () =>
        HttpResponse.json({
          items: [
            { id: 'u1', email: 'o@acme.test', fullName: 'Owner', status: 'Active', emailVerified: true, roleId: 'r1', roleName: 'Owner', createdAt: '2026-01-01T00:00:00Z' },
          ],
          total: 1,
          page: 1,
          pageSize: 20,
        }),
      ),
    );
    const { result } = renderHook(() => useUsers(1, 20), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.data?.items).toHaveLength(1));
    expect(result.current.data?.total).toBe(1);
  });
});

describe('user mutation error mapping', () => {
  it('changes a role on 204', async () => {
    server.use(http.put('/api/bff/users/u1/role', () => new HttpResponse(null, { status: 204 })));
    const { result } = renderHook(() => useChangeUserRole(), { wrapper: createWrapper() });
    await expect(result.current.mutateAsync({ id: 'u1', roleId: 'r2' })).resolves.toBeUndefined();
  });

  it('maps a 409 guard (last Owner / self) to conflict', async () => {
    server.use(http.post('/api/bff/users/u1/deactivate', () => HttpResponse.json({ code: 'conflict' }, { status: 409 })));
    const { result } = renderHook(() => useDeactivateUser(), { wrapper: createWrapper() });
    await expect(result.current.mutateAsync('u1')).rejects.toMatchObject({ code: 'conflict' });
  });

  it('maps a 403 two-factor-required to twoFactorRequired', async () => {
    server.use(http.put('/api/bff/users/u1/role', () => HttpResponse.json({ code: 'twoFactorRequired' }, { status: 403 })));
    const { result } = renderHook(() => useChangeUserRole(), { wrapper: createWrapper() });
    await expect(result.current.mutateAsync({ id: 'u1', roleId: 'r2' })).rejects.toMatchObject({ code: 'twoFactorRequired' });
  });
});
