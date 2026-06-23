import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';
import { inviteMemberSchema, type PendingInvitation } from '@/features/settings/model/members';

export const GET = withBff(async (_req, { api }) => {
  const { data } = await api.get<PendingInvitation[]>('/api/v1/invitations');
  return NextResponse.json(data);
});

export const POST = withBff(async (req, { api }) => {
  const parsed = inviteMemberSchema.safeParse(await req.json().catch(() => null));
  if (!parsed.success) {
    return NextResponse.json({ title: 'Invalid input' }, { status: 422 });
  }

  await api.post('/api/v1/invitations', parsed.data);
  return new NextResponse(null, { status: 204 });
});
