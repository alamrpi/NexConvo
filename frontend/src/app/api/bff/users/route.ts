import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';
import type { PagedUsers } from '@/features/settings/model/users.types';

/** Paginated workspace users. Page params pass straight through to the gateway. */
export const GET = withBff(async (req, { api }) => {
  const url = new URL(req.url);
  const page = url.searchParams.get('page') ?? '1';
  const pageSize = url.searchParams.get('pageSize') ?? '20';
  const { data } = await api.get<PagedUsers>(`/api/v1/users?page=${page}&pageSize=${pageSize}`);
  return NextResponse.json(data);
});
