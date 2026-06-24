import { describe, expect, it } from 'vitest';
import { renderHook } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/tests/msw/server';
import { createWrapper } from '@/tests/react-query';
import { useChangePassword } from './use-change-password';

const body = { currentPassword: 'old-password', newPassword: 'brand-new-123' };

describe('useChangePassword', () => {
  it('resolves on a 204 success', async () => {
    server.use(http.put('/api/bff/account/password', () => new HttpResponse(null, { status: 204 })));
    const { result } = renderHook(() => useChangePassword(), { wrapper: createWrapper() });

    await expect(result.current.mutateAsync(body)).resolves.toBeUndefined();
  });

  it('maps the BFF 422 {code:invalidCurrent} to invalidCurrent', async () => {
    server.use(http.put('/api/bff/account/password', () => HttpResponse.json({ code: 'invalidCurrent' }, { status: 422 })));
    const { result } = renderHook(() => useChangePassword(), { wrapper: createWrapper() });

    await expect(result.current.mutateAsync(body)).rejects.toMatchObject({ code: 'invalidCurrent' });
  });
});
