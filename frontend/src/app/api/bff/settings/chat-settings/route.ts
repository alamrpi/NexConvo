import { NextResponse } from 'next/server';
import axios from 'axios';
import { withBff } from '@/shared/api/server/bff';
import { e2eChatSettingsGet, e2eChatSettingsPut } from '@/shared/api/server/e2e-fixtures';
import { chatSettingsSchema } from '@/features/settings/model/chat-settings.schema';
import type { WorkspaceChatSettingsDto } from '@/features/settings/model/chat-settings.types';

/**
 * Workspace chat settings BFF (frontend standards S3/S4/S8): tenant-implicit (the gateway derives
 * the tenant from the JWT), routed through the resilient `withBff` client (silent refresh +
 * correlation). Client code never reads a token — all gateway traffic flows through this handler.
 */
export const GET = withBff(async (_req, { api }) => {
  const { data } = await api.get<WorkspaceChatSettingsDto>('/api/v1/chat-settings');
  return NextResponse.json(data);
}, e2eChatSettingsGet);

export const PUT = withBff(async (req, { api }) => {
  // Same zod schema as the form — the BFF is the server-side validation boundary (S10).
  const parsed = chatSettingsSchema.safeParse(await req.json().catch(() => null));
  if (!parsed.success) {
    return NextResponse.json({ title: 'Invalid input' }, { status: 422 });
  }

  try {
    const { data } = await api.put('/api/v1/chat-settings', parsed.data);
    return NextResponse.json(data);
  } catch (error) {
    // Surface the backend's optimistic-concurrency conflict (S17) as a stable code.
    if (axios.isAxiosError(error) && error.response?.status === 409) {
      return NextResponse.json({ code: 'conflict' }, { status: 409 });
    }
    throw error;
  }
}, e2eChatSettingsPut);
