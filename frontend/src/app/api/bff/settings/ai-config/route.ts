import { NextResponse } from 'next/server';
import axios from 'axios';
import { withBff } from '@/shared/api/server/bff';
import { aiSettingsSchema } from '@/features/settings/model/ai-settings.schema';
import type { AiConfigDto } from '@/features/settings/model/ai-settings.types';

/**
 * Workspace AI config BFF (frontend standards S3/S4/S8): tenant-implicit (the gateway derives the
 * tenant from the JWT), routed through the resilient `withBff` client (silent refresh + correlation).
 * The API key never crosses back to the browser — the backend returns only `hasApiKey`.
 */
export const GET = withBff(async (_req, { api }) => {
  const { data } = await api.get<AiConfigDto[]>('/api/v1/ai-config');
  return NextResponse.json(data);
});

export const POST = withBff(async (req, { api }) => {
  // Same zod schema as the form — the BFF is the server-side validation boundary (S10).
  const parsed = aiSettingsSchema.safeParse(await req.json().catch(() => null));
  if (!parsed.success) {
    return NextResponse.json({ title: 'Invalid input' }, { status: 422 });
  }

  try {
    const { data } = await api.post('/api/v1/ai-config', parsed.data);
    return NextResponse.json(data);
  } catch (error) {
    // Surface the backend's optimistic-concurrency conflict (S17) as a stable code.
    if (axios.isAxiosError(error) && error.response?.status === 409) {
      return NextResponse.json({ code: 'conflict' }, { status: 409 });
    }
    if (axios.isAxiosError(error) && error.response?.status === 422) {
      // Two kinds of 422 come back from the gateway: the server re-tested the connection on
      // save and it failed (`connection-test-failed`), or FluentValidation rejected the body.
      // Only the former should surface as the "credentials expired, test again" message —
      // discriminate on the backend `code` so a validation failure passes its payload through.
      const data = error.response.data as { code?: string } | undefined;
      if (data?.code === 'connection-test-failed') {
        return NextResponse.json({ code: 'test-failed' }, { status: 422 });
      }
      return NextResponse.json(error.response.data ?? { title: 'Invalid input' }, { status: 422 });
    }
    throw error;
  }
});
