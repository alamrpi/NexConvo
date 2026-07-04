import { NextResponse } from 'next/server';
import axios from 'axios';
import { withBff } from '@/shared/api/server/bff';
import { e2eChannelsGet, e2eChannelsPost } from '@/shared/api/server/e2e-fixtures';
import { saveChannelConnectionSchema } from '@/features/settings/model/channel-connection.schema';
import type { ChannelConnectionDto } from '@/features/settings/model/channel-connection.types';

/**
 * Channel connections BFF (frontend standards S3/S4/S8): tenant-implicit (the gateway derives the
 * tenant from the JWT), routed through the resilient `withBff` client (silent refresh + correlation).
 * Access tokens are never returned in full — the backend exposes only `maskedAccessToken` (S3).
 */
export const GET = withBff(async (_req, { api }) => {
  const { data } = await api.get<ChannelConnectionDto[]>('/api/v1/channel-connections');
  return NextResponse.json(data);
}, e2eChannelsGet);

export const POST = withBff(async (req, { api }) => {
  // Same zod schema as the form — the BFF is the server-side validation boundary (S10).
  const parsed = saveChannelConnectionSchema.safeParse(await req.json().catch(() => null));
  if (!parsed.success) {
    return NextResponse.json({ title: 'Invalid input' }, { status: 422 });
  }

  try {
    const { data } = await api.post('/api/v1/channel-connections', parsed.data);
    return NextResponse.json(data);
  } catch (error) {
    // Surface the backend's optimistic-concurrency conflict (S17) as a stable code.
    if (axios.isAxiosError(error) && error.response?.status === 409) {
      return NextResponse.json({ code: 'conflict' }, { status: 409 });
    }
    throw error;
  }
}, e2eChannelsPost);
