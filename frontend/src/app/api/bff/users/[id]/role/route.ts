import axios from 'axios';
import { z } from 'zod';
import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';
import { gatedWriteCode } from '@/shared/api/server/gated-write';

const bodySchema = z.object({ roleId: z.string().uuid() });

/** Change a user's single role. Forwards 409 (last-Owner) / 403 (unverified | 2FA-required). */
export const PUT = withBff(async (req, { api }, routeCtx) => {
  const { id } = await routeCtx.params;
  const parsed = bodySchema.safeParse(await req.json().catch(() => null));
  if (!parsed.success) {
    return NextResponse.json({ title: 'Invalid input' }, { status: 422 });
  }

  try {
    await api.put(`/api/v1/users/${id}/role`, parsed.data);
    return new NextResponse(null, { status: 204 });
  } catch (error) {
    if (axios.isAxiosError(error) && error.response) {
      return NextResponse.json({ code: gatedWriteCode(error, { 409: 'conflict' }) }, { status: error.response.status });
    }
    throw error;
  }
});
