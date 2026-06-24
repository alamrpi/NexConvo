import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';
import type { PermissionItem } from '@/features/settings/model/roles.types';

/** The assignable permission catalog ({key, module, category}) driving the role editor. */
export const GET = withBff(async (_req, { api }) => {
  const { data } = await api.get<PermissionItem[]>('/api/v1/roles/permissions');
  return NextResponse.json(data);
});
