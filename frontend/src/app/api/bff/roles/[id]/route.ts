import axios from 'axios';
import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';
import { gatedWriteCode } from '@/shared/api/server/gated-write';
import { roleFormSchema } from '@/features/settings/model/role.schema';

/** Update a custom role's name + permissions. System roles are 403 on the backend. */
export const PUT = withBff(async (req, { api }, routeCtx) => {
  const { id } = await routeCtx.params;
  const parsed = roleFormSchema.safeParse(await req.json().catch(() => null));
  if (!parsed.success) {
    return NextResponse.json({ title: 'Invalid input' }, { status: 422 });
  }

  try {
    await api.put(`/api/v1/roles/${id}`, parsed.data);
    return new NextResponse(null, { status: 204 });
  } catch (error) {
    if (axios.isAxiosError(error) && error.response) {
      return NextResponse.json({ code: gatedWriteCode(error, { 409: 'nameTaken' }) }, { status: error.response.status });
    }
    throw error;
  }
});

/** Delete a custom role. The backend blocks deletion while members are assigned. */
export const DELETE = withBff(async (_req, { api }, routeCtx) => {
  const { id } = await routeCtx.params;
  try {
    await api.delete(`/api/v1/roles/${id}`);
    return new NextResponse(null, { status: 204 });
  } catch (error) {
    if (axios.isAxiosError(error) && error.response) {
      return NextResponse.json({ code: gatedWriteCode(error, { 409: 'membersAssigned' }) }, { status: error.response.status });
    }
    throw error;
  }
});
