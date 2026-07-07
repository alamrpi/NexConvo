import { NextResponse } from 'next/server';
import axios from 'axios';
import { withBff } from '@/shared/api/server/bff';
import { s3ConfigSchema } from '@/features/settings/model/s3-config.schema';
import type { S3ConfigDto } from '@/features/settings/model/s3-config.types';

export const GET = withBff(async (_req, { api }) => {
  const { data } = await api.get<S3ConfigDto | null>('/api/v1/s3-config');
  return NextResponse.json(data);
});

export const PUT = withBff(async (req, { api }) => {
  const parsed = s3ConfigSchema.safeParse(await req.json().catch(() => null));
  if (!parsed.success) {
    return NextResponse.json({ title: 'Invalid input' }, { status: 422 });
  }

  try {
    const { data } = await api.put('/api/v1/s3-config', parsed.data);
    return NextResponse.json(data);
  } catch (error) {
    if (axios.isAxiosError(error) && error.response?.status === 409) {
      return NextResponse.json({ code: 'conflict' }, { status: 409 });
    }
    if (axios.isAxiosError(error) && error.response?.status === 422) {
      // Server re-tested the connection on save and it failed (credentials changed since the
      // last successful test) — distinct from the zod validation 422 above via `code`.
      return NextResponse.json({ code: 'test-failed' }, { status: 422 });
    }
    throw error;
  }
});
