import axios from 'axios';
import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';
import { gatedWriteCode } from '@/shared/api/server/gated-write';
import { roleFormSchema } from '@/features/settings/model/role.schema';
import type { Role } from '@/features/settings/model/roles.types';

/** List the tenant's roles (system first), with member counts. */
export const GET = withBff(async (_req, { api }) => {
  const { data } = await api.get<Role[]>('/api/v1/roles');
  return NextResponse.json(data);
});

/** Create a custom role. Surfaces 409 (name taken) / 403 (email-unverified) inline. */
export const POST = withBff(async (req, { api }) => {
  const parsed = roleFormSchema.safeParse(await req.json().catch(() => null));
  if (!parsed.success) {
    return NextResponse.json({ title: 'Invalid input' }, { status: 422 });
  }

  try {
    const { data } = await api.post('/api/v1/roles', parsed.data);
    return NextResponse.json(data, { status: 201 });
  } catch (error) {
    if (axios.isAxiosError(error) && error.response) {
      return NextResponse.json({ code: gatedWriteCode(error, { 409: 'nameTaken' }) }, { status: error.response.status });
    }
    throw error;
  }
});
