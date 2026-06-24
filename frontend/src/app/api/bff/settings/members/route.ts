import axios from 'axios';
import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';
import { gatedWriteCode } from '@/shared/api/server/gated-write';
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

  try {
    await api.post('/api/v1/invitations', parsed.data);
    return new NextResponse(null, { status: 204 });
  } catch (error) {
    // A soft-gated invite (403 email-not-verified | two-factor-required) surfaces as a code,
    // not a 500. The dashboard banner is the primary nudge to enroll/verify.
    if (axios.isAxiosError(error) && error.response) {
      return NextResponse.json({ code: gatedWriteCode(error) }, { status: error.response.status });
    }
    throw error;
  }
});
