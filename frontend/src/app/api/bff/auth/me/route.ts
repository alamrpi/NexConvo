import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';
import type { CurrentUser } from '@/features/auth/model/auth.types';

/** Current session profile. Auto-refreshes the access token on expiry via withBff. */
export const GET = withBff(async (_req, { api }) => {
  const { data } = await api.get<CurrentUser>('/api/v1/auth/me');
  return NextResponse.json(data);
});
