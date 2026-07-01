import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';
import { emailSettingsSchema } from '@/features/settings/model/email-settings.schema';
import type { EmailSettings } from '@/features/settings/model/email-settings.types';

/** Workspace email settings. The secret never crosses this boundary — backend returns only `hasSecret`. */
export const GET = withBff(async (_req, { api }) => {
  const { data } = await api.get<EmailSettings>('/api/v1/settings/email');
  return NextResponse.json(data);
});

export const PUT = withBff(async (req, { api }) => {
  const parsed = emailSettingsSchema.safeParse(await req.json().catch(() => null));
  if (!parsed.success) {
    return NextResponse.json({ title: 'Invalid input' }, { status: 422 });
  }

  await api.put('/api/v1/settings/email', parsed.data);
  return new NextResponse(null, { status: 204 });
});
