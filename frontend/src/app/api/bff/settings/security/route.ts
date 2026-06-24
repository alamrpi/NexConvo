import axios from 'axios';
import { z } from 'zod';
import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';
import { gatedWriteCode } from '@/shared/api/server/gated-write';
import type { SecuritySettings } from '@/features/settings/model/security-settings.types';

const schema = z.object({ requireTwoFactor: z.boolean() });

/** Workspace security policy (the 2FA requirement). Gated settings:manage on the backend. */
export const GET = withBff(async (_req, { api }) => {
  const { data } = await api.get<SecuritySettings>('/api/v1/settings/security');
  return NextResponse.json(data);
});

export const PUT = withBff(async (req, { api }) => {
  const parsed = schema.safeParse(await req.json().catch(() => null));
  if (!parsed.success) {
    return NextResponse.json({ title: 'Invalid input' }, { status: 422 });
  }

  try {
    await api.put('/api/v1/settings/security', parsed.data);
    return new NextResponse(null, { status: 204 });
  } catch (error) {
    // A permission denial surfaces as a clean status + code, not a raw 500.
    if (axios.isAxiosError(error) && error.response) {
      return NextResponse.json({ code: gatedWriteCode(error) }, { status: error.response.status });
    }
    throw error;
  }
});
