import { describe, expect, it } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/tests/msw/server';
import { createWrapper } from '@/tests/react-query';
import { usePermissionCatalog, useRoles } from './use-roles';
import { useCreateRole, useUpdateRole } from './use-role-mutations';

describe('useRoles', () => {
  it('lists the tenant roles from the BFF', async () => {
    server.use(
      http.get('/api/bff/roles', () =>
        HttpResponse.json([
          { id: 'r1', name: 'Owner', isSystem: true, grantsAll: true, permissions: ['*'], memberCount: 1 },
          { id: 'r2', name: 'Member', isSystem: false, grantsAll: false, permissions: ['leads:read'], memberCount: 3 },
        ]),
      ),
    );
    const { result } = renderHook(() => useRoles(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.data).toHaveLength(2));
    expect(result.current.data?.[1]?.name).toBe('Member');
  });
});

describe('usePermissionCatalog', () => {
  it('returns the {key, module, category} catalog', async () => {
    server.use(
      http.get('/api/bff/roles/permissions', () =>
        HttpResponse.json([{ key: 'leads:read', module: 'leads', category: 'Access' }]),
      ),
    );
    const { result } = renderHook(() => usePermissionCatalog(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.data).toHaveLength(1));
    expect(result.current.data?.[0]?.category).toBe('Access');
  });
});

describe('role mutation error mapping', () => {
  it('maps a 409 on create to nameTaken', async () => {
    server.use(http.post('/api/bff/roles', () => HttpResponse.json({ code: 'nameTaken' }, { status: 409 })));
    const { result } = renderHook(() => useCreateRole(), { wrapper: createWrapper() });
    await expect(result.current.mutateAsync({ name: 'Support', permissions: ['leads:read'] })).rejects.toMatchObject({
      code: 'nameTaken',
    });
  });

  it('maps a 403 on update to unverified', async () => {
    server.use(http.put('/api/bff/roles/r2', () => HttpResponse.json({ code: 'unverified' }, { status: 403 })));
    const { result } = renderHook(() => useUpdateRole(), { wrapper: createWrapper() });
    await expect(
      result.current.mutateAsync({ id: 'r2', name: 'Member', permissions: ['leads:read'] }),
    ).rejects.toMatchObject({ code: 'unverified' });
  });
});
